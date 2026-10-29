using System;
using HelloWorld;
public static class PartyCycleTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void Main()
    {
        for(int seed=0;seed<100;seed++)
        {
            var contest=new DuelClockContest(new Random(seed));int played=0;
            for(int cycle=0;cycle<3;cycle++)
            {
                for(int i=0;i<PartyGames.Count;i++)
                {
                    contest.Select(3,0);var state=contest.State;
                    Check(state.PlayedMask==played,"published history before selection");
                    int bit=PartyGames.Bit(state.MiniGame);
                    Check((played&bit)==0,"game repeated within cycle");
                    if(state.MiniGame==PartyMiniGame.QuickHands)
                    {
                        contest.Tick(PartyGames.SelectionDuration,3);
                        Check(contest.State.PlayedMask==played,"preparation is not a played game");
                        contest.Tick(contest.State.StartedAt,3);
                        contest.Tick(contest.State.StartedAt,3);
                    }
                    else contest.Tick(PartyGames.SelectionDuration,3);
                    played|=bit;Check(contest.State.PlayedMask==played,"actual start recorded");
                }
                Check(played==PartyGames.AllMask,"all games played");played=0;
            }
            contest.Reset();Check(contest.State.PlayedMask==0,"new party clears history");
            contest.Select(3,0);contest.Select(3,1);Check(contest.State.PlayedMask==0,"cancelled selection does not count");
        }
        var first=new ClockState();var second=first;second.PlayedMask=1;
        Check(!first.Equals(second),"history affects snapshot equality");
        Console.WriteLine("Party cycle: "+checks+" checks passed.");
    }
}
