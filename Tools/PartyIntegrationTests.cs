using System;
using HelloWorld;
public static class PartyIntegrationTests
{
    static int checks;
    static void Check(bool v,string t){checks++;if(!v)throw new Exception(t);}
    sealed class FixedRandom:Random
    {public override int Next(int min,int max)=>min==0&&max==PartyGames.Count?1:min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    static DuelDirector New(int count=2)
    {var d=new DuelDirector(DuelMode.PvP,new FixedRandom());for(int i=0;i<count;i++)d.Join(i,0);d.Game.SetShotTime(0);for(int i=0;i<count;i++)d.Ready(i,0);d.Tick(3);return d;}
    static double Start(DuelDirector d)
    {
        Check(d.Clock.Phase==ClockPhase.Selecting,"roulette precedes every mini-game");d.Tick(d.Clock.PhaseAt+PartyGames.SelectionDuration+.01);
        if(d.Clock.MiniGame==PartyMiniGame.QuickHands)
        {
            Check(d.Clock.Phase==ClockPhase.ItemPreparation,"quick items before start");var a=d.ForSeat(0);double end=d.Clock.StartedAt;
            Check(!d.Input(0,a.Digit(0),a.Version,end-.1)&&!d.Fire(0,0,0,true,a.Version,end-.1,end-.1),"preparation cannot load or shoot");
            d.Tick(end+.01);Check(d.Clock.Phase==ClockPhase.QuickCountdown,"separate countdown");d.Tick(d.Clock.StartedAt+.01);Check(d.QuickHandsActive,"quick starts");
        }
        return d.Clock.StartedAt+.1;
    }
    static void Load(DuelDirector d,int seat,double now)
    {int n=0;while(d.ForSeat(seat).Action!=DuelAction.Aim&&n++<50){var a=d.ForSeat(seat);Check(d.Input(seat,a.Digit(a.Progress),a.Version,now),"load input");}Check(d.ForSeat(seat).Action==DuelAction.Aim,"loaded");}
    static void Launch(DuelDirector d,int actor,int target,DuelHitPart part,double now)
    {DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(actor),out float y,out float p);DuelGunplay.Sway(actor,d.ForSeat(actor).Weapon,true,now,now,out float sy,out float sp);Check(d.Fire(actor,y-sy,p-sp,true,d.ForSeat(actor).Version,now,now),"authoritative shot");}
    static double Rank(DuelDirector d,int timeout=0)
    {var c=d.Clock;double goal=c.StartedAt+c.Target/100.0;for(int i=0;i<6;i++)if(RouletteRules.Has(d.Game.AliveMask&~timeout,i))Check(d.StopClock(i,c.Iteration,goal+i*.1,goal+i*.1),"stop");if(timeout!=0)d.Tick(goal+2.31);Check(d.Clock.Phase==ClockPhase.Ranking,"ranking");return d.Clock.PhaseAt;}
    static void Return(DuelDirector d)
    {
        var previous=d.Clock.MiniGame;double end=d.Clock.PhaseAt+(d.Clock.Phase==ClockPhase.QuickResults?DuelClockContest.QuickResultsDuration:DuelClockContest.ReturnDuration);d.Tick(end-.7);
        for(int i=0;i<6;i++)if(RouletteRules.Has(d.PartyAliveMask,i)&&d.ForSeat(i).DiedAt>=0)
        {Check(d.ForSeat(i).ReviveAt>=0&&d.ForSeat(i).ReviveAt<end,"server recovery before resurrection phase "+d.Clock.Phase+" seat "+i+" revive "+d.ForSeat(i).ReviveAt+" end "+end+" died "+d.ForSeat(i).DiedAt);Check(!d.Input(i,1,d.ForSeat(i).Version,end-.7),"recovering player cannot act");}
        d.Tick(end+.01);if(d.Game.Phase==DuelPhase.Playing)Check(d.Clock.Phase==ClockPhase.Selecting&&d.Clock.MiniGame!=previous,"immediate previous game excluded");
    }
    static void Trade(DuelDirector d,double now)
    {Load(d,0,now);Load(d,1,now);Launch(d,0,1,DuelHitPart.Head,now);Launch(d,1,0,DuelHitPart.Head,now);d.Tick(now+.3);}
    public static void Validate()
    {
        var d=New(6);for(int i=0;i<6;i++)Check(d.ForSeat(i).LivesRemaining==3&&d.ForSeat(i).Health==100,"all six start HP100 lives3");
        double now=Start(d);Load(d,0,now);for(int i=1;i<6;i++){now+=.4;Launch(d,0,i,DuelHitPart.Head,now);d.Tick(now+.3);}
        Check(d.Game.Phase==DuelPhase.Playing&&d.Clock.Phase==ClockPhase.QuickResults&&d.Clock.First==0&&d.PartyAliveMask==63,"five deaths finish mini but not party");
        for(int i=1;i<6;i++)Check(d.ForSeat(i).LivesRemaining==2&&d.ForSeat(i).DiedAt>=0,"one life per death");
        int old=d.ForSeat(1).Version,life=d.ForSeat(1).Life;d.Tick(now+.31);Check(d.ForSeat(1).LivesRemaining==2,"no repeated deduction");Return(d);
        Check(d.Game.AliveMask==63&&d.ForSeat(1).Life==life+1&&d.ForSeat(1).Health==100&&d.ForSeat(1).ReviveAt<0,"revived fresh incarnation");
        now=Start(d);Check(!d.Input(1,1,old,now),"stale input rejected");double ranked=Rank(d,32);Check(d.ForSeat(5).LivesRemaining==1&&d.PartyAliveMask==63,"timeout loses life");
        d.Tick(ranked+DuelClockContest.RevealDuration+.01);d.Tick(d.Clock.StartedAt+.01);now=d.Clock.PhaseAt+.1;Launch(d,0,4,DuelHitPart.Body,now);d.Tick(now+.3);Return(d);
        Check(d.Game.HealthForSeat(4)==50&&d.ForSeat(4).LivesRemaining==2&&d.ForSeat(5).Health==100,"living countdown HP preserved dead HP restored");
        now=Start(d);Load(d,0,now);for(int i=1;i<6;i++){now+=.4;Launch(d,0,i,DuelHitPart.Head,now);d.Tick(now+.3);}
        Check(d.PartyAliveMask==31&&d.ForSeat(5).LivesRemaining==0&&d.ForSeat(5).ReviveAt<0,"zero lives permanently out");Return(d);
        Check(d.Game.AliveMask==31&&d.ForSeat(5).Health==0&&d.ForSeat(5).DiedAt>=0,"zero lives never resurrects");
        d=New();now=Start(d);Load(d,0,now);Launch(d,0,1,DuelHitPart.Head,now);d.Tick(now+.3);Return(d);
        now=Start(d);ranked=Rank(d);d.Tick(ranked+DuelClockContest.RevealDuration+.01);d.Tick(d.Clock.StartedAt+.01);now=d.Clock.PhaseAt+.1;Launch(d,0,1,DuelHitPart.Head,now);d.Tick(now+.3);Return(d);
        now=Start(d);Load(d,0,now);Launch(d,0,1,DuelHitPart.Head,now);d.Tick(now+.3);
        Check(d.Game.Phase==DuelPhase.Finished&&d.Game.Winner==0&&d.PartyAliveMask==1&&d.ForSeat(1).LivesRemaining==0&&d.ForSeat(0).Kills==3,"third death finishes party");
        d.Tick(now+2);Check(d.Ready(0,now+2)&&d.Game.Round==1,"winner alone cannot restart");Check(d.Ready(1,now+2)&&d.Game.Round==2,"dead connected player must ready");
        Check(d.ForSeat(1).LivesRemaining==3&&d.ForSeat(1).Health==100&&d.ForSeat(0).Kills==0,"rematch resets HP lives stats");d.Tick(now+5);Check(d.Clock.MiniGame==PartyMiniGame.QuickHands,"new party resets previous game");
        d=New();now=Start(d);Trade(d,now);Check(d.Game.AliveMask==0&&d.PartyAliveMask==3&&d.Clock.Phase==ClockPhase.QuickResults,"mutual kill continues");Return(d);
        now=Start(d);ranked=Rank(d,3);Check(d.ForSeat(0).LivesRemaining==1&&d.ForSeat(1).LivesRemaining==1,"batch timeout loses one each");d.Tick(ranked+DuelClockContest.RevealDuration+.01);Check(d.Clock.Phase==ClockPhase.Returning,"no shooter skips shot");Return(d);
        now=Start(d);Trade(d,now);Check(d.Game.Phase==DuelPhase.Finished&&d.Game.Winner==-1&&d.PartyAliveMask==0,"last-life trade draws");
        d=New();now=Start(d);Trade(d,now);Return(d);now=Start(d);ranked=Rank(d,1);d.Tick(ranked+DuelClockContest.RevealDuration+.01);Return(d);now=Start(d);Trade(d,now);
        Check(d.PartyAliveMask==2&&d.Game.AliveMask==0&&d.Game.Phase==DuelPhase.Playing&&d.Clock.Phase==ClockPhase.Returning,"sole life waits to rise");Return(d);
        Check(d.Game.Phase==DuelPhase.Finished&&d.Game.Winner==1&&d.Game.AliveMask==2&&d.ForSeat(1).LivesRemaining==1,"recovered sole life wins");
        d=New();now=Start(d);Load(d,0,now);Launch(d,0,1,DuelHitPart.Head,now);d.Tick(now+.3);d.Disconnect(0,now+.4);Check(d.Game.Phase==DuelPhase.Playing&&d.PartyAliveMask==2,"disconnect does not eliminate recovering player");Return(d);Check(d.Game.Winner==1&&d.ForSeat(1).Health==100,"sole connected player rises then wins");
        var c=new DuelClockContest(new FixedRandom());c.Select(63,0);c.Tick(PartyGames.SelectionDuration+.01,63);c.Tick(c.State.StartedAt+.01,63);c.Tick(c.State.StartedAt+.01,63);c.Return(20);c.Select(63,24);Check(c.State.MiniGame==PartyMiniGame.Countdown,"played quick excluded");c.Return(25);c.Select(63,29);Check(c.State.MiniGame==PartyMiniGame.Countdown,"canceled selection not recorded as played");
        int first=0;for(int seed=0;seed<32;seed++){c=new DuelClockContest(new Random(seed));c.Select(3,0);first|=1<<(int)c.State.MiniGame;}Check(first==31,"all five games can start");
        Console.WriteLine("PASS party lives integration: "+checks+" checks");
    }
}

