using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

// QA-only owner presentation / real NGO peer checks; never install in production Assets.
public sealed class LifeFeedbackRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks,heartCount,gaspCount;double deadline;HelloWorldManager manager;HelloWorldPlayer player;DuelLifeFeedback fx;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--life-feedback-qa");if(i<0)return;
        var q=new GameObject("Life feedback QA").AddComponent<LifeFeedbackRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--life-feedback-dir")+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+120;Application.targetFrameRate=60;
    }
    void Finish(string s,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),s);Debug.Log(s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL life feedback "+role+": "+s,1);throw new Exception(s);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL life feedback timeout "+role,1);}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Phase()
    {
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,Iteration=1,ParticipantsMask=D.Game.AliveMask,First=3,PhaseAt=Now,StartedAt=Now});Publish();
    }
    void Kill(int target,int count=1)
    {
        Check(D.Game.Shoot(3,target,DuelWeapon.Revolver,DuelHitPart.Head,true),"server confirms zero HP "+target);
        typeof(DuelDirector).GetMethod("LoseLife",F).Invoke(D,new object[]{target,DuelDeathCause.Gunshot,Now,count});Publish();
    }
    void Return()=>Field<DuelClockContest>(D,"clock").Return(Now);
    void Model()
    {
        Check(DuelLifeFeedback.Envelope(9,10,-1,false)==0,"no pre-death effect");
        Check(DuelLifeFeedback.Envelope(10,10,-1,false)==0&&DuelLifeFeedback.Envelope(10.3,10,-1,false)>.999,"smooth collapse entrance");
        Check(DuelLifeFeedback.Envelope(12.2,10,-1,false)>.999,"temporary loss stays blurred through life display");
        Check(Mathf.Abs(DuelLifeFeedback.Envelope(12.7,10,-1,false)-.5f)<.0001,"spectator effects fade over one second");
        Check(DuelLifeFeedback.Envelope(13.2,10,-1,false)<.001,"temporary spectator effects fully clear without revival");
        Check(Mathf.Abs(DuelLifeFeedback.Envelope(11.375,10,11,false)-.5f)<.0001,"early revival still normalizes effects during recovery");
        Check(DuelLifeFeedback.Envelope(11.75,10,11,false)==0,"early revival fully normal before recovery ends");
        Check(DuelLifeFeedback.Envelope(12.35,10,-1,true)==0,"permanent death clears before fixed spectating");
        Check(RouletteTable.SpectatorTransitionStart(0)>=DuelLifeFeedback.PermanentRecoveryDuration&&DuelLifeFeedback.Envelope(10+RouletteTable.SpectatorTransitionStart(0),10,-1,true)==0,"permanent spectating starts after vision and sound recover");
        Check(RouletteTable.SpectatorTransitionStart(2)<RouletteTable.SpectatorTransitionStart(0),"temporary loss preserves existing camera timing");
        var profile=Resources.Load<VolumeProfile>("DuelLifeBlur");
        Check(profile!=null&&profile.TryGet<DepthOfField>(out var dof)&&dof.mode.value==DepthOfFieldMode.Gaussian&&dof.gaussianMaxRadius.value>1,"build-included Gaussian blur profile");
        foreach(string name in new[]{"LifeHeartbeat","LifeGasp"})
        {var clip=Resources.Load<AudioClip>("GameFeedback/"+name);Check(clip!=null&&clip.channels==1&&clip.length>1&&clip.length<3,"generated cue imports "+name);}
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fx=manager.GetComponent<DuelLifeFeedback>();
        fx.Played+=s=>{if(s=="LifeHeartbeat")heartCount++;if(s=="LifeGasp")gaspCount++;};
        if(role=="preview")yield return Preview();else if(role=="terminal")yield return Terminal();else yield return Peers();Finish("PASS life feedback "+role+": "+checks+" checks");
    }
    IEnumerator Terminal()
    {
        Model();
        foreach(var mode in new[]{DuelMode.PvP,DuelMode.AI})
        {
            manager.Begin(mode,false,"127.0.0.1","18985");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
            D.Join(1,Now);D.Join(3,Now);D.SetAutoSelectItems(true);D.Game.SetShotTime(0);foreach(int seat in new[]{0,1,3})D.Ready(seat,Now);Phase();yield return new WaitForSecondsRealtime(.15f);
            int priorBreaths=gaspCount;Kill(0,3);D.Game.FinishParty(1<<3);Publish();
            while(Now-D.ForSeat(0).DiedAt<1.8)yield return null;
            Check(fx.RemainingLives==0&&fx.Strength>0&&Camera.main.transform.position.y<2,"eliminated final owner stays at fallen face during recovery "+mode);
            Check(fx.LifeOpacity==0&&gaspCount==priorBreaths,"final panel has priority and no breath "+mode);
            while(Now-D.ForSeat(0).DiedAt<2.4)yield return null;
            Check(fx.Strength==0&&Field<Volume>(fx,"blur").weight==0&&Field<AudioLowPassFilter>(fx,"lowPass").cutoffFrequency>21900&&Field<AudioReverbFilter>(fx,"reverb").reverbLevel==-10000,"vision and listener normal before spectator transition "+mode);
            while(Now-D.ForSeat(0).DiedAt<2.9)yield return null;
            Check(Mathf.Abs(Camera.main.transform.position.y-6)<.01f&&gaspCount==priorBreaths&&!Field<AudioSource>(fx,"breath").isPlaying,"fixed third-person spectator without breath "+mode);
            var rotation=Camera.main.transform.rotation;yield return new WaitForSecondsRealtime(.15f);Check(Quaternion.Angle(rotation,Camera.main.transform.rotation)<.01f,"spectator camera remains fixed "+mode);
            manager.Leave();yield return new WaitForSecondsRealtime(.2f);
        }
    }
    IEnumerator Peers()
    {
        if(role=="client")while(!Seen("host","listening"))yield return null;
        if(role=="host")Model();manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18986");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        if(role=="host")
        {
            Mark("listening");while(D.Game.ConnectedMask!=3)yield return null;D.Join(3,Now);D.SetAutoSelectItems(true);D.Game.SetShotTime(0);foreach(int seat in new[]{0,1,3})D.Ready(seat,Now);Phase();Mark("started");
            while(!Seen("client","ready"))yield return null;Kill(1);while(!Seen("client","holding"))yield return null;
            Check(fx.Strength==0&&heartCount==0,"remote death does not filter or play heartbeat for host");Return();Publish();while(D.ForSeat(1).DiedAt>=0)yield return null;
            while(!Seen("client","recovered"))yield return null;Phase();Kill(0);while(fx.Strength<.99f)yield return null;
            Check(fx.FromLives==3&&fx.RemainingLives==2&&heartCount==1,"host receives its own loss counter and heartbeat");Check(Field<AudioLowPassFilter>(fx,"lowPass").cutoffFrequency<1000,"host hears muffled sound");
            Mark("host-down");while(!Seen("client","host-unaffected"))yield return null;Return();Publish();while(D.ForSeat(0).DiedAt>=0)yield return null;yield return null;
            Check(fx.Strength==0&&gaspCount==1,"host recovers once with gasp and normal sound");Phase();Kill(1,2);Mark("permanent");
            while(!Seen("client","finished"))yield return null;Check(fx.Strength==0&&heartCount==1,"later victim elimination leaves host unchanged");Mark("finished");
        }
        else
        {
            while(!Seen("host","started")||player.State.Value.Phase!=DuelPhase.Playing)yield return null;yield return new WaitForSecondsRealtime(.1f);Mark("ready");
            while(fx.Strength<.99f)yield return null;
            Check(fx.FromLives==3&&fx.RemainingLives==2&&heartCount==1,"remote owner gets own confirmed 3 to 2 loss and heartbeat");
            Check(Field<AudioLowPassFilter>(fx,"lowPass").cutoffFrequency<1000&&Field<AudioReverbFilter>(fx,"reverb").reverbLevel>-3000,"remote owner receives low-pass and small reverb");
            Check(Field<Volume>(fx,"blur").weight>.99&&Field<AudioSource>(fx,"heart").bypassListenerEffects,"local blur active and heartbeat stays audible");
            while(Now-player.State.Value.Seat1.DiedAt<1)yield return null;Check(fx.LifeOpacity>.99&&fx.NumberScroll>0&&fx.NumberScroll<1,"text holds while only number rolls");
            while(Now-player.State.Value.Seat1.DiedAt<3.3)yield return null;Check(fx.Strength==0&&fx.LifeOpacity==0&&gaspCount==0&&player.State.Value.Seat1.ReviveAt<0,"counter and spectator effects fade while player remains down");Mark("holding");
            while(gaspCount==0)yield return null;double revive=player.State.Value.Seat1.ReviveAt;
            while(Now-revive<.4)yield return null;Check(fx.Strength==0&&Field<AudioLowPassFilter>(fx,"lowPass").cutoffFrequency>21900,"sound and vision remain clear during stand-up");
            while(player.State.Value.Seat1.DiedAt>=0)yield return null;yield return null;
            Check(fx.Strength==0&&gaspCount==1&&player.State.Value.Seat1.LivesRemaining==2,"new life is clear without replayed audio");Mark("recovered");
            while(!Seen("host","host-down"))yield return null;Check(fx.Strength==0&&heartCount==1,"host death does not affect other owner");Mark("host-unaffected");
            while(!Seen("host","permanent")||player.State.Value.Seat1.LivesRemaining!=0)yield return null;
            while(Now-player.State.Value.Seat1.DiedAt<.5)yield return null;Check(fx.FromLives==2&&fx.RemainingLives==0&&heartCount==2,"two-life loss scrolls 2 to 0");
            while(Now-player.State.Value.Seat1.DiedAt<1.8)yield return null;Check(Camera.main.transform.position.y<2&&fx.Strength>0,"permanent owner stays with fallen face during normalization");
            while(Now-player.State.Value.Seat1.DiedAt<2.4)yield return null;Check(fx.Strength==0&&gaspCount==1&&Field<AudioLowPassFilter>(fx,"lowPass").cutoffFrequency>21900,"permanent spectator clears without breathing revival");
            while(Now-player.State.Value.Seat1.DiedAt<2.9)yield return null;Check(Mathf.Abs(Camera.main.transform.position.y-6)<.01f&&gaspCount==1&&!Field<AudioSource>(fx,"breath").isPlaying,"permanent owner enters fixed third-person without breath");
            manager.Leave();yield return new WaitForSecondsRealtime(.2f);Check(fx.Strength==0&&Field<AudioLowPassFilter>(fx,"lowPass").cutoffFrequency>21900,"leaving resets listener and overlay");Mark("finished");while(!Seen("host","finished"))yield return null;
        }
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.08f);var settings=view.GetComponent<UIDocument>().panelSettings;var previous=RenderTexture.active;RenderTexture.active=settings.targetTexture;
        var ui=new Texture2D(1200,800,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,1200,800),0,0);ui.Apply();var rt=new RenderTexture(1200,800,24);rt.Create();
        RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var scene=new Texture2D(1200,800,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,1200,800),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(ui);Destroy(scene);
    }
    double ToneEnergy()
    {
        var data=new float[8192];AudioListener.GetOutputData(data,0);double s=0,c=0;
        for(int i=0;i<data.Length;i++){double angle=2*Math.PI*4000*i/AudioSettings.outputSampleRate;s+=data[i]*Math.Sin(angle);c+=data[i]*Math.Cos(angle);}
        return Math.Sqrt(s*s+c*c)/data.Length;
    }
    void World(string name)
    {
        var go=new GameObject("QA fixed blur camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=60;camera.aspect=1.5f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.transform.position=new Vector3(0,3.1f,-7);camera.transform.LookAt(new Vector3(0,.9f,0));
        var rt=new RenderTexture(1200,800,24);rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(path,name),image.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(image);Destroy(go);
    }
    IEnumerator Preview()
    {
        Model();var root=view.GetComponent<UIDocument>().rootVisualElement;var settings=view.GetComponent<UIDocument>().panelSettings;settings.scaleMode=PanelScaleMode.ConstantPixelSize;settings.scale=1;settings.targetTexture=new RenderTexture(1200,800,24);settings.targetTexture.Create();settings.clearColor=true;settings.colorClearValue=Color.clear;root.style.width=1200;root.style.height=800;
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","18985");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;D.Join(1,Now);D.Join(3,Now);D.SetAutoSelectItems(true);D.Game.SetShotTime(0);foreach(int seat in new[]{0,1,3})D.Ready(seat,Now);Phase();yield return new WaitForSecondsRealtime(.2f);
        yield return Capture("Life-Feedback-Before.png");World("Life-Blur-Clear.png");
        var sources=FindObjectsByType<AudioSource>(FindObjectsSortMode.None);var muted=new bool[sources.Length];for(int i=0;i<sources.Length;i++){muted[i]=sources[i].mute;sources[i].mute=true;}
        var probe=new GameObject("QA filter probe").AddComponent<AudioSource>();probe.spatialBlend=0;probe.volume=.04f;probe.loop=true;var clip=AudioClip.Create("QA two-frequency probe",44100,1,44100,false);var samples=new float[44100];for(int i=0;i<samples.Length;i++)samples[i]=.3f*(Mathf.Sin(2*Mathf.PI*250*i/44100)+Mathf.Sin(2*Mathf.PI*4000*i/44100));clip.SetData(samples,0);probe.clip=clip;probe.Play();ToneEnergy();yield return new WaitForSecondsRealtime(.4f);double clear=ToneEnergy();
        Kill(0);while(Now-D.ForSeat(0).DiedAt<.9)yield return null;yield return Capture("Life-Feedback-Scroll.png");
        while(Now-D.ForSeat(0).DiedAt<1.35)yield return null;yield return Capture("Life-Feedback-Lost.png");
        Check(fx.FromLives==3&&fx.RemainingLives==2&&fx.NumberScroll==1&&fx.LifeOpacity>.99,"centered counter settles on remaining lives");
        Check(VolumeManager.instance.stack.GetComponent<DepthOfField>().mode.value==DepthOfFieldMode.Gaussian,"camera volume stack really uses Gaussian blur");
        World("Life-Blur-Down.png");while(Now-D.ForSeat(0).DiedAt<2.8)yield return null;double muffled=ToneEnergy();Debug.Log("Life DSP high-band clear="+clear+" muffled="+muffled);
        Check(clear>.0001&&muffled<clear*.4,"actual listener output attenuates high frequencies");probe.Stop();Destroy(probe.gameObject);Destroy(clip);for(int i=0;i<sources.Length;i++)if(sources[i]!=null)sources[i].mute=muted[i];
        Return();Publish();while(gaspCount==0)yield return null;yield return new WaitForSecondsRealtime(.45f);yield return Capture("Life-Feedback-Rising.png");
        while(D.ForSeat(0).DiedAt>=0)yield return null;yield return Capture("Life-Feedback-Recovered.png");Check(fx.Strength==0&&heartCount==1&&gaspCount==1,"one heartbeat and one breath, cleared after stand-up");
        Phase();Kill(0,2);while(Now-D.ForSeat(0).DiedAt<.5)yield return null;Check(fx.RemainingLives==0,"permanent loss shows zero");D.Game.FinishParty(1<<3);Publish();yield return new WaitForSecondsRealtime(.2f);Check(fx.LifeOpacity==0,"final victory takes priority over center life text");
        while(Now-D.ForSeat(0).DiedAt<1.8)yield return null;Check(Camera.main.transform.position.y<2&&fx.Strength>0,"final result waits for eliminated owner senses to recover");
        while(Now-D.ForSeat(0).DiedAt<2.9)yield return null;Check(fx.Strength==0&&Mathf.Abs(Camera.main.transform.position.y-6)<.01f&&gaspCount==1,"final result spectator restores without breath");
        manager.Leave();yield return new WaitForSecondsRealtime(.2f);Check(fx.Strength==0,"menu clears post effects");
    }
}
