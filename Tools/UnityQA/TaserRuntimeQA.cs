using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// QA-only: copied into the isolated Unity project, never into production Assets.
public sealed class TaserRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--taser-qa");if(i<0)return;
        var qa=new GameObject("Taser QA").AddComponent<TaserRuntimeQA>();qa.role=args[i+1];
        i=Array.IndexOf(args,"--taser-dir");qa.path=args[i+1];Directory.CreateDirectory(qa.path);
        Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }
    void Mark(string key)=>File.WriteAllText(Path.Combine(path,role+"-"+key),"ok");
    bool Seen(string who,string key)=>File.Exists(Path.Combine(path,who+"-"+key));
    void Finish(string msg,int code=0)
    {
        Debug.Log(msg);File.WriteAllText(Path.Combine(path,role+"-done"),msg);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL taser "+role+": "+message,1);throw new Exception(message);}}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    static void ClockState(DuelDirector d,ClockState state)
    {typeof(DuelClockContest).GetProperty("State").SetValue(typeof(DuelDirector).GetField("clock",F).GetValue(d),state);}
    static void Preparation(DuelDirector d,int iteration,PartyMiniGame mini=PartyMiniGame.Countdown)
    {ClockState(d,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=mini,Iteration=iteration,First=0,StartedAt=100,ParticipantsMask=7});}
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+140;yield return new WaitForSecondsRealtime(1);
        if(role=="model")Model();else if(role=="preview")yield return Preview();else yield return Peers();
        Finish("PASS taser "+role+": "+checks+" checks");
    }
    void Model()
    {
        var d=new DuelDirector(DuelMode.PvP);d.Join(0,0);d.Join(1,0);
        d.SelectItem(0,DuelItem.Recovery);d.SelectItem(0,DuelItem.Taser);d.Ready(0,0);
        Check(d.SetAutoSelectItems(true)&&d.Game.ReadyMask==1,"auto-select preserves ready");
        Check(d.ForSeat(1).SelectedItems==DuelLoadout.All&&!d.ForSeat(1).HasTaser,"auto-select chooses without granting inventory");
        Check(!d.SelectItem(1,DuelItem.Taser),"auto-selected items cannot be unchecked while enabled");
        d.Join(2,0);Check(d.ForSeat(2).SelectedItems==DuelLoadout.All,"later join receives all selections");
        d.Ready(0,0);d.ResetDeveloperSettings(0);
        Check(!d.Game.AutoSelectItems&&d.Game.ReadyMask==1&&d.ForSeat(1).SelectedItems==DuelLoadout.All,"F11 reset clears option and preserves actual selections/ready");
        Check(d.SelectItem(1,DuelItem.Taser)&&d.SelectItem(1,DuelItem.Taser),"manual choice resumes when option off");
        d.Ready(1,0);d.Ready(2,0);Check(d.Game.Phase==DuelPhase.Playing,"selected party starts normally");
        Check(!d.SetAutoSelectItems(true)&&!d.Game.AutoSelectItems,"running-game auto-select rejected");
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,0.0});
        Preparation(d,4);
        Check(!d.UseItem(0,DuelItem.Taser,4,1),"minigame winner cannot use taser");
        Check(!d.UseItem(1,DuelItem.Taser,3,1)&&!d.UseItem(5,DuelItem.Taser,4,1),"stale iteration and absent player rejected");
        Check(d.UseItem(1,DuelItem.Taser,4,1)&&d.ForSeat(1).TaserReadyIteration==7,"use schedules reuse after two blocked turns");
        Check(!d.UseItem(1,DuelItem.Taser,4,1),"same-turn repeat rejected");
        typeof(DuelDirector).GetMethod("ClearTurnEffects",F).Invoke(d,null);
        Preparation(d,5);Check(!d.UseItem(1,DuelItem.Taser,5,1),"next turn blocked");
        Preparation(d,6);Check(!d.UseItem(1,DuelItem.Taser,6,1),"second turn blocked");
        Preparation(d,7);Check(d.UseItem(1,DuelItem.Taser,7,1),"third turn ready");
        typeof(DuelDirector).GetMethod("ClearTurnEffects",F).Invoke(d,null);
        Preparation(d,8);typeof(RouletteRules).GetProperty("UnlimitedItems").SetValue(d.Game,true);
        Check(d.UseItem(1,DuelItem.Taser,8,1),"unlimited-items cheat bypasses cooldown");
        typeof(DuelDirector).GetMethod("ClearTurnEffects",F).Invoke(d,null);
        Preparation(d,9,PartyMiniGame.QuickHands);Check(!d.UseItem(1,DuelItem.Taser,9,1),"cheat still forbids quickhands taser");
        var s=DuelSnapshot.From(d);s.AutoSelectItems=true;s.Seat0.Stunned=true;s.Seat0.StunnedAt=12.5;
        using(var writer=new FastBufferWriter(8192,Allocator.Temp))
        {writer.WriteNetworkSerializable(s);using(var reader=new FastBufferReader(writer,Allocator.Temp)){reader.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(s)&&copy.AutoSelectItems&&copy.Seat0.StunnedAt==12.5&&copy.Seat1.TaserReadyIteration==11,"snapshot preserves auto-select/stun/cooldown");}}
        var changed=s;changed.AutoSelectItems=false;Check(!changed.Equals(s),"auto-select-only change publishes");
    }
    void AimAt(int target)
    {
        var delta=DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(player.Seat.Value);
        DuelGunplay.Angles(delta,out float yaw,out float pitch);
        var fps=manager.GetComponent<DuelFirstPerson>();
        typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,yaw);
        typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,pitch);
    }
    IEnumerator Preview()
    {
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","17965");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        D.Game.SetShotTime(0);D.SetAutoSelectItems(true);D.Join(1,Now);D.Ready(0,Now);D.Ready(1,Now);
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        ClockState(D,new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,Iteration=1,First=0,ParticipantsMask=3,PhaseAt=Now});
        var a=Field<DuelActionState[]>(D,"actions");a[1].Stunned=true;a[1].StunnedAt=Now;
        Field<FixedString64Bytes[]>(manager,"names")[1]=new FixedString64Bytes("player2");
        D.Game.Shoot(0,1,DuelWeapon.Taser,DuelHitPart.Head,true);Publish();
        yield return null;yield return null;AimAt(1);
        yield return new WaitForSecondsRealtime(.4f);yield return Capture();Check(File.Exists(Path.Combine(path,"Stun.png")),"third-person stun render captured");
    }
    IEnumerator Capture()
    {
        const int w=1200,h=800;var doc=view.GetComponent<UIDocument>();
        var uiTarget=new RenderTexture(w,h,24);uiTarget.Create();
        doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=uiTarget;
        doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;
        doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;yield return new WaitForSecondsRealtime(.12f);
        var previous=RenderTexture.active;RenderTexture.active=uiTarget;
        var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var target=new RenderTexture(w,h,24);target.Create();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
        RenderTexture.active=target;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();
        var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);
        scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,"Stun.png"),scene.EncodeToPNG());
        RenderTexture.active=previous;doc.panelSettings.targetTexture=null;target.Release();uiTarget.Release();
        Destroy(target);Destroy(uiTarget);Destroy(ui);Destroy(scene);
    }
    IEnumerator Peers()
    {
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role!="host")while(!Seen("host",role=="observer"?"client-ready":"listening"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17964");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==43,"protocol43");
        if(role=="host")
        {
            manager.SetRoomAutoSelectItems(true);Check(!D.Game.AutoSelectItems,"developer-mode gate");
            manager.ToggleDeveloperMode();manager.SetRoomAutoSelectItems(true);manager.SetRoomManualMiniGame(true);manager.SetRoomShotTime(60);
            Check(D.Game.AutoSelectItems&&D.ForSeat(0).SelectedItems==3,"host auto-select option");Mark("listening");
            while(!Seen("client","joined"))yield return null;Mark("client-ready");
        }
        while(!player.State.Value.AutoSelectItems)yield return null;yield return null;yield return null;
        Check(player.State.Value.ForSeat(player.Seat.Value).SelectedItems==DuelLoadout.All,"host/remote/late join all selected");
        Check((role!="host"||Field<Toggle>(view,"autoSelectItems").value)&&!Field<Button[]>(view,"loadoutButtons")[0].enabledSelf,"synced option and selected UI");
        if(role=="client")Mark("joined");
        while(player.State.Value.ConnectedMask!=7)yield return null;
        player.Rematch();while(player.State.Value.Stage!=DuelStage.Racing)yield return null;
        for(int pass=0;pass<2;pass++)
        {
            if(role=="host")
            {
                while(D.Clock.Phase!=ClockPhase.Selecting)yield return null;
                if(pass==1){var clock=D.Clock;clock.Iteration+=2;ClockState(D,clock);}
                Check(D.ChooseMiniGame(PartyMiniGame.Countdown,D.Clock.Iteration,Now),"start test minigame");Publish();
                while(Now<D.Clock.StartedAt+.12)yield return null;
                double now=Now;for(int i=0;i<3;i++)Check(D.StopClock(i,D.Clock.Iteration,now-i*.01,now),"authoritative test ranking");Publish();
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;
            yield return null;yield return null;
            if(role=="client")
            {
                Check(Field<Button[]>(view,"itemButtons")[1].enabledSelf,"taser available before use");
                player.UseItem(DuelItem.Taser);
                while(!player.State.Value.Seat1.TaserArmed)yield return null;yield return null;
                Check(Field<Button[]>(view,"itemButtons")[1].ClassListContains("item-used"),"activated taser yellow state");
                Check(player.State.Value.Seat1.TaserReadyIteration==player.State.Value.Clock.Iteration+3,"remote cooldown synced");
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;
            if(role=="host")
            {
                // Prevent random return fire from shortening the visual inspection window.
                Field<DuelActionState[]>(D,"actions")[0].LoadedMask=0;
                if(pass==1)Field<int[]>(D.Game,"health")[0]=5;
                Publish();Mark("shooting"+pass);
            }
            while(!Seen("host","shooting"+pass))yield return null;
            if(role=="client")
            {
                AimAt(0);yield return new WaitForSecondsRealtime(.15f);
                var target=DuelGunplay.Point(0,DuelHitPart.Head);
                DuelGunplay.Angles(target-DuelGunplay.Eye(1),out float yaw,out float pitch);
                for(int i=0;i<4;i++)DuelGunplay.Angles(target-DuelGunplay.MuzzleOrigin(1,DuelWeapon.Taser,false,DuelGunplay.Direction(yaw,pitch)),out yaw,out pitch);
                var ray=DuelGunplay.Direction(yaw,pitch);var origin=DuelGunplay.MuzzleOrigin(1,DuelWeapon.Taser,false,ray);
                Check(DuelGunplay.TraceFrom(1,player.State.Value.AliveMask,origin,ray,player.State.Value.Tabletop).Seat==0,"test ray reaches target with no prop occlusion");
                double sample=Now;DuelGunplay.Sway(1,DuelWeapon.Taser,false,sample,0,out float sy,out float sp);
                player.Fire(yaw-sy,pitch-sp,false,sample);
            }
            if(role=="observer")AimAt(0);
            if(pass==0)
            {
                while(!player.State.Value.Seat0.Stunned)yield return null;yield return null;yield return null;
                Check(player.State.Value.Seat0.Health==95&&player.State.Value.Seat0.LivesRemaining==3,"taser damage5 without life loss");
                var arcs=Field<LineRenderer[,]>(manager.Table,"stunArcs");
                for(int i=0;i<3;i++)Check(role=="host"?arcs[0,i]==null||!arcs[0,i].enabled:arcs[0,i]!=null&&arcs[0,i].enabled&&arcs[0,i].positionCount==9,"third-person electricity, own first-person clean");
                while(Field<VisualElement>(view,"killFeed").childCount==0)yield return null;
                var feed=Field<VisualElement>(view,"killFeed");
                Check(feed.Q<Label>(className:"kill-stun")?.text=="감전","nonfatal taser feed shown for every peer");
                Check(feed.Q<Image>(className:"kill-weapon").image==Field<Texture2D[]>(view,"weaponIcons")[2],"taser icon in feed");
                Check(!Field<Label>(view,"victoryText").ClassListContains("death-banner"),"stun never reports victim death");
                if(role=="observer")yield return Capture();
                Mark("stun");while(!Seen("host","stun")||!Seen("client","stun")||!Seen("observer","stun"))yield return null;
                if(role=="host"){Field<DuelClockContest>(D,"clock").Return(Now);D.Tick(Now);Publish();}
                while(player.State.Value.Clock.Phase==ClockPhase.Shooting)yield return null;yield return null;yield return null;
                Check(!player.State.Value.Seat0.Stunned,"stun cleared after shooting");
                for(int i=0;i<3;i++)Check(arcs[0,i]==null||!arcs[0,i].enabled,"third-person effect removed after shooting");
                Mark("cleared");while(!Seen("host","cleared")||!Seen("client","cleared")||!Seen("observer","cleared"))yield return null;
                typeof(RouletteView).GetMethod("ClearKills",F).Invoke(view,null);
            }
            else
            {
                while(player.State.Value.Seat0.LivesRemaining!=2)yield return null;
                while(Field<VisualElement>(view,"killFeed").childCount==0)yield return null;yield return null;
                var feed=Field<VisualElement>(view,"killFeed");
                Check(player.State.Value.Seat0.Health==0,"taser fatal damage authoritative");
                Check(feed.childCount==1&&feed.Q<Label>(className:"kill-stun")==null,"one kill entry without nonfatal duplicate");
                Check(feed.Q<Image>(className:"kill-weapon").image==Field<Texture2D[]>(view,"weaponIcons")[2],"taser kill icon for every peer");
                if(role=="host")Check(Field<Label>(view,"victoryText").ClassListContains("death-banner"),"victim life-loss banner on lethal hit");
                Mark("fatal");while(!Seen("host","fatal")||!Seen("client","fatal")||!Seen("observer","fatal"))yield return null;
            }
        }
        manager.Leave();
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL taser "+role+": timeout "+(player==null?"no player":player.State.Value.Clock.Phase+" hp="+player.State.Value.Seat0.Health+" ammo="+player.State.Value.Seat1.Ammo+" shots="+player.State.Value.Shots),1);}
}
