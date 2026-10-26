using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

public sealed class GameFeedbackRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    DuelGameAudio feedback;RouletteView view;UIDocument document;int checks;double deadline;
    readonly List<string> cues=new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--game-feedback-qa")>=0)new GameObject("Game feedback QA").AddComponent<GameFeedbackRuntimeQA>();}
    T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    void Check(bool ok,string message){checks++;if(!ok){Debug.LogError("FAIL game feedback: "+message);Exit(1);throw new Exception(message);}}
    int Count(string key)=>cues.FindAll(x=>x==key).Count;
    static void Exit(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+180;yield return new WaitForSecondsRealtime(1);
        var manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();view.enabled=false;
        feedback=manager.GetComponent<DuelGameAudio>();feedback.Played+=key=>cues.Add(key);
        var timer=Field<AudioSource>(feedback,"timer");var score=Field<AudioSource>(feedback,"score");
        var assets=Resources.LoadAll<AudioClip>("GameFeedback");Check(assets.Length==15,"all 15 cues bundled");
        foreach(var clip in assets)
        {
            var pcm=new float[clip.samples*clip.channels];Check(clip.GetData(pcm,0),"decoded "+clip.name);
            float peak=0;foreach(float x in pcm)peak=Mathf.Max(peak,Mathf.Abs(x));
            Check(peak>.1f&&peak<.7f&&clip.length<1,"short audible clip with headroom "+clip.name);
        }
        feedback.UiClick();feedback.KeyInput();feedback.KeyInput(true);
        Check(Count("UiClick")==1&&Count("KeyTap")==1&&Count("KeyError")==1,"separate local click, key and error cues");
        var s=new DuelSnapshot{Round=10,Phase=DuelPhase.Playing,Stage=DuelStage.Racing,AliveMask=1,
            Clock=new ClockState{Iteration=1,Phase=ClockPhase.Counting,MiniGame=PartyMiniGame.Countdown,StartedAt=100,HideAfter=2.6,Target=1000}};
        feedback.Observe(s,0,99.9);Check(!timer.isPlaying,"no ticks before the shared start");
        feedback.Observe(s,0,102.5);Check(timer.isPlaying&&Mathf.Abs(timer.volume-.17f)<.001,"audible visible clock");
        feedback.Observe(s,0,102.975);Check(Mathf.Abs(timer.volume-.085f)<.001,"halfway through hide fade");
        feedback.Observe(s,0,103.36);Check(!timer.isPlaying&&timer.volume==0,"hidden clock goes fully silent");
        Check(s.Clock.StartedAt==100&&s.Clock.Target==1000,"audio never changes timer math");
        s.Clock.Iteration++;feedback.Observe(s,0,101);feedback.StopPending();feedback.Observe(s,0,101.2);
        Check(!timer.isPlaying,"stop input silences clock immediately");
        feedback.ReportStop(10,2,false,PartyMiniGame.Countdown,MiniFailure.None);feedback.Observe(s,0,101.3);
        Check(timer.isPlaying,"rejected stop restores clock");
        feedback.ReportStop(10,2,true,PartyMiniGame.Countdown,MiniFailure.None);feedback.ReportStop(10,2,true,PartyMiniGame.Countdown,MiniFailure.None);
        Check(Count("ClockConfirm")==1,"accepted stop confirms only once");
        s.Clock.Iteration++;s.Clock.MiniGame=PartyMiniGame.Signal;s.Clock.Signal=MiniSignal.Wait;
        feedback.Observe(s,0,100.05);feedback.Observe(s,0,100.1);Check(Count("SignalWait")==1,"initial wait cue without per-frame repeats");
        s.Clock.Signal=MiniSignal.Fake;s.Clock.SignalAt=100.8;feedback.Observe(s,0,100.85);feedback.Observe(s,0,100.9);
        Check(Count("SignalFake")==1,"fake signal once");
        s.Clock.Signal=MiniSignal.Wait;feedback.Observe(s,0,101.2);Check(Count("SignalWait")==2,"return to wait has a cue");
        s.Clock.Signal=MiniSignal.Go;s.Clock.SignalAt=102;feedback.Observe(s,0,101.99);Check(Count("SignalGo")==0,"buffered signal cannot sound early");
        feedback.Observe(s,0,102);feedback.Observe(s,0,102.1);Check(Count("SignalGo")==1,"go cue at its display timestamp");
        s.Clock.Iteration++;s.Clock.MiniGame=PartyMiniGame.Greed;s.Clock.Signal=MiniSignal.Wait;
        feedback.Observe(s,0,101);Check(score.isPlaying&&score.volume<.15f,"quiet rising score loop");
        feedback.StopPending();feedback.Observe(s,0,101.2);Check(!score.isPlaying,"score ticking stops when deciding");
        feedback.ReportStop(10,4,true,PartyMiniGame.Greed,MiniFailure.None);feedback.ReportStop(10,4,true,PartyMiniGame.Greed,MiniFailure.None);
        Check(Count("GreedConfirm")==1,"score confirmation once");
        s.Clock.Signal=MiniSignal.Burst;s.Clock.SignalAt=102;feedback.Observe(s,0,102);feedback.Observe(s,0,102.1);
        Check(Count("GreedBurst")==1&&!score.isPlaying,"everyone hears burst once, including already stopped owner");
        s.Clock.Iteration++;feedback.Observe(s,0,110);Check(Count("GreedBurst")==1,"initial stale burst is not replayed");
        feedback.Reset();Check(!timer.isPlaying&&!score.isPlaying,"leaving cleans up loops");
        document=FindFirstObjectByType<UIDocument>();var root=document.rootVisualElement;
        int clicks=0;var button=(Button)typeof(RouletteView).GetMethod("Button",F).Invoke(view,new object[]{root,"QA",(Action)(()=>clicks++),"secondary"});
        button.Focus();yield return null;
        using(var submit=NavigationSubmitEvent.GetPooled())button.SendEvent(submit);yield return null;
        Check(clicks==1&&Count("UiClick")==2,"actual UI button activation plays one click: callbacks="+clicks+", cues="+Count("UiClick"));button.RemoveFromHierarchy();
        view.enabled=true;manager.AiCount=5;manager.Begin(DuelMode.AI,false,"127.0.0.1","17892");
        while(HelloWorldPlayer.Local==null)yield return null;yield return new WaitForSecondsRealtime(.3f);
        manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);HelloWorldPlayer.Local.Rematch();
        while(HelloWorldPlayer.Local.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        manager.ChooseMiniGame(PartyMiniGame.QuickHands);
        while(HelloWorldPlayer.Local.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;
        var player=HelloWorldPlayer.Local;int seat=player.Seat.Value;
        for(int guard=0;guard<60&&player.State.Value.ForSeat(seat).Action!=DuelAction.Aim;guard++)
        {var a=player.State.Value.ForSeat(seat);player.InputDigit(a.Digit(a.Progress),a.Version);}
        Check(player.State.Value.ForSeat(seat).Action==DuelAction.Aim,"revolver reaches aim with existing rules");
        var fps=manager.GetComponent<DuelFirstPerson>();fps.enabled=false;
        typeof(DuelFirstPerson).GetProperty("Active").SetValue(fps,true);typeof(DuelFirstPerson).GetProperty("Ads").SetValue(fps,true);
        DuelGunplay.Angles(DuelGunplay.Point(3,DuelHitPart.Head)-DuelGunplay.Eye(seat),out float aimYaw,out float aimPitch);
        typeof(DuelFirstPerson).GetProperty("Yaw").SetValue(fps,aimYaw);typeof(DuelFirstPerson).GetProperty("Pitch").SetValue(fps,aimPitch);
        player.Look(aimYaw,aimPitch,true);yield return null;yield return null;
        var rigs=Field<Array>(manager.Table,"rigs");var rig=rigs.GetValue(seat,0);
        var gun=(Transform)rig.GetType().GetField("Gun").GetValue(rig);
        Check(Mathf.Abs(gun.localScale.x-.58f)<.001&&Mathf.Abs(gun.localScale.y-.64f)<.001&&Mathf.Abs(gun.localScale.z-.78f)<.001,"slimmer width/height with retained length");
        var sight=gun.Find("Front blade sight");var center=Camera.main.WorldToViewportPoint(sight.TransformPoint(new Vector3(0,.5f,0)));
        Check(Mathf.Abs(center.x-.5f)<.02&&Mathf.Abs(center.y-.5f)<.02,"front sight remains aligned after slimming");
        yield return Capture();
        manager.Leave();Debug.Log("PASS game feedback: "+checks+" audio, fade, signals, input/button, deduplication and ADS geometry checks");Exit(0);
    }
    IEnumerator Capture()
    {
        const int w=1200,h=800;var rt=new RenderTexture(w,h,24);rt.Create();
        RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,w,h),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"..","Revolver-Slim-ADS.png"),texture.EncodeToPNG());
        RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(texture);yield return null;
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL game feedback timeout after "+checks+" checks");Exit(1);}}
}
