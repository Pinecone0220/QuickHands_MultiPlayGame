using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class RoomCodesRuntimeQA:MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    string role,path;int checks;double deadline;HelloWorldManager manager;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    void Mark(string n)=>File.WriteAllText(Path.Combine(path,role+"-"+n),"ok");bool Seen(string r,string n)=>File.Exists(Path.Combine(path,r+"-"+n));
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL room codes "+role+": "+why,1);throw new Exception(why);}}
    void Done(string message,int code=0){File.WriteAllText(Path.Combine(path,role+"-done"),message);Debug.Log(message);deadline=double.MaxValue;Application.Quit(code);}
    void Click(Button b)=>typeof(Clickable).GetMethod("SimulateSingleClick",Flags).Invoke(b.clickable,new object[]{null,0});
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--room-codes-qa");if(i<0)return;var q=new GameObject("Room codes QA").AddComponent<RoomCodesRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--room-codes-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+100;Application.targetFrameRate=45;
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL room codes timeout "+role,1);}
    IEnumerator Idle(){while(manager.RoomRequestPending||manager.Busy)yield return null;yield return new WaitForSecondsRealtime(.2f);}
    IEnumerator Start()
    {
        Check(DuelRoomCode.TryNormalize("  abc123  ",out var normalized)&&normalized=="ABC123","code canonicalization");
        foreach(var invalid in new[]{null,"","ABC","ABCDEFGHIJKLM","한글코드","A B1","A-B1","ABC\nD","ſsss","ＡBCD"})Check(!DuelRoomCode.TryNormalize(invalid,out _),"reject malformed code");
        Check(DuelRoomCode.TryNormalize("1234",out _)&&DuelRoomCode.TryNormalize("AbCd12345678",out _),"numeric and maximum-length code");Check(DuelLanRooms.Protocol==55,"new discovery protocol");
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();RoomCameraCaptureQA.Setup(view);
        if(role=="host")
        {
            manager.CreateRoom("Code QA","","bad!");Check(!manager.Busy&&!manager.RoomRequestPending&&manager.Status.Contains("4~12"),"invalid create never starts network");
            Field<TextField>(view,"roomTitleInput").value="방 코드 테스트";Field<TextField>(view,"roomCodeInput").value="abc123";Field<TextField>(view,"roomPasswordInput").value="secret";
            var menu=Field<VisualElement>(view,"menu") as ScrollView;menu.scrollOffset=new Vector2(0,10000);yield return new WaitForSecondsRealtime(.2f);yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"Create-Form.png"));
            Click(Field<Button>(view,"roomCreateButton"));Check(manager.RoomRequestPending&&!manager.Busy,"create asynchronously checks duplicate code");manager.Begin(DuelMode.AI,false,"127.0.0.1","7777");Check(!manager.Busy,"other start blocked during code request");
            yield return null;Check(!Field<Button>(view,"roomCreateButton").enabledSelf&&!Field<Button>(view,"roomCodeJoinButton").enabledSelf,"prevent duplicate UI requests");
            while(HelloWorldPlayer.Local==null)yield return null;yield return new WaitForSecondsRealtime(.3f);
            Check(manager.RoomCode=="ABC123"&&manager.RoomTitle=="방 코드 테스트"&&manager.Rooms.Hosting,"custom code room created");Check(DuelLanRooms.HexId(Field<string>(manager,"roomId"))&&Field<string>(manager,"roomId")!=manager.RoomCode,"unique session id retained separately");
            Check(Field<Label>(view,"roomCodeLabel").text.Contains("ABC123"),"shareable code shown in lobby");yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"Code-Lobby.png"));Mark("hosting");
            while(!Seen("client","joined"))yield return null;Check(HelloWorldPlayer.Local.State.Value.ConnectedMask==3,"remote joins correct host seat");Mark("verified");while(!Seen("client","left"))yield return null;
        }
        else
        {
            while(!Seen("host","hosting"))yield return null;
            manager.JoinRoomCode("bad!","");Check(!manager.RoomRequestPending&&!manager.Busy,"invalid join rejected locally");
            manager.JoinRoomCode("MISS999","");yield return Idle();Check(manager.Status.Contains("찾지 못"),"unknown code ends with useful message");
            manager.JoinRoomCode("MISS999","");manager.Leave();Check(!manager.RoomRequestPending&&!manager.Rooms.Browsing,"pending lookup cancellation");
            manager.CreateRoom("duplicate","","aBc123");yield return Idle();Check(manager.Status.Contains("이미 사용 중")&&!manager.Rooms.Hosting,"duplicate creation rejected");
            using(var duplicate=new DuelLanRooms())
            {
                Check(duplicate.StartHosting(),"second discovery responder");var fake=new DuelLanRoom{id=Guid.NewGuid().ToString("N"),code="ABC123",title="duplicate",port=19002,count=1,max=6,protocol=DuelLanRooms.Protocol,joinable=true};
                manager.JoinRoomCode("ABC123","secret");while(manager.RoomRequestPending){duplicate.Tick(fake,Time.realtimeSinceStartupAsDouble);yield return null;}
                Check(!manager.Busy&&manager.Status.Contains("여러 개"),"ambiguous codes never choose arbitrary host");
            }
            manager.JoinRoomCode("abc123","wrong");yield return Idle();Check(manager.Status.Contains("비밀번호"),"password still enforced by host approval");
            manager.JoinRoomCode(" aBc123 ","secret");while(HelloWorldPlayer.Local==null)yield return null;yield return new WaitForSecondsRealtime(.3f);
            Check(manager.RoomCode=="ABC123"&&manager.RoomTitle=="방 코드 테스트"&&HelloWorldPlayer.Local.Seat.Value==1,"case-insensitive code joins discovered endpoint");Check(HelloWorldPlayer.Local.State.Value.ConnectedMask==3,"initial room snapshot reaches remote");
            Mark("joined");while(!Seen("host","verified"))yield return null;
        }
        manager.Leave();yield return new WaitForSecondsRealtime(.4f);Check(manager.RoomCode==""&&!manager.RoomRequestPending&&!manager.Rooms.Hosting,"leaving clears code and discovery state");Mark("left");
        Done("PASS room codes "+role+": "+checks+" checks");
    }
}
