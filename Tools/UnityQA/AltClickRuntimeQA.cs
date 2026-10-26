using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

// Isolated Unity QA project only. Exercise Clickable through down/up, not a synthetic ClickEvent.
public sealed class AltClickRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string path;int checks;double deadline;Keyboard keyboard;
    HelloWorldManager manager;RouletteView view;HelloWorldPlayer player;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--alt-click-qa");if(i<0)return;
        var q=new GameObject("Alt click QA").AddComponent<AltClickRuntimeQA>();q.path=args[i+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=30;
    }
    void Finish(string message,int code=0)
    {
        Debug.Log(message);File.WriteAllText(Path.Combine(path,"done"),message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL Alt click: "+message,1);throw new Exception(message);}Debug.Log("Alt click check: "+message);}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL Alt click: timeout",1);}
    void Keys(params Key[] keys)
    {
        var state=new KeyboardState(keys);InputState.Change(keyboard,state,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(keyboard,state,InputUpdateType.Editor);
#endif
        keyboard.MakeCurrent();typeof(DuelPointer).GetMethod("Update",F).Invoke(manager.GetComponent<DuelPointer>(),null);
    }
    void Pointer(VisualElement target,bool down,EventModifiers modifiers,Vector2? position=null)
    {
        var raw=new Event{type=down?EventType.MouseDown:EventType.MouseUp,button=0,clickCount=1,mousePosition=position??target.worldBound.center,modifiers=modifiers};
        if(down){using(var e=PointerDownEvent.GetPooled(raw))target.SendEvent(e);}
        else{using(var e=PointerUpEvent.GetPooled(raw))target.SendEvent(e);}
    }
    void Click(VisualElement target,EventModifiers modifiers)
    {Check(target.enabledInHierarchy&&target.resolvedStyle.display!=DisplayStyle.None,"target enabled: "+target.GetType().Name);Pointer(target,true,modifiers);Pointer(target,false,modifiers);}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        keyboard=InputSystem.AddDevice<Keyboard>();Keys();
        Click(Field<Button>(view,"weaponInfoButton"),EventModifiers.None);
        Check(Field<bool>(view,"weaponInfoOpen"),"normal menu pointer click opens information");
        Click(Field<Button>(view,"weaponInfoClose"),EventModifiers.None);Check(!Field<bool>(view,"weaponInfoOpen"),"normal menu pointer click closes information");
        manager.CreateRoom("Alt 클릭 검사","","ALTCLICK");while(HelloWorldPlayer.Local==null)yield return new WaitForSecondsRealtime(.12f);player=HelloWorldPlayer.Local;
        D.Join(1,Now);Publish();yield return new WaitForSecondsRealtime(.12f);yield return new WaitForSecondsRealtime(.12f);
        var weapons=Field<Button[]>(view,"weaponButtons");Keys(Key.LeftAlt);Click(weapons[1],EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(D.ForSeat(0).SelectedWeapon==DuelWeapon.SawedOff,"Alt down/up changes actual weapon selection");
        Keys();Click(weapons[0],EventModifiers.None);yield return new WaitForSecondsRealtime(.12f);
        Check(D.ForSeat(0).SelectedWeapon==DuelWeapon.SawedOff,"no Alt blocks pointer action");
        Keys(Key.RightAlt);Click(weapons[0],EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(D.ForSeat(0).SelectedWeapon==DuelWeapon.Revolver,"right Alt down/up changes weapon");
        var loadout=Field<Button[]>(view,"loadoutButtons");Click(loadout[0],EventModifiers.Alt);Click(loadout[1],EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(DuelLoadout.Complete(D.ForSeat(0).SelectedItems),"Alt pointer selects both inventory slots");
        Click(Field<Button>(view,"rematch"),EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(RouletteRules.Has(D.Game.ReadyMask,0),"Alt pointer readies local player");
        Click(Field<Button>(view,"practiceCancel"),EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(!RouletteRules.Has(D.Game.ReadyMask,0),"Alt pointer cancels ready");
        manager.ToggleDeveloperMode();yield return new WaitForSecondsRealtime(.12f);
        Click(Field<Toggle>(view,"unlimitedItems"),EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(D.Game.UnlimitedItems,"Alt pointer changes actual room toggle");
        Click(Field<Toggle>(view,"unlimitedItems"),EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(!D.Game.UnlimitedItems,"repeated Alt pointer toggles off once");
        var target=weapons[1];Pointer(target,true,EventModifiers.Alt);Keys();yield return new WaitForSecondsRealtime(.12f);
        Pointer(target,false,EventModifiers.None);Check(D.ForSeat(0).SelectedWeapon==DuelWeapon.Revolver,"Alt release cancels pending click");
        Keys(Key.LeftAlt);Click(weapons[2],EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(D.ForSeat(0).SelectedWeapon==DuelWeapon.BAS,"Alt release during press does not leave pointer captured");
        // A real item button must run the same owner RPC path as an actual click.
        D.SetAutoSelectItems(true);D.Ready(0,Now);D.Ready(1,Now);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,Iteration=1,First=1,ParticipantsMask=3,PhaseAt=Now,StartedAt=Now+5});
        D.Game.Shoot(1,0,DuelWeapon.Revolver,DuelHitPart.Body,true);Publish();yield return new WaitForSecondsRealtime(.12f);yield return new WaitForSecondsRealtime(.12f);
        Check(D.ForSeat(0).Health==50&&D.ForSeat(0).HasRecovery,"item context has damaged owner and recovery");
        Click(Field<Button[]>(view,"itemButtons")[0],EventModifiers.Alt);yield return new WaitForSecondsRealtime(.12f);
        Check(D.ForSeat(0).Health==100&&!D.ForSeat(0).HasRecovery,"Alt down/up consumes actual recovery item");
        Keys();yield return new WaitForSecondsRealtime(.12f);yield return new WaitForSecondsRealtime(.12f);Check(!DuelPointer.BlocksActions,"Alt release resumes gameplay");
        Finish("PASS Alt click: "+checks+" checks");
    }
}
