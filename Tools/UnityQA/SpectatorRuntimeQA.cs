using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

// Run in an isolated QA build: real host and remote owner, shared public spectator data.
public sealed class SpectatorRuntimeQA:MonoBehaviour
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;DuelSpectator spectator;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Hidden).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--spectator-qa");if(i<0)return;
        var q=new GameObject("Spectator runtime QA").AddComponent<SpectatorRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--spectator-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+100;Application.targetFrameRate=60;
    }
    void Finish(string text,int code=0){File.WriteAllText(Path.Combine(path,role+"-done"),text);Debug.Log(text);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool ok,string why){checks++;if(!ok){Finish("FAIL spectator "+role+": "+why,1);throw new Exception(why);}}
    void Update(){if(role=="host"&&manager!=null&&HelloWorldPlayer.Local!=null&&D!=null&&Seen("host","ads")&&!Seen("host","target-down")){if(D.Look(2,150,-5,true,Now))Publish();}if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL spectator timeout "+role,1);}
    void Mark(string name)=>File.WriteAllText(Path.Combine(path,role+"-"+name),"ok");
    bool Seen(string who,string name)=>File.Exists(Path.Combine(path,who+"-"+name));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Hidden).Invoke(manager,null);
    void Kill(int seat,int lives){D.Game.Shoot(3,seat,DuelWeapon.Revolver,DuelHitPart.Head,true);typeof(DuelDirector).GetMethod("LoseLife",Hidden).Invoke(D,new object[]{seat,DuelDeathCause.Gunshot,Now,lives});}
    IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));yield return null;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return new WaitForSecondsRealtime(.07f);}
    bool Shown(string field)=>Field<VisualElement>(view,field).resolvedStyle.display!=DisplayStyle.None;
    void CameraAt(int seat,bool ads)
    {
        var eye=DuelGunplay.Eye(seat);Check(Vector3.Distance(Camera.main.transform.position,new Vector3(eye.X,eye.Y,eye.Z))<.001f,"seated eye of target "+seat);
        Check(Quaternion.Angle(Camera.main.transform.rotation,spectator.Rotation(player.State.Value,Now))<1.5f,"camera follows target look and sway");
        Check(Mathf.Abs(Camera.main.fieldOfView-(ads?DuelGunplay.AdsFov(player.State.Value.ForSeat(seat).Weapon):DuelGunplay.BaseFov))<.01f,"target ADS FOV actual="+Camera.main.fieldOfView+" ads="+player.State.Value.ForSeat(seat).Aiming);
        var puppets=Field<Array>(manager.Table,"puppets");
        for(int i=0;i<4;i++)
        {
            var p=puppets.GetValue(i);var heads=(Renderer[])p.GetType().GetField("HeadRenderers").GetValue(p);
            bool ok=true;foreach(var r in (Renderer[])p.GetType().GetField("Renderers").GetValue(p))ok&=r.forceRenderingOff==(i==seat&&Array.IndexOf(heads,r)>=0);
            Check(ok,"only target face hidden, body and other players restored "+i);
        }
        Check(!Shown("crosshair"),"dead viewer crosshair stays hidden");Check(!manager.GetComponent<DuelFirstPerson>().Active,"owner controller remains inactive");
    }
    void PureRules()
    {
        int mask=(1<<2)|(1<<5);Check(DuelSpectator.NextSeat(mask,-1,1)==2,"E first eligible");Check(DuelSpectator.NextSeat(mask,2,1)==5,"E skips empty seats");Check(DuelSpectator.NextSeat(mask,5,1)==-1,"E wraps overview");Check(DuelSpectator.NextSeat(mask,-1,-1)==5,"Q last eligible");Check(DuelSpectator.NextSeat(mask,5,-1)==2,"Q previous");Check(DuelSpectator.NextSeat(mask,2,-1)==-1,"Q wraps overview");Check(DuelSpectator.NextSeat(0,-1,1)==-1,"no target stays overview");
        var s=new DuelSnapshot{ConnectedMask=15,AliveMask=14,Phase=DuelPhase.Playing,Seat0=new DuelActionState{LivesRemaining=0,DiedAt=10},Seat1=new DuelActionState{LivesRemaining=3,DiedAt=-1},Seat2=new DuelActionState{LivesRemaining=2,DiedAt=11},Seat3=new DuelActionState{LivesRemaining=0,DiedAt=-1}};
        Check(DuelSpectator.EligibleMask(s,0)==2,"exclude down and zero-life targets");Check(!DuelSpectator.CanSpectate(s,0,12),"collapse protects camera");Check(DuelSpectator.CanSpectate(s,0,13),"permanent death after recovery");s.Seat0.LivesRemaining=1;Check(!DuelSpectator.CanSpectate(s,0,13),"temporary death cannot spectate");s.Seat0.LivesRemaining=0;s.Phase=DuelPhase.Finished;Check(!DuelSpectator.CanSpectate(s,0,13),"final result locks view");
    }
    IEnumerator Start()
    {
        PureRules();yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;spectator=manager.GetComponent<DuelSpectator>();view=manager.GetComponent<RouletteView>();RoomCameraCaptureQA.Setup(view);if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18995");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;if(role=="host")Mark("listening");
        yield return new WaitForSecondsRealtime(.25f);Check(!spectator.Active,"lobby not spectator");yield return KeyPress(Key.E);Check(spectator.TargetSeat==-1,"lobby E cannot spectate");Mark("lobby");
        if(role=="host")
        {
            while(!Seen("client","lobby"))yield return null;D.Join(2,Now);D.Join(3,Now);D.SelectWeapon(2,DuelWeapon.BAS,Now);D.Game.SetShotTime(0);D.SetAutoSelectItems(true);for(int i=0;i<4;i++)D.Ready(i,Now);
            typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Racing,Now});
            var clock=Field<DuelClockContest>(D,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,First=-1,ParticipantsMask=15,PhaseAt=Now,StartedAt=Now});
            var actions=Field<DuelActionState[]>(D,"actions");actions[2].Action=DuelAction.Aim;actions[2].LoadedMask=1;actions[2].AimYaw=135;actions[2].AimPitch=8;Kill(0,3);Kill(1,3);Publish();
        }
        while(player.State.Value.ForSeat(player.Seat.Value).DiedAt<0)yield return null;
        if(Now-player.State.Value.ForSeat(player.Seat.Value).DiedAt<2.5)
        {bool switched=(bool)typeof(DuelSpectator).GetMethod("Cycle",Hidden).Invoke(spectator,new object[]{1});Check(!switched&&spectator.TargetSeat==-1,"Q/E cannot interrupt death face camera");}
        else Check(spectator.TargetSeat==-1,"delayed frame preserves default death view");
        while(!spectator.Active)yield return null;Check(spectator.TargetSeat==-1&&Mathf.Abs(Camera.main.fieldOfView-57)<.01f,"existing fixed overview retained");
        yield return KeyPress(Key.E);Check(spectator.TargetSeat==2,"actual E selects first living target");CameraAt(2,false);Check(Field<Label>(view,"localHealth").text=="100","watched HP displayed");Check(Field<Label>(view,"spectatorNotice").text.Contains("1인칭 관전"),"spectator label");
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,role+"-FirstPerson.png"));
        var chat=manager.GetComponent<DuelChat>();chat.Open();yield return KeyPress(Key.E);Check(spectator.TargetSeat==2,"chat q/e does not switch");chat.Close();yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.LeftAlt,Key.E));yield return null;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return new WaitForSecondsRealtime(.1f);Check(spectator.TargetSeat==2,"Alt UI q/e does not switch");
        yield return KeyPress(Key.E);Check(spectator.TargetSeat==3,"E next player");CameraAt(3,false);yield return KeyPress(Key.E);Check(spectator.TargetSeat==-1,"E returns overview");yield return KeyPress(Key.Q);Check(spectator.TargetSeat==3,"Q wraps to last");yield return KeyPress(Key.Q);Check(spectator.TargetSeat==2,"Q previous player");Mark("views");
        if(role=="host")
        {
            while(!Seen("client","views"))yield return null;var a=Field<DuelActionState[]>(D,"actions");a[2].Aiming=true;a[2].AimStartedAt=Now;a[2].AimYaw=150;a[2].AimPitch=-5;D.Game.Shoot(3,2,DuelWeapon.Revolver,DuelHitPart.Legs,true);Publish();Mark("ads");
        }
        while(!Seen("host","ads")||!player.State.Value.Seat2.Aiming)yield return null;yield return new WaitForSecondsRealtime(.3f);CameraAt(2,true);Check(Shown("basScope"),"watched sniper scope visible");Check(Field<Label>(view,"localHealth").text=="65","remote watched HP updates");Check(Mathf.Abs(Mathf.DeltaAngle(spectator.Yaw,150))<.1f,"remote target yaw updates");yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,role+"-Scope.png"));Mark("ads");
        if(role=="host")
        {
            while(!Seen("client","ads"))yield return null;Kill(2,1);Publish();Mark("target-down");
        }
        while(!Seen("host","target-down")||player.State.Value.Seat2.DiedAt<0)yield return null;yield return new WaitForSecondsRealtime(.15f);Check(spectator.TargetSeat==-1,"target death auto returns overview");Check(!Shown("basScope"),"scope removed on target death");yield return KeyPress(Key.E);Check(spectator.TargetSeat==3,"down target skipped");Mark("down");
        if(role=="host")
        {
            while(!Seen("client","down"))yield return null;D.Game.FinishParty(8);typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Idle,Now});Publish();
        }
        while(player.State.Value.Phase!=DuelPhase.Finished)yield return null;yield return new WaitForSecondsRealtime(.15f);Check(!spectator.Active&&spectator.TargetSeat==-1,"final result exits first person spectator");yield return KeyPress(Key.E);Check(spectator.TargetSeat==-1,"final result cannot switch");Check(Mathf.Abs(Camera.main.fieldOfView-57)<.01f,"final overview restored");Mark("final");
        if(role=="host")
        {
            while(!Seen("client","final"))yield return null;for(int i=0;i<4;i++)D.Ready(i,Now);Publish();
        }
        while(player.State.Value.Phase==DuelPhase.Finished)yield return null;yield return new WaitForSecondsRealtime(.15f);Check(!spectator.Active&&player.State.Value.ForSeat(player.Seat.Value).LivesRemaining==3,"new party resets spectator");var e=DuelGunplay.Eye(player.Seat.Value);Check(Vector3.Distance(Camera.main.transform.position,new Vector3(e.X,e.Y,e.Z))<.001f,"rematch returns own first person");Mark("rematch");if(role=="host")while(!Seen("client","rematch"))yield return null;
        Finish("PASS spectator "+role+": "+checks+" checks");
    }
}
