using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class ShootingNoticeRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;DuelDirector d;int checks;double deadline;
    sealed class ClockRandom:System.Random{public override int Next(int min,int max)=>min;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--shoot-notice-qa");if(i<0)return;var q=new GameObject("ShootingNoticeQA").AddComponent<ShootingNoticeRuntimeQA>();q.role=a[i+1];q.path=Path.Combine(Application.dataPath,"..","shoot-notice-");q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=30;}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool v,string t){checks++;if(!v){Finish("FAIL shooting notice "+role+": "+t,1);throw new Exception(t);}}
    double Now=>p.NetworkManager.ServerTime.Time;
    Label Notice=>(Label)typeof(RouletteView).GetField("shootingNotice",F).GetValue(m.GetComponent<RouletteView>());
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(m,null);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17932");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;p=HelloWorldPlayer.Local;
        while(RouletteRules.Count(p.State.Value.ConnectedMask)<2)yield return null;m.GetComponent<DuelFirstPerson>().enabled=false;
        if(role=="host")
        {d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,new ClockRandom());d.Join(2,Now);m.SetRoomShotTime(0);d.Ready(2,Now);Publish();File.WriteAllText(path+"setup","ok");}
        while(!File.Exists(path+"setup")||p.State.Value.ConnectedMask!=7||p.State.Value.ShotTimeLimit!=0)yield return null;p.Rematch();while(p.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;
        var clock=p.State.Value.Clock;double goal=clock.StartedAt+clock.Target/100.0;while(Now<goal+(role=="host"?.25:0))yield return null;p.StopClock(Now);
        if(role=="host"){Check(d.StopClock(2,clock.Iteration,goal+.1,Now),"fixture second-place stop");Publish();}
        while(p.State.Value.Clock.Phase!=ClockPhase.Ranking)yield return null;yield return null;
        Check(p.State.Value.Clock.First==1&&p.State.Value.Clock.Last==0,"remote client wins opportunity");
        Check(Notice.style.display.value==DisplayStyle.Flex&&Notice.text.Contains(role=="client"?"당신은 사격자입니다":"준비 중입니다"),"different preparation notices");
        Check(Notice.ClassListContains("shooting-notice-own")== (role=="client"),"only shooter notice highlighted");
        while(p.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;
        Check(Notice.text.Contains(role=="client"?"사격을 준비하세요":"준비 중입니다"),"notice remains through item use");
        p.Fire(0,0,true,Now);yield return new WaitForSecondsRealtime(.2f);Check(p.State.Value.Shots==0,"preparation cannot fire");
        while(p.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;yield return null;
        Check(Notice.text==(role=="client"?"아무나 쏘세요.":"행운을 비십시오."),"shooting phase role notice");
        if(role=="host"){p.Fire(0,0,true,Now);yield return new WaitForSecondsRealtime(.1f);Check(p.State.Value.Shots==0,"non-shooter cannot use opportunity");File.WriteAllText(path+"host-notice","ok");}
        else
        {
            while(!File.Exists(path+"host-notice"))yield return null;
            double now=Now;DuelGunplay.Angles(DuelGunplay.Point(2,DuelHitPart.Head)-DuelGunplay.Eye(1),out float y,out float x);DuelGunplay.Sway(1,DuelWeapon.Revolver,true,now,now,out float sy,out float sp);p.Fire(y-sy,x-sp,true,now);
        }
        while(p.State.Value.Shots==0)yield return null;yield return null;
        Check(p.State.Value.LastTarget==2&&p.State.Value.Seat2.LivesRemaining==2&&p.State.Value.Seat0.LivesRemaining==3,"can damage second-place opponent instead of last place");
        Check(p.State.Value.Clock.Phase==ClockPhase.Returning&&Notice.style.display.value==DisplayStyle.None,"notice clears after shot");
        File.WriteAllText(path+role+"-checked","ok");while(!File.Exists(path+"host-checked")||!File.Exists(path+"client-checked"))yield return null;
        Finish("PASS shooting notice "+role+": "+checks+" role/preparation/permission/free-target/cleanup checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL shooting notice timeout "+role,1);}
}
