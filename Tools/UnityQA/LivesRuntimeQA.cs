using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class LivesRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;DuelDirector d;int seat,checks;double deadline;
    sealed class QuickRandom:System.Random{public override int Next(int min,int max)=>min==0&&max==2?1:min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--lives-qa");if(i<0)return;var q=new GameObject("LivesQA").AddComponent<LivesRuntimeQA>();q.role=a[i+1];q.path=Path.Combine(Application.dataPath,"..","lives-");q.deadline=Time.realtimeSinceStartupAsDouble+240;Application.targetFrameRate=30;}
    object V(string field)=>typeof(RouletteView).GetField(field,F).GetValue(view);
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");
    bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Check(bool value,string text){checks++;if(!value){Finish("FAIL lives "+role+": "+text,1);throw new Exception(text);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    double Now=>p.NetworkManager.ServerTime.Time;
    DuelActionState Own=>p.State.Value.ForSeat(seat);
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(m,null);
    IEnumerator Load()
    {while(Own.Action!=DuelAction.Aim){var a=Own;p.InputDigit(a.Digit(a.Progress),a.Version);while(Own.Version==a.Version)yield return null;}}
    IEnumerator Shoot(int target)
    {
        double now=Now;DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(seat),out float y,out float x);DuelGunplay.Sway(seat,DuelWeapon.Revolver,true,now,now,out float sy,out float sp);
        int shots=p.State.Value.Shots;p.Fire(y-sy,x-sp,true,now);while(p.State.Value.Shots==shots)yield return null;yield return null;
    }
    IEnumerator CheckRecovery(int victim,int remaining,string stage)
    {
        while(p.State.Value.ForSeat(victim).DiedAt<0)yield return null;yield return new WaitForSecondsRealtime(.18f);
        Check(p.State.Value.ForSeat(victim).LivesRemaining==remaining,"life loss replicated "+stage);Check(((Label[])V("lifeIcons"))[victim].text==new string('♥',remaining),"remaining life icons "+stage);
        if(victim==seat)
        {string text=((Label)V("victoryText")).text;Check(text.Contains("목숨을 잃었습니다!")&&text.Contains("남은 목숨: "+remaining),"owner life-loss banner "+stage);Check(p.DeathReport.Value.Count>0&&p.DeathReport.Value.Life==Own.Life,"own cause report "+stage);}
        var action=p.State.Value.ForSeat(victim);int incarnation=action.Life;
        while(p.State.Value.ForSeat(victim).ReviveAt<0)yield return null;double rise=p.State.Value.ForSeat(victim).ReviveAt;
        while(Now<rise+.48)yield return null;yield return null;
        var puppets=(Array)typeof(RouletteTable).GetField("puppets",F).GetValue(m.Table);object puppet=puppets.GetValue(victim);Type type=puppet.GetType();
        var root=(Transform)type.GetField("Root").GetValue(puppet);var body=(Transform)type.GetField("Body").GetValue(puppet);
        foreach(string name in new[]{"Hand","LeftHand"}){var hand=(Transform)type.GetField(name).GetValue(puppet);Check(Mathf.Abs(hand.position.y-.94f)<.08f&&Vector3.Distance(hand.position,root.TransformPoint(new Vector3(name=="Hand"?-.36f:.36f,.94f,-.92f)))<.08f,"both hands brace tabletop "+stage);}
        Check(Mathf.Abs(Mathf.DeltaAngle(body.localEulerAngles.x,0))>10&&Mathf.Abs(Mathf.DeltaAngle(body.localEulerAngles.x,0))<78,"body rises from collapsed posture "+stage);
        Mark(stage+"-rise");if(role=="host")yield return Capture(stage,victim);
        while(p.State.Value.ForSeat(victim).DiedAt>=0)yield return null;yield return null;
        Check(p.State.Value.ForSeat(victim).Life>incarnation&&p.State.Value.ForSeat(victim).Health==100&&p.State.Value.ForSeat(victim).LivesRemaining==remaining,"revival HP100 life unchanged "+stage);
        if(victim==seat){Check(p.DeathReport.Value.Count==0,"old death report cleared "+stage);Check(!((Label)V("victoryText")).ClassListContains("death-banner"),"old death banner cleared "+stage);}
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17930");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();while(RouletteRules.Count(p.State.Value.ConnectedMask)<2)yield return null;
        m.GetComponent<DuelFirstPerson>().enabled=false;
        if(role=="host")
        {d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,new QuickRandom());Check(d.Join(2,Now),"third seat fixture joins lobby");if(!m.DeveloperMode)m.ToggleDeveloperMode();m.SetRoomShotTime(0);d.Ready(2,Now);Publish();Mark("setup");}
        while(!Seen("host","setup")||p.State.Value.ConnectedMask!=7||p.State.Value.ShotTimeLimit!=0)yield return null;p.Rematch();
        for(int round=1;round<=3;round++)
        {
            while(p.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;
            Check(p.State.Value.Clock.MiniGame==PartyMiniGame.QuickHands&&Own.LivesRemaining==(role=="host"?4-round:4-round),"quick iteration start lives "+round);
            if(role=="host")
            {
                yield return Load();yield return Shoot(1);
                if(round==3){while(!Seen("client","permanent"))yield return null;}
                yield return Shoot(2);
            }
            if(round<3)yield return CheckRecovery(1,3-round,"quick"+round);
            else
            {
                if(role=="client")
                {while(Own.DiedAt<0)yield return null;yield return new WaitForSecondsRealtime(.2f);Check(Own.LivesRemaining==0&&!p.State.Value.InParty(seat),"last life permanently eliminated");Check(((Label)V("victoryText")).text.Contains("사망했습니다!")&&((Label)V("victoryText")).text.Contains("남은 목숨: 0"),"zero-life death banner on remote client");Mark("permanent");}
                break;
            }
            while(p.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;
            Check(p.State.Value.Clock.MiniGame==PartyMiniGame.Countdown,"quick excluded next roulette");var clock=p.State.Value.Clock;double goal=clock.StartedAt+clock.Target/100.0;
            while(Now<goal+(role=="host"?.25:0))yield return null;p.StopClock(Now);
            if(role=="host"){while(Now<goal+.1)yield return null;Check(d.StopClock(2,clock.Iteration,goal+.1,Now),"third seat clock stop");Publish();}
            while(p.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;Check(p.State.Value.Clock.First==1&&p.State.Value.Clock.Last==0,"client wins clock opportunity");
            if(role=="client")yield return Shoot(0);
            yield return CheckRecovery(0,3-round,"clock"+round);
        }
        while(p.State.Value.Phase!=DuelPhase.Finished)yield return null;yield return new WaitForSecondsRealtime(.3f);
        Check(p.State.Value.Winner==0&&Own.LivesRemaining==(role=="host"?1:0),"final survivor only after last lives");
        Check(((Label)V("victoryText")).text.Contains("最終")==false&&((Label)V("victoryText")).text.Contains("최종 승리")&&!((Label)V("victoryText")).ClassListContains("death-banner"),"final victory overrides own death banner");
        yield return new WaitForSecondsRealtime(2);if(role=="host"){d.Ready(2,Now);Publish();}p.Rematch();while(p.State.Value.Round<2)yield return null;
        Check(Own.Health==100&&Own.LivesRemaining==3,"all connected ready resets three lives");Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
        Finish("PASS lives "+role+": "+checks+" replication/banner/recovery/hands/no-repeat/zero-lives/final/rematch checks");
    }
    IEnumerator Capture(string label,int victim)
    {
        yield return null;var camera=new GameObject("Lives QA camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;var puppets=(Array)typeof(RouletteTable).GetField("puppets",F).GetValue(m.Table);var puppet=puppets.GetValue(victim);var root=(Transform)puppet.GetType().GetField("Root").GetValue(puppet);camera.transform.position=root.TransformPoint(new Vector3(2.2f,1.7f,-1));camera.transform.LookAt(root.TransformPoint(new Vector3(0,1,-.35f)));camera.fieldOfView=50;
        var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var prev=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(path+label+".png",image.EncodeToPNG());RenderTexture.active=prev;rt.Release();Destroy(rt);Destroy(image);Destroy(camera.gameObject);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL lives timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.Phase+" iteration "+p.State.Value.Clock.Iteration+" HP "+Own.Health+" lives "+Own.LivesRemaining),1);}
}



