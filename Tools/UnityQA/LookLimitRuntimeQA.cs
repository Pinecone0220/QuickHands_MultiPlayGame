using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run only in the isolated QA project.
public sealed class LookLimitRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--look-limit-qa");if(i<0)return;
        var qa=new GameObject("Look limit QA").AddComponent<LookLimitRuntimeQA>();qa.role=args[i+1];i=Array.IndexOf(args,"--look-limit-dir");qa.path=args[i+1];Directory.CreateDirectory(qa.path);
        Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }
    void Finish(string message,int code=0)
    {
        Debug.Log(message);File.WriteAllText(Path.Combine(path,role+"-done"),message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool value,string label){checks++;if(!value){Finish("FAIL look limit "+role+": "+label,1);throw new Exception(label);}}
    void Mark(string key)=>File.WriteAllText(Path.Combine(path,role+"-"+key),"ok");
    bool Seen(string who,string key)=>File.Exists(Path.Combine(path,who+"-"+key));
    sealed class FixedSpread:System.Random{public FixedSpread():base(18){}public override double NextDouble()=>.25;}
    static void Stage(DuelDirector d,double now)=>typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,now});
    void Prepare(DuelDirector d,int seat,DuelWeapon weapon,double now)
    {
        var c=Field<DuelClockContest>(d,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(c,new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,Iteration=1,ParticipantsMask=d.Game.AliveMask,First=seat,Last=(seat+1)%6,PhaseAt=now});
        var a=Field<DuelActionState[]>(d,"actions");a[seat].Weapon=weapon;a[seat].Action=DuelAction.Aim;a[seat].LoadedMask=1;a[seat].Version++;a[seat].StartedAt=now;a[seat].ShootingStartedAt=now;a[seat].AimYaw=seat*60+85;
    }
    DuelDirector Director()
    {
        var d=new DuelDirector(DuelMode.PvP,new FixedSpread());for(int i=0;i<6;i++)d.Join(i,0);d.SetAutoSelectItems(true);for(int i=0;i<6;i++)d.Ready(i,0);Stage(d,0);return d;
    }
    void Model()
    {
        for(int seat=0;seat<6;seat++)
        {
            for(int sign=-1;sign<=1;sign+=2)
            {
                float limit=DuelGunplay.MoveLookYaw(seat,seat*60,sign*10000);
                Check(Math.Abs(DuelGunplay.RelativeYaw(seat,limit)-sign*85)<.001,"large mouse travel reaches correct side without wrapping at seat "+seat);
                float reverse=DuelGunplay.MoveLookYaw(seat,limit,-sign*.5f);
                Check(Math.Abs(DuelGunplay.RelativeYaw(seat,reverse)-sign*84.5f)<.001,"reverse motion responds immediately without hidden overshoot");
                Check(DuelGunplay.ValidSeatAngles(seat,limit,65)&&!DuelGunplay.ValidSeatAngles(seat,seat*60+sign*85.1f,0),"raw limits accept boundary and reject excess");
                int target=(seat+(sign>0?5:1))%6;DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(seat),out float yaw,out float pitch);
                Check(Math.Abs(DuelGunplay.RelativeYaw(seat,yaw))+15<85,"adjacent head leaves enough travel to miss beyond it");
                Check(DuelGunplay.Trace(seat,63,DuelGunplay.Direction(seat*60+sign*85,pitch)).Seat<0,"look boundary does not rest on any player");
            }
            var d=Director();Prepare(d,seat,DuelWeapon.Revolver,10);var before=d.ForSeat(seat);int sequence=d.Sequence;
            Check(!d.Look(seat,seat*60+180,0,false,10.1)&&!d.Fire(seat,seat*60+85.1f,0,false,before.Version,10.1,10.1)&&d.Sequence==sequence&&d.ForSeat(seat).Equals(before),"illegal look/fire rejected without snapping or consuming ammo");
        }
        foreach(int seat in new[]{0,5})foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff})foreach(bool ads in new[]{false,true})foreach(int sign in new[]{-1,1})
        {
            var d=Director();Prepare(d,seat,weapon,10);double now=10.01;float sy=0,sp=0;
            for(int i=0;i<250;i++){now=10.01+i*.01;DuelGunplay.Sway(seat,weapon,ads,now,0,out sy,out sp);if(sign*sy>.5f)break;}
            float yaw=seat*60+sign*85;Check(d.Fire(seat,yaw,0,ads,d.ForSeat(seat).Version,now,now),"legal boundary shot accepted");var fired=d.ForSeat(seat);
            Check(Math.Abs(fired.FiredYaw-(yaw+sy))<.001&&Math.Abs(DuelGunplay.RelativeYaw(seat,fired.FiredYaw))>85,"sway stays outside boundary instead of becoming aim assistance");
            float spread=DuelGunplay.Spread(weapon,ads);float expectedPitch=sp+(weapon==DuelWeapon.Revolver?spread*.5f:0);
            Check(Math.Abs(fired.FiredPitch-expectedPitch)<.001&&Math.Abs(fired.FiredSpread-(weapon==DuelWeapon.SawedOff?spread:0))<.001,"weapon spread and pitch sway stay unchanged");
        }
        Check(!DuelGunplay.ValidSeatAngles(0,float.NaN,0)&&!DuelGunplay.ValidSeatAngles(0,0,float.PositiveInfinity),"nonfinite angles rejected");
    }
    IEnumerator Move(float delta)
    {
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(500,400),delta=new Vector2(delta,0)});yield return null;yield return null;yield return null;
    }
    IEnumerator Peers()
    {
        manager=HelloWorldManager.Instance;if(role!="host")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17977");while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        if(role=="host"){manager.ToggleDeveloperMode();manager.SetRoomAutoSelectItems(true);manager.SetRoomManualMiniGame(true);manager.SetRoomShotTime(0);Mark("listening");}
        while(player.State.Value.ConnectedMask!=3||!player.State.Value.AutoSelectItems||!player.State.Value.ManualMiniGame)yield return null;
        player.Rematch();while(player.State.Value.Clock.Phase!=ClockPhase.Selecting||player.State.Value.Stage!=DuelStage.Racing)yield return null;
        int seat=player.Seat.Value;var fps=manager.GetComponent<DuelFirstPerson>();
        yield return Move(100000);Check(Math.Abs(DuelGunplay.RelativeYaw(seat,fps.Yaw)-85)<.01,"actual mouse motion clamps positive yaw");
        yield return Move(-10);Check(DuelGunplay.RelativeYaw(seat,fps.Yaw)<85&&DuelGunplay.RelativeYaw(seat,fps.Yaw)>80,"mouse immediately moves away from boundary");
        yield return Move(-100000);Check(Math.Abs(DuelGunplay.RelativeYaw(seat,fps.Yaw)+85)<.01,"actual mouse motion clamps negative yaw");
        player.Look(seat*60+180,0,false);yield return new WaitForSecondsRealtime(.2f);Check(DuelGunplay.ValidSeatAngles(seat,player.State.Value.ForSeat(seat).AimYaw,0),"server never publishes invalid raw look");Mark("looked");
        while(!Seen("host","looked")||!Seen("client","looked"))yield return null;
        if(role=="host"){Prepare(D,1,DuelWeapon.Revolver,Now);typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);}
        while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;
        if(role=="client")
        {
            player.Fire(60+85.1f,0,false,Now);yield return new WaitForSecondsRealtime(.25f);Check(player.State.Value.Seat1.Ammo==1&&player.State.Value.Seat1.ShotsFired==0,"remote out-of-range fire rejected without consuming round");
            player.Fire(60+85,0,false,Now);
        }
        while(player.State.Value.Seat1.ShotsFired!=1)yield return null;var shot=player.State.Value.Seat1;
        Check(shot.Ammo==0&&Math.Abs(shot.FiredYaw-145)<11,"remote valid boundary shot preserves real sway and spread");
        yield return new WaitForSecondsRealtime(.4f);Check(player.State.Value.Seat0.Health==100&&player.State.Value.Seat1.Health==100,"boundary remains a miss beyond adjacent opponent");
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
    }
    IEnumerator Start(){deadline=Time.realtimeSinceStartupAsDouble+120;yield return new WaitForSecondsRealtime(1);if(role=="model")Model();else yield return Peers();Finish("PASS look limit "+role+": "+checks+" checks");}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL look limit "+role+": timeout",1);}
}
