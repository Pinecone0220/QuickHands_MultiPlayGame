using System;
using HelloWorld;
public static class MemoryPerfectWinnerTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Answer(DuelArrowMemory game,int seat,int errors,double now)
    {
        var state=game.State;
        for(int i=0;i<state.Length;i++)
        {int direction=DuelArrowMemory.DirectionAt(state.AnswerSeed,i);if(i<errors)direction=(direction+1)%4;Check(game.Input(seat,state.Round,i,direction,now),"accepted answer");}
    }
    public static void Main()
    {
        for(int winner=0;winner<6;winner++)
        {
            var game=new DuelArrowMemory(new Random(winner+1));game.Begin(63,0);game.Tick(3,63);
            for(int i=0;i<6;i++)Answer(game,i,0,3.1);
            Check(game.State.Phase==ArrowMemoryPhase.Results&&game.State.ActiveMask==63,"all perfect advances round");
            game.Tick(game.State.PhaseAt+3,63);game.Tick(game.State.PhaseAt+3,63);
            double now=game.State.PhaseAt+.1;
            for(int i=0;i<6;i++)Answer(game,i,i==winner?0:1+(i%4),now);
            var s=game.State;
            Check(s.Phase==ArrowMemoryPhase.Finished&&s.Winner==winner&&s.ActiveMask==(1<<winner),"sole perfect player wins");
            Check(s.EliminatedMask==(63&~(1<<winner)),"all other players eliminated from minigame");
            Check(game.Dice.Phase!=DuelDicePhase.Rolling,"lower ties do not cause dice");
            for(int i=0;i<6;i++)Check(s.ForSeat(i).Rank==(i==winner?1:2),"winner and losers recorded");
        }
        var timeouts=new DuelArrowMemory(new Random(3));timeouts.Begin(7,0);timeouts.Tick(3,7);Answer(timeouts,1,0,3.1);
        timeouts.Tick(timeouts.State.InputEndsAt,7);
        Check(timeouts.State.Winner==1&&timeouts.State.ForSeat(0).Errors==3&&timeouts.State.ForSeat(2).Errors==3,"unfilled entries count as errors");
        var mixed=new DuelArrowMemory(new Random(4));mixed.Begin(15,0);mixed.Tick(3,15);
        Answer(mixed,0,0,3.1);Answer(mixed,1,0,3.1);Answer(mixed,2,1,3.1);Answer(mixed,3,2,3.1);
        Check(mixed.State.Phase==ArrowMemoryPhase.Results&&mixed.State.ActiveMask==3&&mixed.State.EliminatedMask==12,"all incorrect players eliminated when multiple perfect players remain");
        mixed.Tick(mixed.State.PhaseAt+3,15);mixed.Tick(mixed.State.PhaseAt+3,15);
        Answer(mixed,0,1,mixed.State.PhaseAt+.1);Answer(mixed,1,1,mixed.State.PhaseAt+.1);
        Check(mixed.State.Phase==ArrowMemoryPhase.Dice&&mixed.Dice.PendingMask==3&&mixed.State.RoundMask==3,"only participants from current round roll dice after all fail");
        var failed=new DuelArrowMemory(new Random(5));failed.Begin(7,0);failed.Tick(3,7);
        Answer(failed,0,1,3.1);Answer(failed,1,2,3.1);Answer(failed,2,3,3.1);
        Check(failed.State.Phase==ArrowMemoryPhase.Finished&&failed.State.Winner==0&&failed.Dice.Phase==DuelDicePhase.None,"unique fewest errors wins without dice");
        var tied=new DuelArrowMemory(new Random(7));tied.Begin(7,0);tied.Tick(3,7);
        Answer(tied,0,1,3.1);Answer(tied,1,1,3.1);Answer(tied,2,3,3.1);
        Check(tied.State.Phase==ArrowMemoryPhase.Dice&&tied.Dice.PendingMask==3&&tied.State.RoundMask==7,"only tied lowest errors roll while round retains all participants");
        for(int i=0;i<2;i++)Check(tied.StopDice(i,tied.Dice.Round,3.2),"each tied player can stop dice");
        Check(tied.Tick(4.5,7),"dice comparison progresses");
        while(tied.State.Phase==ArrowMemoryPhase.Dice){var dice=tied.Dice;for(int i=0;i<3;i++)if(dice.CanStop(i))Check(tied.StopDice(i,dice.Round,dice.StartedAt+.1),"tied winners reroll");Check(tied.Tick(tied.Dice.EndsAt+.01,7),"reroll progresses");}
        Check(tied.State.Phase==ArrowMemoryPhase.Finished&&(tied.State.Winner==0||tied.State.Winner==1)&&tied.State.EliminatedMask==(7&~(1<<tied.State.Winner)),"dice selects winner and retains all eliminations");
        var clock=new DuelClockContest(new Random(6));clock.Begin(63,0,PartyMiniGame.Memory);clock.Tick(3,63);
        for(int seat=0;seat<6;seat++)for(int i=0;i<3;i++)
        {int direction=clock.MemoryDirection(i);if(i<seat%3+1&&seat!=0)direction=(direction+1)%4;Check(clock.InputMemory(seat,clock.State.Iteration,clock.State.Memory.Round,i,direction,3.1),"server accepts input");}
        Check(clock.State.Phase==ClockPhase.Ranking&&clock.State.First==0,"sole perfect player reaches final ranking");
        Check(clock.State.Memory.EliminatedMask==62&&clock.State.ParticipantsMask==63,"mini eliminations preserve party participants");
        clock.Tick(clock.State.PhaseAt+3.001,63);Check(clock.State.Phase==ClockPhase.ItemPreparation,"result proceeds to item preparation after three seconds");
        clock.Tick(clock.State.StartedAt,63);Check(clock.State.Phase==ClockPhase.Shooting&&clock.State.First==0,"winner receives shooting turn");
        Console.WriteLine("Memory perfect winner: "+checks+" checks passed.");
    }
}
