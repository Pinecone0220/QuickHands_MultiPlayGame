using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using SW=System.Diagnostics.Stopwatch;

// Isolated QA fixture. No gameplay/package changes; use UDPDelayProxy.py for actual packet delay.
public sealed class PingClockRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path,port;int peers;double deadline,nextPing;bool sent,acked,marked,rankingObserved;
    double sentWall,sentElapsed,previousWall,previousElapsed;int target,displayMismatches;double hide;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;NetworkManager net;
    readonly List<string> frames=new List<string>{"wall,elapsed,hidden,wall_delta,timer_delta"};
    readonly List<string> pings=new List<string>{"wall,utp_ms,app_rtt_ms,uplink_ms,server_clock_lag_ms"};
    readonly List<string> probes=new List<string>{"name,receive_wall,sample_elapsed,sample_age_ms,uplink_ms"};
    readonly List<string> submissions=new List<string>{"stage,wall,elapsed_or_record,value_or_rtt"};
    [DllImport("kernel32.dll")]static extern bool QueryPerformanceCounter(out long value);
    [DllImport("kernel32.dll")]static extern bool QueryPerformanceFrequency(out long value);
    static readonly double WallFrequency=Frequency();
    static double Frequency(){QueryPerformanceFrequency(out long value);return value;}
    static double Wall{get{QueryPerformanceCounter(out long value);return value/WallFrequency;}}
    double Now=>net.ServerTime.Time;
    static string Arg(string name,string fallback){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i<0?fallback:a[i+1];}
    T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,F).GetValue(obj);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ping-clock-qa")<0)return;
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        var q=new GameObject("Ping Clock QA").AddComponent<PingClockRuntimeQA>();q.role=Arg("--ping-clock-qa","host");
        q.path=Arg("--ping-dir",Application.dataPath+"/../PingClock");q.port=Arg("--ping-port","17960");q.peers=int.Parse(Arg("--ping-peers","5"));
        Directory.CreateDirectory(q.path);Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
    }
    void Mark(string key)=>File.WriteAllText(Path.Combine(path,role+"-"+key),"ok");
    bool Seen(string key)=>File.Exists(Path.Combine(path,key));
    void Save()
    {
        File.WriteAllLines(Path.Combine(path,role+"-frames.csv"),frames);File.WriteAllLines(Path.Combine(path,role+"-pings.csv"),pings);
        File.WriteAllLines(Path.Combine(path,role+"-probes.csv"),probes);File.WriteAllLines(Path.Combine(path,role+"-submission.csv"),submissions);
        File.WriteAllText(Path.Combine(path,role+"-config.txt"),$"target_cs={target}\nhide_s={hide:R}\ndisplay_mismatches={displayMismatches}\nranking_observed={rankingObserved}\n");
    }
    void Finish(string message,int code=0)
    {
        Save();Debug.Log(message);File.WriteAllText(Path.Combine(path,role+"-done"),message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator Start()
    {
        deadline=Wall+150;yield return new WaitForSecondsRealtime(1);manager=HelloWorldManager.Instance;manager.Nickname=role;
        if(role!="host")while(!Seen("host-listening"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1",port);
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        player=HelloWorldPlayer.Local;net=player.NetworkManager;view=manager.GetComponent<RouletteView>();
        net.CustomMessagingManager.RegisterNamedMessageHandler("qa-clock-ping",OnPing);
        net.CustomMessagingManager.RegisterNamedMessageHandler("qa-clock-pong",OnPong);
        net.CustomMessagingManager.RegisterNamedMessageHandler("qa-clock-stop",OnProbe);
        player.SelectLoadoutItem(DuelItem.Recovery);player.SelectLoadoutItem(DuelItem.Taser);
        if(role=="host")
        {
            BoundaryChecks();manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);Mark("listening");
            while(RouletteRules.Count(player.State.Value.ConnectedMask)!=peers+1)yield return null;
            while(!AllSelected())yield return null;
            yield return new WaitForSecondsRealtime(10);Mark("start");
        }
        else while(!Seen("host-start"))yield return null;
        player.Rematch();
        if(role=="host")
        {while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;manager.ChooseMiniGame(PartyMiniGame.Countdown);}
        if(role=="host")
        {
            while(player.State.Value.Clock.Phase==ClockPhase.Counting)yield return null;
            yield return new WaitForSecondsRealtime(.75f);var s=player.State.Value;
            var rows=new List<string>{"name,seat,record_cs,error_cs,failure,lives"};
            for(int seat=0;seat<6;seat++)if(s.Connected(seat))
            {var e=s.Clock.ForSeat(seat);rows.Add($"{s.NameForSeat(seat)},{seat},{e.Centiseconds},{e.Error},{e.Failure},{s.ForSeat(seat).LivesRemaining}");}
            File.WriteAllLines(Path.Combine(path,"results.csv"),rows);
        }
        else {while(!Seen("results.csv"))yield return null;yield return new WaitForSecondsRealtime(1);}
        var state=player.State.Value;var entry=state.Clock.ForSeat(player.Seat.Value);
        submissions.Add($"result,{Wall:R},{entry.Centiseconds},{state.ForSeat(player.Seat.Value).LivesRemaining}");
        Save();Mark("captured");
        if(role=="host"){while(CapturedPeers()<peers)yield return null;Mark("release");}
        else while(!Seen("host-release"))yield return null;
        Finish("PASS ping-clock "+role+": measured "+(pings.Count-1)+" ping samples and "+(frames.Count-1)+" timer frames");
    }
    bool AllSelected(){var s=player.State.Value;for(int i=0;i<6;i++)if(s.Connected(i)&&!DuelLoadout.Complete(s.ForSeat(i).SelectedItems))return false;return true;}
    int CapturedPeers(){int count=0;foreach(var file in Directory.GetFiles(path,"*-captured"))if(!Path.GetFileName(file).StartsWith("host-"))count++;return count;}
    void BoundaryChecks()
    {
        foreach(double age in new[]{0,.05,.1,.2,.29,.31,.5,.8})
        {
            var c=new DuelClockContest(new System.Random(14));c.Begin(3,100);double sample=c.State.StartedAt+1;
            bool accepted=c.Stop(0,c.State.Iteration,sample,sample+age);
            if(accepted!=(age<=.3))throw new Exception("clock freshness boundary "+age);
        }
        Debug.Log("PASS ping-clock freshness boundary: 8 checks");
    }
    void Update()
    {
        if(Wall>deadline){Finish("FAIL ping-clock timeout "+role+" "+manager?.Status,1);return;}
        if(net==null||!net.IsListening||role=="host"||Wall<nextPing)return;
        nextPing=Wall+.2;
        using(var w=new FastBufferWriter(32,Allocator.Temp))
        {w.WriteValueSafe(Wall);w.WriteValueSafe(Now);net.CustomMessagingManager.SendNamedMessage("qa-clock-ping",NetworkManager.ServerClientId,w,NetworkDelivery.Unreliable);}
    }
    void LateUpdate()
    {
        if(player==null||!player.IsSpawned)return;var c=player.State.Value.Clock;
        if(c.Phase==ClockPhase.Ranking)rankingObserved=true;
        if(c.Phase==ClockPhase.Counting&&c.MiniGame==PartyMiniGame.Countdown&&Now>=c.StartedAt&&!sent)
        {
            target=c.Target;hide=c.HideAfter;double wall=Wall,elapsed=Now-c.StartedAt;
            string expected=elapsed<hide?elapsed.ToString("F2"):"••.••";
            if(Field<Label>(view,"clockTimer").text!=expected)displayMismatches++;
            frames.Add($"{wall:R},{elapsed:R},{(elapsed>=hide?1:0)},{(previousWall==0?0:wall-previousWall):R},{(previousWall==0?0:elapsed-previousElapsed):R}");
            previousWall=wall;previousElapsed=elapsed;
            if(role=="spike"&&elapsed>=hide&&!marked){Mark("hidden");marked=true;}
            if(elapsed>=c.Target/100.0)
            {
                sent=true;sentWall=Wall;sentElapsed=elapsed;
                submissions.Add($"send,{sentWall:R},{elapsed:R},{net.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId)}");
                typeof(RouletteView).GetMethod("StopClock",F).Invoke(view,null);
                using(var w=new FastBufferWriter(48,Allocator.Temp))
                {
                    w.WriteValueSafe(sentWall);w.WriteValueSafe(Now);w.WriteValueSafe(elapsed);
                    if(role=="host")probes.Add($"host,{Wall:R},{elapsed:R},0,0");
                    else net.CustomMessagingManager.SendNamedMessage("qa-clock-stop",NetworkManager.ServerClientId,w,NetworkDelivery.Unreliable);
                }
            }
        }
        if(sent&&!acked&&!Field<bool>(view,"clockAwaiting"))
        {
            acked=true;bool accepted=Field<bool>(view,"clockStopped");int record=Field<int>(view,"clockRecord");
            submissions.Add($"ack,{Wall:R},{record},{(accepted?1:0)}");submissions.Add($"ack_ms,{Wall:R},{(Wall-sentWall)*1000:R},0");
        }
    }
    void OnPing(ulong sender,FastBufferReader r)
    {
        if(!net.IsServer)return;r.ReadValueSafe(out double wall);r.ReadValueSafe(out double sample);
        using(var w=new FastBufferWriter(64,Allocator.Temp))
        {w.WriteValueSafe(wall);w.WriteValueSafe(sample);w.WriteValueSafe(Now);w.WriteValueSafe(Wall);net.CustomMessagingManager.SendNamedMessage("qa-clock-pong",sender,w,NetworkDelivery.Unreliable);}
    }
    void OnPong(ulong sender,FastBufferReader r)
    {
        if(sender!=NetworkManager.ServerClientId||net.IsServer)return;
        r.ReadValueSafe(out double wall);r.ReadValueSafe(out double sample);r.ReadValueSafe(out double hostNow);r.ReadValueSafe(out double hostWall);
        pings.Add($"{Wall:R},{net.NetworkConfig.NetworkTransport.GetCurrentRtt(0)},{(Wall-wall)*1000:R},{(hostWall-wall)*1000:R},{(hostNow-sample-(hostWall-wall))*1000:R}");
    }
    void OnProbe(ulong sender,FastBufferReader r)
    {
        if(!net.IsServer)return;r.ReadValueSafe(out double wall);r.ReadValueSafe(out double sample);r.ReadValueSafe(out double elapsed);
        var p=net.ConnectedClients[sender].PlayerObject.GetComponent<HelloWorldPlayer>();
        probes.Add($"{player.State.Value.NameForSeat(p.Seat.Value)},{Wall:R},{elapsed:R},{(Now-sample)*1000:R},{(Wall-wall)*1000:R}");
    }
}
