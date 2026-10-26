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

public sealed class LoadoutRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    DuelDirector Director=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--loadout-qa");if(i<0)return;
        var qa=new GameObject("Loadout QA").AddComponent<LoadoutRuntimeQA>();qa.role=args[i+1];
        i=Array.IndexOf(args,"--loadout-dir");qa.path=args[i+1];Directory.CreateDirectory(qa.path);
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
    void Check(bool value,string reason){checks++;if(!value){Finish("FAIL loadout "+role+": "+reason,1);throw new Exception(reason);}}
    void Choose(DuelDirector d,int seat){Check(d.SelectItem(seat,DuelItem.Recovery),"choose recovery");Check(d.SelectItem(seat,DuelItem.Taser),"choose distinct taser");}
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+150;yield return new WaitForSecondsRealtime(1);
        if(role=="model")Model();else yield return Peers();
        Finish("PASS loadout "+role+": "+checks+" checks");
    }
    void Model()
    {
        var rules=new RouletteRules(DuelMode.PvP);for(int i=0;i<3;i++)rules.Join(i);
        rules.SetReady(0);rules.SetReady(1);
        Check(rules.CancelReady(0)&&rules.ReadyMask==2,"cancellation preserves other players' ready bits");
        Check(!rules.CancelReady(0)&&!rules.CancelReady(-1)&&!rules.CancelReady(6)&&!rules.CancelReady(4),"invalid or non-ready cancellation rejected");
        Check(DuelLoadout.Complete(DuelLoadout.All)&&!DuelLoadout.Complete(0)&&!DuelLoadout.Complete(7),"exactly two valid distinct items");
        var d=new DuelDirector(DuelMode.PvP,new System.Random(14));d.Join(0,0);d.Join(1,0);
        Check(!d.Ready(0,0)&&d.Game.ReadyMask==0,"empty selection cannot ready");
        Check(!d.SelectItem(-1,DuelItem.Taser)&&!d.SelectItem(6,DuelItem.Taser)&&!d.SelectItem(2,DuelItem.Taser)&&!d.SelectItem(0,(DuelItem)255),"bad seat/disconnected/invalid item rejected");
        Check(d.SelectItem(0,DuelItem.Recovery)&&!d.Ready(0,0),"one item cannot ready");
        Check(d.SelectItem(0,DuelItem.Recovery)&&d.ForSeat(0).SelectedItems==0,"same item toggles off without duplication");
        Choose(d,0);Check(!d.ForSeat(0).HasRecovery&&!d.ForSeat(0).HasTaser,"selection is not inventory before party");
        Check(d.Ready(0,0)&&!d.SelectItem(0,DuelItem.Taser),"ready freezes selected items");
        Check(d.Ready(0,0)&&d.Game.ReadyMask==0&&d.Stage==DuelStage.Idle,"second press cancels ready without starting");
        Check(d.SelectItem(0,DuelItem.Recovery)&&!d.Ready(0,0),"cancel restores initial item editing and completeness requirement");
        Check(d.SelectItem(0,DuelItem.Recovery)&&d.SelectWeapon(0,DuelWeapon.SawedOff,0)&&d.SelectWeapon(0,DuelWeapon.Revolver,0)&&d.Ready(0,0),"cancel permits weapon editing and ready again");
        d.Game.SetManualMiniGame(true);d.Game.SetUnlimitedItems(true);int ready=d.Game.ReadyMask;d.ResetDeveloperSettings(0);
        Check(d.Game.ReadyMask==ready&&d.ForSeat(0).SelectedItems==DuelLoadout.All,"developer reset preserves selection and ready");
        d.Game.SetManualMiniGame(true);Choose(d,1);Check(d.Ready(1,0)&&d.Stage==DuelStage.Countdown,"all selected and ready starts");
        for(int i=0;i<2;i++)Check(d.ForSeat(i).HasRecovery&&d.ForSeat(i).HasTaser&&d.ForSeat(i).LoadoutLocked,"first start grants and locks each human selection");
        Check(!d.SelectItem(1,DuelItem.Taser),"selection locked during party");
        Check(!d.Ready(0,0)&&!d.Game.CancelReady(0),"countdown cannot cancel a party already started");
        d.Tick(2.6);Check(d.ChooseMiniGame(PartyMiniGame.Countdown,d.Clock.Iteration,2.6),"manual minigame starts");
        double stop=d.Clock.StartedAt+d.Clock.Target/100.0;int iteration=d.Clock.Iteration;
        Check(d.StopClock(0,iteration,stop,stop)&&d.StopClock(1,iteration,stop+.1,stop+.1),"countdown ranking");
        double itemAt=d.Clock.PhaseAt+DuelClockContest.MiniResultsDuration+.01;d.Tick(itemAt);
        Check(d.Clock.Phase==ClockPhase.ItemPreparation,"rank to item turn");
        d.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Body);
        Check(d.UseItem(1,DuelItem.Recovery,iteration,itemAt+.01)&&d.Game.HealthForSeat(1)==100&&!d.ForSeat(1).HasRecovery,"selected ampoule heals and consumes");
        Check(d.UseItem(1,DuelItem.Taser,iteration,itemAt+.02)&&d.ForSeat(1).TaserReadyIteration==iteration+3,"selected taser blocks the next two minigames");
        d.Tick(d.Clock.StartedAt+.01);double end=d.Clock.PhaseAt+d.Game.ShotTimeLimit+.01;d.Tick(end);
        Check(d.Clock.Phase==ClockPhase.Returning&&!d.ForSeat(1).TaserArmed,"shot expiry removes turn effects");
        d.Tick(end+DuelClockContest.ReturnDuration+.01);
        Check(d.Clock.Phase==ClockPhase.Selecting&&!d.ForSeat(1).HasRecovery&&d.ForSeat(1).HasTaser&&d.ForSeat(1).TaserReadyIteration==iteration+3,"next minigame does not reward or refill inventory");
        Check(d.ForSeat(1).SelectedItems==DuelLoadout.All,"consumption does not change next-party selection");
        d.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.AfterShot,end+4});d.Tick(end+6);
        Check(d.Stage==DuelStage.Idle&&!d.SelectItem(0,DuelItem.Recovery)&&!d.SelectItem(1,DuelItem.Taser),"final result cannot reselect first-game loadout");
        Check(d.Ready(0,end+6)&&d.Ready(1,end+6),"final rematch uses retained selections without selecting again");
        Check(d.ForSeat(1).HasRecovery&&d.ForSeat(1).TaserReadyIteration==0,"new party grants selected items and resets cooldown");
        var lobby=new DuelDirector(DuelMode.PvP);lobby.Join(0,0);lobby.Join(1,0);Choose(lobby,1);lobby.Disconnect(1,1);lobby.Join(1,2);
        Check(lobby.ForSeat(1).SelectedItems==0&&!lobby.ForSeat(1).LoadoutLocked,"reused seat gets a fresh unlocked selection");
        var ai=new DuelDirector(DuelMode.AI);ai.Join(0,0);for(int i=1;i<6;i++)ai.Join(i,0);Choose(ai,0);ai.Ready(0,0);
        for(int i=0;i<6;i++)Check(ai.ForSeat(i).SelectedItems==DuelLoadout.All&&ai.ForSeat(i).HasRecovery&&ai.ForSeat(i).HasTaser,"AI party all six loadouts granted");
        var s=DuelSnapshot.From(ai);s.Seat5.SelectedItems=1;
        using(var writer=new FastBufferWriter(8192,Allocator.Temp)){writer.WriteNetworkSerializable(s);using(var reader=new FastBufferReader(writer,Allocator.Temp)){reader.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(s)&&copy.Seat5.SelectedItems==1,"snapshot roundtrip preserves all loadouts");}}
        var change=s;change.Seat5.SelectedItems=2;Check(!s.Equals(change),"selection-only changes publish");
        change=s;change.Seat5.LoadoutLocked=false;Check(!s.Equals(change),"first-start lock changes publish");
    }
    IEnumerator Peers()
    {
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role!="host")while(!Seen("host",role=="late"?"initial":"listening"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17954");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        int seat=player.Seat.Value;Check(player.NetworkManager.NetworkConfig.ProtocolVersion==43,"matching loadout protocol");
        yield return null;yield return null;
        CheckFirstPerson("lobby");
        var buttons=Field<Button[]>(view,"loadoutButtons");var ready=Field<Button>(view,"rematch");
        Check(!ready.enabledSelf&&!player.State.Value.ForSeat(seat).HasRecovery&&!player.State.Value.ForSeat(seat).LoadoutLocked&&Field<VisualElement>(view,"loadoutChoice").style.display.value==DisplayStyle.Flex,"initial lobby exposes empty manual selection only");
        player.Rematch();yield return new WaitForSecondsRealtime(.15f);Check(!RouletteRules.Has(player.State.Value.ReadyMask,seat),"server rejects bypassing item selection");
        player.SelectLoadoutItem((DuelItem)255);player.SelectLoadoutItem(DuelItem.Recovery);
        while(player.State.Value.ForSeat(seat).SelectedItems!=1)yield return null;yield return null;
        Check(!ready.enabledSelf&&buttons[0].ClassListContains("selected")&&!buttons[1].ClassListContains("selected"),"first selected border and incomplete ready");
        player.SelectLoadoutItem(DuelItem.Taser);while(player.State.Value.ForSeat(seat).SelectedItems!=DuelLoadout.All)yield return null;yield return null;
        Check(ready.enabledSelf&&buttons[0].ClassListContains("selected")&&buttons[1].ClassListContains("selected"),"two selected borders unlock ready");
        if(role=="host")
        {
            manager.RequestLoadoutItem(ulong.MaxValue,DuelItem.Recovery,player.State.Value.Round);
            manager.RequestLoadoutItem(player.OwnerClientId,DuelItem.Recovery,player.State.Value.Round-1);
            Check(Director.ForSeat(seat).SelectedItems==DuelLoadout.All,"unregistered and stale-round requests rejected");
            Mark("listening");
        }
        if(role=="client")Mark("selected");
        if(role=="host"){while(!Seen("client","selected"))yield return null;Mark("initial");}
        while(player.State.Value.ConnectedMask!=7)yield return null;
        while(player.State.Value.Seat0.SelectedItems!=3||player.State.Value.Seat1.SelectedItems!=3||player.State.Value.Seat2.SelectedItems!=3)yield return null;
        Check(true,"all three selections including earlier selections reach late joiner");
        if(role=="host")
        {
            player.Rematch();while(!RouletteRules.Has(player.State.Value.ReadyMask,seat))yield return null;yield return null;
            Check(ready.enabledSelf&&ready.text=="준비 취소","host ready button stays clickable as cancel");
            manager.RequestRematch(ulong.MaxValue,player.State.Value.Round);manager.RequestRematch(player.OwnerClientId,player.State.Value.Round-1);
            Check(Director.Game.ReadyMask==1,"invalid and stale ready requests cannot cancel host");Mark("cancel-host-ready");
            while(!Seen("client","cancel-tested")||!Seen("late","saw-ready"))yield return null;
            player.Rematch();while(RouletteRules.Has(player.State.Value.ReadyMask,seat))yield return null;yield return null;
            Check(player.State.Value.ReadyMask==0&&ready.enabledSelf&&ready.text=="준비 완료","host cancels back to waiting");Mark("cancel-tested");
        }
        else
        {
            while(!Seen("host","cancel-host-ready")||!RouletteRules.Has(player.State.Value.ReadyMask,0))yield return null;
            if(role=="client")
            {
                player.Rematch();while(!RouletteRules.Has(player.State.Value.ReadyMask,seat))yield return null;yield return null;
                Check(ready.enabledSelf&&ready.text=="준비 취소","remote ready button stays clickable as cancel");
                player.Rematch();while(RouletteRules.Has(player.State.Value.ReadyMask,seat))yield return null;yield return null;
                Check(player.State.Value.ReadyMask==1&&Field<Button[]>(view,"weaponButtons")[0].enabledSelf&&buttons[0].enabledSelf,"remote cancel preserves host and unlocks choices");
                player.SelectLoadoutItem(DuelItem.Recovery);while(player.State.Value.ForSeat(seat).SelectedItems!=2)yield return null;yield return null;
                Check(!ready.enabledSelf,"editing items after cancel still requires two distinct choices");
                player.SelectLoadoutItem(DuelItem.Recovery);while(player.State.Value.ForSeat(seat).SelectedItems!=3)yield return null;Mark("cancel-tested");
            }
            else Mark("saw-ready");
            while(!Seen("host","cancel-tested"))yield return null;
        }
        if(role=="host"){yield return Capture("Lobby");manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);}
        while(!player.State.Value.ManualMiniGame)yield return null;
        player.Rematch();while(!RouletteRules.Has(player.State.Value.ReadyMask,seat)&&player.State.Value.Phase!=DuelPhase.Playing)yield return null;
        while(player.State.Value.Phase!=DuelPhase.Playing)yield return null;yield return null;yield return null;
        CheckFirstPerson("countdown");
        player.SelectLoadoutItem(DuelItem.Taser);yield return new WaitForSecondsRealtime(.15f);
        Check(player.State.Value.ForSeat(seat).SelectedItems==3,"owner cannot mutate ready loadout");
        while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        for(int i=0;i<3;i++)Check(player.State.Value.ForSeat(i).HasRecovery&&player.State.Value.ForSeat(i).HasTaser&&player.State.Value.ForSeat(i).LoadoutLocked,"everyone receives and locks selected inventory");
        Check(Field<VisualElement>(view,"loadoutChoice").style.display.value==DisplayStyle.None,"no selection in minigame flow");
        Mark("started");
        if(role=="host")
        {
            while(!Seen("client","started")||!Seen("late","started"))yield return null;
            var actions=Field<DuelActionState[]>(Director,"actions");actions[1].DiedAt=Now;actions[1].LivesRemaining=0;
            Director.Game.Eliminate(2);
            typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
        }
        while(player.State.Value.Seat1.LivesRemaining!=0)yield return null;
        Check(player.State.Value.Phase==DuelPhase.Playing&&player.State.Value.AliveMask==5,"party continues after one permanent elimination");
        if(role=="client")
        {
            yield return new WaitForSecondsRealtime(2.5f);
            yield return CheckFixedCamera("permanently eliminated spectator");
            Mark("spectator-fixed");
        }
        else while(!Seen("client","spectator-fixed"))yield return null;
        if(role=="host")
        {
            while(!Seen("client","started")||!Seen("late","started"))yield return null;
            var actions=Field<DuelActionState[]>(Director,"actions");
            for(int i=0;i<3;i++){actions[i].HitAt=Now;if(i>0){actions[i].DiedAt=Now;actions[i].LivesRemaining=0;}}
            Director.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(Director,new object[]{DuelStage.AfterShot,Now});
            typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
        }
        while(player.State.Value.Phase!=DuelPhase.Finished)yield return null;yield return null;yield return null;
        yield return CheckFixedCamera("final results");
        while(player.State.Value.Stage!=DuelStage.Idle)yield return null;yield return null;yield return null;
        Check(ready.enabledSelf&&!buttons[0].enabledSelf&&!buttons[1].enabledSelf&&Field<VisualElement>(view,"loadoutChoice").style.display.value==DisplayStyle.None,"final result hides selection and permits ready directly");
        if(role=="host")
        {
            while(Field<VisualElement>(view,"resultsLayer").style.display.value!=DisplayStyle.Flex)yield return null;
            yield return Capture("Results");
        }
        player.SelectLoadoutItem(DuelItem.Recovery);yield return new WaitForSecondsRealtime(.2f);
        Check(player.State.Value.ForSeat(seat).SelectedItems==3&&ready.enabledSelf,"server rejects reselect request in final results");
        if(role=="client")
        {
            player.Rematch();while(!RouletteRules.Has(player.State.Value.ReadyMask,seat))yield return null;yield return null;
            Check(ready.enabledSelf&&ready.text=="준비 취소","result ready also remains cancellable");
            player.Rematch();while(RouletteRules.Has(player.State.Value.ReadyMask,seat))yield return null;yield return null;
            Check(ready.text=="준비 완료"&&Field<Button[]>(view,"weaponButtons")[0].enabledSelf&&player.State.Value.ForSeat(seat).LoadoutLocked,"result cancel unlocks weapon choice while retaining first-game item lock");
            Mark("result-cancelled");
        }
        else while(!Seen("client","result-cancelled"))yield return null;
        player.Rematch();
        while(player.State.Value.Round!=2)yield return null;
        yield return null;yield return null;CheckFirstPerson("rematch countdown");
        Check(player.State.Value.ForSeat(seat).HasRecovery&&player.State.Value.ForSeat(seat).HasTaser,"each owner receives new-party loadout again");
        Mark("verified");while(!Seen("host","verified")||!Seen("client","verified")||!Seen("late","verified"))yield return null;
    }
    IEnumerator CheckFixedCamera(string phase)
    {
        var fps=manager.GetComponent<DuelFirstPerson>();int seat=player.Seat.Value;var eye=DuelGunplay.Eye(seat);
        Vector3 position=Camera.main.transform.position;Quaternion rotation=Camera.main.transform.rotation;
        float yaw=fps.Yaw,pitch=fps.Pitch;
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
        var mouse=new UnityEngine.InputSystem.LowLevel.MouseState{position=new Vector2(100,500),delta=new Vector2(120,35)};
        InputSystem.QueueStateEvent(Mouse.current,mouse.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));yield return null;yield return null;
        InputSystem.QueueStateEvent(Mouse.current,new UnityEngine.InputSystem.LowLevel.MouseState{position=new Vector2(100,500)});yield return null;yield return null;
        Check(fps.Yaw==yaw&&fps.Pitch==pitch&&!fps.Ads&&!fps.Active,phase+" ignores mouse look and ADS");
        Check(Vector3.Distance(position,Camera.main.transform.position)<.001f&&Quaternion.Angle(rotation,Camera.main.transform.rotation)<.01f,phase+" camera stays fixed despite death/hit and mouse movement");
        Check(Vector3.Distance(position,new Vector3(eye.X,eye.Y,eye.Z))>.5f&&Mathf.Abs(Camera.main.fieldOfView-57)<.01f,phase+" uses third-person camera");
        Check(UnityEngine.Cursor.lockState==CursorLockMode.None,phase+" retains UI cursor");
        var puppets=Field<Array>(manager.Table,"puppets");var own=puppets.GetValue(seat);var renderers=(Renderer[])own.GetType().GetField("Renderers").GetValue(own);
        Check(Array.TrueForAll(renderers,r=>!r.forceRenderingOff),phase+" restores own character");
    }
    void CheckFirstPerson(string phase)
    {
        int seat=player.Seat.Value;var eye=DuelGunplay.Eye(seat);var fps=manager.GetComponent<DuelFirstPerson>();
        Check(Vector3.Distance(Camera.main.transform.position,new Vector3(eye.X,eye.Y,eye.Z))<.001f,phase+" uses owner seat's gameplay eye position");
        Check(Quaternion.Angle(Camera.main.transform.rotation,fps.Rotation(Now))<.01f,phase+" uses gameplay view rotation");
        Check(Mathf.Abs(Camera.main.fieldOfView-DuelGunplay.BaseFov)<.01f,phase+" uses gameplay FOV");
        Check(UnityEngine.Cursor.lockState==CursorLockMode.None,phase+" keeps selection cursor available");
        var puppets=Field<Array>(manager.Table,"puppets");var own=puppets.GetValue(seat);
        var renderers=(Renderer[])own.GetType().GetField("Renderers").GetValue(own);
        Check(Array.TrueForAll(renderers,r=>r.forceRenderingOff),phase+" hides owner animation in first person");
    }
    IEnumerator Capture(string name)
    {
        var doc=FindFirstObjectByType<UIDocument>();var target=new RenderTexture(1200,800,24);target.Create();
        doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;
        doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=1200;doc.rootVisualElement.style.height=800;
        yield return new WaitForSecondsRealtime(.15f);
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1200,800,TextureFormat.RGBA32,false);
        image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(path,name+".png"),image.EncodeToPNG());
        RenderTexture.active=previous;doc.panelSettings.targetTexture=null;target.Release();Destroy(target);Destroy(image);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL loadout timeout "+role,1);}
}
