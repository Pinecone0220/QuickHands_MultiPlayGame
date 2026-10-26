using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public sealed class HeadLookRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--head-look-qa");if(i<0)return;var q=new GameObject("Head look QA").AddComponent<HeadLookRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--head-look-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+110;Application.targetFrameRate=60;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
    void Finish(string s,int code=0)
    {File.WriteAllText(Path.Combine(path,role+"-done"),s);Debug.Log(s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL head look "+role+": "+s,1);throw new Exception(s);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL head look timeout "+role,1);}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    object Puppet(int seat)=>Field<Array>(manager.GetComponent<RouletteTable>(),"puppets").GetValue(seat);
    T Part<T>(int seat,string n)=>(T)Puppet(seat).GetType().GetField(n).GetValue(Puppet(seat));
    void Model()
    {
        var look=new DuelHeadLook();look.Step(0,0,.016f);float last=0;bool bounded=true,monotonic=true;
        for(int i=0;i<60;i++){look.Step(70,40,1f/60);bounded&=look.Yaw>=0&&look.Yaw<=70&&look.Pitch>=0&&look.Pitch<=40;monotonic&=look.Yaw>=last;last=look.Yaw;}
        Check(bounded&&monotonic&&Mathf.Abs(look.Yaw-70)<.01&&Mathf.Abs(look.Pitch-40)<.01,"smooth monotonic follow settles without overshoot");
        Check(Mathf.Abs(look.Roll)<=2.2f,"subtle head tilt remains small");look.Step(-300,-200,.016f);for(int i=0;i<180;i++)look.Step(-300,-200,.016f);Check(Mathf.Abs(look.Yaw+85)<.01&&Mathf.Abs(look.Pitch+45)<.01,"natural neck limits");
        bounded=true;for(int i=0;i<500;i++){look.Step(i%8<4?85:-85,i%12<6?65:-65,1f/120);bounded&=Mathf.Abs(look.Yaw)<=85&&look.Pitch>=-45&&look.Pitch<=55&&Mathf.Abs(look.Roll)<=2.2f;}Check(bounded,"rapid reversal stays within neck limits");
        var still=look;look.Step(0,0,0);Check(look.Yaw==still.Yaw&&look.Pitch==still.Pitch,"zero delta does not snap");
        float slow=0;foreach(int fps in new[]{30,60,144}){var rate=new DuelHeadLook();rate.Step(0,0,.01f);for(int i=0;i<fps;i++)rate.Step(60,25,1f/fps);if(slow==0)slow=rate.Yaw;Check(Mathf.Abs(rate.Yaw-slow)<.02,"frame rate independent settle "+fps);}
        var d=new DuelDirector(DuelMode.PvP);d.Join(0,0);d.Join(1,0);Check(d.Look(0,40,25,true,1)&&d.ForSeat(0).AimYaw==40&&!d.ForSeat(0).Aiming,"unready lobby look accepted but ADS stays off");Check(d.Game.ReadyMask==0&&d.Game.Shots==0&&!d.Fire(0,40,25,false,d.ForSeat(0).Version,1.1,1.1),"lobby look never grants readiness or firing");
        int seq=d.Sequence;Check(!d.Look(-1,0,0,false,1.2)&&!d.Look(6,0,0,false,1.2)&&!d.Look(0,float.NaN,0,false,1.2)&&!d.Look(0,180,0,false,1.2)&&d.Sequence==seq,"invalid seat/angles rejected");
        Check(!d.Look(0,0,0,false,1.001),"lobby look rate limited");d.SetAutoSelectItems(true);d.Ready(0,2);d.Ready(1,2);Check(d.Stage==DuelStage.Countdown&&d.Look(0,-30,-15,true,2.2)&&!d.ForSeat(0).Aiming,"countdown face moves without ADS");Check(!d.Fire(0,0,0,false,d.ForSeat(0).Version,2.3,2.3),"countdown still blocks firing");
    }
    IEnumerator Start()
    {yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;if(role=="model")Model();else if(role=="preview")yield return Preview();else yield return Peers();Finish("PASS head look "+role+": "+checks+" checks");}
    IEnumerator Peers()
    {
        if(role=="client")while(!Seen("host","listening"))yield return null;manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18989");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        if(role=="host")
        {
            Mark("listening");while(D.Game.ConnectedMask!=3)yield return null;Mark("joined");while(!Seen("client","moved"))yield return null;while(Mathf.Abs(D.ForSeat(1).AimYaw-115)>1||Mathf.Abs(D.ForSeat(1).AimPitch-25)>1)yield return null;yield return new WaitForSecondsRealtime(.4f);
            var head=Part<Transform>(1,"Head");var root=Part<Transform>(1,"Root");var body=Part<Transform>(1,"Body");DuelGunplay.Angles(new AimVector(-head.forward.x,-head.forward.y,-head.forward.z),out float yaw,out float pitch);
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw,115))<1&&Mathf.Abs(pitch-25-Part<DuelCharacterPose>(1,"Pose").HeadLift)<1,"remote unready lobby face follows actual mouse view");Check(Mathf.Abs(Mathf.DeltaAngle(0,body.localEulerAngles.y))<.01,"remote torso does not turn with view");Check(D.Game.ReadyMask==0&&D.Game.Shots==0,"face updates leave gameplay unchanged");Mark("verified");while(!Seen("client","finished"))yield return null;
        }
        else
        {
            while(!Seen("host","joined"))yield return null;var mouse=InputSystem.AddDevice<Mouse>();var keys=InputSystem.AddDevice<Keyboard>();InputState.Change(keys,new KeyboardState());keys.MakeCurrent();mouse.MakeCurrent();var fps=manager.GetComponent<DuelFirstPerson>();yield return new WaitForSecondsRealtime(.1f);
            for(int i=0;i<5;i++){InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(55/.096f/5,25/.096f/5)});yield return new WaitForSecondsRealtime(.05f);}InputSystem.QueueStateEvent(mouse,new MouseState());Mark("moved");
            while(!Seen("host","verified"))yield return null;Check(!fps.Active&&Mathf.Abs(fps.Yaw-115)<1&&Mathf.Abs(fps.Pitch-25)<1,"actual mouse rotates unready first-person lobby view");Check(player.State.Value.ReadyMask==0&&!player.State.Value.Seat1.Aiming,"unready look sync is cosmetic");Mark("finished");
        }
    }
    IEnumerator SetLook(int seat,float yaw,float pitch)
    {var a=Field<DuelActionState[]>(D,"actions");a[seat].AimYaw=yaw;a[seat].AimPitch=pitch;Publish();yield return new WaitForSecondsRealtime(.45f);}
    void Portrait(string name,int seat)
    {
        var head=Part<Transform>(seat,"Head");var forward=DuelGunplay.Forward(seat);var go=new GameObject("QA portrait camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=42;camera.aspect=1.5f;camera.transform.position=head.position+new Vector3(forward.X,0,forward.Z)*1.6f+Vector3.up*.02f;camera.transform.LookAt(head.position+Vector3.down*.10f);
        var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1200,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,800),0,0);t.Apply();File.WriteAllBytes(Path.Combine(path,name),t.EncodeToPNG());RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(t);Destroy(go);
    }
    IEnumerator Preview()
    {
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","18988");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;for(int i=1;i<6;i++)D.Join(i,Now);var tabletop=Field<DuelTabletop>(D,"tabletop");var props=tabletop.State;props.DrankMask=63;typeof(DuelTabletop).GetProperty("State").SetValue(tabletop,props);Publish();
        yield return SetLook(3,180,0);var body=Part<Transform>(3,"Body");var baseline=body.localRotation;Portrait("Head-Look-Centre.png",3);
        yield return SetLook(3,240,25);var head=Part<Transform>(3,"Head");DuelGunplay.Angles(new AimVector(-head.forward.x,-head.forward.y,-head.forward.z),out float yaw,out float pitch);Check(Mathf.Abs(Mathf.DeltaAngle(yaw,240))<1&&Mathf.Abs(pitch-25)<1,"portrait head reaches right/up target");Check(Mathf.Abs(Mathf.DeltaAngle(0,body.localEulerAngles.y))<.01,"portrait keeps torso facing table");Portrait("Head-Look-Right-Up.png",3);
        yield return SetLook(3,110,-35);Portrait("Head-Look-Left-Down.png",3);var local=Part<Transform>(3,"Head").localRotation;Check(Quaternion.Angle(local,Quaternion.identity)>20,"head moves independently of body");
        for(int seat=0;seat<6;seat++)
        {var a=Field<DuelActionState[]>(D,"actions");a[seat].AimYaw=seat*60+45;a[seat].AimPitch=15;}Publish();yield return new WaitForSecondsRealtime(.5f);
        for(int seat=1;seat<6;seat++){var h=Part<Transform>(seat,"Head");DuelGunplay.Angles(new AimVector(-h.forward.x,-h.forward.y,-h.forward.z),out yaw,out pitch);Check(Mathf.Abs(Mathf.DeltaAngle(yaw,seat*60+45))<1,"seat-correct face direction "+seat);}
        D.SetAutoSelectItems(true);for(int i=0;i<6;i++)D.Ready(i,Now);D.Game.Shoot(0,3,DuelWeapon.Revolver,DuelHitPart.Head,true);typeof(DuelDirector).GetMethod("LoseLife",F).Invoke(D,new object[]{3,DuelDeathCause.Gunshot,Now,1});Publish();yield return new WaitForSecondsRealtime(1.1f);Check(Quaternion.Angle(Part<Transform>(3,"Head").localRotation,Quaternion.identity)<.1,"death collapse releases head tracking");
    }
}
