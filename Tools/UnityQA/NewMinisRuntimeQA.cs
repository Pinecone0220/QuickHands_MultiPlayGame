using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
public sealed class NewMinisRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;DuelDirector d;int seat,checks;double deadline;
    sealed class MiniRandom:System.Random{public override int Next(int min,int max)=>min==0?2:min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--new-minis-qa");if(i<0)return;var q=new GameObject("NewMinisQA").AddComponent<NewMinisRuntimeQA>();q.role=a[i+1];q.path=Path.Combine(Application.dataPath,"..","new-minis-");q.deadline=Time.realtimeSinceStartupAsDouble+180;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
    T V<T>(string field)=>(T)typeof(RouletteView).GetField(field,F).GetValue(view);
    double Now=>p.NetworkManager.ServerTime.Time;
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool v,string text){checks++;if(!v){Finish("FAIL new minis "+role+": "+text,1);throw new Exception(text);}}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(m,null);
    IEnumerator Press()
    {
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
        var point=V<Button>("clockStop").worldBound.center;var position=new Vector2(point.x,Screen.height-point.y);
        InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=position,buttons=1});yield return new WaitForSecondsRealtime(.1f);
        InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=position,buttons=0});yield return new WaitForSecondsRealtime(.1f);
        if(!V<bool>("clockStopped")){Debug.Log("BACKGROUND_INPUT_FALLBACK "+role);typeof(RouletteView).GetMethod("StopClock",F).Invoke(view,null);}
        while(V<bool>("clockAwaiting"))yield return null;yield return null;
    }
    IEnumerator Shoot(int target,DuelHitPart part)
    {
        var fps=m.GetComponent<DuelFirstPerson>();fps.enabled=false;double now=Now;DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(seat),out float y,out float x);DuelGunplay.Sway(seat,DuelWeapon.Revolver,true,now,now,out float sy,out float sp);int shots=p.State.Value.Shots;p.Fire(y-sy,x-sp,true,now);while(p.State.Value.Shots==shots)yield return null;fps.enabled=true;yield return null;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(role=="host"?1:role=="client"?2:3);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17934");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();while(RouletteRules.Count(p.State.Value.ConnectedMask)<3)yield return null;
        if(role=="host")
        {d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,new MiniRandom());var names=(FixedString64Bytes[])typeof(HelloWorldManager).GetField("names",F).GetValue(m);for(int i=3;i<6;i++){d.Join(i,Now);names[i]=new FixedString64Bytes("Player"+(i+1));}if(!m.DeveloperMode)m.ToggleDeveloperMode();m.SetRoomShotTime(0);for(int i=3;i<6;i++)d.Ready(i,Now);Publish();Mark("setup");}
        while(!Seen("host","setup")||p.State.Value.ConnectedMask!=63||p.State.Value.ShotTimeLimit!=0)yield return null;p.Rematch();
        while(p.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;Check(p.State.Value.Clock.MiniGame==PartyMiniGame.Signal,"signal appears in common selection roulette");
        Check(V<Label[]>("partyReelLabels").Length>=4,"selection supports four entries");
        while(p.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;Check(p.State.Value.Clock.MiniGame==PartyMiniGame.Signal&&p.State.Value.Clock.Target==0,"signal begins without leaking true cue time");
        if(role=="host")
        {
            while(p.State.Value.Clock.Signal!=MiniSignal.Fake)yield return null;yield return Press();Check(V<bool>("clockStopped")&&V<MiniFailure>("clockFailure")==MiniFailure.EarlyInput,"fake cue input gateway failed stopped="+V<bool>("clockStopped")+" failure="+V<MiniFailure>("clockFailure")+" cue="+p.State.Value.Clock.Signal+" phase="+p.State.Value.Clock.Phase);yield return Capture("signal-fake");
            while(p.State.Value.Clock.Signal!=MiniSignal.Go)yield return null;double go=p.State.Value.Clock.SignalAt;while(Now<go+1.15)yield return null;for(int i=3;i<6;i++)Check(d.StopClock(i,d.Clock.Iteration,Now,Now),"six-seat scripted reaction accepted");Publish();
        }
        else if(role=="client")
        {
            while(p.State.Value.Clock.Signal!=MiniSignal.Go||V<Label>("clockTimer").text!="지금!")yield return null;Check(Now>=p.State.Value.Clock.SignalAt&&V<Label>("clockTimer").text=="지금!"&&V<Label>("clockTimer").ClassListContains("signal-go"),"green true cue renders");Check(p.State.Value.Clock.Seat0.Failure==MiniFailure.None,"peer false start remains private");yield return Capture("signal-go");yield return Press();Check(V<bool>("clockStopped")&&V<MiniFailure>("clockFailure")==MiniFailure.None&&V<int>("clockRecord")>=0,"reaction input gateway confirmed by server");
        }
        while(p.State.Value.Clock.Phase!=ClockPhase.Ranking)yield return null;yield return null;
        Check(p.State.Value.Clock.First==1&&p.State.Value.Clock.Seat0.Failure==MiniFailure.EarlyInput&&p.State.Value.Clock.Seat2.Failure==MiniFailure.MissedSignal,"six-player results distinguish early/missed/success");
        Check(p.State.Value.AliveMask==63&&p.State.Value.ForSeat(seat).LivesRemaining==3,"mini failures never cause direct life loss");CheckOwnRow();if(role=="witness")yield return Capture("signal-results");
        while(p.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;Check(V<Label>("shootingNotice").text.Contains(role=="client"?"당신은 사격자입니다":"준비 중입니다"),"new mini role notice during item phase");
        while(p.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;yield return null;Check(V<Label>("shootingNotice").text==(role=="client"?"아무나 쏘세요.":"행운을 비십시오."),"role notices on new-mini shooting");Mark("signal-shoot");
        if(role=="client"){while(!Seen("host","signal-shoot")||!Seen("witness","signal-shoot"))yield return null;yield return Shoot(3,DuelHitPart.Body);}
        while(p.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;Check(p.State.Value.Seat3.Health==50&&p.State.Value.Seat3.LivesRemaining==3,"signal opportunity applies body damage without life loss");
        while(p.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;Check(p.State.Value.Clock.MiniGame==PartyMiniGame.Greed&&p.State.Value.Seat3.Health==50,"greed selected after signal and preserves living health");
        double start=p.State.Value.Clock.StartedAt;yield return null;
        Check(start-p.State.Value.Clock.PhaseAt==3&&Now<start,"greed has three-second preparation on every peer");
        Check(V<VisualElement>("quickStartLayer").style.display.value==DisplayStyle.Flex&&V<Label>("quickStartTitle").text=="욕심의 대가","greed countdown uses centered start panel");
        Check(V<VisualElement>("nameplateLayer").style.display.value==DisplayStyle.None,"nameplates do not overlap start countdown");
        typeof(RouletteView).GetMethod("StopClock",F).Invoke(view,null);Check(!V<bool>("clockStopped"),"countdown input cannot bank score");
        if(role=="witness")yield return Capture("greed-countdown");
        if(role=="host"){while(Now<start+.9)yield return null;yield return Press();Check(V<bool>("clockStopped")&&V<int>("clockRecord")>0,"host banks points through input gateway");}
        if(role=="client"){while(Now<start+2.3)yield return null;yield return Press();Check(V<bool>("clockStopped")&&V<int>("clockRecord")>0,"remote player banks points through input gateway");yield return Capture("greed-banked");}
        if(role=="witness")
        {while(Now<start+2.8)yield return null;Check(p.State.Value.Clock.Seat0.Points==-1&&p.State.Value.Clock.Seat1.Points==-1,"peer banked scores private");
            int before=int.Parse(V<Label>("clockTimer").text.Split('점')[0]);double sample=Now;yield return new WaitForSecondsRealtime(.15f);
            int after=int.Parse(V<Label>("clockTimer").text.Split('점')[0]);Check(after>before&&Math.Abs((after-before)-(Now-sample)*40)<3,"rendered score follows constant rate between snapshots");
            yield return Capture("greed-running");while(p.State.Value.Clock.Signal!=MiniSignal.Burst)yield return null;yield return null;Check(V<Label>("clockTimer").text.Contains("터졌습니다! 0점"),"unbanked player sees crash animation message");yield return Capture("greed-burst");}
        while(p.State.Value.Clock.Phase!=ClockPhase.Ranking)yield return null;yield return null;
        Check(p.State.Value.Clock.First==1&&p.State.Value.Clock.Seat1.Points>p.State.Value.Clock.Seat0.Points&&p.State.Value.Clock.Seat2.Failure==MiniFailure.Burst&&p.State.Value.Clock.Seat2.Points==0,"highest banked points win, failed points zero");Check(p.State.Value.ForSeat(seat).LivesRemaining==3,"greed crash retains lives");CheckOwnRow();
        if(role=="witness")yield return Capture("greed-results");double rankedAt=p.State.Value.Clock.PhaseAt;
        while(Now<rankedAt+6.5)yield return null;Check(p.State.Value.Clock.Phase==ClockPhase.Ranking,"greed results stay visible after six seconds");
        while(p.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;Check(Now>=rankedAt+7.8,"greed reveal lasts eight seconds before items");
        while(p.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;Mark("greed-shoot");
        if(role=="client"){while(!Seen("host","greed-shoot")||!Seen("witness","greed-shoot"))yield return null;yield return Shoot(3,DuelHitPart.Head);}
        while(p.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;Check(p.State.Value.Seat3.LivesRemaining==2&&p.State.Value.Seat3.DiedAt>=0,"greed opportunity headshot loses one life");
        while(p.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;Check(p.State.Value.AliveMask==63&&p.State.Value.Seat3.Health==100&&p.State.Value.Clock.MiniGame!=PartyMiniGame.Greed,"revival and immediate-repeat exclusion work");Check(p.State.Value.Seat1.ClockWins==0&&p.State.Value.Seat1.ClockStops==0,"new mini statistics do not pollute countdown");
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked")||!Seen("witness","checked"))yield return null;
        Finish("PASS new minis "+role+": "+checks+" input/privacy/6-seat/results/items/shot/lives/roulette/UI checks");
    }
    void CheckOwnRow(){Check(V<VisualElement>("nameplateLayer").style.display.value==DisplayStyle.None,"nameplates do not overlap mini results");int own=0;foreach(var r in V<Label[]>("clockRankingEntries"))if(r.ClassListContains("clock-ranking-own")&&r.style.display.value!=DisplayStyle.None){own++;Check(r.text.Contains("나 · "),"own result highlighted");}Check(own==1,"exactly one own record");}
    IEnumerator Capture(string label)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.06f);
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path+label+".png",scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL new minis timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.MiniGame+" "+p.State.Value.Clock.Phase),1);}
}

