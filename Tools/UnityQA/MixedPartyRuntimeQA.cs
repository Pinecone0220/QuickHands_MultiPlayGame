using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class MixedPartyRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;DuelFirstPerson fps;
    int seat,trace=-1,stopped,checks;double deadline;
    bool wrong,shotClock,capturedQuick,capturedClock,checkedRevive;
    int preparedGames;bool sawQuickItems,sawClockItems;double itemDeadline;
    sealed class AlternatingRandom:System.Random
    {int games;readonly bool ai;public AlternatingRandom(bool ai=false){this.ai=ai;}public override int Next(int min,int max)=>min==0&&max==2?(games++%2==0?1:0):min;public override int Next(int max)=>0;public override double NextDouble()=>ai?.5:0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--mixed-party-qa");if(i<0)return;var q=new GameObject("MixedPartyQA").AddComponent<MixedPartyRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","mixed-party-");q.deadline=Time.realtimeSinceStartupAsDouble+270;Application.targetFrameRate=30;}
    void Check(bool value,string text){checks++;if(!value){Finish("FAIL mixed party "+role+" seat "+seat+": "+text,1);throw new Exception(text);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    VisualElement Element(string name)=>(VisualElement)typeof(RouletteView).GetField(name,F).GetValue(view);
    void Prop(string name,object value)=>typeof(DuelFirstPerson).GetField("<"+name+">k__BackingField",F).SetValue(fps,value);
    IEnumerator Load()
    {
        while(p.State.Value.ForSeat(seat).Action!=DuelAction.Aim)
        {
            if(!(bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null)){yield return null;continue;}
            var a=p.State.Value.ForSeat(seat);typeof(RouletteView).GetMethod("Press",F).Invoke(view,new object[]{a.Digit(a.Progress)});
            while(p.State.Value.ForSeat(seat).Version==a.Version)yield return null;yield return null;
        }
    }
    IEnumerator Shot(int victim,DuelHitPart part)
    {
        yield return Load();double now=p.NetworkManager.ServerTime.Time;
        DuelGunplay.Angles(DuelGunplay.Point(victim,part)-DuelGunplay.Eye(seat),out float y,out float x);DuelGunplay.Sway(seat,true,now,now,out float sy,out float sp);y-=sy;x-=sp;
        Prop("Yaw",y);Prop("Pitch",x);Prop("Ads",true);int shots=p.State.Value.Shots;p.Fire(y,x,true,now);
        while(p.State.Value.Shots==shots)yield return null;yield return null;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;bool ai=role=="ai";
        if(ai){m.AiCount=5;m.Difficulty=AiDifficulty.Hard;}
        m.Begin(ai?DuelMode.AI:DuelMode.PvP,role!="host"&&!ai,"127.0.0.1","17889");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();fps=m.GetComponent<DuelFirstPerson>();
        while(p.State.Value.ConnectedMask!=63)yield return null;
        if(role=="host"||ai)
        {var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var r=new AlternatingRandom(ai);typeof(DuelDirector).GetField("random",F).SetValue(d,r);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,r);}
        if(seat==0||seat==4){p.SelectWeapon(DuelWeapon.SawedOff);while(p.State.Value.ForSeat(seat).SelectedWeapon!=DuelWeapon.SawedOff)yield return null;}
        p.Rematch();while(p.State.Value.Stage!=DuelStage.Racing)yield return null;
        fps.enabled=false;
        while(p.State.Value.Phase==DuelPhase.Playing)
        {
            var s=p.State.Value;var c=s.Clock;double now=p.NetworkManager.ServerTime.Time;
            int key=c.Iteration*10+(int)c.Phase;if(trace!=key){trace=key;Debug.Log($"MixedQA {role} game {c.Iteration} {c.MiniGame}/{c.Phase} localAlive {s.AliveMask} partyAlive {s.PartyAliveMask} shots {s.Shots}");}
            Check(!s.Results.IsValid,"no full party result at intermediate mini-game end");
            if(c.Phase==ClockPhase.Selecting)
            {
                Check(PartyGames.Count==2,"two mini-games only");Check(Element("partySelection").style.display.value==DisplayStyle.Flex,"roulette shown");
                if(role=="host"&&c.Iteration==1&&!capturedQuick&&now-c.PhaseAt>2.5){capturedQuick=true;yield return Capture("quick-selected");}
                if(role=="host"&&c.Iteration==2&&!capturedClock&&now-c.PhaseAt>2.5){capturedClock=true;yield return Capture("clock-selected");}
                if(!ai&&c.Iteration==2)
                {
                    Check(s.PartyAliveMask==62&&s.AliveMask==62,"only first quick death eliminated");
                    if(seat>0){Check(s.ForSeat(seat).Health==100&&s.ForSeat(seat).DiedAt<0&&s.ForSeat(seat).Life==2,"temporary deaths revive with full health");Check(s.ForSeat(seat).Weapon==(seat==4?DuelWeapon.SawedOff:DuelWeapon.Revolver),"selected gun retained");}
                    if(!checkedRevive&&seat>0)
                    {checkedRevive=true;while(p.DeathReport.Value.Count>0)yield return null;Check(Element("deathDetails").style.display.value==DisplayStyle.None,"revival clears owner death report and panel");fps.enabled=true;yield return null;Check(fps.Active,"revival restores first-person input");fps.enabled=false;}
                }
            }
            else if(c.Phase==ClockPhase.ItemPreparation)
            {
                if(preparedGames!=c.Iteration)
                {
                    preparedGames=c.Iteration;itemDeadline=c.StartedAt;
                    if(c.MiniGame==PartyMiniGame.QuickHands)sawQuickItems=true;else sawClockItems=true;
                    var a=s.ForSeat(seat);p.InputDigit(a.Digit(0),a.Version);p.Fire(0,0,true,now);
                }
                Check(c.StartedAt==itemDeadline&&Math.Abs(c.StartedAt-c.PhaseAt-5)<.00001,"shared five-second item deadline");
                Check(Element("clockPanel").style.display.value==DisplayStyle.Flex&&((Label)Element("clockTitle")).text=="아이템 사용","both mini-games show item preparation");
                Check(!(bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null),"numeric input unavailable during items");
                if(c.MiniGame==PartyMiniGame.Countdown)
                {Check(s.ForSeat(c.First).Action!=DuelAction.Aim,"countdown winner not granted shot early");Check(now-c.PhaseAt<5.1,"item stage bounded");}
            }
            else if(c.Phase==ClockPhase.QuickHands)
            {
                Check(Element("clockPanel").style.display.value==DisplayStyle.None,"no timer panel during quick hands");
                if(!ai&&seat==0&&!wrong)
                {wrong=true;var a=s.ForSeat(seat);typeof(RouletteView).GetMethod("Press",F).Invoke(view,new object[]{a.Digit(a.Progress)%5+1});}
                if(!ai&&seat==5)
                    for(int victim=0;victim<5;victim++)if(p.State.Value.Alive(victim))yield return Shot(victim,DuelHitPart.Head);
            }
            else if(c.Phase==ClockPhase.QuickResults && !ai)
            {
                Check(c.First==5&&c.Last==(c.Iteration==1?0:1),"first death is only permanent loser");
                Check(s.PartyAliveMask==(c.Iteration==1?62:60)&&s.AliveMask==32,"mini winner does not end party");
                if(role=="host"&&c.Iteration==1)yield return Capture("quick-results");
            }
            else if(c.Phase==ClockPhase.Counting && !ai)
            {
                Check(!(bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null),"numeric action cannot leak into clock");
                int guns=0;foreach(var t in m.Table.GetComponentsInChildren<Transform>())if(t.name.StartsWith("6연발 리볼버 seat")||t.name.StartsWith("단발 소드오프 seat"))guns++;
                Check(guns==1,"clock has one shared gun");
                bool stop=s.Alive(seat)&&(c.Iteration==2||seat==5);
                double sample=c.StartedAt+c.Target/100.0+(5-seat)*.1;
                if(stop&&stopped!=c.Iteration&&now>=sample){stopped=c.Iteration;p.StopClock(sample);}
            }
            else if(c.Phase==ClockPhase.Shooting&&!ai&&c.First==seat&&!shotClock)
            {
                shotClock=true;double shotTime=p.NetworkManager.ServerTime.Time;
                DuelGunplay.Angles(DuelGunplay.Point(c.Last,DuelHitPart.Body)-DuelGunplay.Eye(seat),out float y,out float x);DuelGunplay.Sway(seat,true,shotTime,shotTime,out float sy,out float sp);p.Fire(y-sy,x-sp,true,shotTime);
            }
            yield return null;
        }
        var final=p.State.Value;Check(final.Results.IsValid&&RouletteRules.Count(final.PartyAliveMask)==1,"whole party final only at one survivor");
        Check(sawQuickItems&&sawClockItems,"both item windows exercised before party final");
        if(!ai)
        {
            Check(final.Winner==5&&final.Shots==10&&final.Results.Seat5.Kills==9&&final.Results.Seat5.DamageDealt==900,"mixed cumulative kills and actual damage");
            Check(final.Results.Seat0.Mistakes==1&&final.Results.Seat5.ClockWins==2&&final.Results.Seat5.CorrectInputs==96,"mixed numeric and clock stats preserved");
            if(seat==1){while(p.DeathReport.Value.Count!=2)yield return null;Check(p.DeathReport.Value.Life==2,"new life has only clock and second quick hits");}
            if(seat>=2&&seat<=4){Check(final.ForSeat(seat).DeathCause==DuelDeathCause.ClockTimeout,"final clock timeouts");while(p.DeathReport.Value.Life!=3)yield return null;Check(p.DeathReport.Value.Count==0,"old quick bullet deaths not retained after revival");}
        }
        while(p.State.Value.Stage!=DuelStage.Idle)yield return null;yield return new WaitForSecondsRealtime(3.1f);
        if(role=="host")yield return Capture("final-results");
        File.WriteAllText(path+role+"-ready","ready");
        if(!ai){foreach(string r in new[]{"host","c1","c2","c3","c4","c5"})while(!File.Exists(path+r+"-ready"))yield return null;}
        p.Rematch();while(p.State.Value.Round==final.Round)yield return null;Check(p.State.Value.PartyAliveMask==63&&p.State.Value.Seat5.Kills==0&&(ai||p.State.Value.Seat4.SelectedWeapon==DuelWeapon.SawedOff),"all connected ready resets whole party");
        File.WriteAllText(path+role+"-reset","ready");
        if(!ai){foreach(string r in new[]{"host","c1","c2","c3","c4","c5"})while(!File.Exists(path+r+"-reset"))yield return null;}
        Finish("PASS mixed party "+role+" seat "+seat+": "+checks+" quick/clock/loser/revive/life-report/stats/ready checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL mixed party timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.Phase.ToString()),1);}
    IEnumerator Capture(string name)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.15f);
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path+name+".png",scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
}
