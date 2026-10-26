using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

// Isolated-project QA only: real throws, owner overlay, remote face/audio and turn-end fade.
public sealed class InkPresentationRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks,winds,hits;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelInkAudio audio;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    VisualElement Overlay=>Field<VisualElement>(view,"inkOverlay");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--ink-presentation-qa");if(i<0)return;var q=new GameObject("Ink presentation QA").AddComponent<InkPresentationRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--ink-presentation-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+140;Application.targetFrameRate=60;}
    void Finish(string s,int code=0){File.WriteAllText(Path.Combine(path,role+"-done"),s);Debug.Log(s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL ink presentation "+role+": "+s,1);throw new Exception(s);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL ink presentation timeout "+role,1);}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Phase(ClockPhase phase,int iteration=1,bool quick=false)
    {typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=phase,MiniGame=quick?PartyMiniGame.QuickHands:PartyMiniGame.Countdown,Iteration=iteration,ParticipantsMask=D.Game.AliveMask,First=0,Last=3,PhaseAt=Now,StartedAt=Now+5});Publish();}
    void Throw(int actor,int target,int iteration)
    {var delta=DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(actor);float distance=Mathf.Sqrt(delta.X*delta.X+delta.Z*delta.Z),t=(distance-.65f)/DuelInk.Speed;DuelGunplay.Angles(delta,out float yaw,out float pitch);yaw-=Mathf.Atan2(.24f,distance)*Mathf.Rad2Deg;pitch=Mathf.Atan2(delta.Y+.1f-DuelInk.UpwardSpeed*t+.5f*DuelInk.Gravity*t*t,distance)*Mathf.Rad2Deg;var a=D.ForSeat(actor);Check(D.ThrowInk(actor,yaw,pitch,iteration,a.Life,a.Ink.Version,Now,Now),"server accepts actual throw "+iteration);Publish();}
    void Model()
    {
        var ink=DuelInkState.Empty;ink.InkedAt=10;var fade=new DuelInkFade();
        Check(fade.Observe(ink,11,DuelPhase.Playing,ClockPhase.Shooting,10,10)==1,"normal shooting hold");
        Check(Mathf.Abs(fade.Observe(ink,11.25,DuelPhase.Playing,ClockPhase.Returning,11,10)-.5f)<.001,"immediate half-second end fade");
        Check(fade.Observe(ink,11.6,DuelPhase.Playing,ClockPhase.Returning,11,10)==0,"early end clears before five seconds");
        Check(fade.Observe(ink,11.8,DuelPhase.Finished,ClockPhase.Returning,11,11.8)==0,"final transition cannot revive cleared ink");
        ink.InkedAt=12;Check(fade.Observe(ink,12,DuelPhase.Playing,ClockPhase.QuickHands,12,10)==1,"new impact resets expired fade");
        Check(fade.Observe(ink,13.6,DuelPhase.Playing,ClockPhase.Dice,13,10)==0&&fade.Observe(ink,14,DuelPhase.Playing,ClockPhase.QuickResults,14,10)==0,"QuickHands dice then results never restarts fade");
        ink.InkedAt=20;fade=default;Check(Mathf.Abs(fade.Observe(ink,20.45,DuelPhase.Finished,ClockPhase.Shooting,20,20.2)-.5f)<.001,"final-shot fade uses server stage time");
        fade=default;Check(Mathf.Abs(fade.Observe(ink,25.25,DuelPhase.Playing,ClockPhase.Shooting,20,20)-.5f)<.001&&fade.Observe(ink,25.6,DuelPhase.Playing,ClockPhase.Shooting,20,20)==0,"natural five-second expiry preserved");
        fade=default;fade.Observe(ink,25.25,DuelPhase.Playing,ClockPhase.Returning,25.25,20);Check(fade.Observe(ink,25.5,DuelPhase.Playing,ClockPhase.Returning,25.25,20)==0,"end fade never extends natural expiration");
        int wind=0,splash=0;Action<int,string> recorder=(seat,name)=>{if(name=="InkThrow")wind++;else splash++;};audio.Played+=recorder;
        var s=new DuelSnapshot{Phase=DuelPhase.Playing,ConnectedMask=1,AliveMask=1,Clock=new ClockState{Phase=ClockPhase.Shooting},Seat0=new DuelActionState{Life=1,Ink=new DuelInkState{Serial=1,ThrownAt=10,ImpactAt=10.3}}};
        audio.Observe(s,0,12);Check(wind==0&&splash==0,"initial historical timestamps stay silent");s.Seat0.Ink.Serial=2;s.Seat0.Ink.ThrownAt=20;s.Seat0.Ink.ImpactAt=-1;audio.Observe(s,0,20.05);Check(wind==0,"whoosh waits for hand release");audio.Observe(s,0,20.17);audio.Observe(s,0,20.18);Check(wind==1,"release whoosh deduplicated");
        s.Seat0.Ink.ImpactAt=20.3;audio.Observe(s,0,20.25);Check(splash==0,"future impact waits for common time");audio.Observe(s,0,20.31);audio.Observe(s,0,20.33);Check(splash==1,"confirmed impact plays exactly once");s.Seat0.Life++;audio.Observe(s,0,20.34);Check(wind==1&&splash==1,"new life does not replay old sounds");audio.Played-=recorder;winds=hits=0;
        foreach(string name in new[]{"InkThrow","InkImpact"}){var clip=Resources.Load<AudioClip>("GameFeedback/"+name);Check(clip!=null&&clip.channels==1&&clip.length>.15&&clip.length<.5,"included short original clip "+name);}
    }
    IEnumerator Start()
    {yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();audio=manager.GetComponent<DuelInkAudio>();audio.Played+=(seat,name)=>{if(name=="InkThrow")winds++;else hits++;};if(role!="client")Model();if(role=="preview")yield return Preview();else yield return Peers();Finish("PASS ink presentation "+role+": "+checks+" checks");}
    IEnumerator Peers()
    {
        if(role=="client")while(!Seen("host","listening"))yield return null;manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18983");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        if(role=="host")
        {
            D.Join(1,Now);D.Join(2,Now);Publish();Mark("listening");while(D.Game.ConnectedMask!=15)yield return null;D.SetAutoSelectItems(true);D.Game.SetShotTime(0);D.Game.SetUnlimitedItems(true);for(int i=0;i<4;i++)D.Ready(i,Now);Phase(ClockPhase.Shooting);Mark("started");while(!Seen("client","ready"))yield return null;
            for(int iteration=1;iteration<=3;iteration++)
            {
                if(iteration==2){Phase(ClockPhase.ItemPreparation,2,true);Check(D.UseItem(0,DuelItem.Ink,2,Now),"reserve QuickHands pouch");Phase(ClockPhase.QuickHands,2,true);var a=D.ForSeat(0);Check(D.SwitchQuickItem(0,true,2,a.Life,a.QuickItemVersion,Now),"equip QuickHands pouch");}
                else {Phase(ClockPhase.Shooting,iteration);Check(D.UseItem(0,DuelItem.Ink,iteration,Now),"equip normal pouch");}
                Throw(0,3,iteration);while(D.ForSeat(0).Ink.ImpactAt<D.ForSeat(0).Ink.ThrownAt)yield return null;while(!Seen("client","hit"+iteration))yield return null;
                Check(D.ForSeat(0).Ink.Target==3&&D.Game.HealthForSeat(3)==100,"actual victim hit without damage");Check(winds==iteration&&hits==iteration,"host hears each throw and hit once");Check(Overlay.resolvedStyle.display==DisplayStyle.None,"thrower screen remains clear");
                var visuals=manager.GetComponent<DuelInkVisuals>();Check(Field<Transform[]>(visuals,"coats")[3].gameObject.activeInHierarchy,"remote victim face shows ink");
                double ended=Now;
                if(iteration==1)Field<DuelClockContest>(D,"clock").Return(ended);
                else if(iteration==2)
                {
                    var clock=Field<DuelClockContest>(D,"clock");var ranked=clock.State;for(int i=0;i<4;i++)ranked.Set(i,new ClockEntry{Rank=i+1});
                    typeof(DuelClockContest).GetProperty("State").SetValue(clock,ranked);clock.FinishQuick(0,3,ended);
                }
                else{D.Game.FinishParty(1<<3);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.AfterShot,ended});}
                Publish();while(Now-ended<.24)yield return null;float alpha=Field<Material[]>(visuals,"coatMaterials")[3].GetColor("_BaseColor").a;
                Check(alpha>0&&alpha<.8,"face fades immediately at end "+iteration);while(!Seen("client","clear"+iteration))yield return null;Check(!Field<Transform[]>(visuals,"coats")[3].gameObject.activeInHierarchy,"face fully clears "+iteration);
            }
            Mark("finished");while(!Seen("client","finished"))yield return null;
        }
        else
        {
            while(!Seen("host","started")||player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;Check(player.Seat.Value==3,"real remote owner assigned target seat");yield return new WaitForSecondsRealtime(.1f);Mark("ready");double previous=-1;
            for(int iteration=1;iteration<=3;iteration++)
            {
                while(player.State.Value.Seat3.Ink.InkedAt<=previous)yield return null;previous=player.State.Value.Seat3.Ink.InkedAt;yield return new WaitForSecondsRealtime(.12f);
                Check(Overlay.resolvedStyle.opacity>.99&&Overlay.resolvedStyle.display==DisplayStyle.Flex,"remote victim screen covered "+iteration);Check(winds==iteration&&hits==iteration,"remote hears each throw and hit once");Mark("hit"+iteration);
                while(iteration==3?player.State.Value.Phase!=DuelPhase.Finished:player.State.Value.Clock.Phase!=(iteration==1?ClockPhase.Returning:ClockPhase.QuickResults))yield return null;
                double ended=iteration==3?player.State.Value.StageStartedAt:player.State.Value.Clock.PhaseAt;while(Now-ended<.24)yield return null;yield return null;
                Check(Overlay.resolvedStyle.opacity>0&&Overlay.resolvedStyle.opacity<.8,"remote end fade starts immediately "+iteration);while(Now-ended<.6)yield return null;yield return null;
                Check(Overlay.resolvedStyle.display==DisplayStyle.None,"remote effect clears in half second "+iteration);Mark("clear"+iteration);
            }
            Mark("finished");while(!Seen("host","finished"))yield return null;
        }
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.06f);var settings=view.GetComponent<UIDocument>().panelSettings;var previous=RenderTexture.active;RenderTexture.active=settings.targetTexture;var ui=new Texture2D(1200,800,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,1200,800),0,0);ui.Apply();var rt=new RenderTexture(1200,800,24);rt.Create();RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var scene=new Texture2D(1200,800,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,1200,800),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(ui);Destroy(scene);
    }
    IEnumerator Preview()
    {
        var root=view.GetComponent<UIDocument>().rootVisualElement;var settings=view.GetComponent<UIDocument>().panelSettings;settings.scaleMode=PanelScaleMode.ConstantPixelSize;settings.scale=1;settings.targetTexture=new RenderTexture(1200,800,24);settings.targetTexture.Create();settings.clearColor=true;settings.colorClearValue=Color.clear;root.style.width=1200;root.style.height=800;
        var texture=Field<Texture2D>(view,"inkTexture");var rt=RenderTexture.GetTemporary(512,512);Graphics.Blit(texture,rt);var previous=RenderTexture.active;RenderTexture.active=rt;var copy=new Texture2D(512,512,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,512,512),0,0);copy.Apply();int opaque=0;foreach(var color in copy.GetPixels32())if(color.a>230)opaque++;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(copy);Check(opaque/(512f*512)>.95,"GPU ink texture covers over 95 percent");
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","18982");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;D.Join(3,Now);D.SetAutoSelectItems(true);D.Game.SetShotTime(0);D.Ready(0,Now);D.Ready(3,Now);Phase(ClockPhase.Shooting);yield return new WaitForSecondsRealtime(.2f);
        Check(D.UseItem(3,DuelItem.Ink,1,Now),"non-shooter equips ink");Throw(3,0,1);while(D.ForSeat(0).Ink.InkedAt<0)yield return null;yield return new WaitForSecondsRealtime(.12f);yield return Capture("Ink-Nearly-Full.png");Check(Overlay.pickingMode==PickingMode.Ignore&&Overlay.resolvedStyle.opacity>.99,"full ink keeps UI input available");
        Check(root.IndexOf(Overlay)>root.IndexOf(Field<VisualElement>(view,"table"))&&root.IndexOf(Overlay)>root.IndexOf(root.Q<VisualElement>("room-chat")),"ink covers existing HUD and chat layers");
        double end=Now;Field<DuelClockContest>(D,"clock").Return(end);Publish();while(Now-end<.18)yield return null;yield return Capture("Ink-End-Fading.png");Check(Overlay.resolvedStyle.opacity>0&&Overlay.resolvedStyle.opacity<.8,"captured immediate end fade");
        var clockPanel=Field<VisualElement>(view,"clockPanel");Check(clockPanel.parent==root&&root.IndexOf(clockPanel)>root.IndexOf(Overlay)&&root.IndexOf(Field<VisualElement>(view,"victoryLayer"))>root.IndexOf(Overlay),"new results and victory remain above ink");
        while(Now-end<.6)yield return null;yield return Capture("Ink-End-Clear.png");Check(Overlay.resolvedStyle.display==DisplayStyle.None&&winds==1&&hits==1,"clear screen and single confirmed sounds");Check(clockPanel.parent==Field<VisualElement>(view,"table"),"result HUD parent restored after fade");
    }
}
