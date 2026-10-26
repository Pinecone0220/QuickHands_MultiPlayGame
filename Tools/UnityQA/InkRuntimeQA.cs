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

// QA-only: run in the isolated project, not production Assets.
public sealed class InkRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--ink-qa");if(i<0)return;
        var q=new GameObject("Ink QA").AddComponent<InkRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--ink-dir")+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+150;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
    }
    void Finish(string s,int code=0)
    {
        Debug.Log(s);File.WriteAllText(Path.Combine(path,role+"-done"),s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL ink "+role+": "+s,1);throw new Exception(s);}Debug.Log("Ink check: "+s);}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL ink timeout "+role,1);}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Keys(Keyboard keyboard,params Key[] keys)
    {
        var state=new KeyboardState(keys);InputState.Change(keyboard,state,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(keyboard,state,InputUpdateType.Editor);
#endif
        keyboard.MakeCurrent();typeof(DuelPointer).GetMethod("Update",F).Invoke(manager.GetComponent<DuelPointer>(),null);
    }
    void Click(VisualElement target,bool alt)
    {
        var raw=new Event{type=EventType.MouseDown,button=0,clickCount=1,mousePosition=target.worldBound.center,modifiers=alt?EventModifiers.Alt:EventModifiers.None};
        using(var e=PointerDownEvent.GetPooled(raw))target.SendEvent(e);raw.type=EventType.MouseUp;using(var e=PointerUpEvent.GetPooled(raw))target.SendEvent(e);
    }
    void Phase(DuelDirector d,ClockPhase phase,double now,int first=0,int iteration=1)
    {
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(d,"clock"),new ClockState{Phase=phase,MiniGame=phase==ClockPhase.QuickHands?PartyMiniGame.QuickHands:PartyMiniGame.Countdown,Iteration=iteration,ParticipantsMask=d.Game.AliveMask,First=first,PhaseAt=now,StartedAt=now+5});
    }
    DuelDirector Setup(int count=4,bool unlimited=false)
    {
        var d=new DuelDirector(DuelMode.PvP,new System.Random(53));for(int i=0;i<count;i++)d.Join(i,0);d.SetAutoSelectItems(true);if(unlimited)d.Game.SetUnlimitedItems(true);for(int i=0;i<count;i++)d.Ready(i,0);
        Phase(d,ClockPhase.QuickHands,10);return d;
    }
    bool InkTick(DuelDirector d,double now)=>(bool)typeof(DuelDirector).GetMethod("TickInk",F).Invoke(d,new object[]{now});
    void Target(int actor,int target,out float yaw,out float pitch)
    {
        var delta=DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(actor);float distance=Mathf.Sqrt(delta.X*delta.X+delta.Z*delta.Z),t=(distance-.65f)/DuelInk.Speed;
        DuelGunplay.Angles(delta,out yaw,out pitch);yaw-=Mathf.Atan2(.24f,distance)*Mathf.Rad2Deg;
        pitch=Mathf.Atan2(delta.Y+.1f-DuelInk.UpwardSpeed*t+.5f*DuelInk.Gravity*t*t,distance)*Mathf.Rad2Deg;
    }
    void Model()
    {
        Check(DuelLoadout.ItemCount==3&&DuelLoadout.Required==2&&DuelItemArt.Name(DuelItem.Ink)=="먹물팩","catalog and selection count");
        var lobby=new DuelDirector(DuelMode.PvP);lobby.Join(0,0);Check(lobby.ForSeat(0).Ink.InkedAt<0&&lobby.ForSeat(0).Ink.ThrownAt<0,"empty joined ink state");
        Check(lobby.SelectItem(0,DuelItem.Ink)&&lobby.SelectItem(0,DuelItem.Recovery)&&!lobby.SelectItem(0,DuelItem.Taser),"two different choices; third rejected");
        Check(!lobby.UseItem(0,DuelItem.Ink,0,0),"lobby cannot equip");
        for(int i=1;i<6;i++)Check(DuelLoadout.Count(DuelLoadout.AiSelection(i))==2,"AI two choices "+i);
        var d=Setup();Check(d.Game.Phase==DuelPhase.Playing&&d.ForSeat(3).Ink.Owned&&DuelLoadout.Count(d.ForSeat(0).SelectedItems)==3,"all-items cheat includes pouch and starts");
        Phase(d,ClockPhase.ItemPreparation,10);Check(d.UseItem(1,DuelItem.Ink,1,10.1)&&d.ForSeat(1).Ink.Armed&&d.ForSeat(1).Ink.Owned,"non-shooter equips without consuming in prep");
        var state=d.ForSeat(1);Check(!d.ThrowInk(1,60,0,1,state.Life,state.Ink.Version,10.2,10.2),"preparation cannot throw");
        Check(d.UseItem(1,DuelItem.Ink,1,10.3)&&!d.ForSeat(1).Ink.Armed,"selection toggles back to gun");
        Check(!d.UseItem(0,DuelItem.Ink,0,10.4)&&!d.UseItem(0,DuelItem.Ink,1,15),"stale iteration and expired prep rejected");
        foreach(var phase in new[]{ClockPhase.Counting,ClockPhase.Ranking,ClockPhase.Selecting,ClockPhase.QuickCountdown})
        {Phase(d,phase,10);Check(!d.UseItem(0,DuelItem.Ink,1,10.2),"disallowed phase "+phase);}
        Phase(d,ClockPhase.Shooting,10);Check(d.UseItem(1,DuelItem.Ink,1,10.2),"non-shooter may equip during shooting");
        Check(!d.CanUseInk(-1,10.2)&&!d.CanUseInk(6,10.2)&&!d.CanUseInk(1,20),"seat bounds and firing deadline");
        d=Setup();var actions=Field<DuelActionState[]>(d,"actions");actions[0].Action=DuelAction.Load;actions[0].Progress=1;actions[0].LoadedMask=3;actions[0].SpentMask=4;
        var before=d.ForSeat(0);Check(d.UseItem(0,DuelItem.Ink,1,10.1),"QuickHands equips during partial reload");
        Check(!d.Input(0,before.Digit(before.Progress),before.Version,10.12),"own gun input paused while pouch held");
        var other=d.ForSeat(1);Check(d.Input(1,other.Digit(other.Progress),other.Version,10.12),"other player's action input continues");
        Target(0,3,out float y,out float p);int v=d.ForSeat(0).Ink.Version,life=before.Life;
        Check(!d.ThrowInk(0,y,p,0,life,v,10.2,10.2)&&!d.ThrowInk(0,y,p,1,life+1,v,10.2,10.2)&&!d.ThrowInk(0,y,p,1,life,v-1,10.2,10.2),"stale iteration/life/equip rejected");
        Check(!d.ThrowInk(0,float.NaN,p,1,life,v,10.2,10.2)&&!d.ThrowInk(0,y,p,1,life,v,double.NaN,10.2)&&!d.ThrowInk(0,180,p,1,life,v,10.2,10.2)&&!d.ThrowInk(0,y,p,1,life,v,9,10.2),"invalid aim/timestamp rejected");
        Check(d.ThrowInk(0,y,p,1,life,v,10.2,10.2),"actual throw accepted");var thrown=d.ForSeat(0);
        Check(!thrown.Ink.Owned&&!thrown.Ink.Armed&&thrown.Ink.Serial==1,"consumed exactly on release request");
        Check(!d.ThrowInk(0,y,p,1,life,v,10.2,10.2),"replayed throw rejected");
        Check(thrown.Action==before.Action&&thrown.Progress==before.Progress&&thrown.Code==before.Code&&thrown.Version==before.Version&&thrown.LoadedMask==before.LoadedMask&&thrown.SpentMask==before.SpentMask,"gun action/code/progress/version/ammo preserved");
        Check(thrown.ShotsFired==0&&thrown.DamageDealt==0&&thrown.Kills==0,"not recorded as firearm shot or damage");
        Check(DuelInk.Position(thrown.Ink,10.3).Equals(thrown.Ink.Origin),"wind-up before release has no flight");
        var at=DuelInk.Position(thrown.Ink,10.86);var line=thrown.Ink.Origin+thrown.Ink.Direction*7;
        Check(Math.Abs(at.Y-line.Y-(-.2))<.001,"analytic slight parabolic drop");
        for(int i=1;i<=70&&d.ForSeat(0).Ink.ImpactAt<0;i++)InkTick(d,10.2+i*.025);
        Check(d.ForSeat(0).Ink.Target==3&&d.ForSeat(3).Ink.InkedAt>=10.2,"swept server trajectory hits actual opponent");
        Check(d.Game.HealthForSeat(3)==100&&d.ForSeat(3).LivesRemaining==3&&d.ForSeat(0).DamageDealt==0,"impact causes no HP/life/stat damage");
        Check(d.ForSeat(1).Ink.InkedAt<0&&d.ForSeat(2).Ink.InkedAt<0,"only contacted player is inked");
        Check(d.Input(0,before.Digit(before.Progress),before.Version,11),"gun input resumes after throw animation");
        Check(DuelInk.Opacity(-.01)==0&&DuelInk.Opacity(0)==1&&DuelInk.Opacity(5)==1&&Math.Abs(DuelInk.Opacity(5.25)-.5)<.0001&&DuelInk.Opacity(5.5)==0,"five second hold and half second fade");
        var snapshot=DuelSnapshot.From(d);using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(snapshot);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(snapshot)&&copy.Seat3.Ink.Equals(snapshot.Seat3.Ink),"all six ink states round-trip and equality");}}
        var u=Setup(unlimited:true);u.UseItem(0,DuelItem.Ink,1,10.1);Check(u.ThrowInk(0,y,p,1,u.ForSeat(0).Life,u.ForSeat(0).Ink.Version,10.2,10.2)&&u.ForSeat(0).Ink.Owned,"unlimited cheat retains consumable");
        Check(!u.UseItem(0,DuelItem.Ink,1,10.3),"one flight per player");
        for(int i=1;i<=70&&u.ForSeat(0).Ink.ImpactAt<0;i++)InkTick(u,10.2+i*.025);
        Check(u.UseItem(0,DuelItem.Ink,1,11)&&u.ThrowInk(0,y,p,1,u.ForSeat(0).Life,u.ForSeat(0).Ink.Version,11.1,11.1),"unlimited may throw again after flight");
        var old=Field<DuelActionState[]>(u,"actions");old[0].Life++;InkTick(u,11.8);Check(u.ForSeat(0).Ink.Serial==2&&u.ForSeat(0).Ink.ImpactAt<0,"old-life flight cancelled");
        var miss=Setup();miss.UseItem(0,DuelItem.Ink,1,10.1);miss.ThrowInk(0,25,30,1,miss.ForSeat(0).Life,miss.ForSeat(0).Ink.Version,10.2,10.2);InkTick(miss,13);
        Check(miss.ForSeat(0).Ink.Target<0&&miss.ForSeat(1).Ink.InkedAt<0&&miss.ForSeat(3).Ink.InkedAt<0,"off-target arc does not auto-hit");
        var wall=Setup();var props=wall.Tabletop;wall.UseItem(0,DuelItem.Ink,1,10.1);wall.ThrowInk(0,0,-50,1,wall.ForSeat(0).Life,wall.ForSeat(0).Ink.Version,10.2,10.2);InkTick(wall,13);
        Check(wall.ForSeat(0).Ink.Target<0&&wall.Tabletop.Equals(props),"table/props block pouch without breaking or damage");
        typeof(DuelDirector).GetMethod("ClearTurnEffects",F).Invoke(u,null);Check(!u.ForSeat(0).Ink.Armed,"turn end disarms without consuming inventory");
        for(int actor=0;actor<6;actor++)
        {
            var seats=Setup(6);Target(actor,(actor+3)%6,out float sy,out float sp);seats.UseItem(actor,DuelItem.Ink,1,10.1);seats.ThrowInk(actor,sy,sp,1,seats.ForSeat(actor).Life,seats.ForSeat(actor).Ink.Version,10.2,10.2);InkTick(seats,11.2);
            Check(seats.ForSeat(actor).Ink.Target==(actor+3)%6,"six-seat arc geometry "+actor);
        }
        var shoot=Setup();Phase(shoot,ClockPhase.Shooting,10);var sa=Field<DuelActionState[]>(shoot,"actions");sa[0].Action=DuelAction.Aim;sa[0].LoadedMask=1;sa[0].ShootingStartedAt=10;
        shoot.UseItem(0,DuelItem.Ink,1,10.1);shoot.ThrowInk(0,y,p,1,sa[0].Life,sa[0].Ink.Version,10.2,10.2);InkTick(shoot,11.2);
        Check(shoot.Fire(0,0,60,false,sa[0].Version,11.3,11.3)&&shoot.ForSeat(0).ShotsFired==1&&shoot.ForSeat(0).Ammo==0,"ink does not consume winner's firearm shot");
        var dead=Setup();var da=Field<DuelActionState[]>(dead,"actions");da[0].Ink.InkedAt=10;dead.UseItem(0,DuelItem.Ink,1,10.1);dead.Game.Shoot(1,0,DuelWeapon.Revolver,DuelHitPart.Head,true);typeof(DuelDirector).GetMethod("LoseLife",F).Invoke(dead,new object[]{0,DuelDeathCause.Gunshot,10.2,1});
        Check(!dead.ForSeat(0).Ink.Armed&&dead.ForSeat(0).Ink.InkedAt<0&&dead.ForSeat(0).Ink.Owned&&!dead.CanUseInk(0,10.3),"death clears effect/equip without consuming unthrown pouch");
        typeof(DuelDirector).GetMethod("RestoreParty",F).Invoke(dead,new object[]{11.5,false});Check(dead.ForSeat(0).Life==2&&dead.ForSeat(0).Ink.InkedAt<0&&dead.ForSeat(0).Ink.Owned,"new life keeps unused inventory and clears old ink");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role=="model")Model();else if(role=="preview")yield return Preview();else yield return Peers();Finish("PASS ink "+role+": "+checks+" checks");
    }
    IEnumerator Peers()
    {
        if(role!="host")while(!Seen("host","listening"))yield return null;
        if(role=="observer")while(!Seen("client","joined"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","18997");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==51,"current protocol");
        if(role=="host")
        {
            Mark("listening");while(D.Game.ConnectedMask!=7)yield return null;D.Join(3,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);D.Ready(1,Now);D.Ready(2,Now);D.Ready(3,Now);Phase(D,ClockPhase.QuickHands,Now);Publish();Mark("setup");
            while(!Seen("client","thrown"))yield return null;
            while(D.ForSeat(0).Ink.InkedAt<0)yield return null;
            Check(!D.ForSeat(1).Ink.Owned&&D.ForSeat(1).Ink.Serial==1&&D.ForSeat(1).Ink.Target==0,"remote owner throw consumed and server target hit");
            Check(D.ForSeat(0).Health==100&&D.ForSeat(1).ShotsFired==0,"remote throw does not damage or consume firearm opportunity");Mark("inked");
            while(!Seen("client","effect"))yield return null;var overlay=Field<VisualElement>(view,"inkOverlay");Check(overlay.resolvedStyle.display!=DisplayStyle.None,"host victim alone receives central ink overlay");
            while(!Seen("observer","observed"))yield return null;
            while(Now-D.ForSeat(0).Ink.InkedAt<5.65)yield return null;
            Check(overlay.resolvedStyle.display==DisplayStyle.None,"victim overlay fades and hides on schedule");Mark("verified");while(!Seen("client","finished"))yield return null;
        }
        else if(role=="client")
        {
            Mark("joined");while(!Seen("host","setup")||player.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;int own=player.Seat.Value;Check(own==1,"remote owner seat");
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();Keys(keyboard);
            var item=Field<Button[]>(view,"itemButtons")[2];yield return new WaitForSecondsRealtime(.15f);Click(item,false);yield return new WaitForSecondsRealtime(.15f);
            Check(!player.State.Value.ForSeat(own).Ink.Armed,"live item ignores pointer click without Alt");
            Keys(keyboard,Key.LeftAlt);Click(item,true);while(!player.State.Value.ForSeat(own).Ink.Armed)yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            Check(item.resolvedStyle.display!=DisplayStyle.None&&item.ClassListContains("item-used"),"QuickHands live item button visible and equipped yellow");
            Check(item.resolvedStyle.borderTopWidth==2&&item.enabledInHierarchy,"equipped yellow border remains cancellable");
            Check(!(bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null),"held pouch blocks local gun-input prediction");
            Keys(keyboard);Target(1,0,out float y,out float p);var fps=manager.GetComponent<DuelFirstPerson>();typeof(DuelFirstPerson).GetProperty("Yaw").SetValue(fps,y);typeof(DuelFirstPerson).GetProperty("Pitch").SetValue(fps,p);
            yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(mouse,new MouseState());
            while(player.State.Value.ForSeat(own).Ink.Serial<1)yield return null;Check(!fps.Ads,"left mouse throws equipped pouch without ADS");Mark("thrown");
            while(player.State.Value.Seat0.Ink.InkedAt<0)yield return null;
            Check(player.State.Value.ForSeat(own).Ink.InkedAt<0&&Field<VisualElement>(view,"inkOverlay").resolvedStyle.display==DisplayStyle.None,"thrower sees target ink but has no own overlay");
            yield return null;Check(item.resolvedStyle.display==DisplayStyle.None,"consumed pouch disappears from inventory");Mark("effect");
            while(Now-player.State.Value.ForSeat(own).Ink.ThrownAt<.75)yield return null;
            Check((bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null),"local gun input resumes after pouch animation");
            while(!Seen("host","verified"))yield return null;Mark("finished");
        }
        else
        {
            while(player.State.Value.Seat0.Ink.InkedAt<0)yield return null;
            Check(player.State.Value.Seat1.Ink.Serial==1&&player.State.Value.Seat0.Ink.InkedAt>0,"observer receives persistent throw and target ink timestamps");
            Check(Field<VisualElement>(view,"inkOverlay").resolvedStyle.display==DisplayStyle.None,"observer never receives victim overlay");Mark("observed");while(!Seen("host","verified"))yield return null;
        }
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.12f);var settings=view.GetComponent<UIDocument>().panelSettings;var previous=RenderTexture.active;RenderTexture.active=settings.targetTexture;
        var ui=new Texture2D(1200,800,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,1200,800),0,0);ui.Apply();var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var scene=new Texture2D(1200,800,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,1200,800),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(ui);Destroy(scene);
    }
    IEnumerator Preview()
    {
        var root=view.GetComponent<UIDocument>().rootVisualElement;var settings=view.GetComponent<UIDocument>().panelSettings;settings.scaleMode=PanelScaleMode.ConstantPixelSize;settings.scale=1;settings.targetTexture=new RenderTexture(1200,800,24);settings.targetTexture.Create();settings.clearColor=true;settings.colorClearValue=Color.clear;root.style.width=1200;root.style.height=800;
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","18998");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        yield return new WaitForSecondsRealtime(.3f);Check(Field<Button[]>(view,"loadoutButtons").Length==3,"three distinct loadout icons");yield return Capture("Ink-Loadout.png");
        D.Join(1,Now);D.Join(3,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);D.Ready(1,Now);D.Ready(3,Now);Phase(D,ClockPhase.QuickHands,Now);D.UseItem(0,DuelItem.Ink,1,Now);Publish();
        yield return new WaitForSecondsRealtime(.2f);Check(Field<Button[]>(view,"itemButtons")[2].ClassListContains("item-used"),"held pouch visibly selected");
        var visuals=manager.GetComponent<DuelInkVisuals>();Check(Field<Transform[]>(visuals,"held")[0].gameObject.activeInHierarchy,"local held pouch renderer");yield return Capture("Ink-Held.png");
        D.UseItem(0,DuelItem.Ink,1,Now);D.UseItem(3,DuelItem.Ink,1,Now);Target(3,0,out float y,out float p);Check(D.ThrowInk(3,y,p,1,D.ForSeat(3).Life,D.ForSeat(3).Ink.Version,Now,Now),"preview remote pouch thrown");Publish();
        yield return new WaitForSecondsRealtime(.32f);Check(Field<Transform[]>(visuals,"flight")[3].gameObject.activeInHierarchy,"world parabolic pouch visible");yield return Capture("Ink-Flight.png");
        while(D.ForSeat(0).Ink.InkedAt<0)yield return null;
        yield return new WaitForSecondsRealtime(.15f);Check(Field<VisualElement>(view,"inkOverlay").resolvedStyle.display!=DisplayStyle.None,"local central ink visible");yield return Capture("Ink-Impact.png");
        var a=Field<DuelActionState[]>(D,"actions");a[1].Ink.InkedAt=Now;Publish();yield return new WaitForSecondsRealtime(.2f);Check(Field<Transform[]>(visuals,"coats")[1].gameObject.activeInHierarchy,"third person facial ink visible");
        while(Now-D.ForSeat(0).Ink.InkedAt<5.2)yield return null;Check(Field<VisualElement>(view,"inkOverlay").resolvedStyle.opacity<1,"fade starts after five seconds");yield return Capture("Ink-Fading.png");
        yield return new WaitForSecondsRealtime(.5f);Check(Field<VisualElement>(view,"inkOverlay").resolvedStyle.display==DisplayStyle.None,"ink fully clears");yield return Capture("Ink-Cleared.png");
    }
}
