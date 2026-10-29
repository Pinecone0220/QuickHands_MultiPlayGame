using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class LanReliabilityRuntimeQA:MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    string role,path;int checks;double deadline;HelloWorldManager manager;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    void Set(object o,string n,object value)=>o.GetType().GetField(n,Flags).SetValue(o,value);
    void Click(Button b)=>typeof(Clickable).GetMethod("SimulateSingleClick",Flags).Invoke(b.clickable,new object[]{null,0});
    void Mark(string n)=>File.WriteAllText(Path.Combine(path,role+"-"+n),"ok");bool Seen(string r,string n)=>File.Exists(Path.Combine(path,r+"-"+n));
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL LAN reliability "+role+": "+why,1);throw new Exception(why);}}
    void Done(string message,int code=0){File.WriteAllText(Path.Combine(path,role+"-done"),message);Debug.Log(message);deadline=double.MaxValue;Application.Quit(code);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--lan-reliability-qa");if(i<0)return;var q=new GameObject("LAN reliability QA").AddComponent<LanReliabilityRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--lan-reliability-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+160;Application.targetFrameRate=45;Application.runInBackground=true;
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL LAN reliability timeout "+role,1);}
    DuelLanRoom Ad(int protocol=DuelLanRooms.Protocol)=>new DuelLanRoom{id=Guid.NewGuid().ToString("N"),title="LAN reliability",code="LANFIX",port=17777,count=1,max=6,protocol=protocol,joinable=true,locked=true};
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role=="model"){yield return Model();Done("PASS LAN reliability model: "+checks+" checks");yield break;}
        RoomCameraCaptureQA.Setup(view);
        if(role=="host")
        {
            manager.CreateRoom("LAN 안정성 테스트","secret","LANFIX");while(HelloWorldPlayer.Local==null)yield return null;
            string invite=manager.RoomInvite;Check(invite.StartsWith("QH1."),"host creates direct invite");Click(Field<Button>(view,"roomInviteCopy"));Check(GUIUtility.systemCopyBuffer==invite&&Field<Button>(view,"roomInviteCopy").text=="복사 완료","copy button exports invite");File.WriteAllText(Path.Combine(path,"invite"),invite);yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"Host.png"));Mark("hosting");
            while(!Seen("client","listed-joined"))yield return null;Check(HelloWorldPlayer.Local.State.Value.ConnectedMask==3,"list guest state");Mark("list-verified");
            while(!Seen("client","direct-joined"))yield return null;Check(HelloWorldPlayer.Local.State.Value.ConnectedMask==3,"direct guest state");Mark("direct-verified");
            while(!Seen("client","left"))yield return null;manager.Leave();yield return new WaitForSecondsRealtime(.5f);Check(!manager.Rooms.Hosting&&!manager.CanShareRoomInvite,"host cleanup");
        }
        else
        {
            while(!Seen("host","hosting"))yield return null;
            // Prove that the ordinary room list works with broadcasts and loopback disabled.
            Set(manager.Rooms,"BroadcastEnabled",false);Set(manager.Rooms,"LoopbackEnabled",false);
            typeof(RouletteView).GetMethod("SetRoomBrowser",Flags).Invoke(view,new object[]{true});DuelLanRoom found=null;
            while(found==null){foreach(var r in manager.Rooms.Rooms)if(r.code=="LANFIX")found=r;yield return null;}
            Check(found.address!="127.0.0.1"&&found.locked,"unicast-only room list finds real LAN endpoint");
            yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"Room-List.png"));manager.JoinRoom(found,"secret");while(HelloWorldPlayer.Local==null)yield return null;Check(HelloWorldPlayer.Local.Seat.Value==1,"list joins approval");Mark("listed-joined");while(!Seen("host","list-verified"))yield return null;
            manager.Leave();while(manager.Busy)yield return null;yield return new WaitForSecondsRealtime(.4f);
            string invite=File.ReadAllText(Path.Combine(path,"invite"));manager.JoinRoomInvite(invite,"wrong");Check(!manager.Rooms.Browsing&&!manager.RoomRequestPending,"direct join never starts discovery");while(manager.Busy)yield return null;Check(manager.Status.Contains("비밀번호"),"direct invite still enforces password");yield return new WaitForSecondsRealtime(.3f);
            Check(DuelRoomInvite.TryDecode(invite,out var stale,out _),"decode for stale session test");stale.id=Guid.NewGuid().ToString("N");manager.JoinRoomInvite(DuelRoomInvite.Encode(stale),"secret");while(manager.Busy)yield return null;Check(HelloWorldPlayer.Local==null,"wrong session never spawns");yield return new WaitForSecondsRealtime(.3f);
            Field<TextField>(view,"roomInviteInput").value=invite;Field<TextField>(view,"roomInvitePassword").value="secret";
            foreach(var button in Field<VisualElement>(view,"root").Query<Button>().ToList())if(button.text=="초대 코드로 직접 참가"){Click(button);break;}
            Check(manager.Busy&&!manager.Rooms.Browsing&&!manager.RoomRequestPending,"direct UI connection with discovery stopped");while(HelloWorldPlayer.Local==null)yield return null;Check(HelloWorldPlayer.Local.Seat.Value==1&&HelloWorldPlayer.Local.State.Value.ConnectedMask==3,"valid invite joins and initializes state");Mark("direct-joined");while(!Seen("host","direct-verified"))yield return null;
            manager.Leave();while(manager.Busy)yield return null;Check(!manager.Rooms.Browsing&&!manager.Rooms.Hosting,"guest cleanup");Mark("left");
        }
        Done("PASS LAN reliability "+role+": "+checks+" checks");
    }
    IEnumerator Model()
    {
        Check(DuelLanRooms.LocalAddresses().Count>0,"LAN adapter available");Check(!DuelLanRooms.IsLanAddress(IPAddress.Loopback)&&!DuelLanRooms.IsLanAddress(IPAddress.Parse("8.8.8.8")),"invites limited to LAN");
        var room=Ad();string invite=DuelRoomInvite.Encode(room);Check(DuelRoomInvite.TryDecode(invite,out var decoded,out _)&&decoded.id==room.id&&decoded.address!="127.0.0.1"&&decoded.port==room.port,"direct invite roundtrip");
        foreach(string invalid in new[]{null,"", "LANFIX","QH1.!","QH1."+new string('a',4100),"QH1.e30","QH1.////"})Check(!DuelRoomInvite.TryDecode(invalid,out _,out _),"malformed invite rejected");
        var incompatible=Ad(54);Check(!DuelRoomInvite.TryDecode(DuelRoomInvite.Encode(incompatible),out _,out var error)&&error.Contains("버전"),"invite version mismatch");incompatible.code=null; // Protocol54 did not advertise custom codes.
        using(var host=new DuelLanRooms())using(var second=new DuelLanRooms())using(var browser=new DuelLanRooms())
        {
            Check(host.StartHosting()&&second.StartHosting(),"multiple discovery ports");Set(browser,"BroadcastEnabled",false);Set(browser,"LoopbackEnabled",false);double start=Time.realtimeSinceStartupAsDouble;Check(browser.Search(start),"unicast-only search starts");
            // Host deliberately starts replying after the old two-second lookup would have ended.
            while(Time.realtimeSinceStartupAsDouble-start<3.2){browser.Tick(null,Time.realtimeSinceStartupAsDouble);yield return null;}
            Check(browser.Rooms.Count==0,"no fabricated rooms while host silent");
            while(Time.realtimeSinceStartupAsDouble-start<18&&browser.Rooms.Count<1){double now=Time.realtimeSinceStartupAsDouble;browser.Tick(null,now);host.Tick(room,now);second.Tick(incompatible,now);yield return null;}
            Check(browser.Rooms.Count==1,"late host discovered without broadcasts or loopback");
            double until=Time.realtimeSinceStartupAsDouble+4;while(Time.realtimeSinceStartupAsDouble<until){double now=Time.realtimeSinceStartupAsDouble;browser.Tick(null,now);host.Tick(room,now);second.Tick(incompatible,now);yield return null;}
            Check(browser.IncompatibleCount==1&&browser.Rooms.Count==1,"incompatible room counted not joined");
            int revision=browser.Revision;room.count=6;room.joinable=false;until=Time.realtimeSinceStartupAsDouble+4;
            while(Time.realtimeSinceStartupAsDouble<until){double now=Time.realtimeSinceStartupAsDouble;browser.Tick(null,now);host.Tick(room,now);yield return null;}
            bool updated=false;foreach(var r in browser.Rooms)updated=r.count==6&&!r.joinable;Check(updated&&browser.Revision>revision,"unicast metadata refresh");
            host.StopHosting();second.StopHosting();double last=Time.realtimeSinceStartupAsDouble;browser.Tick(null,last);browser.Tick(null,last+13);Check(browser.Rooms.Count==0,"stale room removed");Check(browser.SearchStatus(last+13).Contains("버전"),"version diagnosis shown instead of generic Wi-Fi error");browser.StopBrowsing();Check(!browser.Browsing,"all interface sockets disposed");
        }
    }
}
