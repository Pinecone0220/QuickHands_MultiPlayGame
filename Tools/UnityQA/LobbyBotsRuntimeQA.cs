using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class LobbyBotsRuntimeQA:MonoBehaviour
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Hidden).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--lobby-bots-qa");if(i<0)return;
        var q=new GameObject("Lobby bot QA").AddComponent<LobbyBotsRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--lobby-bots-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=60;
    }
    void Done(string text,int code=0){Debug.Log(text);File.WriteAllText(Path.Combine(path,role+"-done"),text);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL lobby bots "+role+": "+why,1);throw new Exception(why);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL lobby bots timeout "+role,1);}
    void Mark(string n)=>File.WriteAllText(Path.Combine(path,role+"-"+n),"ok");bool Seen(string r,string n)=>File.Exists(Path.Combine(path,r+"-"+n));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Hidden).Invoke(manager,null);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;manager.Difficulty=AiDifficulty.Hard;
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18996");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;view=manager.GetComponent<RouletteView>();RoomCameraCaptureQA.Setup(view);if(role=="host")Mark("listening");
        while(RouletteRules.Count(player.State.Value.ConnectedMask)!=2)yield return null;yield return new WaitForSecondsRealtime(.2f);
        var button=Field<Button>(view,"addBot");Check((button.resolvedStyle.display!=DisplayStyle.None)==(role=="host"),"add-bot control visible only to host");
        if(role=="client"){Check(!manager.CanAddBot,"remote cannot add bots");manager.AddLobbyBot();Check(player.State.Value.ConnectedMask==3,"remote local call cannot change roster");}
        player.SelectLoadoutItem(DuelItem.Recovery);player.SelectLoadoutItem(DuelItem.Taser);while(!DuelLoadout.Complete(player.State.Value.ForSeat(player.Seat.Value).SelectedItems))yield return null;Mark("loadout");
        if(role=="host")
        {
            while(!Seen("client","loadout"))yield return null;
            var reservations=Field<HashSet<ulong>>(manager,"reservations");reservations.Add(999999);
            Check(!manager.CanAddBot,"approved pending human blocks roster edits");manager.AddLobbyBot();Check(D.Game.ConnectedMask==3,"pending human cannot be displaced by bot");reservations.Remove(999999);
            D.Ready(0,Now);Publish();
            for(int seat=2;seat<6;seat++){Check(manager.CanAddBot,"host can add to empty seat");manager.AddLobbyBot();Check(D.IsBot(seat)&&RouletteRules.Has(D.Game.ReadyMask,0)&&!RouletteRules.Has(D.Game.ReadyMask,1),"added bot preserves human readiness");yield return null;}
            Check(!manager.CanAddBot,"full room blocks addition");manager.AddLobbyBot();Check(D.Game.ConnectedMask==63,"extra add cannot overfill");Mark("filled");
        }
        while(!Seen("host","filled")||player.State.Value.ConnectedMask!=63)yield return null;yield return new WaitForSecondsRealtime(.15f);
        Check(player.State.Value.ReadyMask==61,"remote receives ready humans and auto-ready bots");for(int bot=2;bot<6;bot++)Check(player.State.Value.NameForSeat(bot)=="AI"+bot&&DuelLoadout.Complete(player.State.Value.ForSeat(bot).SelectedItems),"bot name and chosen inventory synchronized "+bot);
        if(role=="host")Check(!button.enabledSelf&&button.text=="정원 6명","full lobby button disabled");yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,role+"-Lobby.png"));Mark("filled");
        if(role=="host")
        {
            while(!Seen("client","filled"))yield return null;D.Game.SetShotTime(0);D.Game.SetManualMiniGame(true);Publish();Mark("ready");
        }
        if(role=="client"){while(!Seen("host","ready"))yield return null;player.Rematch();}
        while(player.State.Value.Phase!=DuelPhase.Playing)yield return null;yield return new WaitForSecondsRealtime(.15f);Check(button.resolvedStyle.display==DisplayStyle.None,"add-bot button hidden in game");Mark("started");
        if(role=="host")
        {
            while(!Seen("client","started"))yield return null;int count=RouletteRules.Count(D.Game.ConnectedMask);manager.AddLobbyBot();Check(RouletteRules.Count(D.Game.ConnectedMask)==count,"add during game rejected");
            typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Racing,Now});var clock=Field<DuelClockContest>(D,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.QuickHands,Iteration=1,First=-1,ParticipantsMask=63,PhaseAt=Now,StartedAt=Now+5});Publish();Mark("prep");
        }
        while(!Seen("host","prep")||player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;while(player.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;
        double begin=Now;bool taser=false,ink=false,gun=false;
        while(Now-begin<28&&!(taser&&ink&&gun))
        {
            for(int bot=2;bot<6;bot++){var a=player.State.Value.ForSeat(bot);taser|=a.ShotsFired>0&&a.FiredWeapon==DuelWeapon.Taser;gun|=a.ShotsFired>0&&a.FiredWeapon!=DuelWeapon.Taser;ink|=a.Ink.Serial>0;}yield return null;
        }
        Check(taser&&ink&&gun,"mixed-room AI uses taser, ink and numeric gun actions");Check(player.State.Value.Seat1.CorrectInputs==0&&player.State.Value.Seat1.ShotsFired==0,"remote human never receives AI control");Mark("played");
        if(role=="host")while(!Seen("client","played"))yield return null;
        Done("PASS lobby bots "+role+": "+checks+" checks");
    }
}
