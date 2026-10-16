using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class ClockRankingRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;DuelDirector d;int checks;double deadline;
    sealed class ClockRandom:System.Random{public override int Next(int min,int max)=>min;public override int Next(int max)=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--ranking-qa");if(i<0)return;var q=new GameObject("RankingQA").AddComponent<ClockRankingRuntimeQA>();q.role=a[i+1];q.path=Path.Combine(Application.dataPath,"..","ranking-");q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=30;}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool v,string t){checks++;if(!v){Finish("FAIL ranking "+role+": "+t,1);throw new Exception(t);}}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17931");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;p=HelloWorldPlayer.Local;
        while(RouletteRules.Count(p.State.Value.ConnectedMask)<2)yield return null;m.GetComponent<DuelFirstPerson>().enabled=false;
        if(role=="host")
        {d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,new ClockRandom());for(int i=2;i<6;i++)d.Join(i,p.NetworkManager.ServerTime.Time);for(int i=2;i<6;i++)d.Ready(i,p.NetworkManager.ServerTime.Time);typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(m,null);File.WriteAllText(path+"setup","ok");}
        while(!File.Exists(path+"setup")||p.State.Value.ConnectedMask!=63)yield return null;p.Rematch();while(p.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;
        var clock=p.State.Value.Clock;double goal=clock.StartedAt+clock.Target/100.0;
        if(role=="host")
        {while(p.NetworkManager.ServerTime.Time<goal)yield return null;p.StopClock(p.NetworkManager.ServerTime.Time);for(int i=2;i<6;i++)d.StopClock(i,clock.Iteration,p.NetworkManager.ServerTime.Time,p.NetworkManager.ServerTime.Time);typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(m,null);}
        // Client deliberately times out: its own row must still be highlighted.
        while(p.State.Value.Clock.Phase!=ClockPhase.Ranking)yield return null;yield return new WaitForSecondsRealtime(.2f);
        var view=m.GetComponent<RouletteView>();var rows=(Label[])typeof(RouletteView).GetField("clockRankingEntries",F).GetValue(view);int shown=0,own=0;
        foreach(var row in rows){if(row.style.display.value==DisplayStyle.None)continue;shown++;if(row.ClassListContains("clock-ranking-own")){own++;Check(row.text.Contains("나 · "+p.State.Value.NameForSeat(p.Seat.Value)),"own nickname marker");Check(row.resolvedStyle.backgroundColor.a>.4f&&row.resolvedStyle.color.r>.8f,"own gold background and text");if(role=="client")Check(row.text.Contains("시간 초과"),"timeout own record highlighted");}}
        Check(shown==6&&own==1,"six rows and exactly one own row per client");
        double ranked=p.State.Value.Clock.PhaseAt;
        while(p.NetworkManager.ServerTime.Time<ranked+3.5)yield return null;Check(p.State.Value.Clock.Phase==ClockPhase.Ranking,"results remain after former three second deadline");
        while(p.NetworkManager.ServerTime.Time<ranked+5.7)yield return null;Check(p.State.Value.Clock.Phase==ClockPhase.Ranking,"full six seconds to read rankings");
        while(p.State.Value.Clock.Phase==ClockPhase.Ranking)yield return null;Check(p.State.Value.Clock.Phase==ClockPhase.ItemPreparation&&p.NetworkManager.ServerTime.Time-ranked>=6,"server advances to items after six seconds");
        File.WriteAllText(path+role+"-checked","ok");while(!File.Exists(path+"host-checked")||!File.Exists(path+"client-checked"))yield return null;
        Finish("PASS ranking "+role+": "+checks+" timing/local-highlight/timeout/six-row checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL ranking timeout "+role,1);}
}

