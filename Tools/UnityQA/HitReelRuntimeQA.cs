using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class HitReelRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;int seat,checks;double deadline;
    sealed class QuickRandom:System.Random{public override int Next(int min,int max)=>min==0&&max==2?1:min;public override int Next(int max)=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--hit-reel-qa");if(i<0)return;var q=new GameObject("HitReelQA").AddComponent<HitReelRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","hit-reel-");q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=30;}
    object V(string field)=>typeof(RouletteView).GetField(field,F).GetValue(view);
    bool Marker=>((VisualElement)V("hitMarker")).style.display.value==DisplayStyle.Flex;
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");
    bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Check(bool v,string text){checks++;if(!v){Finish("FAIL "+role+": "+text,1);throw new Exception(text);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    IEnumerator Load()
    {while(p.State.Value.ForSeat(seat).Action!=DuelAction.Aim){var a=p.State.Value.ForSeat(seat);p.InputDigit(a.Digit(a.Progress),a.Version);while(p.State.Value.ForSeat(seat).Version==a.Version)yield return null;}}
    IEnumerator Shot(bool miss,DuelHitPart part)
    {
        yield return Load();double now=p.NetworkManager.ServerTime.Time;float y=0,x=65;
        if(!miss)DuelGunplay.Angles(DuelGunplay.Point(1-seat,part)-DuelGunplay.Eye(seat),out y,out x);
        DuelGunplay.Sway(seat,p.State.Value.ForSeat(seat).Weapon,true,now,now,out float sy,out float sp);
        int old=p.State.Value.Shots;p.Fire(miss?0:y-sy,miss?65:x-sp,true,now);
        while(p.State.Value.Shots==old)yield return null;
        if(miss){yield return null;Check(!Marker&&(int)V("lastHitShot")==0,"miss never produces hit marker");}
        else
        {double until=Time.realtimeSinceStartupAsDouble+.5;while((int)V("lastHitShot")!=old+1&&Time.realtimeSinceStartupAsDouble<until)yield return null;yield return null;Check(Marker&&(int)V("lastHitShot")==old+1,"authoritative hit reaches shooter owner");}
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17926");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();while(p.State.Value.ConnectedMask!=3)yield return null;
        if(role=="host")
        {var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,new QuickRandom());p.SelectWeapon(DuelWeapon.SawedOff);}
        p.Rematch();while(p.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        double selected=p.State.Value.Clock.PhaseAt;bool ticks=false,stop=false;
        var audio=(AudioSource)V("reelSound");
        foreach(var name in new[]{"reelTickSound","reelStopSound"})
        {var clip=(AudioClip)V(name);var samples=new float[clip.samples];clip.GetData(samples,0);double energy=0;foreach(float sample in samples)energy+=sample*sample;Check(energy>1,"audible PCM clip: "+name);}
        while(p.State.Value.Clock.Phase==ClockPhase.Selecting)
        {double age=p.NetworkManager.ServerTime.Time-selected;if(audio.isPlaying){if(age<PartyGames.SpinDuration)ticks=true;else stop=true;}Check(!Marker,"no permanent crosshair during reel");yield return null;}
        Check(ticks&&stop,"reel ticks and completion sound actually play");
        Check(p.State.Value.Clock.PhaseAt-selected>=5.8&&p.State.Value.Clock.PhaseAt-selected<6.2,"slower roulette respects common server deadline");
        while(p.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;m.GetComponent<DuelFirstPerson>().enabled=false;
        if(role=="host")
        {
            yield return Shot(true,DuelHitPart.None);Mark("miss");while(!Seen("client","miss"))yield return null;
            yield return Shot(false,DuelHitPart.Body);Check(p.State.Value.Seat1.Health==25,"shotgun actual hit75");
            view.ReportHit(p.State.Value.Round,p.State.Value.Seat0.Life-1,999);Check((int)V("lastHitShot")==2,"old life cannot show a new marker");
            yield return new WaitForSecondsRealtime(.4f);Check(!Marker,"marker fades out without permanent crosshair");Mark("hit");
            while(p.State.Value.Shots<3)yield return null;Check(!Marker&&(int)V("lastHitShot")==2,"foreign kill does not create victim hit marker");
        }
        else
        {
            while(!Seen("host","miss"))yield return null;Check(!Marker&&(int)V("lastHitShot")==0,"foreign miss has no marker");Mark("miss");
            while(!Seen("host","hit"))yield return null;Check(!Marker&&(int)V("lastHitShot")==0,"foreign shotgun hit has no shooter marker");
            yield return Shot(false,DuelHitPart.Head);Check(p.State.Value.Phase==DuelPhase.Finished&&p.State.Value.Winner==seat,"remote derringer kill is confirmed");
        }
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
        Finish("PASS hit/reel "+role+": "+checks+" owner/miss/shotgun/derringer/fade/PCM/audio/deadline checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL hit/reel timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.Phase+" shots "+p.State.Value.Shots),1);}
}
