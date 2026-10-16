using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class RoomItemsRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;int checks;double deadline;
    sealed class QuickRandom:System.Random{public override int Next(int min,int max)=>min==0&&max==2?1:min;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--room-items-qa");if(i<0)return;var q=new GameObject("RoomItemsQA").AddComponent<RoomItemsRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","room-items-");q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=30;}
    VisualElement E(string field)=>(VisualElement)typeof(RouletteView).GetField(field,F).GetValue(view);
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");
    bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Check(bool value,string message){checks++;if(!value){Finish("FAIL "+role+": "+message,1);throw new Exception(message);}}
    void Finish(string message,int code=0){Debug.Log(message);File.WriteAllText(path+role+"-done",message);deadline=double.MaxValue;Application.Quit(code);}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);
        if(role!="host")while(!Seen("host","configured"))yield return null;
        m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17925");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;view=m.GetComponent<RouletteView>();yield return null;
        if(role=="host")
        {
            Check(!p.State.Value.StartWithItems,"new room defaults to empty inventory setting");
            var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);
            typeof(DuelClockContest).GetField("random",F).SetValue(c,new QuickRandom());
            m.ToggleDeveloperMode();yield return null;var toggle=(Toggle)E("startWithItems");Check(E("roomSettings").style.display.value==DisplayStyle.Flex,"host can see items setting");
            toggle.value=true;Check(p.State.Value.StartWithItems,"toggle changes authoritative setting");p.Rematch();Mark("configured");
            while(p.State.Value.ConnectedMask!=3||p.State.Value.ReadyMask!=2)yield return null;
            toggle.value=false;Check(!p.State.Value.StartWithItems&&p.State.Value.ReadyMask==2,"changing items preserves ready flags");
            while(!Seen("client","off"))yield return null;
            toggle.value=true;p.Rematch();
        }
        else
        {
            Check(p.State.Value.StartWithItems,"late join receives current room item setting");
            Check(E("roomSettings").style.display.value==DisplayStyle.None,"client cannot access host controls");
            Check(((Label)E("roomShotSummary")).text.Contains("시작 아이템 · 있음"),"client sees initial item setting summary");
            m.SetRoomStartWithItems(false);yield return new WaitForSecondsRealtime(.15f);Check(p.State.Value.StartWithItems,"client cannot change room setting");p.Rematch();
            while(p.State.Value.StartWithItems)yield return null;
            Check(p.State.Value.ReadyMask==2,"ready preservation replicated");Mark("off");
            while(!p.State.Value.StartWithItems)yield return null;
        }
        while(p.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;
        Check(((Label)E("clockTitle")).text=="아이템 사용","item phase uses requested title");
        Check((role=="host"?E("roomSettings").style.display.value==DisplayStyle.Flex: E("roomSettings").style.display.value==DisplayStyle.None)&&p.State.Value.StartWithItems,"room item setting retained after start");
        m.SetRoomStartWithItems(false);yield return new WaitForSecondsRealtime(.15f);Check(p.State.Value.StartWithItems,"setting locked during match");
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
        Finish("PASS room items "+role+": "+checks+" toggle/authority/late-join/ready/title checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL room items timeout "+role,1);}
}
