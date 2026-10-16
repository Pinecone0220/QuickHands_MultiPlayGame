using System;
using HelloWorld;

public static class ArrowMemoryTests
{
    static int checks;
    static void Check(bool value,string label)
    { checks++;if(!value)throw new Exception("Arrow memory: "+label); }
    sealed class FixedRandom:Random
    {
        public override int Next()=>1;
        public override int Next(int max)=>0;
        public override int Next(int min,int max)=>min;
        public override double NextDouble()=>0;
    }
    static DuelArrowMemory Game(int mask=7,int seed=154)
    {var game=new DuelArrowMemory(new Random(seed));game.Begin(mask,0);return game;}
    static double StartInput(DuelArrowMemory game)
    {
        var state=game.State;
        Check(state.Phase==ArrowMemoryPhase.Showing,"round starts by showing arrows");
        Check(!game.Input(0,state.Round,0,DuelArrowMemory.DirectionAt(state.AnswerSeed,0),state.PhaseAt+2.99),"input refused during memorization");
        Check(!game.Tick(state.PhaseAt+2.99,state.ActiveMask),"memorization does not finish early");
        Check(game.Tick(state.PhaseAt+3,state.ActiveMask),"memorization ends at exactly three seconds");
        Check(game.State.Phase==ArrowMemoryPhase.Input,"hidden input follows memorization");
        Check(Math.Abs(game.State.InputEndsAt-game.State.PhaseAt-10)<.000001,"each round has ten seconds to input");
        return game.State.PhaseAt;
    }
    static void Answer(DuelArrowMemory game,int seat,int errors,double now)
    {
        var state=game.State;
        for(int index=0;index<state.Length;index++)
        {
            int direction=DuelArrowMemory.DirectionAt(state.AnswerSeed,index);
            if(index<errors)direction=(direction+1)%4;
            Check(game.Input(seat,state.Round,index,direction,now),"ordered answer accepted for seat "+seat+" index "+index);
        }
        var entry=game.State.ForSeat(seat);
        Check(entry.Count==state.Length&&entry.Errors==errors&&entry.Locked,"required length automatically locks complete answer");
        Check(!game.Input(seat,state.Round,state.Length,0,now),"completed answer cannot be extended");
    }
    static int Bits(int mask)
    {int n=0;for(int i=0;i<6;i++)if((mask&(1<<i))!=0)n++;return n;}
    static void TimingAndValidation()
    {
        var game=Game();var first=game.State;
        Check(first.Round==1&&first.Length==3&&first.ActiveMask==7&&first.RoundMask==7,"first round starts with three arrows and all participants");
        Check(first.EliminatedMask==0&&first.Winner==-1,"new minigame has no old eliminations or winner");
        Check(game.PublicState().AnswerSeed==first.AnswerSeed,"memorization sequence is public while shown");
        double at=StartInput(game);int round=game.State.Round;
        Check(!game.Input(-1,round,0,0,at)&&!game.Input(6,round,0,0,at)&&!game.Input(5,round,0,0,at),"invalid and absent seats refused");
        Check(!game.Input(0,round-1,0,0,at)&&!game.Input(0,round+1,0,0,at),"stale and future rounds refused");
        Check(!game.Input(0,round,-1,0,at)&&!game.Input(0,round,1,0,at)&&!game.Input(0,round,3,0,at),"only expected input index accepted");
        Check(!game.Input(0,round,0,-1,at)&&!game.Input(0,round,0,4,at),"only four arrow directions accepted");
        Check(!game.Input(0,round,0,0,double.NaN)&&!game.Input(0,round,0,0,double.PositiveInfinity)&&!game.Input(0,round,0,0,at-.001),"nonfinite and pre-input timestamps refused");
        Check(game.State.ForSeat(0).Count==0,"invalid requests do not advance input");
        int answer=DuelArrowMemory.DirectionAt(game.State.AnswerSeed,0);
        Check(game.Input(0,round,0,answer,at+.1),"first valid arrow accepted");
        Check(!game.Input(0,round,0,answer,at+.2)&&game.State.ForSeat(0).Count==1,"replayed index does not advance input twice");
        for(int index=0;index<2;index++)Check(game.Input(1,round,index,DuelArrowMemory.DirectionAt(game.State.AnswerSeed,index),at+.3),"partial second answer accepted");
        Check(!game.Input(0,round,1,0,game.State.InputEndsAt),"deadline itself no longer accepts input");
        Check(!game.Tick(game.State.InputEndsAt-.001,7),"input remains open until deadline when not all locked");
        Check(game.Tick(game.State.InputEndsAt,7)&&game.State.Phase==ArrowMemoryPhase.Results,"timeout confirms incomplete answers");
        Check(game.State.ForSeat(0).Errors==2&&game.State.ForSeat(1).Errors==1&&game.State.ForSeat(2).Errors==3,"missing arrows each count as an error");
        Check(game.State.ForSeat(0).Locked&&game.State.ForSeat(1).Locked,"timeout locks incomplete entries");
        Check(Bits(game.State.EliminatedMask)==1&&Bits(game.State.ActiveMask)==2,"a failed round removes exactly one participant");
        double ranked=game.State.PhaseAt;
        Check(!game.Tick(ranked+2.999,7)&&game.State.Phase==ArrowMemoryPhase.Results,"round results remain visible for three seconds");
        Check(game.Tick(ranked+3,7)&&game.State.Phase==ArrowMemoryPhase.Showing,"results advance at exactly three seconds");
        Check(game.State.Round==2&&game.State.Length==4,"following round adds one arrow");
        Check(!game.Input(0,round,1,0,ranked+3),"old-round request cannot affect following round");
    }
    static void PrivacyAndCopies()
    {
        var game=Game();double at=StartInput(game);int seed=game.State.AnswerSeed;
        Check(game.Input(0,game.State.Round,0,(DuelArrowMemory.DirectionAt(seed,0)+1)%4,at),"private incorrect input accepted");
        Answer(game,1,0,at);
        var hidden=game.PublicState();
        Check(hidden.AnswerSeed==0&&hidden.Phase==ArrowMemoryPhase.Input,"hidden phase never republishes sequence seed");
        for(int seat=0;seat<6;seat++)
        {
            var e=hidden.ForSeat(seat);
            Check(e.Count==0&&e.Errors<0&&!e.Locked&&e.Dice==0&&e.Rolls==0,"public input snapshot hides private progress, errors and lock status");
        }
        hidden.Set(0,new ArrowMemoryEntry{Count=900,Errors=900,Locked=true});
        Check(game.State.ForSeat(0).Count==1&&game.State.ForSeat(0).Errors==1,"public snapshot edits cannot alter authoritative state");
        Answer(game,2,1,at);
        Check(game.State.Phase==ArrowMemoryPhase.Input,"one unfinished participant keeps input open");
        game.Tick(game.State.InputEndsAt,7);
        Check(game.PublicState().ForSeat(0).Errors==3&&game.PublicState().ForSeat(1).Locked,"results disclose actual records after input closes");
    }
    static void MostErrorsOnly()
    {
        var game=Game();double at=StartInput(game);
        Answer(game,0,1,at);Answer(game,1,2,at);Answer(game,2,3,at);
        Check(game.State.Phase==ArrowMemoryPhase.Results,"all submitted answers resolve without waiting ten seconds");
        Check(game.State.EliminatedMask==4&&game.State.ActiveMask==3,"only highest error count is eliminated even if everyone failed");
        Check(game.State.ForSeat(0).Dice==0&&game.State.ForSeat(1).Dice==0&&game.State.ForSeat(2).Dice==0,"unequal errors require no dice");
        game.Tick(game.State.PhaseAt+3,7);at=StartInput(game);
        Answer(game,0,0,at);Answer(game,1,1,at);
        Check(game.State.Phase==ArrowMemoryPhase.Finished&&game.State.Winner==0,"one remaining player wins the minigame");
        Check(game.State.ActiveMask==1&&game.State.EliminatedMask==2&&game.State.ForSeat(2).Rank==3,"prior eliminations stay out and preserve their minigame rank");
        Check(!game.Input(0,game.State.Round,0,0,at)&&!game.Tick(at+100,7),"finished minigame accepts no further play");
        game.Begin(63,100);
        Check(game.State.Round==1&&game.State.Length==3&&game.State.EliminatedMask==0&&game.State.ActiveMask==63,"new minigame resets prior results");
    }
    static void TiesAndPathologicalRandom()
    {
        foreach(int mask in new[]{3,7,63})
        {
            var game=new DuelArrowMemory(new FixedRandom());game.Begin(mask,0);double at=StartInput(game);
            for(int seat=0;seat<6;seat++)if((mask&(1<<seat))!=0)Answer(game,seat,1,at);
            var state=game.State;
            Check(state.Phase==ArrowMemoryPhase.Finished,"equal failures finish entire minigame immediately using dice");
            for(int seat=0;seat<6;seat++)if((mask&(1<<seat))!=0)
            {
                var entry=state.ForSeat(seat);
                Check(entry.Dice>=1&&entry.Dice<=6&&entry.Rolls>0&&entry.Rolls<1000,"constant random source reaches finite legal dice fallback");
            }
            CheckFinalRanks(state,mask);
        }
        var partial=Game(15);double time=StartInput(partial);
        Answer(partial,0,0,time);Answer(partial,1,1,time);Answer(partial,2,3,time);Answer(partial,3,3,time);
        Check(partial.State.Phase==ArrowMemoryPhase.Finished&&partial.State.Winner==0,"tied worst players finalize minigame with fewer-error winner");
        Check(partial.State.ForSeat(0).Dice==0&&partial.State.ForSeat(1).Dice==0,"unique error groups need no dice");
        CheckFinalRanks(partial.State,15);
        var middle=Game(15);time=StartInput(middle);
        Answer(middle,0,0,time);Answer(middle,1,1,time);Answer(middle,2,1,time);Answer(middle,3,3,time);
        Check(middle.State.Phase==ArrowMemoryPhase.Finished&&middle.State.Winner==0,"non-worst mistake tie also finalizes whole minigame");
        CheckFinalRanks(middle.State,15);
        var best=Game(63);time=StartInput(best);
        foreach(int seat in new[]{0,1,2,3,4,5})Answer(best,seat,seat<2?0:seat<4?1:seat-2,time);
        Check(best.State.Phase==ArrowMemoryPhase.Finished&&(best.State.Winner==0||best.State.Winner==1),"mistake tie finalizes even correct-player tie with dice");
        Check(best.State.ForSeat(0).Dice>0&&best.State.ForSeat(1).Dice>0,"correct tied finalists roll only when whole ranking must finalize");
        CheckFinalRanks(best.State,63);
    }
    static void CheckFinalRanks(ArrowMemoryState state,int mask)
    {
        int ranks=0;
        for(int seat=0;seat<6;seat++)if((mask&(1<<seat))!=0)
        {
            var a=state.ForSeat(seat);Check(a.Rank>=1&&a.Rank<=Bits(mask),"final rank is within participant range");
            Check((ranks&(1<<a.Rank))==0,"final ranks are unique");ranks|=1<<a.Rank;
            Check((a.Rank==1)==(state.Winner==seat),"rank one matches shooting winner");
            for(int other=seat+1;other<6;other++)if((mask&(1<<other))!=0)
            {
                var b=state.ForSeat(other);
                Check(a.Errors==b.Errors?(a.Dice>b.Dice)==(a.Rank<b.Rank):(a.Errors<b.Errors)==(a.Rank<b.Rank),"final ranking sorts fewer errors then higher dice");
            }
        }
    }
    static void CorrectRoundsAndDisconnects()
    {
        var game=Game(63);int directions=0;
        for(int r=1;r<=24;r++)
        {
            Check(game.State.Round==r&&game.State.Length==r+2,"sequence grows one arrow every round without a short fixed cap");
            int seed=game.State.AnswerSeed;
            for(int i=0;i<game.State.Length;i++)
            {
                int a=DuelArrowMemory.DirectionAt(seed,i),b=DuelArrowMemory.DirectionAt(seed,i);
                Check(a>=0&&a<4&&a==b,"seeded arrows are deterministic valid directions");directions|=1<<a;
            }
            double at=StartInput(game);
            for(int seat=0;seat<6;seat++)Answer(game,seat,0,at);
            Check(game.State.Phase==ArrowMemoryPhase.Results&&game.State.ActiveMask==63&&game.State.EliminatedMask==0,"all-correct round retains everybody");
            for(int seat=0;seat<6;seat++)Check(game.State.ForSeat(seat).Dice==0,"all correct does not trigger elimination dice");
            game.Tick(game.State.PhaseAt+3,63);
        }
        Check(directions==15,"generated rounds exercise all four directions");
        game=Game();game.Tick(.5,5);
        Check(game.State.ActiveMask==5&&(game.State.RoundMask&2)==0,"disconnect removes participant during memorization");
        double input=StartInput(game);Answer(game,0,0,input);Answer(game,2,0,input);
        Check(game.State.Phase==ArrowMemoryPhase.Results&&game.State.ActiveMask==5,"disconnected seat does not hold completion open");
        game.Tick(game.State.PhaseAt+3,5);input=StartInput(game);
        Check(!game.Input(1,game.State.Round,0,0,input),"disconnected seat cannot reenter via input");
        Check(game.Tick(input+.1,4)&&game.State.Phase==ArrowMemoryPhase.Finished&&game.State.Winner==2,"last connected participant wins without waiting for missing peer");
        game=Game();input=StartInput(game);Answer(game,0,0,input);Answer(game,1,1,input);Answer(game,2,2,input);
        int one=game.State.ActiveMask&-game.State.ActiveMask;
        Check(game.Tick(game.State.PhaseAt+.1,one)&&game.State.Phase==ArrowMemoryPhase.Finished,"disconnect during results cannot leave next round stuck");
        game=Game();Check(game.Tick(.1,0)&&game.State.Phase==ArrowMemoryPhase.Finished&&game.State.Winner==-1,"all disconnected ends safely without inventing winner");
    }
    static void DirectorAnswer(DuelDirector director,int seat,int errors,int seed,double now)
    {
        var clock=director.Clock;var state=clock.Memory;
        for(int index=0;index<state.Length;index++)
        {
            int direction=DuelArrowMemory.DirectionAt(seed,index);if(index<errors)direction=(direction+1)%4;
            Check(director.InputMemory(seat,clock.Iteration,state.Round,index,direction,now),"director accepts current player's memory input");
        }
        Check(!director.InputMemory(seat,clock.Iteration,state.Round,state.Length,0,now),"director rejects extra completed input");
    }
    static void PartyIntegration()
    {
        var director=new DuelDirector(DuelMode.PvP,new Random(192));
        Check(director.Game.SetManualMiniGame(true),"test room allows selecting the new minigame");
        for(int seat=0;seat<3;seat++)Check(director.Join(seat,0),"test participant joins");
        for(int seat=0;seat<3;seat++)Check(director.Ready(seat,0),"all test participants ready");
        director.Tick(3);int iteration=director.Clock.Iteration;
        Check(director.Clock.Phase==ClockPhase.Selecting&&director.ChooseMiniGame(PartyMiniGame.Memory,iteration,3.1),"host selects memory from party minigames");
        Check(director.Clock.Phase==ClockPhase.Memory&&director.Clock.Memory.Phase==ArrowMemoryPhase.Showing,"party enters memory's show phase");
        Check(director.Game.Shoot(0,2,DuelWeapon.Revolver,DuelHitPart.Body)&&director.ForSeat(2).Health==50,"test participant starts memory with existing injury");
        int seed=director.Clock.Memory.AnswerSeed;double now=director.Clock.Memory.PhaseAt+3;director.Tick(now);
        Check(director.Clock.Memory.Phase==ArrowMemoryPhase.Input&&director.Clock.Memory.AnswerSeed==0,"director publishes hidden input state without answer seed");
        Check(!director.InputMemory(0,iteration-1,director.Clock.Memory.Round,0,0,now),"director rejects previous selection iteration");
        DirectorAnswer(director,0,0,seed,now);DirectorAnswer(director,1,1,seed,now);DirectorAnswer(director,2,2,seed,now);
        Check(director.Clock.Phase==ClockPhase.Memory&&director.Clock.Memory.Phase==ArrowMemoryPhase.Results&&director.Clock.Memory.ActiveMask==3,"first memory elimination remains within minigame");
        Check(director.Game.AliveMask==7&&director.PartyAliveMask==7&&director.ForSeat(2).Health==50&&director.ForSeat(2).LivesRemaining==3&&director.ForSeat(2).DiedAt<0,"memory elimination does not kill, heal, or consume party life");
        double resultAt=director.Clock.Memory.PhaseAt;director.Tick(resultAt+2.999);
        Check(director.Clock.Memory.Phase==ArrowMemoryPhase.Results,"memory round results remain for three seconds");
        director.Tick(resultAt+3);
        Check(director.Clock.Memory.Phase==ArrowMemoryPhase.Showing&&director.Clock.Memory.Length==4,"party begins longer second memory round");
        seed=director.Clock.Memory.AnswerSeed;now=director.Clock.Memory.PhaseAt+3;director.Tick(now);
        Check(!director.InputMemory(2,iteration,director.Clock.Memory.Round,0,0,now),"minigame eliminated participant cannot submit following round");
        DirectorAnswer(director,0,0,seed,now);DirectorAnswer(director,1,1,seed,now);
        Check(director.Clock.Phase==ClockPhase.Ranking&&director.Clock.First==0,"last memory participant becomes shooting winner");
        for(int seat=0;seat<3;seat++)Check(director.ForSeat(seat).LivesRemaining==3,"every memory participant retains lives before shooting");
        Check(director.ForSeat(2).Health==50&&director.ForSeat(0).ClockWins==0&&director.ForSeat(0).ClockStops==0,"memory preserves HP and does not pollute countdown statistics");
        double ranked=director.Clock.PhaseAt;director.Tick(ranked+2.999);
        Check(director.Clock.Phase==ClockPhase.Ranking,"final memory rankings remain for three seconds");
        director.Tick(ranked+3);Check(director.Clock.Phase==ClockPhase.ItemPreparation,"final memory rankings proceed to item use");
        double itemEnds=director.Clock.StartedAt;
        Check(Math.Abs(itemEnds-director.Clock.PhaseAt-5)<.000001,"memory item phase lasts five seconds");
        director.Tick(itemEnds-.001);Check(director.Clock.Phase==ClockPhase.ItemPreparation,"items do not finish early");
        director.Tick(itemEnds);Check(director.Clock.Phase==ClockPhase.Shooting&&director.ForSeat(0).Ammo==1,"winner gets exactly one bullet after item use");
        now=itemEnds+.1;DuelGunplay.Angles(DuelGunplay.Point(2,DuelHitPart.Head)-DuelGunplay.Eye(0),out float yaw,out float pitch);
        DuelGunplay.Sway(0,DuelWeapon.Revolver,true,now,now,out float swayYaw,out float swayPitch);
        Check(!director.Fire(1,yaw-swayYaw,pitch-swayPitch,true,director.ForSeat(1).Version,now,now),"memory runner-up cannot fire shared gun");
        Check(director.Fire(0,yaw-swayYaw,pitch-swayPitch,true,director.ForSeat(0).Version,now,now),"winner may aim at earlier memory elimination");
        Check(!director.Fire(0,yaw-swayYaw,pitch-swayPitch,true,director.ForSeat(0).Version,now+.01,now+.01),"memory shooting opportunity cannot fire twice");
        director.Tick(now+.5);
        Check(director.ForSeat(2).LivesRemaining==2&&director.Clock.Phase==ClockPhase.Returning,"real projectile shooting alone consumes party life");
        director.Tick(director.Clock.PhaseAt+3.01);
        Check(director.Clock.Phase==ClockPhase.Selecting&&director.PartyAliveMask==7&&director.Game.AliveMask==7,"remaining party lives rejoin next selection");
        Check(director.ChooseMiniGame(PartyMiniGame.Memory,director.Clock.Iteration,director.Clock.PhaseAt+.01),"manual test mode may choose same minigame again");
        Check(director.Clock.Memory.Length==3&&director.Clock.Memory.ActiveMask==7,"new memory game resets length and restores earlier mini eliminations");
    }
    static void TiePartyFinish()
    {
        foreach(bool tiedCorrect in new[]{false,true})
        {
            var d=new DuelDirector(DuelMode.PvP,new Random(391));d.Game.SetManualMiniGame(true);
            for(int i=0;i<4;i++)d.Join(i,0);for(int i=0;i<4;i++)d.Ready(i,0);d.Tick(3);
            d.ChooseMiniGame(PartyMiniGame.Memory,d.Clock.Iteration,3.1);int seed=d.Clock.Memory.AnswerSeed;d.Tick(6.1);
            int[] errors=tiedCorrect?new[]{0,0,1,1}:new[]{0,1,1,3};
            for(int i=0;i<4;i++)DirectorAnswer(d,i,errors[i],seed,6.2);
            Check(d.Clock.Phase==ClockPhase.Ranking&&d.Clock.Memory.Phase==ArrowMemoryPhase.Finished,"dice tie ends the entire mini immediately even in round one");
            Check(tiedCorrect?d.Clock.First<2:d.Clock.First==0,"fewer errors take priority over dice");
            Check(d.Clock.EliminatedMask==0&&d.Game.AliveMask==15&&d.PartyAliveMask==15,"memory ranking never feeds party life elimination mask");
            for(int i=0;i<4;i++)Check(d.ForSeat(i).LivesRemaining==3&&d.ForSeat(i).Health==100,"dice ranking preserves health and lives");
            d.Tick(d.Clock.PhaseAt+3.001);Check(d.Clock.Phase==ClockPhase.ItemPreparation,"dice-decided mini reaches the shared item turn after three seconds");
        }
    }
    static void AiParticipation()
    {
        var completion=new double[3];
        foreach(AiDifficulty difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})
        {
            var director=new DuelDirector(DuelMode.AI,new Random(2026),difficulty);director.Game.SetManualMiniGame(true);
            for(int seat=0;seat<6;seat++)Check(director.Join(seat,0),"AI memory participant joins");
            Check(director.Ready(0,0),"AI memory bots ready automatically");director.Tick(3);
            Check(director.ChooseMiniGame(PartyMiniGame.Memory,director.Clock.Iteration,3.1),"AI room selects memory");
            int seed=director.Clock.Memory.AnswerSeed;double start=6.1;director.Tick(start);
            DirectorAnswer(director,0,0,seed,start);
            double now=start;
            while(director.Clock.Phase==ClockPhase.Memory&&director.Clock.Memory.Phase==ArrowMemoryPhase.Input&&now<start+10.1){now+=.05;director.Tick(now);}
            Check(now<start+10,"all five AI enter their arrows before the deadline");completion[(int)difficulty]=now-start;
            for(int bot=1;bot<6;bot++)Check(director.MemoryProgress(bot)==3&&director.ForSeat(bot).LivesRemaining==3&&director.ForSeat(bot).Health==100,"AI completes three inputs without mini-game life loss");
        }
        Check(completion[0]>completion[1]&&completion[1]>completion[2],"AI difficulty changes actual memory input speed");
    }
    public static void Main()
    {
        TimingAndValidation();PrivacyAndCopies();MostErrorsOnly();TiesAndPathologicalRandom();CorrectRoundsAndDisconnects();PartyIntegration();TiePartyFinish();AiParticipation();
        Console.WriteLine("PASS arrow memory: "+checks+" checks");
    }
}
