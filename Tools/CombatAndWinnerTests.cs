using System;
using System.Collections.Generic;
using System.Reflection;
using HelloWorld;
class CombatAndWinnerTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    class Faces:Random{readonly Queue<int> faces;public Faces(params int[] values){faces=new Queue<int>(values);}public override int Next(int min,int max)=>min==1&&max==9&&faces.Count>0?faces.Dequeue():min;public override double NextDouble()=>.5;}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static void Phase(DuelDirector d,ClockPhase phase,double now,int first=2)
    {typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=phase,MiniGame=phase==ClockPhase.QuickHands?PartyMiniGame.QuickHands:PartyMiniGame.Countdown,First=first,ParticipantsMask=7,Iteration=1,PhaseAt=now,StartedAt=now});}
    static DuelDirector Party()
    {
        var d=new DuelDirector(DuelMode.PvP,new Faces());for(int i=0;i<3;i++)d.Join(i,0);d.SetAutoSelectItems(true);d.Game.SetShotTime(0);for(int i=0;i<3;i++)d.Ready(i,0);
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,1d});Phase(d,ClockPhase.QuickHands,10);return d;
    }
    static void Hit(DuelDirector d,double now)
    {var shot=new DuelProjectileVolley(1,d.ForSeat(1).Life,1,DuelWeapon.Taser,DuelGunplay.Eye(1),new AimVector(0,0,1),0,0,now);typeof(DuelDirector).GetMethod("ResolveProjectile",Hidden).Invoke(d,new object[]{shot,new DuelRayHit{Seat=0,Part=DuelHitPart.Body},now});}
    static bool Shock(DuelDirector d,double now)=>(bool)typeof(DuelDirector).GetMethod("TickShock",Hidden).Invoke(d,new object[]{now});
    static DuelClockContest Countdown(params double[] errors)
    {
        var c=new DuelClockContest(new Faces(8,3,3,6,4));int mask=(1<<errors.Length)-1;c.Begin(mask,0);double target=c.State.StartedAt+c.State.Target/100d;
        for(int i=0;i<errors.Length;i++)Check(c.Stop(i,c.State.Iteration,target+errors[i],target+errors[i]),"valid countdown stop");return c;
    }
    static void StopDice(DuelClockContest c,double now,int mask)
    {int round=c.State.Dice.Round;for(int i=0;i<6;i++)if(RouletteRules.Has(mask,i))Check(c.StopDice(i,c.State.Iteration,round,now),"candidate stops dice");c.Tick(now+DuelDiceContest.CompareDuration+.01,c.State.ParticipantsMask);}
    static void Main()
    {
        var d=Party();Hit(d,10);Check(d.ForSeat(0).Stunned&&d.ForSeat(0).Health==95,"taser applies five damage and shock");Check(!d.Look(0,0,0,false,10.1),"look blocked during shock");Shock(d,11.299);Check(d.ForSeat(0).Stunned&&d.ForSeat(0).IsStunned(11.299),"shock lasts through pre-deadline");
        var stale=d.ForSeat(0);Check(!stale.IsStunned(11.3),"stale snapshots expire visually at exactly 1.3 seconds");int sequence=d.Sequence;Shock(d,11.3);Check(!d.ForSeat(0).Stunned&&d.ForSeat(0).StunnedAt==-1&&d.Sequence>sequence,"server expiry publishes changed state");Check(d.Look(0,1,0,false,11.3),"control returns at deadline");
        var a=(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(d);a[0].Action=DuelAction.Aim;a[0].LoadedMask=63;int shots=a[0].ShotsFired;Shock(d,13);Shock(d,15);Check(d.ForSeat(0).ShotsFired==shots,"no involuntary fire after expiry even with ammo");
        Hit(d,20);Phase(d,ClockPhase.Returning,20.4);Shock(d,20.4);Check(!d.ForSeat(0).Stunned,"shooting end clears shock before 1.3 seconds");
        Phase(d,ClockPhase.Shooting,30);Hit(d,30);Shock(d,31.299);Check(d.ForSeat(0).Stunned,"shared shooting shock active");Shock(d,31.3);Check(!d.ForSeat(0).Stunned&&d.Look(0,2,0,false,31.3),"non-shooter recovers at 1.3 seconds");
        Phase(d,ClockPhase.QuickHands,40);Hit(d,40);Hit(d,41);Check(d.ForSeat(0).StunnedAt==41,"new hit has its own start time");Shock(d,42);Check(d.ForSeat(0).Stunned,"new hit can refresh effect");Shock(d,42.301);Check(!d.ForSeat(0).Stunned,"refreshed effect also expires");
        var locked=Party();var lockedActions=(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(locked);lockedActions[0].Action=DuelAction.Aim;lockedActions[0].LoadedMask=63;Hit(locked,50);
        var before=locked.ForSeat(0);foreach(double time in new[]{50.15,50.4,50.8,51.29})Shock(locked,time);
        Check(locked.ForSeat(0).ShotsFired==before.ShotsFired&&locked.ForSeat(0).Ammo==before.Ammo&&locked.ForSeat(0).AimYaw==before.AimYaw&&locked.ForSeat(0).AimPitch==before.AimPitch,"stun never moves server aim or consumes loaded ammunition");
        Check(!locked.Fire(0,0,0,false,locked.ForSeat(0).Version,51,51),"manual fire is blocked during shock");Shock(locked,51.301);Check(!locked.ForSeat(0).Stunned,"armed player recovers after 1.3 seconds");
        var unique=Countdown(0,.2,.2);Check(unique.State.Phase==ClockPhase.Ranking&&unique.State.First==0,"unique countdown winner skips lower tie");
        var top=Countdown(0,0,.4);Check(top.State.Phase==ClockPhase.Dice&&top.State.Dice.PendingMask==3,"only first-place countdown tie rolls");StopDice(top,top.State.PhaseAt+.1,3);Check(top.State.Phase==ClockPhase.Ranking&&top.State.First==0,"top dice chooses shooter");
        var lowerDice=Countdown(0,0,0,.5);StopDice(lowerDice,lowerDice.State.PhaseAt+.1,7);Check(lowerDice.State.Phase==ClockPhase.Ranking&&lowerDice.State.First==0,"lower equal dice never reroll after unique highest face");
        var reroll=new DuelDiceContest(new Faces(8,8,3,6,4));reroll.Begin(new List<List<int>>{new List<int>{0,1,2},new List<int>{3,4}},0,true);int round=reroll.State.Round;for(int i=0;i<3;i++)reroll.Stop(i,round,.1);reroll.Tick(1.31,31);Check(reroll.State.Phase==DuelDicePhase.Rolling&&reroll.State.PendingMask==3,"only tied highest dice reroll");reroll.Stop(0,reroll.State.Round,1.4);reroll.Stop(1,reroll.State.Round,1.4);reroll.Tick(2.61,31);Check(reroll.State.Phase==DuelDicePhase.Finished&&reroll.Order[0]==0,"finish as soon as first place known");
        var gone=new DuelDiceContest(new Faces());gone.Begin(new List<List<int>>{new List<int>{0,1},new List<int>{2,3}},0,true);gone.Tick(.1,14);Check(gone.State.Phase==DuelDicePhase.Finished&&gone.Order[0]==1,"top contender disconnect resolves winner immediately");
        foreach(var game in new[]{PartyMiniGame.Signal,PartyMiniGame.Greed})
        {var c=new DuelClockContest(new Random(4));c.Begin(7,0,game);var s=c.State;s.Set(0,new ClockEntry{Centiseconds=1,Error=game==PartyMiniGame.Greed?-200:10});s.Set(1,new ClockEntry{Centiseconds=2,Error=game==PartyMiniGame.Greed?-100:20});s.Set(2,s.ForSeat(1));typeof(DuelClockContest).GetProperty("State").SetValue(c,s);typeof(DuelClockContest).GetMethod("Resolve",Hidden).Invoke(c,new object[]{3d});Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0,"unique winner skips lower ties in "+game);}
        var timeout=new DuelClockContest(new Faces());timeout.Begin(7,0);timeout.Tick(timeout.Deadline+.31,7);Check(timeout.State.Phase==ClockPhase.Ranking&&timeout.State.First==-1&&timeout.State.EliminatedMask==7,"all countdown timeouts keep life-loss and no shooter");
        var quick=new DuelClockContest(new Faces());typeof(DuelClockContest).GetProperty("State").SetValue(quick,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=15,First=-1});quick.RecordQuickDeaths(7,1);Check(!quick.HasQuickTies,"surviving winner makes lower death ties irrelevant");quick.FinishQuick(3,0,1);Check(quick.State.Phase==ClockPhase.QuickResults&&quick.State.First==3&&quick.State.Dice.ParticipantsMask==0,"quick hands skips lower death dice");
        var dead=new DuelClockContest(new Faces(8,4));typeof(DuelClockContest).GetProperty("State").SetValue(dead,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=3,First=-1});dead.RecordQuickDeaths(3,0);Check(dead.HasQuickTies,"simultaneous top death has no unique winner");dead.FinishQuick(-1,0,1);Check(dead.State.Phase==ClockPhase.Dice&&dead.State.Dice.PendingMask==3,"unresolved quick winner still rolls");StopDice(dead,1.1,3);Check(dead.State.Phase==ClockPhase.QuickResults&&dead.State.First==0,"quick top tie winner retained");
        var memory=new DuelArrowMemory(new Faces());memory.Begin(7,0);memory.Tick(3,7);int seed=memory.State.AnswerSeed;
        for(int seat=0;seat<3;seat++)for(int i=0;i<3;i++)Check(memory.Input(seat,memory.State.Round,i,(DuelArrowMemory.DirectionAt(seed,i)+(seat==0?0:1))%4,3.1),"memory input");
        Check(memory.State.Phase==ArrowMemoryPhase.Finished&&memory.State.Winner==0&&memory.Dice.Phase!=DuelDicePhase.Rolling,"unique memory winner skips lower mistakes tie");
        Console.WriteLine("PASS taser duration and winner-only dice: "+checks+" checks");
    }
}
