using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Isolated QA project only. No production input or test hooks.
public sealed class PracticeRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks,hits;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelFirstPerson fps;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--practice-qa");if(i<0)return;
        var q=new GameObject("Practice QA").AddComponent<PracticeRuntimeQA>();q.role=args[i+1];
        q.path=args[Array.IndexOf(args,"--practice-dir")+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+180;Application.targetFrameRate=30;
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
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL practice "+role+": "+message,1);throw new Exception(message);}}
    void Mark(string key)=>File.WriteAllText(Path.Combine(path,role+"-"+key),"ok");
    bool Seen(string who,string key)=>File.Exists(Path.Combine(path,who+"-"+key));
    bool All(string key)=>Seen("host",key)&&Seen("client",key)&&Seen("observer",key);
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    static void Aim(DuelDirector d,int actor,int target,double now,out float yaw,out float pitch)
    {
        var a=d.ForSeat(actor);DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(actor),out yaw,out pitch);
        DuelGunplay.Sway(actor,a.Weapon,true,now,a.AimStartedAt,out float sy,out float sp);yaw=(yaw-sy+360)%360;pitch-=sp;
    }
    sealed class FixedSpread:System.Random{public override double NextDouble()=>0;}
    void Model()
    {
        var d=new DuelDirector(DuelMode.PvP,new FixedSpread());for(int i=0;i<3;i++)d.Join(i,0);
        d.SetAutoSelectItems(true);d.SelectWeapon(1,DuelWeapon.SawedOff,0);
        Check(!d.Fire(0,0,0,false,d.ForSeat(0).Version,0,0),"unready cannot shoot");
        d.Ready(0,0);d.Ready(1,0);
        Check(d.PracticeMask==3&&d.Stage==DuelStage.Idle,"two ready players can practice while a third waits");
        Check(d.ForSeat(0).Ammo==6&&d.ForSeat(1).Ammo==1,"chosen guns appear loaded");
        double now=1;int confirmed=0;d.PracticeHitConfirmed+=(actor,session,shot,head)=>confirmed++;
        for(int actor=0;actor<2;actor++)for(int i=0;i<(actor==0?8:3);i++)
        {
            now+=.7;Aim(d,actor,1-actor,now,out float y,out float p);int version=d.ForSeat(actor).Version;
            Check(d.Fire(actor,y,p,true,version,now,now),"unlimited practice shot accepted");
            Check(!d.Fire(actor,y,p,true,version,now,now),"replayed shot version rejected");
            Check(!d.Fire(actor,y,p,true,d.ForSeat(actor).Version,now+.01,now+.01),"practice firing rate bounded");
            d.Tick(now+.6);now+=.6;
            Check(d.Game.HealthForSeat(0)==100&&d.Game.HealthForSeat(1)==100&&d.Game.Shots==0,"warm-up never changes real HP or shot statistics");
            Check(d.ForSeat(actor).ShotsFired==0&&d.ForSeat(actor).Kills==0&&d.ForSeat(actor).DamageDealt==0,"practice excluded from results");
        }
        Check(confirmed==11,"actual server projectile hits produce one confirmation each");
        Check(d.ForSeat(0).PracticeShotsFired==8&&d.ForSeat(1).PracticeShotsFired==3,"more shots than either weapon capacity");
        Check(d.ForSeat(0).Ammo==6&&d.ForSeat(1).Ammo==1&&d.PracticeMask==3,"ammo and practice persist beyond normal shot timer");
        now+=1;Aim(d,0,2,now,out float unreadyY,out float unreadyP);d.Fire(0,unreadyY,unreadyP,true,d.ForSeat(0).Version,now,now);d.Tick(now+.6);
        Check(d.ForSeat(2).HitAt<0&&confirmed==11,"unready player is not a hit target");
        Check(!d.Fire(0,float.NaN,0,true,d.ForSeat(0).Version,now,now),"nonfinite angles rejected");
        Check(!d.Fire(0,180,0,true,d.ForSeat(0).Version,now,now),"outside-seat angle rejected");
        Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,now-1,now),"old timestamp rejected");
        now+=1;Aim(d,0,1,now,out float cy,out float cp);d.Fire(0,cy,cp,true,d.ForSeat(0).Version,now,now);
        int session=d.ForSeat(1).PracticeSession;d.Ready(1,now+.01);d.Tick(now+.6);
        Check(!d.IsPracticing(1)&&d.ForSeat(1).HitAt<0,"cancelled target avoids in-flight warm-up hit");
        d.Ready(1,now+.7);Check(d.ForSeat(1).PracticeSession>session,"new ready session invalidates old confirmations");
        now+=2;Aim(d,0,1,now,out cy,out cp);d.Fire(0,cy,cp,true,d.ForSeat(0).Version,now,now);
        d.Ready(2,now+.01);d.Tick(now+.2);
        Check(d.Game.Phase==DuelPhase.Playing&&d.Stage==DuelStage.Countdown&&d.PracticeMask==0,"all ready starts existing match countdown");
        Check(d.ForSeat(1).HitAt<0&&d.ForSeat(0).PracticeShotsFired==0&&d.Game.Shots==0,"match start clears practice flights, effects and counters");
        Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,now+.2,now+.2),"no practice fire during start countdown");
        d.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Head,true);d.Game.FinishParty(1);
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Idle,now});
        var raw=Field<DuelActionState[]>(d,"actions");raw[1].LivesRemaining=0;raw[1].DiedAt=now;
        var frozen=DuelRoundResults.Capture(d,new FixedString64Bytes[6],now);
        d.Ready(1,now+.3);
        Check(d.IsPracticing(1)&&d.ForSeat(1).DiedAt<0&&d.ForSeat(1).Health==100&&d.Game.HealthForSeat(1)==0,"eliminated player can practice without rewriting last match health");
        Check(frozen.Seat1.RemainingHealth==0,"finished result remains frozen");
        d.Ready(0,now+.31);raw[0].ShootingStartedAt=0;
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(d,"clock"),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1});
        int idleVersion=d.ForSeat(0).Version;d.Tick(now+1);
        Check(d.ForSeat(0).Version==idleVersion,"finished quick-hands timer cannot reset warm-up action");
        var s=DuelSnapshot.From(d);s.Results=frozen;var changed=s;changed.Seat1.PracticeSession++;
        Check(!s.Equals(changed),"practice session participates in snapshot equality");
        changed=s;changed.Seat1.PracticeShotsFired++;Check(!s.Equals(changed),"practice shot counter participates in equality");
        using(var writer=new FastBufferWriter(8192,Allocator.Temp))
        {writer.WriteNetworkSerializable(s);using(var reader=new FastBufferReader(writer,Allocator.Temp)){reader.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(s)&&copy.IsPracticing(1),"practice fields and frozen result serialize together");}}
        now+=2;Aim(d,1,0,now,out float jy,out float jp);d.Fire(1,jy,jp,true,d.ForSeat(1).Version,now,now);
        int previousSession=d.ForSeat(1).PracticeSession;d.Join(3,now+.01);d.Tick(now+.6);
        Check(d.PracticeMask==0,"new join resets ready practice through existing room policy");
        Check(d.ForSeat(1).FiredAt<0&&d.ForSeat(1).PracticeSession>previousSession,"new join invalidates old visuals and delayed confirmations");
    }
    IEnumerator FireAt(int target)
    {
        int seat=player.Seat.Value,old=player.State.Value.ForSeat(seat).PracticeShotsFired;
        var a=player.State.Value.ForSeat(seat);double now=Now;
        DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(seat),out float y,out float p);
        DuelGunplay.Sway(seat,a.Weapon,true,now,a.AimStartedAt,out float sy,out float sp);y=(y-sy+360)%360;p-=sp;
        player.Fire(y,p,true,now);player.Fire(y,p,true,now);
        while(player.State.Value.ForSeat(seat).PracticeShotsFired==old)yield return null;
        yield return new WaitForSecondsRealtime(.36f);
        Check(player.State.Value.ForSeat(seat).PracticeShotsFired==old+1,"duplicate owner request produces one practice shot");
    }
    IEnumerator Peers()
    {
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        manager.GetComponent<DuelGameAudio>().Played+=name=>{if(name=="HitConfirm"||name=="HitHead")hits++;};
        if(role!="host")while(!Seen("host",role=="client"?"listening":"client-joined"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17969");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==45,"protocol45");
        if(role=="host"){Mark("listening");while(!Seen("client","joined"))yield return null;Mark("client-joined");}
        else if(role=="client")Mark("joined");
        while(player.State.Value.ConnectedMask!=7)yield return null;
        if(role=="host"){D.SetAutoSelectItems(true);Publish();}
        while(player.State.Value.ForSeat(player.Seat.Value).SelectedItems!=DuelLoadout.All)yield return null;
        if(role=="client"){player.SelectWeapon(DuelWeapon.SawedOff);while(player.State.Value.ForSeat(1).SelectedWeapon!=DuelWeapon.SawedOff)yield return null;}
        Mark("configured");while(!All("configured"))yield return null;
        if(role!="observer")player.Rematch();
        while(player.State.Value.PracticeMask!=3)yield return null;yield return null;yield return null;
        Check(fps.Active==(role!="observer"),"only ready players receive active first-person gun controls");
        Check((Field<Button>(view,"practiceCancel").style.display.value==DisplayStyle.Flex)==(role!="observer"),"ready cancel stays accessible during practice");
        if(role!="observer")fps.enabled=false;
        Mark("ready");while(!All("ready"))yield return null;
        if(role=="host")
        {
            for(int i=0;i<8;i++)yield return FireAt(1);
            Check(hits==8,"host receives exactly eight own hit sounds");
            Check(D.Game.HealthForSeat(0)==100&&D.Game.HealthForSeat(1)==100&&D.Game.Shots==0,"actual server health and game statistics untouched");
            Mark("revolver");
        }
        while(!Seen("host","revolver"))yield return null;
        if(role!="host")Check(hits==0,"foreign revolver shots do not play owner hit sounds");
        if(role=="client")
        {for(int i=0;i<3;i++)yield return FireAt(0);Check(hits==3,"remote sawed-off fires repeatedly beyond single-shell capacity");Mark("sawed");}
        while(!Seen("client","sawed"))yield return null;
        Check(player.State.Value.Seat0.Health==100&&player.State.Value.Seat1.Health==100&&player.State.Value.Shots==0,"all peers retain practice HP and zero match shots");
        Check(Field<VisualElement>(view,"killFeed").childCount==0,"no warm-up kill entries");
        if(role=="observer")Check(player.State.Value.Seat2.HitAt<0&&hits==0,"unready observer stays outside target and feedback masks");
        Mark("shots");while(!All("shots"))yield return null;
        if(role=="client")
        {
            fps.enabled=true;int previous=player.State.Value.Seat1.PracticeSession;player.Rematch();while(player.State.Value.IsPracticing(1))yield return null;yield return null;
            Check(!fps.Active,"cancel exits practice controls");player.Rematch();while(!player.State.Value.IsPracticing(1))yield return null;
            Check(player.State.Value.Seat1.PracticeSession>previous,"new ready session synchronized");Mark("reready");
        }
        while(!Seen("client","reready"))yield return null;
        fps.enabled=true;if(role=="observer")player.Rematch();
        while(player.State.Value.Round<1)yield return null;
        Check(player.State.Value.PracticeMask==0&&player.State.Value.Stage==DuelStage.Countdown,"all ready begins existing countdown and stops practice");
        Check(player.State.Value.Seat0.PracticeShotsFired==0&&player.State.Value.Seat1.PracticeShotsFired==0&&player.State.Value.Seat0.HitAt<0,"match clears practice counters and hit pulses");
        Mark("started");while(!All("started"))yield return null;
        if(role=="host")
        {
            D.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Head,true);D.Game.FinishParty(1);
            typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Idle,Now});
            var a=Field<DuelActionState[]>(D,"actions");a[1].DiedAt=Now;a[1].LivesRemaining=0;Publish();Mark("finished");
        }
        while(!Seen("host","finished")||player.State.Value.Phase!=DuelPhase.Finished)yield return null;
        yield return new WaitForSecondsRealtime(3.1f);Check(!fps.Active,"unready final results retain fixed third-person controls");
        var results=player.State.Value.Results;
        if(role=="client")player.Rematch();
        while(!player.State.Value.IsPracticing(1))yield return null;yield return null;yield return null;
        Check(player.State.Value.Results.Equals(results),"frozen final result preserved through warm-up");
        if(role=="client")
        {
            Check(fps.Active&&player.State.Value.Seat1.Health==100&&player.State.Value.Seat1.DiedAt<0,"eliminated remote player returns to first-person practice on ready");
            Check(Field<VisualElement>(view,"resultsLayer").style.display.value==DisplayStyle.None,"results panel no longer blocks ready player's aiming");
        }
        else Check(!fps.Active,"unready players retain final results viewpoint");
        Mark("complete");while(!All("complete"))yield return null;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);
        if(role=="model")Model();else if(role=="preview")yield return Preview();else yield return Peers();
        Finish("PASS practice "+role+": "+checks+" checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL practice timeout "+role,1);}

    void CameraAt(int target)
    {
        DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(0),out float y,out float p);
        typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,y);
        typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,p);
    }
    IEnumerator Preview()
    {
        var props=new DuelTabletop();props.Reset(0);var origin=DuelGunplay.Local(0,.66f,1.08f,1.5f);var direction=DuelGunplay.Direction(180,0);
        var practiceBullet=new DuelProjectileVolley(0,0,0,DuelWeapon.Revolver,origin,direction,0,0,0);
        Check(!practiceBullet.Tick(0,.001,out var contact,null,props.State),"practice prop collision respects finite travel");
        Check(practiceBullet.Tick(0,.03,out contact,null,props.State)&&!props.State.Broken(0,DuelTableProp.Bottle),"practice props block without breaking");
        var liveBullet=new DuelProjectileVolley(0,0,0,DuelWeapon.Revolver,origin,direction,0,0,0);
        Check(liveBullet.Tick(0,.03,out contact,props)&&props.State.Broken(0,DuelTableProp.Bottle),"normal prop collision still breaks bottle");
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","17970");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        D.Join(1,Now);D.Join(2,Now);D.SetAutoSelectItems(true);D.SelectWeapon(1,DuelWeapon.SawedOff,Now);
        var names=Field<FixedString64Bytes[]>(manager,"names");names[1]=new FixedString64Bytes("연습 상대");names[2]=new FixedString64Bytes("준비 대기");
        var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
        D.Ready(0,Now);D.Ready(1,Now);Publish();yield return null;yield return null;
        Check(D.ForSeat(0).PracticeShotsFired==0,"ready click cannot also fire on practice entry");
        InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;fps.enabled=false;CameraAt(1);
        Check(fps.Active&&player.State.Value.PracticeMask==3,"rendered warm-up has owner camera and exactly two ready players");
        for(int i=0;i<20&&D.ForSeat(1).Target!=0;i++)
        {Aim(D,1,0,Now,out float y,out float p);D.Look(1,y,p,true,Now);Publish();yield return new WaitForSecondsRealtime(.04f);}
        yield return Capture("Practice-Aim-Orange.png");
        Check(Field<VisualElement>(view,"aimWarning").style.display.value==DisplayStyle.Flex,"practice victim sees aim warning");
        Aim(D,1,0,Now,out float sy,out float sp);Check(D.Fire(1,sy,sp,true,D.ForSeat(1).Version,Now,Now),"preview practice shot accepted");Publish();
        while(D.ForSeat(0).HitAt<0)yield return null;
        yield return new WaitForSecondsRealtime(.04f);yield return Capture("Practice-Hit-Peak.png");
        Check(Field<VisualElement>(view,"damageOverlay").style.opacity.value>0,"practice hit border visible");
        yield return new WaitForSecondsRealtime(.10f);yield return Capture("Practice-Hit-Spread.png");
        yield return new WaitForSecondsRealtime(.5f);yield return Capture("Practice-Hit-Clear.png");
        Check(Field<VisualElement>(view,"damageOverlay").style.display.value==DisplayStyle.None,"practice hit border expires");
        D.Ready(2,Now);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=7,StartedAt=Now});
        var a=Field<DuelActionState[]>(D,"actions");a[0].Action=a[1].Action=DuelAction.Aim;a[0].LoadedMask=63;a[1].LoadedMask=1;
        Publish();fps.enabled=true;yield return null;yield return null;fps.enabled=false;CameraAt(1);
        Aim(D,1,0,Now,out sy,out sp);Check(D.Fire(1,sy,sp,true,D.ForSeat(1).Version,Now,Now),"normal match shot accepted");Publish();
        while(D.Game.HealthForSeat(0)==100)yield return null;
        yield return new WaitForSecondsRealtime(.04f);yield return Capture("Match-Hit-Orange.png");
        Check(D.Game.HealthForSeat(0)==25&&Field<VisualElement>(view,"damageOverlay").style.opacity.value>0,"normal damage and widened hit vignette coexist");
    }
    IEnumerator Capture(string name)
    {
        const int w=1200,h=800;var doc=view.GetComponent<UIDocument>();var uiTarget=new RenderTexture(w,h,24);uiTarget.Create();
        doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=uiTarget;
        doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;
        yield return new WaitForSecondsRealtime(.12f);
        var previous=RenderTexture.active;RenderTexture.active=uiTarget;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var target=new RenderTexture(w,h,24);target.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
        RenderTexture.active=target;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();
        var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();
        File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;doc.panelSettings.targetTexture=null;
        target.Release();uiTarget.Release();Destroy(target);Destroy(uiTarget);Destroy(ui);Destroy(scene);
    }
}
