using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
public sealed class ReloadRuntimeQA:MonoBehaviour
{
    string role,path;HelloWorldManager manager;HelloWorldPlayer player;int seat,checks;double deadline,nextStatus;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--reload-qa");if(i<0)return;var q=new GameObject("ReloadQA").AddComponent<ReloadRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","reload-");q.deadline=Time.realtimeSinceStartupAsDouble+160;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class PreciseRandom:System.Random{public override double NextDouble()=>0;}
    bool SettingsVisible=>((VisualElement)typeof(RouletteView).GetField("roomSettings",F).GetValue(manager.GetComponent<RouletteView>())).style.display.value==DisplayStyle.Flex;
    bool Defaults=>player.State.Value.ShotTimeLimit==10&&!player.State.Value.StartWithItems&&!player.State.Value.UnlimitedItems&&!player.State.Value.ManualMiniGame;
    IEnumerator F11()
    {
        if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.F11));yield return null;yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return null;
    }
    IEnumerator BreakProp(DuelTableProp kind)
    {
        yield return ShootPoint(DuelTabletop.Rest(3,kind));Check(player.State.Value.Tabletop.Broken(3,kind),"actual server projectile breaks "+kind);
    }
    IEnumerator ShootPoint(AimVector target)
    {
        var direction=target-DuelGunplay.Eye(0);DuelGunplay.Angles(direction,out float yaw,out float pitch);
        for(int i=0;i<8;i++){var origin=DuelGunplay.MuzzleOrigin(0,DuelWeapon.Revolver,true,DuelGunplay.Direction(yaw,pitch));DuelGunplay.Angles(target-origin,out yaw,out pitch);}
        double at=player.NetworkManager.ServerTime.Time;DuelGunplay.Sway(0,DuelWeapon.Revolver,true,at,at,out float sy,out float sp);
        int fired=Own.ShotsFired;player.Fire(yaw-sy,pitch-sp,true,at);while(Own.ShotsFired==fired)yield return null;
        yield return new WaitForSecondsRealtime(.65f);
    }
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");
    bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL reload "+role+": "+message,1);throw new Exception(message);}}
    void Finish(string message,int code=0){Debug.Log(message);File.WriteAllText(path+role+"-done",message);deadline=double.MaxValue;Application.Quit(code);}
    DuelActionState Own=>player.State.Value.ForSeat(seat);
    void CheckView()
    {
        var table=manager.Table;var puppets=(Array)typeof(RouletteTable).GetField("puppets",F).GetValue(table);
        for(int i=0;i<2;i++)
        {
            var p=puppets.GetValue(i);var renderers=(Renderer[])p.GetType().GetField("Renderers").GetValue(p);
            bool correct=true;foreach(var renderer in renderers)correct&=renderer.forceRenderingOff==(i==seat);
            Check(correct,"own full avatar hidden and remote avatar visible at seat "+i);
        }
    }
    IEnumerator CheckGaze()
    {
        var fp=manager.GetComponent<DuelFirstPerson>();
        typeof(DuelFirstPerson).GetProperty("Yaw").SetValue(fp,seat*60+30f);typeof(DuelFirstPerson).GetProperty("Pitch").SetValue(fp,12f);
        player.Look(seat*60+30,12,false);Mark("look");
        while(!Seen(role=="host"?"client":"host","look")||Mathf.Abs(player.State.Value.ForSeat(1-seat).AimPitch-12)>.1f)yield return null;
        yield return new WaitForSecondsRealtime(1);
        var puppets=(Array)typeof(RouletteTable).GetField("puppets",F).GetValue(manager.Table);
        for(int i=0;i<2;i++)
        {
            var p=puppets.GetValue(i);var head=(Transform)p.GetType().GetField("Head").GetValue(p);var direction=DuelGunplay.Direction(i*60+30,12);
            Check(Vector3.Angle(-head.forward,new Vector3(direction.X,direction.Y,direction.Z))<2,"local and replicated gaze follow camera yaw and pitch at seat "+i);
        }
        var eye=DuelGunplay.Eye(seat);Check(Vector3.Distance(Camera.main.transform.position,new Vector3(eye.X,eye.Y,eye.Z))<.002f,"first person camera uses the shared lowered seat eye position");CheckView();
    }
    IEnumerator Digit(){var a=Own;double retry=0;while(Own.Version==a.Version){if(Time.realtimeSinceStartupAsDouble>=retry){player.InputDigit(a.Digit(a.Progress),a.Version);retry=Time.realtimeSinceStartupAsDouble+1;}yield return null;}}
    IEnumerator Load(){while(Own.Action!=DuelAction.Aim)yield return Digit();}
    IEnumerator Miss(bool ads=false)
    {int shots=Own.ShotsFired,ammo=Own.Ammo;player.Fire(0,65,ads,player.NetworkManager.ServerTime.Time);while(Own.ShotsFired==shots)yield return null;Check(Own.Ammo==ammo-1,"one server cartridge consumed");yield return new WaitForSecondsRealtime(.65f);}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);manager=HelloWorldManager.Instance;
        if(role!="host")while(!Seen("host","configured"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17939");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        player=HelloWorldPlayer.Local;seat=player.Seat.Value;
        Check(!manager.DeveloperMode&&!SettingsVisible&&(role!="host"||Defaults),"new room hides developer controls; host defaults start clean");
        if(role=="host")
        {yield return F11();Check(manager.DeveloperMode&&SettingsVisible,"host F11 opens developer panel");manager.SetRoomManualMiniGame(true);manager.SetRoomShotTime(0);manager.SetRoomStartWithItems(true);manager.SetRoomUnlimitedItems(true);Mark("configured");}
        else
        {yield return F11();Check(!manager.DeveloperMode&&!SettingsVisible,"remote F11 has no developer authority");player.SelectWeapon(DuelWeapon.SawedOff);while(Own.Weapon!=DuelWeapon.SawedOff)yield return null;}
        while(player.State.Value.ConnectedMask!=3||!player.State.Value.ManualMiniGame)yield return null;
        if(role=="host")
        {
            while(!Seen("client","ready"))yield return null;
            while((player.State.Value.Tabletop.DrankMask&3)!=3)yield return null;
            Check(player.State.Value.Tabletop.Seat0.Action==DuelDrinkAction.None||player.State.Value.Tabletop.Seat0.Action==DuelDrinkAction.Sip,"lobby permits one drink");Mark("drank");
            while(!Seen("client","drank"))yield return null;
            yield return F11();Check(!manager.DeveloperMode&&!SettingsVisible&&Defaults&&player.State.Value.ReadyMask==2,"closing F11 restores all settings and preserves remote readiness");Mark("defaults");
            while(!Seen("client","defaults"))yield return null;
            yield return F11();Check(Defaults&&SettingsVisible,"reopening does not resurrect old cheats");manager.SetRoomManualMiniGame(true);manager.SetRoomShotTime(0);Mark("start");player.Rematch();
        }
        else
        {
            Check(player.State.Value.StartWithItems&&player.State.Value.UnlimitedItems&&player.State.Value.ShotTimeLimit==0,"developer settings replicated");player.Rematch();Mark("ready");
            while(!Seen("host","drank")||(player.State.Value.Tabletop.DrankMask&3)!=3)yield return null;
            Check(player.State.Value.Tabletop.FilledMask==0,"remote sees one-drink completion");Mark("drank");
            while(!Seen("host","defaults")||!Defaults)yield return null;Check(player.State.Value.ReadyMask==2,"remote sees defaults without losing readiness");Mark("defaults");while(!Seen("host","start"))yield return null;
        }
        if(role=="host"){while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;manager.ChooseMiniGame(PartyMiniGame.QuickHands);}
        while(player.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==38,"matching protocol");
        yield return CheckGaze();
        Check(player.State.Value.Tabletop.DrankMask==0,"new party resets drinking permission");
        yield return Load();Check(Own.CorrectInputs==(role=="host"?48:16),"initial preparation unchanged");
        if(role=="host")
        {
            yield return Miss();yield return Miss();
            int live=Own.LoadedMask,before=Own.CorrectInputs;
            Check(Own.Action==DuelAction.CloseBarrel&&Own.Ammo==4&&RouletteRules.Count(Own.SpentMask)==2,"live cartridges retained after two shots");
            Mark("partial");while(!Seen("client","observed"))yield return null;
            for(int n=0;n<6;n++)yield return Digit();
            Check(Own.LoadedMask==live&&Own.SpentMask==0&&Own.Action==DuelAction.Load,"owner ejection removes only spent cases");
            yield return Load();Check(Own.CorrectInputs-before==12&&Own.Ammo==6,"only two empty chambers refilled");
            Mark("refilled");while(!Seen("client","saw"))yield return null;
            Check(player.State.Value.Seat1.Action==DuelAction.OpenBarrel&&player.State.Value.Seat1.SpentMask==1,"remote sawed-off shell and opening replicated");
            Mark("observed");
        }
        else
        {
            while(!Seen("host","partial")||player.State.Value.Seat0.Ammo!=4)yield return null;
            Check(player.State.Value.Seat0.Action==DuelAction.CloseBarrel&&RouletteRules.Count(player.State.Value.Seat0.SpentMask)==2,"host partial cylinder replicated");Mark("observed");
            while(!Seen("host","refilled")||player.State.Value.Seat0.Ammo!=6)yield return null;
            Check(player.State.Value.Seat0.SpentMask==0,"host ejection and refilled cylinder replicated");
            yield return Miss();Check(Own.Action==DuelAction.OpenBarrel&&Own.SpentMask==1,"sawed-off opens after firing");Mark("saw");
            while(!Seen("host","observed"))yield return null;
            for(int cycle=0;cycle<2;cycle++)
            {
                int before=Own.CorrectInputs;for(int n=0;n<4;n++)yield return Digit();
                Check(Own.Action==DuelAction.Load&&Own.SpentMask==0&&Own.Ammo==0,"opening automatically ejects spent shell");
                yield return Load();Check(Own.CorrectInputs-before==12,"repeat loading skips pickup");if(cycle==0)yield return Miss();
            }
        }
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
        if(role=="host")
        {
            var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(manager);typeof(DuelDirector).GetField("random",F).SetValue(d,new PreciseRandom());
            yield return BreakProp(DuelTableProp.Bottle);yield return BreakProp(DuelTableProp.Glass);
            Check(player.State.Value.Seat0.Health==100&&player.State.Value.Seat1.Health==100,"glass impacts do not fabricate player damage");Mark("props");while(!Seen("client","props"))yield return null;
            yield return ShootPoint(DuelGunplay.Point(1,DuelHitPart.Head));
            while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
            int iteration=player.State.Value.Clock.Iteration;yield return F11();Check(!manager.DeveloperMode&&!SettingsVisible&&Defaults,"F11 off during play restores defaults too");
            Check(player.State.Value.Clock.Iteration>iteration&&player.State.Value.Clock.MiniGame!=PartyMiniGame.QuickHands,"turning off manual selection restarts normal reel with previous game excluded");Mark("off");while(!Seen("client","off"))yield return null;
            while(player.State.Value.Clock.Phase==ClockPhase.Selecting)yield return null;Check(player.State.Value.Clock.Phase==ClockPhase.Counting,"automatic selection resumes without host input");Mark("auto");while(!Seen("client","auto"))yield return null;
        }
        else
        {
            while(!Seen("host","props")||(player.State.Value.Tabletop.BottlesBrokenMask&8)==0||(player.State.Value.Tabletop.GlassesBrokenMask&8)==0)yield return null;
            Check(player.State.Value.Tabletop.Seat3.BottleBrokenAt>0&&player.State.Value.Tabletop.Seat3.GlassBrokenAt>0,"remote receives both break times and masks");Mark("props");
            while(!Seen("host","off")||!Defaults)yield return null;Check(!SettingsVisible&&player.State.Value.Clock.MiniGame!=PartyMiniGame.QuickHands,"remote receives reset and normal previous-game exclusion");Mark("off");
            while(!Seen("host","auto")||player.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;
            Check((player.State.Value.Tabletop.BottlesBrokenMask&8)!=0&&(player.State.Value.Tabletop.GlassesBrokenMask&8)!=0,"broken objects persist into next mini-game");Mark("auto");
        }
        Mark("final");while(!Seen("host","final")||!Seen("client","final"))yield return null;
        Finish("PASS reload "+role+": "+checks+" F11 defaults/authority, drinking, owner RPC, reload, actual prop hits and remote replication checks");
    }
    void Update()
    {
        if(player!=null&&player.IsSpawned&&Time.realtimeSinceStartupAsDouble>=nextStatus)
        {File.WriteAllText(path+role+"-state",player.State.Value.Clock.Phase+" seat "+seat+" action "+Own.Action+" progress "+Own.Progress+" version "+Own.Version+" ammo "+Own.Ammo+" inputs "+Own.CorrectInputs+" peer ammo "+player.State.Value.ForSeat(1-seat).Ammo);nextStatus=Time.realtimeSinceStartupAsDouble+1;}
        if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL reload timeout "+role,1);
    }
}
