using System;
using HelloWorld;
public static class ClockContestTests
{
    static int checks;
    static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
    sealed class FixedRandom:Random
    {public override int Next(int min,int max)=>min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    static DuelDirector Director(int count=3,DuelMode mode=DuelMode.PvP,AiDifficulty difficulty=AiDifficulty.Normal,float shotLimit=4)
    {
        var d=new DuelDirector(mode,mode==DuelMode.AI?new Random(count+(int)difficulty):new FixedRandom(),difficulty);
        for(int i=0;i<count;i++)d.Join(i,0);
        d.Game.SetShotTime(shotLimit);
        for(int i=0;i<count;i++)d.Ready(i,0);
        d.Tick(3);
        Check(d.Clock.Phase==ClockPhase.Selecting,"party selection precedes every mini-game");
        Check(!d.StopClock(0,d.Clock.Iteration,3,3),"cannot stop during selection");
        d.Tick(3+PartyGames.SelectionDuration+.01);return d;
    }
    static double Rank(DuelDirector d)
    {
        if(d.Clock.Phase==ClockPhase.Selecting)d.Tick(d.Clock.PhaseAt+PartyGames.SelectionDuration+.01);
        var c=d.Clock;double at=c.StartedAt+c.Target/100.0;
        for(int i=0;i<6;i++)if(RouletteRules.Has(d.Game.AliveMask,i))
            Check(d.StopClock(i,c.Iteration,at+i*.1,at+i*.1),$"rank stop accepted seat {i} phase {d.Clock.Phase} iteration {c.Iteration}");
        Check(d.Clock.Phase==ClockPhase.Ranking,"ranking resolved");double ranked=d.Clock.PhaseAt;
        d.Tick(ranked+DuelClockContest.RevealDuration);Check(d.Clock.Phase==ClockPhase.ItemPreparation,"item preparation follows ranking");
        return ranked+DuelClockContest.ItemPreparationDuration;
    }
    static void Shot(DuelDirector d,int actor,int target,DuelHitPart part,double now)
    {
        DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(actor),out float yaw,out float pitch);
        DuelGunplay.Sway(actor,true,now,now,out float sy,out float sp);
        Check(d.Fire(actor,yaw-sy,pitch-sp,true,d.ForSeat(actor).Version,now,now),"shot accepted");
        d.Tick(now+.3);
    }
    static void Main()
    {
        var rng=new Random(732);var seen=new bool[9];
        var c=new DuelClockContest(rng);
        for(int i=0;i<500;i++)
        {c.Begin(63,i*30);Check(c.State.Target>=700&&c.State.Target<=1500&&c.State.Target%100==0,"7-15 inclusive target");seen[c.State.Target/100-7]=true;Check(c.State.HideAfter>=2.4&&c.State.HideAfter<2.9&&c.State.HideAfter<3,"hide around before three");}
        foreach(bool value in seen)Check(value,"every target reachable");
        Check(DuelClockContest.Quantize(10.004)==1000&&DuelClockContest.Quantize(10.006)==1001,"two decimals rounding");
        c.Begin(7,0);double start=c.State.StartedAt,goal=start+c.State.Target/100.0;int id=c.State.Iteration;
        Check(!c.Stop(0,id,double.NaN,goal)&&!c.Stop(0,id,double.PositiveInfinity,goal),"invalid timestamp");
        Check(!c.Stop(0,id,start-.01,start)&&!c.Stop(-1,id,goal,goal)&&!c.Stop(6,id,goal,goal),"seat and prestart validation");
        Check(!c.Stop(0,id-1,goal,goal)&&!c.Stop(0,id,goal,goal+.31)&&!c.Stop(0,id,goal+.06,goal),"stale and sample window");
        Check(c.Stop(0,id,goal-.2,goal-.2),"first stop");Check(!c.Stop(0,id,goal,goal),"duplicate ignored");
        Check(c.PublicState().Seat0.Centiseconds==-1&&c.PublicState().Seat0.Rank==0,"stop remains private");
        Check(c.Stop(1,id,goal+.2,goal+.2)&&c.Stop(2,id,goal+.7,goal+.7),"others stop");
        Check(c.State.Seat0.Error==20&&c.State.Seat1.Error==20&&c.State.Seat2.Error==70,"absolute rounded errors");
        Check(c.State.Seat0.Dice!=c.State.Seat1.Dice&&c.State.Seat0.Rank!=c.State.Seat1.Rank&&c.State.Last==2,"dice breaks equal error");
        c=new DuelClockContest(new FixedRandom());c.Begin(63,0);goal=c.State.StartedAt+7;
        for(int i=0;i<6;i++)Check(c.Stop(i,c.State.Iteration,goal,goal),"six equal stops");
        int faces=0,ranks=0;for(int i=0;i<6;i++){var e=c.State.ForSeat(i);faces|=1<<e.Dice;ranks|=1<<e.Rank;Check(e.Rolls==33,"bounded pathological RNG reroll");}
        Check(faces==126&&ranks==126,"six unique dice and ranks");
        c.Begin(7,20);goal=c.State.StartedAt+7;c.Stop(0,c.State.Iteration,goal,goal);c.Stop(1,c.State.Iteration,goal+.1,goal+.1);
        Check(c.Deadline==goal+2,"target plus two deadline");
        Check(!c.Stop(2,c.State.Iteration,c.Deadline+.001,c.Deadline+.001),"no input after plus two");
        Check(!c.Tick(c.Deadline+.29,7)&&c.Tick(c.Deadline+.31,7),"deadline with network grace");
        Check(c.State.First==0&&c.State.Last==1&&c.State.EliminatedMask==4&&c.State.ForSeat(2).Rank==0,"DNF excluded from shot ranking");
        double reveal=c.State.PhaseAt;Check(!c.Tick(reveal+DuelClockContest.RevealDuration-.01,7)&&c.Tick(reveal+DuelClockContest.RevealDuration,7)&&c.State.Phase==ClockPhase.ItemPreparation,"full result reveal then item window");
        Check(!c.Tick(reveal+DuelClockContest.RevealDuration+DuelClockContest.ItemPreparationDuration-.01,7)&&c.Tick(reveal+DuelClockContest.RevealDuration+DuelClockContest.ItemPreparationDuration,7)&&c.State.Phase==ClockPhase.Shooting,"full five second item preparation before shooting");
        Check(new RouletteRules(DuelMode.PvP).ShotTimeLimit==10&&new RouletteRules(DuelMode.AI).ShotTimeLimit==10,"AI and PvP default shooting limit is ten seconds");
        Check(!c.Tick(reveal+DuelClockContest.RevealDuration+DuelClockContest.ItemPreparationDuration+9.99,7)&&c.State.Phase==ClockPhase.Shooting,"shooting remains available before ten seconds");
        Check(c.Tick(reveal+DuelClockContest.RevealDuration+DuelClockContest.ItemPreparationDuration+10,7)&&c.State.Phase==ClockPhase.Returning,"ten second shot timeout after item window");
        c.Begin(7,50);goal=c.State.StartedAt+7;c.Stop(0,c.State.Iteration,goal,goal);c.Stop(1,c.State.Iteration,goal+.1,goal+.1);
        Check(c.Tick(goal+.2,3)&&c.State.Phase==ClockPhase.Ranking&&c.State.Last==1,"disconnect drops absent participant");
        Check(c.Tick(goal+.3,1)&&c.State.Phase==ClockPhase.Returning,"target leaves cancel shot");
        var d=Director();Check(!d.Input(0,1,d.ForSeat(0).Version,4)&&!d.SelectWeapon(0,DuelWeapon.SawedOff,4),"shared fixed gun no load commands");
        double ranked=Rank(d);Check(d.Clock.First==0&&d.Clock.Last==2,"first last selection");
        Check(d.ForSeat(0).ClockWins==1&&d.ForSeat(0).ClockStops==1&&d.ForSeat(2).ClockErrorTotal==20,"clock stats recorded once");
        Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,ranked,ranked),"no shooting during reveal");
        d.Tick(ranked+DuelClockContest.RevealDuration);Check(d.ForSeat(0).Action==DuelAction.Aim&&d.ForSeat(1).Action==DuelAction.Pickup,"only first gets aim");
        Check(!d.Fire(1,60,0,true,d.ForSeat(1).Version,ranked+DuelClockContest.RevealDuration,ranked+DuelClockContest.RevealDuration),"other cannot shoot");
        Shot(d,0,1,DuelHitPart.Head,ranked+DuelClockContest.RevealDuration+.1);Check(d.Game.Shots==1&&d.Game.LastDamage==100&&d.Game.AliveMask==5&&d.Clock.Phase==ClockPhase.Returning,"shooter may hit any living opponent, not only last place");
        Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,ranked+DuelClockContest.RevealDuration+.2,ranked+DuelClockContest.RevealDuration+.2),"one shot only");
        d.Tick(ranked+DuelClockContest.RevealDuration+DuelClockContest.ReturnDuration+.42);Check(d.Clock.Iteration==2&&d.Clock.Phase==ClockPhase.Selecting,"select again after miss");
        Check(d.Clock.MiniGame==PartyMiniGame.QuickHands,"previous countdown excluded on next selection");
        d=Director();ranked=Rank(d);d.Tick(ranked+DuelClockContest.RevealDuration);Shot(d,0,2,DuelHitPart.Body,ranked+DuelClockContest.RevealDuration+.1);Check(d.Game.HealthForSeat(2)==50&&d.Game.AliveMask==7&&d.ForSeat(2).LivesRemaining==3,"body hit nonfatal without life loss");
        d.Tick(ranked+DuelClockContest.RevealDuration+DuelClockContest.ReturnDuration+.42);Check(d.Game.HealthForSeat(2)==50,"living countdown survivor preserves health");
        d=Director();ranked=Rank(d);d.Tick(ranked+DuelClockContest.RevealDuration);Shot(d,0,2,DuelHitPart.Head,ranked+DuelClockContest.RevealDuration+.1);Check(d.Game.AliveMask==3&&d.PartyAliveMask==7&&d.ForSeat(2).LivesRemaining==2&&d.ForSeat(0).Kills==1,"clock fatal hit consumes exactly one life");
        d.Tick(ranked+DuelClockContest.RevealDuration+DuelClockContest.ReturnDuration+.42);Check(d.Game.AliveMask==7&&d.ForSeat(2).Health==100&&d.ForSeat(2).DiedAt<0,"clock victim returns for next mini-game");
        d=Director();ranked=Rank(d);d.Tick(ranked+DuelClockContest.RevealDuration);int version=d.ForSeat(0).Version;d.Disconnect(1,ranked+DuelClockContest.RevealDuration+.1);d.Tick(ranked+DuelClockContest.RevealDuration+.1);Check(d.ForSeat(0).Version==version,"unrelated disconnect does not regrant gun");
        foreach(int leaving in new[]{0,2})
        {d=Director();ranked=Rank(d);d.Disconnect(leaving,ranked);d.Tick(ranked);Check(leaving==0?d.Clock.Phase==ClockPhase.Returning:d.Clock.Phase==ClockPhase.ItemPreparation,"shooter disconnect cancels, other disconnect keeps opportunity with remaining target");}
        d=Director();ranked=Rank(d);d.Tick(ranked+7);Check(d.Clock.Phase==ClockPhase.Shooting,"late tick grants full shot window");
        Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,ranked+11,ranked+11),"fire rejected at exact deadline");d.Tick(ranked+11);d.Tick(ranked+14.01);Check(d.Clock.Iteration==2,"timeout cycles");
        d=Director(shotLimit:0);ranked=Rank(d);d.Tick(ranked+DuelClockContest.RevealDuration);d.Tick(ranked+100);
        Check(d.Clock.Phase==ClockPhase.Shooting,"unlimited clock opportunity never times out");Shot(d,0,2,DuelHitPart.Body,ranked+100);
        Check(d.Game.LastDamage==50,"unlimited clock shot still damages");
        d=Director(shotLimit:1);ranked=Rank(d);d.Tick(ranked+DuelClockContest.RevealDuration);d.Tick(ranked+DuelClockContest.RevealDuration+1);
        Check(d.Clock.Phase==ClockPhase.Returning,"custom clock opportunity expires at configured limit");
        d=Director(4);var state0=d.Clock;double finish=state0.StartedAt+state0.Target/100.0+2;
        d.StopClock(0,state0.Iteration,finish-2,finish-2);d.StopClock(2,state0.Iteration,finish-1.9,finish-1.9);d.Tick(finish+.31);
        Check(d.Game.AliveMask==5&&d.Game.ConnectedMask==15&&d.Game.Shots==0,"timeouts preserve connection without fake shots");
        Check(d.ForSeat(1).DiedAt>=0&&d.ForSeat(3).DeathCause==DuelDeathCause.ClockTimeout&&d.ForSeat(1).Health==0,"all late players fall with timeout cause");
        Check(d.Clock.First==0&&d.Clock.Last==2&&d.Clock.EliminatedMask==10,"only stopped survivors ranked");
        d.Tick(finish+.4);Check(d.Clock.ParticipantsMask==15,"timeout rows retained for reveal");
        d.Tick(finish+DuelClockContest.RevealDuration+.32);Check(d.Clock.Phase==ClockPhase.ItemPreparation,"qualified players prepare items");d.Tick(finish+DuelClockContest.RevealDuration+DuelClockContest.ItemPreparationDuration+.33);Check(d.ForSeat(0).Action==DuelAction.Aim,"qualified first shoots qualified last");
        d=Director();state0=d.Clock;finish=state0.StartedAt+state0.Target/100.0+2;d.StopClock(1,state0.Iteration,finish-2,finish-2);d.Tick(finish+.31);
        Check(d.Game.Phase==DuelPhase.Playing&&d.Game.AliveMask==2&&d.PartyAliveMask==7&&d.ForSeat(0).LivesRemaining==2&&d.Game.Shots==0,"one stopped participant does not prematurely end a three-life party");
        d=Director();state0=d.Clock;finish=state0.StartedAt+state0.Target/100.0+2;d.Tick(finish+.31);
        Check(d.Game.Phase==DuelPhase.Playing&&d.Game.AliveMask==0&&d.PartyAliveMask==7&&d.ForSeat(0).LivesRemaining==2,"all missing stop lose one life atomically and can continue");
        Check(d.ForSeat(0).Kills==0&&d.ForSeat(1).Kills==0,"no fabricated timeout kills");
        d.Tick(finish+DuelClockContest.RevealDuration+.32);d.Tick(d.Clock.PhaseAt+3.01);Check(d.Game.AliveMask==7&&d.Clock.Phase==ClockPhase.Selecting,"all timeout victims recover before next selection");
        c=new DuelClockContest(new FixedRandom());c.Select(7,0);Check(!c.Tick(PartyGames.SelectionDuration-.01,7),"selection holds until common server deadline");Check(c.Tick(PartyGames.SelectionDuration+.01,7)&&c.State.Iteration==1&&c.State.MiniGame==PartyMiniGame.Countdown,"selection transitions to supported game");
        Check(PartyGames.Count==5&&PartyGames.Name(PartyGames.At(2))=="신호등"&&PartyGames.Name(PartyGames.At(3))=="욕심의 대가","all five mini-games in catalogue");
        foreach(AiDifficulty difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})for(int count=2;count<=6;count++)
        {
            d=Director(count,DuelMode.AI,difficulty);int stopped=0;
            for(double now=3;now<5400&&d.Game.Phase==DuelPhase.Playing;now+=.02)
            {var state=d.Clock;if(state.Phase==ClockPhase.Counting&&stopped!=state.Iteration&&now>=state.StartedAt+state.Target/100.0+2.1){d.StopClock(0,state.Iteration,now,now);stopped=state.Iteration;}d.Tick(now);}
            Check(d.Game.Phase==DuelPhase.Finished&&d.Game.Winner>0,"AI 1-5 contestants difficulty plays to completion "+difficulty+"/"+count+" phase "+d.Game.Phase+" alive "+d.Game.AliveMask+" party "+d.PartyAliveMask+" clock "+d.Clock.Phase+" shots "+d.Game.Shots);
        }
        NewMiniGameTests.Validate();ItemRulesTests.Validate();PartyIntegrationTests.Validate();Console.WriteLine("PASS countdown: "+checks+" checks");
    }
}
