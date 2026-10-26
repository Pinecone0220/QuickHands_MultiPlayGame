using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

public sealed class QuickInventoryRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;Keyboard keyboard;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--quick-items-qa");if(i<0)return;
        var q=new GameObject("Quick inventory QA").AddComponent<QuickInventoryRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--quick-items-dir")+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+140;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
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
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL quick items "+role+": "+s,1);throw new Exception(s);}Debug.Log("Quick items check: "+s);}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL quick items timeout "+role,1);}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Invoke(DuelDirector d,string method,params object[] args)=>typeof(DuelDirector).GetMethod(method,F).Invoke(d,args);
    void Phase(DuelDirector d,ClockPhase phase,double now,int iteration=1,PartyMiniGame mini=PartyMiniGame.QuickHands)
    {
        Invoke(d,"SetStage",DuelStage.Racing,now);
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(d,"clock"),new ClockState{Phase=phase,MiniGame=mini,Iteration=iteration,ParticipantsMask=d.Game.AliveMask,First=0,Last=1,StartedAt=now+5,PhaseAt=now});
    }
    DuelDirector Setup(bool unlimited=false,DuelMode mode=DuelMode.PvP)
    {
        var d=new DuelDirector(mode,new System.Random(63));for(int i=0;i<4;i++)d.Join(i,0);d.SetAutoSelectItems(true);if(unlimited)d.Game.SetUnlimitedItems(true);
        if(mode==DuelMode.PvP)for(int i=0;i<4;i++)d.Ready(i,0);else d.Ready(0,0);
        Phase(d,ClockPhase.ItemPreparation,10);return d;
    }
    bool Switch(DuelDirector d,int seat,bool equip,double now)
    {var a=d.ForSeat(seat);return d.SwitchQuickItem(seat,equip,d.Clock.Iteration,a.Life,a.QuickItemVersion,now);}
    void Reserve(DuelDirector d,int seat,DuelItem item){Check(d.UseItem(seat,item,1,10.1),"reserve "+item+" seat "+seat);}
    void Gun(DuelDirector d,int seat,DuelWeapon weapon=DuelWeapon.Revolver)
    {var a=Field<DuelActionState[]>(d,"actions");a[seat].Weapon=weapon;a[seat].Action=DuelAction.Load;a[seat].Code=123;a[seat].Progress=1;a[seat].LoadedMask=3;a[seat].SpentMask=4;a[seat].StartedAt=10;a[seat].ShootingStartedAt=-1;a[seat].BlockedUntil=0;}
    bool SameGun(DuelActionState a,DuelActionState b)=>a.Weapon==b.Weapon&&a.Action==b.Action&&a.Code==b.Code&&a.Progress==b.Progress&&a.LoadedMask==b.LoadedMask&&a.SpentMask==b.SpentMask&&a.NextChamber==b.NextChamber;
    void Model()
    {
        var d=Setup();Reserve(d,0,DuelItem.Taser);Check(d.ForSeat(0).QuickItem==2&&!d.ForSeat(0).QuickEquipped&&d.ForSeat(0).Weapon==DuelWeapon.Revolver&&d.ForSeat(0).TaserReadyIteration==0,"prep reserves without auto-equip/cooldown");
        Check(d.UseItem(0,DuelItem.Ink,1,10.2)&&d.ForSeat(0).QuickItem==3,"only one inventory slot; change selection before start");
        Check(d.UseItem(0,DuelItem.Ink,1,10.3)&&d.ForSeat(0).QuickItem==0,"click selected item to cancel before start");Reserve(d,0,DuelItem.Taser);
        Check(!Switch(d,0,true,10.5),"cannot equip during prep");Phase(d,ClockPhase.QuickHands,11);Gun(d,0);var gun=d.ForSeat(0);
        Check(!d.UseItem(0,DuelItem.Ink,1,11.1),"cannot add another item during QuickHands");Check(Switch(d,0,true,11.1)&&d.ForSeat(0).Weapon==DuelWeapon.Taser&&d.ForSeat(0).Ammo==1,"equip taser with one round");
        Check(!d.Input(0,1,d.ForSeat(0).Version,11.2),"taser blocks gun digits");var b=d.ForSeat(1);Check(d.Input(1,b.Digit(b.Progress),b.Version,11.2),"other player's digits continue");
        Check(Switch(d,0,false,11.3)&&SameGun(gun,d.ForSeat(0))&&d.ForSeat(0).Version>gun.Version,"holster restores complete gun mechanism with fresh version");
        Check(!d.SwitchQuickItem(0,true,0,gun.Life,d.ForSeat(0).QuickItemVersion,11.4)&&!d.SwitchQuickItem(0,true,1,gun.Life+1,d.ForSeat(0).QuickItemVersion,11.4)&&!d.SwitchQuickItem(0,true,1,gun.Life,0,11.4),"stale iteration/life/revision denied");
        Check(!d.SwitchQuickItem(-1,true,1,1,0,11.4)&&!d.SwitchQuickItem(6,true,1,1,0,11.4),"bounds validated");
        Switch(d,0,true,11.5);var a=d.ForSeat(0);DuelGunplay.Angles(DuelGunplay.Point(3,DuelHitPart.Body)-DuelGunplay.Eye(0),out float yaw,out float pitch);
        Check(d.Fire(0,yaw,pitch,false,a.Version,11.6,11.6)&&d.ForSeat(0).QuickSpent&&d.ForSeat(0).TaserReadyIteration==4,"one taser shot spends slot and starts two-game cooldown");
        for(int i=1;i<=20;i++)Invoke(d,"TickProjectiles",11.6+i*.05);
        Check(d.ForSeat(0).FiredWeapon==DuelWeapon.Taser&&d.ForSeat(3).Health==95&&d.ForSeat(3).Stunned,"actual taser impact deals five and stuns in QuickHands");
        Check(!d.Fire(0,yaw,pitch,false,d.ForSeat(0).Version,12.7,12.7),"spent taser cannot fire twice");Check(Switch(d,0,false,12.8)&&SameGun(gun,d.ForSeat(0)),"gun preserved after taser firing");Check(!Switch(d,0,true,12.9),"spent inventory cannot be rearmed");
        var fresh=Setup();Reserve(fresh,0,DuelItem.Taser);Reserve(fresh,1,DuelItem.Ink);Phase(fresh,ClockPhase.QuickHands,11);Switch(fresh,0,true,11.1);Switch(fresh,0,false,11.2);Switch(fresh,1,true,11.1);Switch(fresh,1,false,11.2);
        Invoke(fresh,"ClearTurnEffects");Check(fresh.ForSeat(0).TaserReadyIteration==4&&fresh.ForSeat(0).QuickSpent&&!fresh.ForSeat(1).Ink.Owned&&fresh.ForSeat(1).QuickSpent,"unused holstered items charged on round completion");
        Invoke(fresh,"ClearTurnEffects");Check(fresh.ForSeat(0).TaserReadyIteration==4,"cleanup is idempotent");
        for(int iter=2;iter<=4;iter++){Invoke(fresh,"ResetQuickItems");Phase(fresh,ClockPhase.ItemPreparation,20,iter);Check(fresh.UseItem(0,DuelItem.Taser,iter,20.1)==(iter==4),"cooldown availability at game "+iter);}
        foreach(var item in new[]{DuelItem.Taser,DuelItem.Ink})
        {
            var loser=Setup();Reserve(loser,0,item);Phase(loser,ClockPhase.QuickHands,11);Switch(loser,0,true,11.1);loser.Game.Shoot(1,0,DuelWeapon.Revolver,DuelHitPart.Head,true);Invoke(loser,"LoseLife",0,DuelDeathCause.Gunshot,11.2,1);
            Check(loser.ForSeat(0).QuickSpent&&!loser.ForSeat(0).QuickEquipped&&(item==DuelItem.Taser?loser.ForSeat(0).TaserReadyIteration==4:!loser.ForSeat(0).Ink.Owned),"unused held item charged on loss "+item);
            var winner=Setup();Reserve(winner,0,item);Phase(winner,ClockPhase.QuickHands,11);for(int seat=1;seat<4;seat++){winner.Game.Shoot(0,seat,DuelWeapon.Revolver,DuelHitPart.Head,true);Invoke(winner,"LoseLife",seat,DuelDeathCause.Gunshot,11.2,1);}Invoke(winner,"FinishQuick",11.4);Check(winner.Game.AliveMask==1&&winner.ForSeat(0).QuickSpent&&(item==DuelItem.Taser?winner.ForSeat(0).TaserReadyIteration==4:!winner.ForSeat(0).Ink.Owned),"unused inventory charged on actual survival win "+item);
        }
        var ink=Setup();Reserve(ink,0,DuelItem.Ink);Phase(ink,ClockPhase.QuickHands,11);Gun(ink,0);var inkGun=ink.ForSeat(0);Switch(ink,0,true,11.1);Check(ink.ForSeat(0).Ink.Armed&&ink.ForSeat(0).Ink.Owned,"ink equip does not consume");Switch(ink,0,false,11.2);Check(SameGun(inkGun,ink.ForSeat(0))&&ink.ForSeat(0).Ink.Owned,"ink can be holstered unused");Switch(ink,0,true,11.3);a=ink.ForSeat(0);Check(ink.ThrowInk(0,0,0,1,a.Life,a.Ink.Version,11.4,11.4)&&ink.ForSeat(0).QuickSpent&&!ink.ForSeat(0).Ink.Owned&&!ink.ForSeat(0).QuickEquipped,"throw consumes and returns to gun");
        var heal=Setup();heal.Game.Shoot(1,0,DuelWeapon.Revolver,DuelHitPart.Body,true);Check(heal.UseItem(0,DuelItem.Recovery,1,10.1)&&heal.ForSeat(0).Health==100&&heal.ForSeat(0).QuickSpent,"amp immediate heal uses the one slot");Check(!heal.UseItem(0,DuelItem.Taser,1,10.2)&&!heal.UseItem(0,DuelItem.Ink,1,10.2),"amp prevents a second selection");
        var cheat=Setup(true);Reserve(cheat,0,DuelItem.Taser);Reserve(cheat,1,DuelItem.Ink);Phase(cheat,ClockPhase.QuickHands,11);Invoke(cheat,"ClearTurnEffects");Check(cheat.ForSeat(0).TaserReadyIteration==0&&cheat.ForSeat(1).Ink.Owned,"unlimited item cheat preserved");
        var normal=Setup();Phase(normal,ClockPhase.ItemPreparation,10,1,PartyMiniGame.Countdown);Check(normal.UseItem(1,DuelItem.Taser,1,10.1)&&normal.ForSeat(1).TaserArmed&&!normal.UseItem(0,DuelItem.Taser,1,10.1),"ordinary firing turn non-shooter taser unchanged");
        var flow=Setup();Reserve(flow,0,DuelItem.Taser);flow.Tick(15.1);Check(flow.Clock.Phase==ClockPhase.QuickCountdown&&flow.ForSeat(0).QuickItem==2&&!flow.ForSeat(0).QuickEquipped,"reservation survives real countdown transition");flow.Tick(18.2);Check(flow.Clock.Phase==ClockPhase.QuickHands&&flow.ForSeat(0).QuickItem==2&&Switch(flow,0,true,18.3),"real QuickHands start keeps inventory and original gun ready for input");
        foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff,DuelWeapon.BAS}){var cycle=Setup();Reserve(cycle,0,DuelItem.Taser);Phase(cycle,ClockPhase.QuickHands,11);Gun(cycle,0,weapon);var mechanism=cycle.ForSeat(0);Switch(cycle,0,true,11.1);Switch(cycle,0,false,11.2);Check(SameGun(mechanism,cycle.ForSeat(0)),"all weapon mechanism backups "+weapon);}
        var ai=Setup(mode:DuelMode.AI);ai.Tick(10.8);Check(ai.ForSeat(1).QuickItem==2,"AI chooses quick inventory");Phase(ai,ClockPhase.QuickHands,11);for(int i=0;i<35;i++)ai.Tick(11+i*.05);bool botShot=false;for(int seat=1;seat<4;seat++)botShot|=ai.ForSeat(seat).FiredWeapon==DuelWeapon.Taser&&ai.ForSeat(seat).ShotsFired>0;Check(botShot,"AI equips and fires reserved taser");
        var snapshot=DuelSnapshot.From(d);using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(snapshot);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(snapshot)&&copy.Seat0.QuickSpent&&copy.Seat0.QuickItem==2,"inventory snapshot roundtrip");}}
    }
    void Keys(bool alt)
    {var state=alt?new KeyboardState(Key.LeftAlt):new KeyboardState();InputState.Change(keyboard,state,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(keyboard,state,InputUpdateType.Editor);
#endif
        keyboard.MakeCurrent();typeof(DuelPointer).GetMethod("Update",F).Invoke(manager.GetComponent<DuelPointer>(),null);}
    void Click(VisualElement target,bool alt=true)
    {var raw=new Event{type=EventType.MouseDown,button=0,clickCount=1,mousePosition=target.worldBound.center,modifiers=alt?EventModifiers.Alt:EventModifiers.None};using(var e=PointerDownEvent.GetPooled(raw))target.SendEvent(e);raw.type=EventType.MouseUp;using(var e=PointerUpEvent.GetPooled(raw))target.SendEvent(e);}
    IEnumerator Start()
    {yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();if(role=="model")Model();else if(role=="preview")yield return Preview();else yield return Peers();Finish("PASS quick items "+role+": "+checks+" checks");}
    IEnumerator Peers()
    {
        if(role=="client")while(!Seen("host","listening"))yield return null;manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18995");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==DuelLanRooms.Protocol,"current protocol");
        if(role=="host")
        {
            Mark("listening");while(D.Game.ConnectedMask!=3)yield return null;D.Join(3,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);D.Ready(1,Now);D.Ready(3,Now);Phase(D,ClockPhase.ItemPreparation,Now);Publish();Mark("prep");while(!Seen("client","reserved"))yield return null;
            Check(D.ForSeat(1).QuickItem==2&&D.ForSeat(1).Weapon==DuelWeapon.Revolver,"remote prep only reserves inventory");Gun(D,1);Phase(D,ClockPhase.QuickHands,Now);Publish();Mark("quick");while(!Seen("client","shot"))yield return null;
            Check(D.ForSeat(1).FiredWeapon==DuelWeapon.Taser&&D.ForSeat(1).TaserReadyIteration==4&&D.ForSeat(1).QuickSpent,"remote shot recorded as taser with cooldown");Mark("verified");while(!Seen("client","holstered"))yield return null;Check(D.ForSeat(1).LoadedMask==3&&D.ForSeat(1).Progress==1&&D.ForSeat(1).Weapon==DuelWeapon.Revolver,"remote holster preserves gun reload and ammo");
            Invoke(D,"ClearTurnEffects");Invoke(D,"ResetQuickItems");Phase(D,ClockPhase.ItemPreparation,Now,2);Publish();Mark("prep2");while(!Seen("client","reserved2"))yield return null;Gun(D,1);Phase(D,ClockPhase.QuickHands,Now,2);Publish();Mark("quick2");while(!Seen("client","ink-thrown"))yield return null;Check(!D.ForSeat(1).Ink.Owned&&D.ForSeat(1).QuickSpent&&!D.ForSeat(1).QuickEquipped,"remote ink throw consumes the reserved slot");Mark("complete");while(!Seen("client","finished"))yield return null;
        }
        else
        {
            while(!Seen("host","prep")||player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;keyboard=InputSystem.AddDevice<Keyboard>();Keys(true);yield return new WaitForSecondsRealtime(.15f);Click(Field<Button[]>(view,"itemButtons")[1]);while(player.State.Value.Seat1.QuickItem!=2)yield return null;Check(!player.State.Value.Seat1.TaserArmed,"selection is inventory, not immediate taser");Mark("reserved");
            while(!Seen("host","quick")||player.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;var hud=view.GetComponent<UIDocument>().rootVisualElement.Q<Button>(className:"local-weapon-panel");yield return new WaitForSecondsRealtime(.15f);Keys(false);Click(hud,false);yield return new WaitForSecondsRealtime(.15f);Check(!player.State.Value.Seat1.QuickEquipped,"HUD click requires Alt");
            Keys(true);Click(hud);while(!player.State.Value.Seat1.QuickEquipped)yield return null;yield return new WaitForSecondsRealtime(.15f);Check(player.State.Value.Seat1.Weapon==DuelWeapon.Taser&&Field<Image>(view,"localWeaponIcon").image==Field<Texture2D[]>(view,"itemIcons")[1],"HUD Alt click equips taser and changes icon");
            Click(hud);while(player.State.Value.Seat1.QuickEquipped)yield return null;Check(player.State.Value.Seat1.Progress==1&&player.State.Value.Seat1.Ammo==2,"HUD returns to loaded gun without using item");Click(hud);while(!player.State.Value.Seat1.QuickEquipped)yield return null;Keys(false);
            var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(mouse,new MouseState());while(!player.State.Value.Seat1.QuickSpent)yield return null;Check(player.State.Value.Seat1.ShotsFired==1&&player.State.Value.Seat1.FiredWeapon==DuelWeapon.Taser,"actual left mouse uses owner RPC to fire taser");Mark("shot");while(!Seen("host","verified"))yield return null;
            Keys(true);Click(hud);while(player.State.Value.Seat1.QuickEquipped)yield return null;Mark("holstered");while(!Seen("host","prep2")||player.State.Value.Clock.Iteration!=2||player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return new WaitForSecondsRealtime(.15f);Click(Field<Button[]>(view,"itemButtons")[2]);while(player.State.Value.Seat1.QuickItem!=3)yield return null;Mark("reserved2");
            while(!Seen("host","quick2")||player.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;yield return new WaitForSecondsRealtime(.15f);Click(hud);while(!player.State.Value.Seat1.Ink.Armed)yield return null;yield return new WaitForSecondsRealtime(.15f);Check(Field<Image>(view,"localWeaponIcon").image==Field<Texture2D[]>(view,"itemIcons")[2],"HUD switches to ink thumbnail");Keys(false);yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(mouse,new MouseState());while(!player.State.Value.Seat1.QuickSpent)yield return null;Check(!player.State.Value.Seat1.Ink.Owned&&!player.State.Value.Seat1.QuickEquipped,"left mouse consumes ink and returns to gun");Mark("ink-thrown");while(!Seen("host","complete"))yield return null;Mark("finished");
        }
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);var previous=RenderTexture.active;RenderTexture.active=view.GetComponent<UIDocument>().panelSettings.targetTexture;var ui=new Texture2D(1200,800,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,1200,800),0,0);ui.Apply();var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var scene=new Texture2D(1200,800,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,1200,800),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(ui);Destroy(scene);
    }
    IEnumerator Preview()
    {
        var doc=view.GetComponent<UIDocument>();var root=doc.rootVisualElement;var settings=doc.panelSettings;settings.scaleMode=PanelScaleMode.ConstantPixelSize;settings.scale=1;settings.targetTexture=new RenderTexture(1200,800,24);settings.targetTexture.Create();settings.clearColor=true;settings.colorClearValue=Color.clear;root.style.width=1200;root.style.height=800;
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","18994");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;keyboard=InputSystem.AddDevice<Keyboard>();Keys(true);D.Join(1,Now);D.Join(3,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);D.Ready(1,Now);D.Ready(3,Now);Phase(D,ClockPhase.ItemPreparation,Now);Publish();yield return new WaitForSecondsRealtime(.2f);
        Click(Field<Button[]>(view,"itemButtons")[1]);yield return new WaitForSecondsRealtime(.2f);Check(D.ForSeat(0).QuickItem==2,"prep taser selection");yield return Capture("Quick-Item-Preparation.png");Gun(D,0);Phase(D,ClockPhase.QuickHands,Now);Publish();yield return new WaitForSecondsRealtime(.2f);var hud=root.Q<Button>(className:"local-weapon-panel");Click(hud);yield return new WaitForSecondsRealtime(.2f);Check(D.ForSeat(0).Weapon==DuelWeapon.Taser,"HUD equips taser");yield return Capture("Quick-Taser-Equipped.png");
        var actors=Field<DuelActionState[]>(D,"actions");actors[3].Stunned=true;actors[3].StunnedAt=Now;Publish();yield return new WaitForSecondsRealtime(.2f);Check(Field<LineRenderer[,]>(manager.GetComponent<RouletteTable>(),"stunArcs")[3,0].enabled,"third-person electricity visible during QuickHands");yield return Capture("Quick-Taser-Effect.png");
        Click(hud);yield return new WaitForSecondsRealtime(.2f);Check(D.ForSeat(0).LoadedMask==3&&D.ForSeat(0).Progress==1,"HUD restores gun");
        Invoke(D,"ClearTurnEffects");Invoke(D,"ResetQuickItems");Phase(D,ClockPhase.ItemPreparation,Now,2);D.UseItem(0,DuelItem.Ink,2,Now);Phase(D,ClockPhase.QuickHands,Now,2);Publish();yield return new WaitForSecondsRealtime(.2f);Click(hud);yield return new WaitForSecondsRealtime(.2f);Check(D.ForSeat(0).Ink.Armed&&D.ForSeat(0).QuickEquipped,"HUD equips ink pouch");yield return Capture("Quick-Ink-Equipped.png");Click(hud);yield return new WaitForSecondsRealtime(.2f);Invoke(D,"ClearTurnEffects");Publish();yield return new WaitForSecondsRealtime(.2f);Check(!D.ForSeat(0).Ink.Owned,"unused ink consumed at end");
    }
}
