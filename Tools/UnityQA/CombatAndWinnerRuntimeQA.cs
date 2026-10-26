using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class CombatAndWinnerRuntimeQA:MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    void Mark(string n)=>File.WriteAllText(Path.Combine(path,role+"-"+n),"ok");bool Seen(string r,string n)=>File.Exists(Path.Combine(path,r+"-"+n));
    void Done(string message,int code=0){File.WriteAllText(Path.Combine(path,role+"-done"),message);Debug.Log(message);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL combat/winner "+role+": "+why,1);throw new Exception(why);}}
    bool Shown(string field)=>Field<VisualElement>(view,field).resolvedStyle.display!=DisplayStyle.None;
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Flags).Invoke(manager,null);
    AudioSource Contact(int seat){var audio=Field<DuelWeaponAudio>(manager.Table,"weaponAudio");var data=Field<Array>(audio,"seats").GetValue(seat);return Field<AudioSource>(data,"Contact");}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--combat-winner-qa");if(i<0)return;var q=new GameObject("Combat winner QA").AddComponent<CombatAndWinnerRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--combat-winner-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+90;Application.targetFrameRate=60;
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL combat/winner timeout "+role,1);}
    void Ranking(bool tie)
    {
        var c=Field<DuelClockContest>(D,"clock");var s=new ClockState{Phase=ClockPhase.Counting,MiniGame=PartyMiniGame.Countdown,Iteration=c.State.Iteration+1,ParticipantsMask=7,First=-1,PhaseAt=Now,StartedAt=Now-10,Target=1000};
        s.Set(0,new ClockEntry{Centiseconds=1000,Error=0});s.Set(1,new ClockEntry{Centiseconds=tie?1000:1100,Error=tie?0:100});s.Set(2,new ClockEntry{Centiseconds=1100,Error=100});typeof(DuelClockContest).GetProperty("State").SetValue(c,s);typeof(DuelClockContest).GetField("stopped",Flags).SetValue(c,7);typeof(DuelClockContest).GetMethod("Resolve",Flags).Invoke(c,new object[]{Now});Publish();
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","19004");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;if(role=="host")Mark("listening");yield return new WaitForSecondsRealtime(.2f);Mark("ready");
        if(role=="host")
        {
            while(!Seen("client","ready"))yield return null;D.Join(2,Now);D.SetAutoSelectItems(true);D.Game.SetShotTime(0);for(int i=0;i<3;i++)D.Ready(i,Now);typeof(DuelDirector).GetMethod("SetStage",Flags).Invoke(D,new object[]{DuelStage.Racing,Now});
            var c=Field<DuelClockContest>(D,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(c,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,First=-1,Iteration=1,ParticipantsMask=7,PhaseAt=Now,StartedAt=Now});
            for(int target=0;target<2;target++){var shot=new DuelProjectileVolley(2,D.ForSeat(2).Life,1,DuelWeapon.Taser,DuelGunplay.Eye(2),new AimVector(0,0,1),0,0,Now);typeof(DuelDirector).GetMethod("ResolveProjectile",Flags).Invoke(D,new object[]{shot,new DuelRayHit{Seat=target,Part=DuelHitPart.Body},Now});}Publish();
        }
        while(!player.State.Value.ForSeat(player.Seat.Value).Stunned)yield return null;double start=player.State.Value.ForSeat(player.Seat.Value).StunnedAt;yield return new WaitForSecondsRealtime(.15f);
        Check(player.State.Value.ForSeat(player.Seat.Value).Health==95,"taser actual network damage");Check(Shown("electricOverlay"),"victim screen shock starts");Check(Contact(player.Seat.Value).isPlaying,"taser contact sound starts");
        var arcs=Field<LineRenderer[,]>(manager.Table,"stunArcs");int other=1-player.Seat.Value;Check(arcs[other,0]!=null&&arcs[other,0].enabled,"other victim has visible electricity");
        while(Now-start<2)yield return null;Check(!player.State.Value.ForSeat(player.Seat.Value).IsStunned(Now),"owner deadline ignores any stale stun flag");while(Now-start<2.25)yield return null;
        Check(!player.State.Value.ForSeat(player.Seat.Value).Stunned&&!Shown("electricOverlay"),"server and victim UI clear at two seconds");Check(!Contact(player.Seat.Value).isPlaying&&!arcs[other,0].enabled,"contact sound and third-person electricity stop");Check(player.State.Value.Clock.Phase==ClockPhase.QuickHands,"quick hands continues after shock expires");
        var fps=manager.GetComponent<DuelFirstPerson>();fps.enabled=false;yield return new WaitForSecondsRealtime(.1f);float yaw=player.Seat.Value*60+10;player.Look(yaw,0,false);double until=Now+2;while(Mathf.Abs(Mathf.DeltaAngle(player.State.Value.ForSeat(player.Seat.Value).AimYaw,yaw))>.1f&&Now<until)yield return null;Check(Mathf.Abs(Mathf.DeltaAngle(player.State.Value.ForSeat(player.Seat.Value).AimYaw,yaw))<.1f,"owner look RPC accepted after expiry");fps.enabled=true;Mark("recovered");
        if(role=="host"){while(!Seen("client","recovered"))yield return null;Ranking(false);Mark("unique");}
        while(!Seen("host","unique")||player.State.Value.Clock.Iteration<2)yield return null;yield return new WaitForSecondsRealtime(.1f);
        Check(player.State.Value.Clock.Phase==ClockPhase.Ranking&&player.State.Value.Clock.First==0&&!Shown("diceLayer"),"unique first skips lower-place tie on remote UI");Mark("unique");
        if(role=="host"){while(!Seen("client","unique"))yield return null;Ranking(true);Mark("tie");}
        while(!Seen("host","tie")||player.State.Value.Clock.Phase!=ClockPhase.Dice)yield return null;yield return new WaitForSecondsRealtime(.1f);Check(player.State.Value.Clock.Dice.PendingMask==3&&Shown("diceLayer"),"only tied top candidates see dice stage");int round=-1;
        while(player.State.Value.Clock.Phase==ClockPhase.Dice)
        {
            var dice=player.State.Value.Clock.Dice;if(dice.CanStop(player.Seat.Value)&&round!=dice.Round){round=dice.Round;player.StopDice();}yield return null;
        }
        Check(player.State.Value.Clock.Phase==ClockPhase.Ranking&&(player.State.Value.Clock.First==0||player.State.Value.Clock.First==1),"top dice winner becomes shooter");Check(player.State.Value.Clock.Dice.ForSeat(2).Rolls==0,"lower participant never rolls");Mark("complete");if(role=="host")while(!Seen("client","complete"))yield return null;
        Done("PASS combat/winner "+role+": "+checks+" checks");
    }
}
