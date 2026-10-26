using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

public sealed class DiceRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--dice-qa");if(i<0)return;
        var qa=new GameObject("Dice QA").AddComponent<DiceRuntimeQA>();qa.role=args[i+1];i=Array.IndexOf(args,"--dice-dir");qa.path=args[i+1];Directory.CreateDirectory(qa.path);
        Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }
    void Mark(string key)=>File.WriteAllText(Path.Combine(path,role+"-"+key),"ok");
    bool Seen(string who,string key)=>File.Exists(Path.Combine(path,who+"-"+key));
    void Finish(string message,int code=0)
    {
        Debug.Log(message);File.WriteAllText(Path.Combine(path,role+"-done"),message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL dice "+role+": "+message,1);throw new Exception(message);}}
    void Publish()=>Call(manager,"Publish");
    sealed class Faces: System.Random
    {
        readonly Queue<int> faces;
        public Faces(params int[] values):base(28){faces=new Queue<int>(values);}
        public override int Next(int min,int max)=>min==1&&max==9&&faces.Count>0?faces.Dequeue():base.Next(min,max);
    }
    List<List<int>> Groups(params int[][] groups){var r=new List<List<int>>();foreach(var g in groups)r.Add(new List<int>(g));return r;}
    void Model()
    {
        var dice=new DuelDiceContest(new Faces(8,3));Check(dice.Begin(Groups(new[]{0},new[]{1,2},new[]{3}),0),"only a tie starts rolling");
        Check(dice.State.ParticipantsMask==6&&dice.State.PendingMask==6,"non-tied scores stay outside dice");
        Check(!dice.Stop(0,1,1)&&!dice.Stop(1,0,1)&&!dice.Stop(1,1,-.01)&&!dice.Stop(1,1,10)&&!dice.Stop(1,1,double.NaN),"eligibility, nonce and time boundaries");
        Check(dice.Stop(1,1,1)&&dice.State.Seat1.Face==8&&!dice.Stop(1,1,1.1),"server d8 supports eight and locks once");
        Check(dice.State.Phase==DuelDicePhase.Rolling&&dice.State.Seat2.Face==0,"one person cannot decide everyone");
        Check(dice.Stop(2,1,2)&&dice.State.Phase==DuelDicePhase.Comparing,"wait for every tied player then compare");
        Check(!dice.Tick(3.19,15)&&dice.Tick(3.2,15)&&dice.State.Phase==DuelDicePhase.Finished,"comparison hold boundary");
        Check(string.Join(",",dice.Order)=="0,1,2,3","original score groups outrank dice");
        dice=new DuelDiceContest(new Faces(4,4,8,1));dice.Begin(Groups(new[]{0,1},new[]{2}),0);dice.Stop(0,1,1);dice.Stop(1,1,1);
        dice.Tick(2.21,7);Check(dice.State.Round==2&&dice.State.PendingMask==3&&dice.State.Seat0.Face==0,"equal faces reroll only their subgroup");
        Check(!dice.Stop(0,1,2.3)&&dice.Stop(0,2,2.3)&&dice.Stop(1,2,2.3),"stale reroll request rejected");dice.Tick(3.51,7);
        Check(string.Join(",",dice.Order)=="0,1,2"&&dice.State.Seat0.Rolls==2,"reroll outcome and per-person rolls");
        dice=new DuelDiceContest(new Faces(1,8,8,7));dice.Begin(Groups(new[]{0,1},new[]{2,3}),0);for(int i=0;i<4;i++)dice.Stop(i,1,1);dice.Tick(2.21,15);
        Check(string.Join(",",dice.Order)=="1,0,2,3","a lower original-score group never jumps a better group");
        dice=new DuelDiceContest(new Faces(7,2));dice.Begin(Groups(new[]{0,1}),0);
        Check(!dice.Tick(9.999,3)&&dice.State.Seat0.Face==0,"no early auto-confirm");
        Check(dice.Tick(10,3)&&dice.State.Phase==DuelDicePhase.Comparing&&dice.State.LockedMask==3,"ten-second automatic confirmation");dice.Tick(11.21,3);
        Check(dice.State.Phase==DuelDicePhase.Finished,"automatic confirmation completes");
        dice=new DuelDiceContest(new Faces(1,2));dice.Begin(Groups(new[]{0},new[]{1,2}),0);dice.Tick(1,2);dice.Tick(2.21,2);
        Check(dice.State.Phase==DuelDicePhase.Finished&&dice.Order.Count==1&&dice.Order[0]==1,"disconnect removes both tied and non-tied missing players without hanging");
        foreach(var game in new[]{PartyMiniGame.Countdown,PartyMiniGame.Signal,PartyMiniGame.Greed})
        {
            var c=new DuelClockContest(new Faces(8,1));c.Begin(7,0,game);
            double at=game==PartyMiniGame.Signal?c.ReactionGoAt+.3:game==PartyMiniGame.Greed?c.State.StartedAt+1:c.State.StartedAt+c.State.Target/100.0;
            Check(c.Stop(0,c.State.Iteration,at,at)&&c.Stop(1,c.State.Iteration,at,at)&&c.Stop(2,c.State.Iteration,game==PartyMiniGame.Greed?at-.1:at+.1,game==PartyMiniGame.Greed?at:at+.1),game+" records");
            Check(c.State.Phase==ClockPhase.Dice&&c.State.Dice.ParticipantsMask==3,game+" tie delays rankings");
            Check(!c.StopDice(2,c.State.Iteration,1,at+.2)&&!c.StopDice(0,c.State.Iteration-1,1,at+.2),game+" rejects spectator and stale iteration");
            c.StopDice(0,c.State.Iteration,1,at+.3);c.StopDice(1,c.State.Iteration,1,at+.4);c.Tick(at+1.61,7);
            Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0&&c.State.Seat2.Rank==3,game+" d8 ranking applied");
            Check(!c.Tick(c.State.PhaseAt+2.999,7),game+" keeps full three-second results");c.Tick(c.State.PhaseAt+3.001,7);Check(c.State.Phase==ClockPhase.ItemPreparation,game+" result then item turn");
        }
        var m=new DuelArrowMemory(new Faces(2,8));m.Begin(7,0);m.Tick(3,7);MemoryAnswer(m,new[]{0,1,1},3.1);
        Check(m.State.Phase!=ArrowMemoryPhase.Dice&&m.Dice.ParticipantsMask==0,"three-player lower tie never starts dice");
        m=new DuelArrowMemory(new Faces(3,8));m.Begin(7,0);m.Tick(3,7);MemoryAnswer(m,new[]{0,0,2},3.1);
        Check(m.State.Phase==ArrowMemoryPhase.Results&&m.State.ActiveMask==3&&m.State.Seat2.Rank==2,"unique worst memory player eliminated normally");
        m.Tick(6.11,7);m.Tick(9.12,7);MemoryAnswer(m,new[]{1,1,0},9.2);
        Check(m.State.Phase==ArrowMemoryPhase.Dice&&m.State.Round==2,"later memory round uses interactive dice too");m.StopDice(0,1,9.3);m.StopDice(1,1,9.4);m.Tick(10.61,7);
        Check(m.State.Winner==1&&m.State.Seat2.Rank==2,"prior memory elimination remains unranked below winner");
        var quick=new DuelClockContest(new Faces(7,1));quick.Select(7,0,true);quick.ChooseManual(PartyMiniGame.QuickHands,0);quick.Tick(0,7,10,false,true,true);quick.Tick(5,7);quick.Tick(8,7);
        quick.RecordQuickDeaths(6,1);quick.FinishQuick(0,1,9);
        Check(quick.State.Phase==ClockPhase.Dice&&quick.State.Dice.ParticipantsMask==6,"quick simultaneous death ties wait until minigame ends");
        quick.Tick(9.1,1,10,false,false,false,7);Check(quick.State.Dice.PendingMask==6,"dead players stay eligible while connected");
        quick.StopDice(1,quick.State.Iteration,1,9.2);quick.StopDice(2,quick.State.Iteration,1,9.3);quick.Tick(10.51,1,10,false,false,false,7);
        Check(quick.State.Phase==ClockPhase.QuickResults&&quick.State.First==0&&quick.State.Last==2&&quick.State.Seat1.Rank==2,"quick lower die loses its tied death group");
        var abandoned=new DuelClockContest(new Faces(7,1));abandoned.Select(7,0,true);abandoned.ChooseManual(PartyMiniGame.QuickHands,0);abandoned.Tick(0,7,10,false,true,true);abandoned.Tick(5,7);abandoned.Tick(8,7);abandoned.RecordQuickDeaths(6,1);abandoned.FinishQuick(0,1,9);
        abandoned.StopDice(1,abandoned.State.Iteration,1,9.1);abandoned.StopDice(2,abandoned.State.Iteration,1,9.2);abandoned.Tick(9.3,0,10,false,false,false,6);abandoned.Tick(10.41,0,10,false,false,false,6);
        Check(abandoned.State.Phase==ClockPhase.QuickResults&&abandoned.State.First==-1,"disconnected quick survivor is never declared winner after dice");
        var terminal=ReadyDirector(DuelMode.PvP);var terminalClock=Field<DuelClockContest>(terminal,"clock");terminalClock.Select(7,0,true);terminalClock.ChooseManual(PartyMiniGame.QuickHands,0);terminalClock.Tick(0,7,10,false,true,true);terminalClock.Tick(5,7);terminalClock.Tick(8,7);
        var terminalActions=Field<DuelActionState[]>(terminal,"actions");terminalActions[1].LivesRemaining=terminalActions[2].LivesRemaining=1;
        terminal.Game.Eliminate(6);Call(terminal,"LoseLife",1,DuelDeathCause.Gunshot,9.0);Call(terminal,"LoseLife",2,DuelDeathCause.Gunshot,9.0);terminalClock.RecordQuickDeaths(6,1);Call(terminal,"FinishQuick",9.0);
        Check(terminal.Game.Phase==DuelPhase.Playing&&terminal.Clock.Phase==ClockPhase.Dice,"final party victory waits for tied quick death dice");
        double terminalTime=9.1;int terminalRounds=0;while(terminal.Clock.Phase==ClockPhase.Dice&&terminalRounds++<20){var ds=terminal.Clock.Dice;for(int i=1;i<3;i++)if(ds.CanStop(i))terminal.StopDice(i,terminal.Clock.Iteration,ds.Round,terminalTime);terminalTime+=1.21;terminal.Tick(terminalTime);}
        Check(terminal.Game.Phase==DuelPhase.Finished&&terminal.Game.Winner==0,"final party victory resumes after quick dice");
        var d=ReadyDirector(DuelMode.PvP);var contest=Field<DuelClockContest>(d,"clock");contest.Begin(7,0);
        double stop=contest.State.StartedAt+contest.State.Target/100.0;
        d.StopClock(0,contest.State.Iteration,stop,stop);d.StopClock(1,contest.State.Iteration,stop,stop);d.StopClock(2,contest.State.Iteration,stop+.1,stop+.1);
        Check(d.Clock.Phase==ClockPhase.Dice&&d.ForSeat(0).ClockWins==0,"statistics wait until dice ends");
        int rounds=0;double now=stop+.2;
        while(d.Clock.Phase==ClockPhase.Dice&&rounds++<20)
        {var diceState=d.Clock.Dice;for(int i=0;i<3;i++)if(diceState.CanStop(i))d.StopDice(i,d.Clock.Iteration,diceState.Round,now);now+=1.21;d.Tick(now);}
        Check(d.Clock.Phase==ClockPhase.Ranking&&d.ForSeat(d.Clock.First).ClockWins==1,"director records ranking once after dice");d.Tick(now+.01);
        Check(d.ForSeat(d.Clock.First).ClockWins==1,"no duplicate stats on next tick");
        var ai=ReadyDirector(DuelMode.AI);var aiClock=Field<DuelClockContest>(ai,"clock");aiClock.Begin(7,0);
        stop=aiClock.State.StartedAt+aiClock.State.Target/100.0;for(int i=0;i<3;i++)ai.StopClock(i,ai.Clock.Iteration,stop,stop);
        now=stop+.1;rounds=0;while(ai.Clock.Phase==ClockPhase.Dice&&rounds++<20)
        {var ds=ai.Clock.Dice;if(ds.CanStop(0))ai.StopDice(0,ai.Clock.Iteration,ds.Round,now);now+=6;ai.Tick(now);now+=1.3;ai.Tick(now);}
        Check(ai.Clock.Phase!=ClockPhase.Dice,"AI auto-stops without human input for bots");
        var snapshot=DuelSnapshot.From(d);snapshot.Clock.Phase=ClockPhase.Dice;snapshot.Clock.Dice=new DuelDiceState{Phase=DuelDicePhase.Rolling,Round=8,ParticipantsMask=63,PendingMask=48,LockedMask=16,StartedAt=22,EndsAt=32,Seat5=new DuelDiceEntry{Face=8,Rolls=3}};
        using(var writer=new FastBufferWriter(8192,Allocator.Temp)){writer.WriteNetworkSerializable(snapshot);using(var reader=new FastBufferReader(writer,Allocator.Temp)){reader.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(snapshot)&&copy.Clock.Dice.Seat5.Face==8&&copy.Clock.Dice.EndsAt==32,"six-seat dice snapshot roundtrip");}}
        var changed=snapshot;changed.Clock.Dice.Round++;Check(!changed.Equals(snapshot),"dice nonce changes publish");changed=snapshot;changed.Clock.Dice.Seat5.Face=1;Check(!changed.Equals(snapshot),"face-only changes publish");
        AllFailedTies();
        MemoryWinnerOnly();
    }
    void MemoryWinnerOnly()
    {
        var m=new DuelArrowMemory(new Faces(3,8));m.Begin(63,0);double now=0;
        for(int round=1;round<=4;round++)
        {
            m.Tick(now+3,63);var errors=new int[6];errors[6-round]=1;MemoryAnswer(m,errors,now+3.1);
            Check(m.State.Phase==ArrowMemoryPhase.Results&&RouletteRules.Count(m.State.ActiveMask)==6-round&&m.State.Round==round,"unique worst elimination preserves ongoing round "+round);
            now+=6.11;m.Tick(now,63);
        }
        Check(m.State.Round==5&&m.State.Length==7&&m.State.ActiveMask==3&&m.State.Phase==ArrowMemoryPhase.Showing,"final pair continues at round five and length seven");
        m.Tick(now+3,63);MemoryAnswer(m,new[]{0,0,0,0,0,0},now+3.1);
        Check(m.State.Phase==ArrowMemoryPhase.Results&&m.State.ActiveMask==3&&m.Dice.ParticipantsMask==0,"two correct finalists continue without dice");
        now+=6.11;m.Tick(now,63);m.Tick(now+3,63);MemoryAnswer(m,new[]{1,1,0,0,0,0},now+3.1);
        Check(m.State.Phase==ArrowMemoryPhase.Dice&&m.State.Round==6&&m.State.Length==8&&m.Dice.ParticipantsMask==3,"equal mistakes roll only final pair without round or length reset");
        Check(!m.StopDice(2,m.Dice.Round,now+3.2),"previously eliminated participant cannot roll");
        m.StopDice(0,m.Dice.Round,now+3.2);m.StopDice(1,m.Dice.Round,now+3.2);m.Tick(now+4.41,63);
        Check(m.State.Phase==ArrowMemoryPhase.Finished&&m.State.Winner==1&&m.State.Round==6&&m.State.Length==8,"final pair dice selects winner within original sequence progression");
        for(int i=0;i<6;i++)Check(m.State.ForSeat(i).Rank==(i==1?1:2),"only winner and elimination appear in memory results");
        m=new DuelArrowMemory(new Faces(8,2));m.Begin(3,0);m.Tick(3,3);MemoryAnswer(m,new[]{1,2},3.1);
        Check(m.State.Phase==ArrowMemoryPhase.Finished&&m.State.Winner==0&&m.Dice.ParticipantsMask==0,"different finalist errors settle without dice");
        m=new DuelArrowMemory(new Faces(8,2));m.Begin(3,0);m.Tick(3,3);m.Tick(13,3);
        Check(m.State.Phase==ArrowMemoryPhase.Dice&&m.Dice.ParticipantsMask==3,"two equally missing input records contest first place with dice");
        m=new DuelArrowMemory(new Faces(8,2,2));m.Begin(7,0);m.Tick(3,7);MemoryAnswer(m,new[]{1,1,1},3.1);
        Check(m.Dice.ParticipantsMask==7,"all remaining equal wrong records roll for first even in initial round");
        for(int i=0;i<3;i++)m.StopDice(i,m.Dice.Round,3.2);m.Tick(4.41,7);
        Check(m.State.Phase==ArrowMemoryPhase.Finished&&m.State.Winner==0&&m.Dice.Round==1&&m.State.Seat1.Dice==m.State.Seat2.Dice,"unique first ends memory dice without rerolling equal lower faces");
        m=new DuelArrowMemory(new Faces(8,8,2,1,7));m.Begin(7,0);m.Tick(3,7);MemoryAnswer(m,new[]{1,1,1},3.1);
        for(int i=0;i<3;i++)m.StopDice(i,m.Dice.Round,3.2);m.Tick(4.41,7);
        Check(m.Dice.Round==2&&m.Dice.PendingMask==3&&!m.StopDice(2,2,4.5),"only equal highest memory dice reroll");
        m.StopDice(0,2,4.5);m.StopDice(1,2,4.5);m.Tick(5.71,7);
        Check(m.State.Winner==1&&m.State.Seat2.Rolls==1,"higher original dice group remains ahead after reroll; loser does not reroll");
        m=new DuelArrowMemory(new Faces(8,2));m.Begin(63,0);m.Tick(3,63);MemoryAnswer(m,new[]{0,0,1,1,2,2},3.1);
        Check(m.State.Phase==ArrowMemoryPhase.Results&&m.State.ActiveMask==15&&m.State.EliminatedMask==48&&m.Dice.ParticipantsMask==0,"only worst tied records drop; lower ranks never start dice");
        m.Tick(6.11,63);m.Tick(9.11,63);MemoryAnswer(m,new[]{0,0,1,1,0,0},9.2);
        Check(m.State.Phase==ArrowMemoryPhase.Results&&m.State.Round==2&&m.State.Length==4&&m.State.ActiveMask==3&&m.State.EliminatedMask==12,"lower tie elimination reaches final pair without restarting minigame");
        m.Tick(12.21,63);Check(m.State.Round==3&&m.State.Length==5&&m.State.ActiveMask==3,"final pair continues with increased sequence length");
    }
    void AllFailedTies()
    {
        foreach(int mask in new[]{3,63})
        foreach(int scenario in new[]{0,1,2,3,4,5})
        {
            var game=scenario<3?PartyMiniGame.Signal:scenario<5?PartyMiniGame.Greed:PartyMiniGame.Countdown;
            var c=new DuelClockContest(new Faces(8,7,6,5,4,3));c.Begin(mask,0,game);double now;
            if(scenario==0||scenario==2)
            {
                now=c.State.StartedAt+.1;
                for(int seat=0;seat<6;seat++)if(RouletteRules.Has(mask,seat)&&(scenario==0||seat==0))c.Stop(seat,c.State.Iteration,now,now);
                if(scenario==2){now=c.Deadline+.31;c.Tick(now,mask);}
            }
            else if(scenario==1){now=c.Deadline+.31;c.Tick(now,mask);}
            else if(scenario==3){now=c.GreedBurstAt+.91;c.Tick(now,mask);}
            else
            {
                now=c.State.StartedAt+(scenario==4?.001:c.State.Target/100.0);
                for(int seat=0;seat<6;seat++)if(RouletteRules.Has(mask,seat))c.Stop(seat,c.State.Iteration,now,now);
            }
            Check(c.State.Phase==ClockPhase.Dice&&c.State.Dice.ParticipantsMask==mask,game+" all-equal scenario "+scenario+" rolls every participant");
            now+=.1;for(int seat=0;seat<6;seat++)if(RouletteRules.Has(mask,seat))c.StopDice(seat,c.State.Iteration,c.State.Dice.Round,now);
            now+=1.21;c.Tick(now,mask);
            Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0&&c.State.Seat0.Rank==1&&c.State.EliminatedMask==0,game+" failed or successful tie awards highest die without life loss");
            c.Tick(now+3,mask);Check(c.State.Phase==ClockPhase.ItemPreparation,game+" tie winner gets normal item window");
            c.Tick(now+8,mask);Check(c.State.Phase==ClockPhase.Shooting&&c.State.First==0,game+" tie winner reaches shooting");
        }
        foreach(var game in new[]{PartyMiniGame.Signal,PartyMiniGame.Greed})
        {
            var d=ReadyDirector(DuelMode.PvP);var c=Field<DuelClockContest>(d,"clock");c.Begin(7,0,game);
            double now=(game==PartyMiniGame.Signal?c.Deadline:c.GreedBurstAt)+1;d.Tick(now);
            Check(d.Clock.Phase==ClockPhase.Dice,"director waits for all-failed "+game+" dice");
            int tries=0;while(d.Clock.Phase==ClockPhase.Dice&&tries++<20)
            {var ds=d.Clock.Dice;now+=.1;for(int seat=0;seat<3;seat++)if(ds.CanStop(seat))d.StopDice(seat,d.Clock.Iteration,ds.Round,now);now+=1.21;d.Tick(now);}
            int winner=d.Clock.First;Check(winner>=0&&d.Game.AliveMask==7&&d.ForSeat(0).LivesRemaining==3,"all-failed "+game+" has a living shooter and no penalty");
            d.Tick(now+3);d.Tick(now+8);
            Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(winner).Action==DuelAction.Aim&&d.ForSeat(winner).Ammo==1,"all-failed "+game+" shooter receives one ready round");
        }
        var timeout=ReadyDirector(DuelMode.PvP);var clock=Field<DuelClockContest>(timeout,"clock");clock.Begin(7,0);double end=clock.Deadline+.31;timeout.Tick(end);
        Check(timeout.Clock.Phase==ClockPhase.Ranking&&timeout.Clock.First==-1&&timeout.Clock.Dice.ParticipantsMask==0,"all countdown timeouts skip dice and shooter");
        Check(timeout.Game.AliveMask==0&&timeout.PartyAliveMask==7&&timeout.ForSeat(0).LivesRemaining==2&&timeout.ForSeat(1).LivesRemaining==2&&timeout.ForSeat(2).LivesRemaining==2,"all countdown timeouts lose exactly one life each");
        timeout.Tick(end+3);Check(timeout.Clock.Phase==ClockPhase.Returning,"all countdown timeouts skip item/shooting window");
        timeout.Tick(end+6.01);Check(timeout.Clock.Phase==ClockPhase.Selecting&&timeout.Game.AliveMask==7&&timeout.ForSeat(0).Health==100&&timeout.ForSeat(0).LivesRemaining==2,"timeout survivors return for next mini without another life loss");
    }
    DuelDirector ReadyDirector(DuelMode mode)
    {
        var d=new DuelDirector(mode,new System.Random(17));for(int i=0;i<3;i++)d.Join(i,0);d.SetAutoSelectItems(true);
        d.Ready(0,0);if(mode==DuelMode.PvP){d.Ready(1,0);d.Ready(2,0);}Call(d,"SetStage",DuelStage.Racing,0.0);return d;
    }
    void MemoryAnswer(DuelArrowMemory m,int[] errors,double now)
    {var state=m.State;for(int seat=0;seat<errors.Length;seat++)if(RouletteRules.Has(state.ActiveMask,seat))for(int i=0;i<state.Length;i++){int direction=DuelArrowMemory.DirectionAt(state.AnswerSeed,i);m.Input(seat,state.Round,i,(direction+(i<errors[seat]?1:0))%4,now);}}
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+160;yield return new WaitForSecondsRealtime(1);
        if(role=="model")Model();else if(role=="review"){Model();yield return Preview();}else if(role=="preview")yield return Preview();else if(role=="overhead")yield return OverheadPreview();else yield return Peers();Finish("PASS dice "+role+": "+checks+" checks");
    }
    IEnumerator KeyPress(Key key)
    {if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return null;}
    IEnumerator Peers()
    {
        bool allFailed=Array.IndexOf(Environment.GetCommandLineArgs(),"--dice-all-failed")>=0;
        bool memoryWinner=Array.IndexOf(Environment.GetCommandLineArgs(),"--dice-memory-winner")>=0;
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();if(role!="host")while(!Seen(role=="observer"?"client":"host",role=="observer"?"joined":"listening"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17974");while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==43,"protocol43");
        if(role=="client")Mark("joined");
        if(role=="host"){manager.ToggleDeveloperMode();manager.SetRoomAutoSelectItems(true);manager.SetRoomManualMiniGame(true);Mark("listening");}
        while(player.State.Value.ConnectedMask!=7||!player.State.Value.AutoSelectItems)yield return null;
        player.Rematch();while(player.State.Value.Stage!=DuelStage.Racing)yield return null;Mark("started");
        if(role=="host")while(!Seen("client","started")||!Seen("observer","started"))yield return null;
        for(int contestIndex=0;contestIndex<2;contestIndex++)
        {
            if(role=="host")
            {
                if(contestIndex==1){Call(D,"RestoreParty",Now,true);Call(D,"BeginPartySelection",Now);}
                if(memoryWinner&&contestIndex==0){var c=new DuelClockContest(new Faces(8,2,2));c.Select(7,Now,true);typeof(DuelDirector).GetField("clock",F).SetValue(D,c);}
                Check(D.ChooseMiniGame(contestIndex==0?(memoryWinner?PartyMiniGame.Memory:allFailed?PartyMiniGame.Greed:PartyMiniGame.Countdown):PartyMiniGame.QuickHands,D.Clock.Iteration,Now),"start minigame for tie");Publish();
                if(contestIndex==0)
                {
                    if(memoryWinner)
                    {
                        while(D.Clock.Memory.Phase!=ArrowMemoryPhase.Input)yield return null;
                        var m=D.Clock.Memory;var c=Field<DuelClockContest>(D,"clock");
                        for(int targetSeat=0;targetSeat<3;targetSeat++)for(int index=0;index<m.Length;index++)Check(D.InputMemory(targetSeat,D.Clock.Iteration,m.Round,index,(c.MemoryDirection(index)+(index==0?1:0))%4,Now),"server creates equal wrong memory records");Publish();
                    }
                    else if(allFailed){while(D.Clock.Phase==ClockPhase.Counting)yield return null;}
                    else{while(Now<D.Clock.StartedAt+.15)yield return null;double at=Now;
                    Check(D.StopClock(0,D.Clock.Iteration,at,at)&&D.StopClock(1,D.Clock.Iteration,at,at)&&D.StopClock(2,D.Clock.Iteration,at-.1,at),"server creates exact countdown tie");Publish();}
                }
                else
                {
                    while(D.Clock.Phase!=ClockPhase.QuickHands)yield return null;
                    var actions=Field<DuelActionState[]>(D,"actions");actions[1].LivesRemaining=1;
                    D.Game.Eliminate(6);Call(D,"LoseLife",1,DuelDeathCause.Gunshot,Now);Call(D,"LoseLife",2,DuelDeathCause.Gunshot,Now);
                    Field<DuelClockContest>(D,"clock").RecordQuickDeaths(6,1);Call(D,"FinishQuick",Now);Publish();
                }
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Dice)yield return null;yield return null;yield return null;
            var state=player.State.Value;int seat=player.Seat.Value;bool eligible=RouletteRules.Has(state.Clock.Dice.ParticipantsMask,seat);
            Check(state.Clock.Dice.ParticipantsMask==(contestIndex==0?(allFailed||memoryWinner?7:3):6),"same tie membership on every peer");
            Check(Field<VisualElement>(view,"diceLayer").style.display.value==DisplayStyle.Flex,"dice UI visible");
            Check(Field<Image>(view,"diceImage").style.display.value==(eligible?DisplayStyle.Flex:DisplayStyle.None),"only tied players see personal die");
            if(eligible)Check(Field<DuelDiceVisual>(view,"diceVisual").Texture!=null,"personal die has a render texture");
            int watched=-1;
            if(contestIndex==0)
            {
                for(int other=0;other<6;other++)if(other!=seat&&RouletteRules.Has(state.Clock.Dice.ParticipantsMask,other)){watched=other;break;}
                var direction=DuelGunplay.Point(watched,DuelHitPart.Head)-DuelGunplay.Eye(seat);DuelGunplay.Angles(direction,out float yaw,out float pitch);
                var fps=manager.GetComponent<DuelFirstPerson>();typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,yaw);typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,pitch);
                yield return new WaitForSecondsRealtime(.15f);
                Check(Field<VisualElement[]>(view,"seatDice")[watched].style.display.value==DisplayStyle.Flex&&Field<Image[]>(view,"seatDiceImages")[watched].image!=null,"other participant has animated overhead die on every peer");
                Check(Field<VisualElement[]>(view,"seatDice")[watched].pickingMode==PickingMode.Ignore,"public dice never capture input");
            }
            if(contestIndex==0&&role=="client")
            {
                yield return KeyPress(Key.Enter);Check(DuelChat.BlocksGameplay,"chat opened above dice");
                yield return KeyPress(Key.Space);Check(player.State.Value.Clock.Dice.CanStop(seat),"chat space cannot confirm dice");yield return KeyPress(Key.Escape);
            }
            if(contestIndex==1&&role=="client")
            {Check(state.Seat1.LivesRemaining==0&&eligible,"permanently eliminated owner can roll their tied death");yield return new WaitForSecondsRealtime(2.5f);}
            if(role=="host")
            {
                int seq=D.Sequence;manager.RequestStopDice(ulong.MaxValue,state.Round,state.Clock.Iteration,state.Clock.Dice.Round);
                manager.RequestStopDice(player.OwnerClientId,state.Round-1,state.Clock.Iteration,state.Clock.Dice.Round);
                Check(D.Sequence==seq,"unregistered and previous-game requests rejected");
            }
            int lastRound=0;
            while(player.State.Value.Clock.Phase==ClockPhase.Dice)
            {
                var current=player.State.Value.Clock;
                if(current.Dice.CanStop(seat)&&current.Dice.Round!=lastRound)
                {lastRound=current.Dice.Round;yield return KeyPress(Key.Space);}
                else yield return null;
            }
            state=player.State.Value;
            Check(state.Clock.Phase==(contestIndex==0?ClockPhase.Ranking:ClockPhase.QuickResults),"results only after dice comparison");
            if(contestIndex==0){yield return null;Check(Field<Label[]>(view,"seatDiceResults")[watched].text==state.Clock.Dice.ForSeat(watched).Face+" 확정","other player's confirmed overhead face matches server result");}
            if(memoryWinner&&contestIndex==0)
            {
                Check(state.Clock.Memory.Winner==state.Clock.First&&state.Clock.Memory.Round==1&&state.Clock.Memory.Length==3&&state.Clock.Dice.Round==1,"all equal memory records decide only first with no lower reroll on every peer");
                foreach(var label in Field<Label[]>(view,"clockRankingEntries"))if(label.style.display.value==DisplayStyle.Flex)Check(!label.text.Contains("2위")&&!label.text.Contains("3위"),"memory results show elimination instead of lower rankings");
            }
            else if(!allFailed||contestIndex==1)Check(state.Clock.ForSeat(contestIndex==0?2:0).Rank==(contestIndex==0?3:1),"untied score/death group rank preserved");
            else Check(state.Clock.First>=0&&state.Clock.ForSeat(state.Clock.First).Rank==1&&state.Clock.Seat0.Failure==MiniFailure.Burst&&state.Clock.Seat1.Failure==MiniFailure.Burst&&state.Clock.Seat2.Failure==MiniFailure.Burst,"all-burst dice winner becomes shooter on every peer");
            int a=contestIndex==0?0:1,b=a+1;var x=state.Clock.ForSeat(a);var y=state.Clock.ForSeat(b);
            Check(x.Dice>=1&&x.Dice<=8&&y.Dice>=1&&y.Dice<=8&&((memoryWinner||allFailed)&&contestIndex==0||x.Dice!=y.Dice),"valid final d8 values replicated");
            bool winnerOnly=(memoryWinner||allFailed)&&contestIndex==0;
            Check(winnerOnly&&x.Rolls!=y.Rolls?(x.Rolls>y.Rolls)==(x.Rank<y.Rank):x.Dice==y.Dice?x.Rank>1&&y.Rank>1:(x.Dice>y.Dice)==(x.Rank<y.Rank),"highest reroll group keeps priority; lower equal faces stay below winner");
            if(contestIndex==1)Check(state.Seat1.LivesRemaining==0&&state.Seat2.LivesRemaining==2,"dice never charges another life");
            Mark("result"+contestIndex);while(!Seen("host","result"+contestIndex)||!Seen("client","result"+contestIndex)||!Seen("observer","result"+contestIndex))yield return null;
            if((allFailed||memoryWinner)&&contestIndex==0)
            {
                while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;
                state=player.State.Value;Check(state.ForSeat(state.Clock.First).Action==DuelAction.Aim&&state.ForSeat(state.Clock.First).Ammo==1,"all-burst shooter receives one round on every peer");
                Check(state.AliveMask==7&&state.Seat0.LivesRemaining==3&&state.Seat1.LivesRemaining==3&&state.Seat2.LivesRemaining==3,"all-burst tie preserves all lives");
                Mark("shooting");while(!Seen("host","shooting")||!Seen("client","shooting")||!Seen("observer","shooting"))yield return null;
            }
        }
        Mark("verified");while(!Seen("host","verified")||!Seen("client","verified")||!Seen("observer","verified"))yield return null;
    }
    IEnumerator Preview()
    {
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();manager.Begin(DuelMode.PvP,false,"127.0.0.1","17975");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        D.SetAutoSelectItems(true);D.Join(1,Now);D.Join(2,Now);D.Ready(0,Now);D.Ready(1,Now);D.Ready(2,Now);Call(D,"SetStage",DuelStage.Racing,Now);
        var c=new DuelClockContest(new Faces(8,1));typeof(DuelDirector).GetField("clock",F).SetValue(D,c);c.Begin(7,Now-1);
        Field<FixedString64Bytes[]>(manager,"names")[1]=new FixedString64Bytes("player2");Field<FixedString64Bytes[]>(manager,"names")[2]=new FixedString64Bytes("player3");
        double sample=Now+.02;D.StopClock(0,D.Clock.Iteration,sample,sample);D.StopClock(1,D.Clock.Iteration,sample,sample);D.StopClock(2,D.Clock.Iteration,sample-.02,sample);Publish();
        yield return new WaitForSecondsRealtime(.4f);yield return Capture("Rolling");
        D.StopDice(0,D.Clock.Iteration,D.Clock.Dice.Round,Now);Publish();yield return new WaitForSecondsRealtime(.7f);yield return Capture("Stopped-8");
        Check(D.Clock.Dice.Seat0.Face==8,"preview locks numeric face8");D.StopDice(1,D.Clock.Iteration,D.Clock.Dice.Round,Now);Publish();
        yield return new WaitForSecondsRealtime(.5f);yield return Capture("Comparing");
        yield return new WaitForSecondsRealtime(1);Check(D.Clock.Phase==ClockPhase.Ranking,"preview completes ranking");
    }
    IEnumerator OverheadPreview()
    {
        manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();manager.Begin(DuelMode.PvP,false,"127.0.0.1","17975");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        D.SetAutoSelectItems(true);for(int i=1;i<6;i++)D.Join(i,Now);for(int i=0;i<6;i++)D.Ready(i,Now);Call(D,"SetStage",DuelStage.Racing,Now);
        for(int i=1;i<6;i++)Field<FixedString64Bytes[]>(manager,"names")[i]=new FixedString64Bytes("player"+(i+1));
        var c=new DuelClockContest(new Faces(6,3,8,2,7,1));typeof(DuelDirector).GetField("clock",F).SetValue(D,c);c.Begin(63,Now-1);
        double sample=Now;for(int i=0;i<6;i++)D.StopClock(i,D.Clock.Iteration,sample,sample);Publish();yield return new WaitForSecondsRealtime(.3f);
        manager.Table.enabled=false;manager.GetComponent<DuelFirstPerson>().enabled=false;
        Camera.main.transform.position=new Vector3(0,4.6f,-8);Camera.main.transform.LookAt(new Vector3(0,1,0));Camera.main.fieldOfView=64;
        yield return new WaitForSecondsRealtime(.3f);var badges=Field<VisualElement[]>(view,"seatDice");var labels=Field<Label[]>(view,"seatDiceResults");var visuals=Field<DuelDiceVisual[]>(view,"seatDiceVisuals");
        int visible=0,watched=-1;for(int i=1;i<6;i++)if(badges[i].style.display.value==DisplayStyle.Flex){visible++;watched=i;}
        Check(visible>=3,"several other participants show dice above nameplates");
        var die=Field<Transform>(visuals[watched],"die");var angle=die.localRotation;yield return Capture("Overhead-Rolling");yield return new WaitForSecondsRealtime(.2f);
        Check(Quaternion.Angle(angle,die.localRotation)>1,"overhead octahedron actually rotates");
        var root=view.GetComponent<UIDocument>().rootVisualElement;Check(root.IndexOf(Field<VisualElement>(view,"nameplateLayer"))<root.IndexOf(Field<VisualElement>(view,"diceLayer")),"public dice stay behind normal windows");
        for(int i=0;i<6;i++)D.StopDice(i,D.Clock.Iteration,D.Clock.Dice.Round,Now);Publish();yield return new WaitForSecondsRealtime(.7f);
        for(int i=1;i<6;i++)if(badges[i].style.display.value==DisplayStyle.Flex)Check(labels[i].text==D.Clock.Dice.ForSeat(i).Face+" 확정","visible overhead result uses authoritative face");
        yield return Capture("Overhead-Comparing");yield return new WaitForSecondsRealtime(.7f);Check(D.Clock.Phase==ClockPhase.Ranking,"public results continue into mini results");yield return Capture("Overhead-Results");
        yield return new WaitForSecondsRealtime(3.1f);for(int i=0;i<6;i++)Check(badges[i].style.display.value==DisplayStyle.None,"public dice hide after result window");
        manager.Leave();yield return null;yield return null;for(int i=0;i<6;i++)if(visuals[i]!=null)Check(!Field<Transform>(visuals[i],"rig").gameObject.activeSelf,"leaving table disables cached dice cameras");
    }
    IEnumerator Capture(string name)
    {
        const int w=1200,h=800;var doc=view.GetComponent<UIDocument>();var dice=Field<DuelDiceVisual>(view,"diceVisual");var cam=Field<Camera>(dice,"diceCamera");
        if(cam!=null&&cam.isActiveAndEnabled)UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=dice.Texture});
        foreach(var other in Field<DuelDiceVisual[]>(view,"seatDiceVisuals"))if(other!=null){var otherCam=Field<Camera>(other,"diceCamera");if(otherCam!=null&&otherCam.isActiveAndEnabled)UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(otherCam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=other.Texture});}
        var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.targetTexture=target;doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;
        doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;yield return new WaitForSecondsRealtime(.1f);
        var previous=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var background=new RenderTexture(w,h,24);background.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=background});
        RenderTexture.active=background;var image=new Texture2D(w,h,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
        var fg=ui.GetPixels();var bg=image.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);image.SetPixels(bg);image.Apply();File.WriteAllBytes(Path.Combine(path,name+".png"),image.EncodeToPNG());
        RenderTexture.active=previous;doc.panelSettings.targetTexture=null;target.Release();background.Release();Destroy(target);Destroy(background);Destroy(ui);Destroy(image);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL dice "+role+": timeout "+(player==null?"no owner":player.State.Value.Clock.Phase+" / "+player.State.Value.Clock.Dice.Phase),1);}
}
