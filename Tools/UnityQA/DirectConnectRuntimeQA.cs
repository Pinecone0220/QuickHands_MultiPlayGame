using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DirectConnectRuntimeQA : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    string role, path; int checks; double deadline;
    HelloWorldManager manager; RouletteView view;
    readonly List<Socket> blockers = new List<Socket>();
    T Field<T>(object source, string name) => (T)source.GetType().GetField(name, Flags).GetValue(source);
    void Mark(string name, string value = "ok") => File.WriteAllText(Path.Combine(path, role + "-" + name), value);
    bool Seen(string other, string name) => File.Exists(Path.Combine(path, other + "-" + name));
    void Check(bool value, string label) { checks++; if (!value) { Done("FAIL " + role + ": " + label, 1); throw new Exception(label); } }
    void Done(string message, int code = 0) { Debug.Log(message); Mark("done", message); deadline = double.MaxValue; Application.Quit(code); }
    void Click(Button button) => typeof(Clickable).GetMethod("SimulateSingleClick", Flags).Invoke(button.clickable, new object[] { null, 0 });
    void Update() { if (Time.realtimeSinceStartupAsDouble > deadline) Done("FAIL direct timeout " + role, 1); }
    void OnDestroy() { foreach (var socket in blockers) socket.Dispose(); }
    void Block(int port) { var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); socket.ExclusiveAddressUse = true; socket.Bind(new IPEndPoint(IPAddress.Any, port)); blockers.Add(socket); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
    {
        var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "--direct-qa"); if (i < 0) return;
        var qa = new GameObject("Direct connection QA").AddComponent<DirectConnectRuntimeQA>(); qa.role = args[i + 1];
        qa.path = args[Array.IndexOf(args, "--direct-dir") + 1]; Directory.CreateDirectory(qa.path);
        qa.deadline = Time.realtimeSinceStartupAsDouble + 90; Application.targetFrameRate = 45; Application.runInBackground = true;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);
        manager = HelloWorldManager.Instance; view = manager.GetComponent<RouletteView>();
        RoomCameraCaptureQA.Setup(view); Click(Field<Button>(view, "patchNotesClose")); yield return null;
        var address = Field<TextField>(view, "hostAddressInput"); var port = Field<TextField>(view, "serverPortInput");
        var host = Field<Button>(view, "hostRoomButton"); var join = Field<Button>(view, "joinRoomButton");
        Check(address.value == "127.0.0.1" && port.value == "7777", "original direct defaults");
        Check(host.text == "방 만들기" && join.text == "참가하기", "direct button labels");
        var root = Field<VisualElement>(view, "root");
        Check(!root.Query<TextField>().ToList().Any(x => x.label == "방 코드" || x.label == "비밀번호" || x.label == "방 제목"), "no discovery admission fields");
        Check(!root.Query<Button>().ToList().Any(x => x.text == "방 목록보기" || x.text == "코드로 참가" || x.text == "초대 코드 복사"), "no discovery buttons");
        port.value = "0"; Click(host); Check(!manager.Busy && manager.Status.Contains("포트"), "invalid port rejected");
        port.value = "18911"; address.value = "bad-address"; Click(join); Check(!manager.Busy && manager.Status.Contains("IP"), "invalid IP rejected"); address.value = "127.0.0.1";
        if (role == "host") { for (int p = 47770; p < 47786; p++) Block(p); Block(18911); join.GetFirstAncestorOfType<ScrollView>().ScrollTo(join); yield return null; yield return null; yield return RoomCameraCaptureQA.Capture(view, Path.Combine(path, "Direct-Connect-Menu.png")); }
        manager.Nickname = role == "host" ? "방장한글" : "참가한글";
        for (int session = 1; session <= 2; session++)
        {
            string tag = session.ToString();
            if (role == "host") { port.value = session == 1 ? "18911" : "18913"; Click(host); }
            else { while (!Seen("host", "port" + tag)) yield return null; port.value = File.ReadAllText(Path.Combine(path, "host-port" + tag)); Click(join); }
            while (HelloWorldPlayer.Local == null || HelloWorldPlayer.Local.Seat.Value < 0) yield return null;
            var player = HelloWorldPlayer.Local; var network = player.NetworkManager;
            Check(player.NetworkManager.NetworkConfig.ProtocolVersion == DuelNetworkProtocol.Version && DuelNetworkProtocol.Version == 64, "protocol 64");
            Check(player.Seat.Value == (role == "host" ? 0 : 1), "assigned seat");
            if (role == "host")
            {
                Check(manager.SessionPort == (session == 1 ? 18912 : 18913), "port fallback / requested port");
                Check(manager.ServerGame.ReadyMask == 0 && !player.State.Value.Seat0.LoadoutLocked, "new session resets preparation");
                if (session == 1) { manager.ToggleDeveloperMode(); manager.SetRoomAutoSelectItems(true); player.Rematch(); while (manager.ServerGame.ReadyMask != 1) yield return null; }
                yield return null; Check(port.value == manager.SessionPort.ToString(), "actual host port reflected in UI"); Mark("port" + tag, manager.SessionPort.ToString());
            }
            while (player.State.Value.ConnectedMask != 3) yield return null;
            Check(player.State.Value.NameForSeat(0) == "방장한글" && player.State.Value.NameForSeat(1) == "참가한글", "raw UTF8 nickname roundtrip");
            Check(player.State.Value.Seat1.Health == 100 && player.State.Value.Seat1.LivesRemaining == 3, "late join snapshot");
            if (session == 1)
            {
                Check(player.State.Value.ReadyMask == 1, "joining preserves host readiness");
                if (role == "client") { player.Rematch(); Mark("ready"); }
                while (player.State.Value.Stage == DuelStage.Idle) yield return null;
                Check(player.State.Value.Stage == DuelStage.Countdown, "two prepared participants start");
            }
            Mark("verified" + tag);
            if (role == "host") { while (!Seen("client", "verified" + tag)) yield return null; manager.Leave(); }
            while (manager.Busy) yield return null;
            Check(HelloWorldPlayer.Local == null && !network.IsListening, "shutdown clears local connection");
            Mark("left" + tag);
            if (role == "host") while (!Seen("client", "left" + tag)) yield return null;
        }
        Done("PASS direct " + role + ": " + checks + " checks");
    }
}
