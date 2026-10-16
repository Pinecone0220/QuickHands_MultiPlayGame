using System;
using HelloWorld;
public static class NewMiniGameTests
{
    static int checks;
    static void Check(bool v,string text){checks++;if(!v)throw new Exception(text);}
    sealed class FixedRandom:Random{readonly int first;public FixedRandom(int first=0){this.first=first;}public override int Next(int min,int max)=>min==0&&max==PartyGames.Count?first:min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    static DuelDirector Director(PartyMiniGame game,int count=3)
    {var d=new DuelDirector(DuelMode.PvP,new FixedRandom((int)game));for(int i=0;i<count;i++)d.Join(i,0);for(int i=0;i<count;i++)d.Ready(i,0);d.Tick(3);Check(d.Clock.MiniGame==game,"new game is selectable from same roulette");d.Tick(3+PartyGames.SelectionDuration+.01);Check(d.Clock.Phase==ClockPhase.Counting,"new game begins after selection");return d;}
    public static void Validate()
    {
        var c=new DuelClockContest(new FixedRandom());c.Begin(7,0,PartyMiniGame.Signal);int id=c.State.Iteration;double start=c.State.StartedAt,go=c.ReactionGoAt;
        Check(go>=start+2.5&&go<start+6.01,"unpredictable true signal window");Check(c.PublicState().Signal!=MiniSignal.Go&&c.PublicState().SignalAt==0&&c.PublicState().Target==0,"upcoming true signal never published");
        c.Tick(start+.65,7);Check(c.State.Signal==MiniSignal.Fake,"fake cue displays first");Check(c.Stop(2,id,start+.65,start+.65)&&c.State.Seat2.Failure==MiniFailure.EarlyInput,"fake input fails once");Check(!c.Stop(2,id,go+.01,go+.01),"failed player cannot retry");
        Check(c.PublicState().Seat2.Failure==MiniFailure.None&&c.PublicState().Seat2.Centiseconds==-1,"other players cannot see false-start status");
        c.Tick(go,7);Check(c.State.Signal==MiniSignal.Go&&c.State.SignalAt==go,"true cue timestamp only revealed when active");
        Check(!c.Stop(0,id,double.NaN,go)&&!c.Stop(0,id,go-.31,go)&&!c.Stop(0,id,go+.051,go)&&!c.Stop(0,id-1,go,go),"invalid timestamp and old iteration refused");
        Check(c.Stop(1,id,go+.24,go+.24)&&c.Stop(0,id,go+.12,go+.12),"valid reaction samples accepted");Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0&&c.State.Last==2&&c.State.Seat0.Centiseconds==12&&c.State.EliminatedMask==0,"fastest reaction earns gun, mistakes never consume lives");
        c.Begin(63,20,PartyMiniGame.Signal);go=c.ReactionGoAt;id=c.State.Iteration;for(int i=0;i<6;i++)Check(c.Stop(i,id,go+.2,go+.2),"six equal reactions accepted");int ranks=0,faces=0;for(int i=0;i<6;i++){ranks|=1<<c.State.ForSeat(i).Rank;faces|=1<<c.State.ForSeat(i).Dice;}Check(ranks==126&&faces==126,"equal reactions get unique dice rankings");
        c.Begin(7,40,PartyMiniGame.Signal);c.Tick(c.Deadline+.31,7);Check(c.State.First==-1&&c.State.Seat0.Failure==MiniFailure.MissedSignal&&c.State.EliminatedMask==0,"all missed cues skip shooting without death");c.Tick(c.State.PhaseAt+6,7);Check(c.State.Phase==ClockPhase.Returning,"all failures return to selection");
        c.Begin(7,60,PartyMiniGame.Greed);id=c.State.Iteration;start=c.State.StartedAt;double burst=c.GreedBurstAt;
        Check(start==63&&!c.Stop(0,id,start-.01,start-.01),"three-second countdown rejects early banking");
        Check(c.ScoreAt(start-1)==0&&c.ScoreAt(start)==0,"score remains zero before start");
        Check(c.ScoreAt(start+.26)==10&&c.ScoreAt(start+1.26)==50&&c.ScoreAt(start+2.26)==90,"score increases forty points each second");
        Check(c.ScoreAt(start+.26)<c.ScoreAt(start+.31),"score advances between old half-second updates");
        Check(burst>=start+4&&burst<=start+9.5&&c.PublicState().SignalAt==0,"crash time remains server secret");c.Tick(start+2.01,7);int score=c.State.Points;Check(score==80,"score uses common clock rather than frame count");
        Check(c.Stop(0,id,start+2,start+2)&&c.State.Seat0.Points==score,"bank score comes from server history");Check(!c.Stop(0,id,start+3,start+3),"bank cannot change afterward");Check(c.PublicState().Seat0.Points==-1,"banked peer score private before results");
        Check(c.Stop(1,id,start+3.5,start+3.5),"higher score can be banked");c.Tick(burst+.01,7);Check(c.State.Signal==MiniSignal.Burst&&c.State.Phase==ClockPhase.Counting,"crash visual before results");c.Tick(burst+.91,7);
        Check(c.State.First==1&&c.State.Seat2.Points==0&&c.State.Seat2.Failure==MiniFailure.Burst&&c.State.EliminatedMask==0,"highest banked score wins, crash zeros unbanked player");
        c.Begin(3,80,PartyMiniGame.Greed);id=c.State.Iteration;burst=c.GreedBurstAt;c.Tick(burst+.01,3);Check(c.Stop(0,id,burst-.01,burst+.1),"pre-crash bank may arrive after crash inside network grace");Check(c.Stop(1,id,burst,burst+.11)&&c.State.Seat1.Failure==MiniFailure.Burst,"bank at crash threshold fails");
        c.Begin(3,100,PartyMiniGame.Greed);c.Tick(c.GreedBurstAt+.91,3);Check(c.State.First==-1&&c.State.Seat0.Points==0&&c.State.Seat1.Failure==MiniFailure.Burst,"all greedy players fail, no fake winner");
        c.Begin(3,120,PartyMiniGame.Greed);id=c.State.Iteration;start=c.State.StartedAt;Check(c.Stop(0,id,start+2,start+2)&&c.Stop(1,id,start+2,start+2)&&c.State.Seat0.Dice!=c.State.Seat1.Dice,"equal banked scores resolved by dice");
        foreach(var game in new[]{PartyMiniGame.Signal,PartyMiniGame.Greed})
        {
            var d=Director(game);var state=d.Clock;double a=game==PartyMiniGame.Signal?state.StartedAt+2.62:state.StartedAt+2;
            Check(d.StopClock(0,state.Iteration,a,a),"winner submits action");double b=game==PartyMiniGame.Signal?a+.2:a+.5;Check(d.StopClock(1,state.Iteration,b,b),"opponent submits action");double now=game==PartyMiniGame.Signal?state.StartedAt+4.81:state.StartedAt+4.91;d.Tick(now);
            Check(d.Clock.Phase==ClockPhase.Ranking&&d.Game.AliveMask==7&&d.ForSeat(2).LivesRemaining==3,"mini failure retains HP and lives");Check(d.ForSeat(0).ClockWins==0&&d.ForSeat(0).ClockStops==0,"new-game results do not pollute countdown stats");
            double ranked=d.Clock.PhaseAt;
            d.Tick(ranked+2.99);Check(d.Clock.Phase==ClockPhase.Ranking,"mini results remain until three seconds");
            d.Tick(ranked+3);Check(d.Clock.Phase==ClockPhase.ItemPreparation,"mini results end at exactly three seconds");
            d.Tick(ranked+DuelClockContest.RevealFor(game)+.01);Check(d.Clock.Phase==ClockPhase.ItemPreparation,"new mini results reveal then items");var shotSeat=d.Clock.First;d.Tick(d.Clock.StartedAt+.01);Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(shotSeat).Ammo==1,"winner granted one loaded shared gun");
            int victim=2;now=d.Clock.PhaseAt+.1;DuelGunplay.Angles(DuelGunplay.Point(victim,DuelHitPart.Head)-DuelGunplay.Eye(shotSeat),out float y,out float p);DuelGunplay.Sway(shotSeat,DuelWeapon.Revolver,true,now,now,out float sy,out float sp);
            Check(!d.Fire(victim,0,0,true,d.ForSeat(victim).Version,now,now),"others cannot shoot");Check(d.Fire(shotSeat,y-sy,p-sp,true,d.ForSeat(shotSeat).Version,now,now),"winner may freely choose living opponent");d.Tick(now+.3);Check(d.ForSeat(victim).LivesRemaining==2&&d.Clock.Phase==ClockPhase.Returning,"actual shot uses existing life loss");d.Tick(d.Clock.PhaseAt+3.01);Check(d.Clock.Phase==ClockPhase.Selecting&&d.Clock.MiniGame!=game&&d.Game.AliveMask==7,"revival and previous-game exclusion preserved");
            d=Director(game);d.Disconnect(2,d.Clock.StartedAt+.1);Check(d.PartyAliveMask==3,"new mini disconnect removes membership");
        }
        int seen=0;var rng=new Random(149);PartyMiniGame? previous=null;for(int i=0;i<600;i++){var game=PartyGames.Choose(rng,previous);Check(game!=previous,"never repeat immediate previous among five candidates");seen|=1<<(int)game;previous=game;}Check(seen==31,"all five candidates remain reachable");
        Console.WriteLine("PASS signal and greed: "+checks+" checks");
    }
}
