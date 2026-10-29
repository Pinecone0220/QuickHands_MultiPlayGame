using System;
using System.Collections.Generic;
namespace HelloWorld
{
    public enum ClockPhase : byte { Waiting, Counting, Ranking, Shooting, Returning, Selecting, QuickHands, QuickResults, QuickCountdown, ItemPreparation, Memory, Dice }
    public enum MiniSignal : byte { Wait, Fake, Go, Burst }
    public enum MiniFailure : byte { None, EarlyInput, MissedSignal, Burst }
    public struct ClockEntry : IEquatable<ClockEntry>
    {
        public int Centiseconds, Error, Rank, Dice, Rolls, Points;
        public MiniFailure Failure;
        public bool Equals(ClockEntry o) => Centiseconds==o.Centiseconds && Error==o.Error && Rank==o.Rank && Dice==o.Dice && Rolls==o.Rolls && Points==o.Points && Failure==o.Failure;
    }
    public struct ClockState : IEquatable<ClockState>
    {
        public ClockPhase Phase;
        public PartyMiniGame MiniGame;
        public int EliminatedMask, PlayedMask;
        public MiniSignal Signal;
        public int Points;
        public double SignalAt;
        public int Iteration, ParticipantsMask, Target, First, Last;
        public double StartedAt, PhaseAt, HideAfter;
        public ClockEntry Seat0, Seat1, Seat2, Seat3, Seat4, Seat5;
        public ArrowMemoryState Memory;
        public DuelDiceState Dice;
        public ClockEntry ForSeat(int seat) => seat==0?Seat0:seat==1?Seat1:seat==2?Seat2:seat==3?Seat3:seat==4?Seat4:seat==5?Seat5:default;
        public void Set(int seat,ClockEntry entry)
        { if(seat==0)Seat0=entry;else if(seat==1)Seat1=entry;else if(seat==2)Seat2=entry;else if(seat==3)Seat3=entry;else if(seat==4)Seat4=entry;else if(seat==5)Seat5=entry; }
        public bool Equals(ClockState o) => Phase==o.Phase && MiniGame==o.MiniGame && EliminatedMask==o.EliminatedMask && PlayedMask==o.PlayedMask && Signal==o.Signal && Points==o.Points && SignalAt==o.SignalAt && Iteration==o.Iteration && ParticipantsMask==o.ParticipantsMask && Target==o.Target && First==o.First && Last==o.Last && StartedAt==o.StartedAt && PhaseAt==o.PhaseAt && HideAfter==o.HideAfter && Memory.Equals(o.Memory) && Dice.Equals(o.Dice) && Seat0.Equals(o.Seat0) && Seat1.Equals(o.Seat1) && Seat2.Equals(o.Seat2) && Seat3.Equals(o.Seat3) && Seat4.Equals(o.Seat4) && Seat5.Equals(o.Seat5);
    }
    public sealed class DuelClockContest
    {
        public const double MiniResultsDuration=3;
        public const double ItemHeadStart=0.5;
        public static double ShootingStartsAt(ClockState state)=>state.PhaseAt+ItemHeadStart;
        public const double RevealDuration=MiniResultsDuration, GreedRevealDuration=MiniResultsDuration, GreedCountdownDuration=3, ShotDuration=10, ReturnDuration=3, QuickResultsDuration=MiniResultsDuration, QuickCountdownDuration=3, ItemPreparationDuration=5;
        public const int GreedMinimum=50,GreedMaximum=150000;
        public const double GreedDuration=30;
        public static double RevealFor(PartyMiniGame game)=>game==PartyMiniGame.Greed?GreedRevealDuration:RevealDuration;
        public static int GreedScoreAt(double startedAt,double sample)
        {
            double elapsed=sample-startedAt;if(double.IsNaN(elapsed)||elapsed<=0)return 0;if(elapsed>=GreedDuration)return GreedMaximum;
            double progress=elapsed/GreedDuration;return (int)Math.Floor(GreedMaximum*progress*progress*progress+1e-6);
        }
        public static double GreedElapsedAt(double score)=>GreedDuration*Math.Pow(Math.Max(0,Math.Min(GreedMaximum,score))/GreedMaximum,1.0/3);
        // Cumulative chance by score, sampled once on the server so tick/frame rates cannot change it.
        public static double GreedBurstChance(double score,int threshold)
        {
            if(score<GreedMinimum)return 0;if(score>=threshold)return 1;
            return Math.Pow((score-GreedMinimum)/(threshold-GreedMinimum),4);
        }
        public ClockState State { get; private set; }
        private readonly Random random;
        private readonly DuelArrowMemory memory;
        private readonly DuelDiceContest dice;
        private int stopped;
        private double goAt,burstAt;
        private int greedThreshold;
        private readonly double[] fakeAt=new double[3];
        private int fakeCount;
        public double ReactionGoAt=>goAt;
        public double GreedBurstAt=>burstAt;
        public DuelClockContest(Random random) { this.random=random;memory=new DuelArrowMemory(random);dice=new DuelDiceContest(random); }
        public double Deadline => State.MiniGame==PartyMiniGame.Signal?goAt+2:State.MiniGame==PartyMiniGame.Greed?burstAt:State.StartedAt+State.Target/100.0+2;
        public ClockState PublicState()
        {
            var state=State;
            state.Dice=state.MiniGame==PartyMiniGame.Memory?memory.Dice:dice.State;
            if(state.MiniGame==PartyMiniGame.Memory)state.Memory=memory.PublicState();
            // Neither other people's stop times nor their stop status is revealed during counting.
            if(state.Phase==ClockPhase.Counting)
                for(int i=0;i<RouletteRules.MaxPlayers;i++)state.Set(i,new ClockEntry{Centiseconds=-1,Error=-1,Points=-1});
            return state;
        }
        public static int Quantize(double seconds) => (int)Math.Round(seconds*100,MidpointRounding.AwayFromZero);
        public void Reset() { State=default;stopped=0;memory.Reset();dice.Reset();greedThreshold=0;goAt=burstAt=0; }
        public void Select(int alive,double now,bool manual=false)
        {
            int played=State.PlayedMask&PartyGames.AllMask;
            if(played==PartyGames.AllMask && RouletteRules.Count(alive)>=2)played=0;
            State=new ClockState{Iteration=State.Iteration+1,ParticipantsMask=alive,PlayedMask=played,MiniGame=manual?PartyMiniGame.Countdown:PartyGames.ChooseAvailable(random,played),Phase=ClockPhase.Selecting,PhaseAt=now,First=-1,Last=-1};
            stopped=0;memory.Reset();dice.Reset();
        }
        public bool ChooseManual(PartyMiniGame game,double now)
        {if(State.Phase!=ClockPhase.Selecting||!PartyGames.IsValid(game))return false;var s=State;s.MiniGame=game;s.PhaseAt=now;State=s;return true;}
        public void Begin(int alive,double now,PartyMiniGame game=PartyMiniGame.Countdown)
        {
            int iteration=State.Phase==ClockPhase.Selecting?State.Iteration:State.Iteration+1;
            var s=new ClockState{Iteration=iteration,MiniGame=game,PlayedMask=State.PlayedMask|PartyGames.Bit(game),ParticipantsMask=alive,Target=game==PartyMiniGame.Countdown?random.Next(7,16)*100:0,HideAfter=game==PartyMiniGame.Countdown?2.4+random.NextDouble()*.5:0,StartedAt=now+(game==PartyMiniGame.Greed?GreedCountdownDuration:1),PhaseAt=now,Phase=ClockPhase.Counting,First=-1,Last=-1};
            for(int i=0;i<RouletteRules.MaxPlayers;i++)s.Set(i,new ClockEntry{Centiseconds=-1,Error=-1,Points=-1});
            State=s;stopped=0;dice.Reset();
            if(game==PartyMiniGame.Memory){s.Phase=ClockPhase.Memory;s.StartedAt=now;State=s;memory.Begin(alive,now);SyncMemory(now);return;}
            memory.Reset();
            if(game==PartyMiniGame.Signal)
            {goAt=s.StartedAt+2.5+random.NextDouble()*3.5;fakeCount=random.Next(1,4);for(int i=0;i<fakeCount;i++)fakeAt[i]=s.StartedAt+.6+i*.65+random.NextDouble()*.15;}
            if(game==PartyMiniGame.Greed)
            {
                greedThreshold=random.Next(GreedMinimum,GreedMaximum+1);
                double burstScore=Math.Ceiling(GreedMinimum+(greedThreshold-GreedMinimum)*Math.Pow(random.NextDouble(),.25));
                burstAt=s.StartedAt+GreedElapsedAt(burstScore);
            }
        }
        private void BeginQuickHands(double now)
        {var s=State;s.Phase=ClockPhase.QuickHands;s.StartedAt=s.PhaseAt=now;s.PlayedMask|=PartyGames.Bit(s.MiniGame);State=s;}
        private void PrepareQuickHands(double now)
        {var s=State;s.Phase=ClockPhase.QuickCountdown;s.PhaseAt=now;s.StartedAt=now+QuickCountdownDuration;State=s;}
        private void PrepareItems(double now)
        {var s=State;s.Phase=ClockPhase.ItemPreparation;s.PhaseAt=now;s.StartedAt=now+ItemPreparationDuration;State=s;}
        public void RecordQuickDeaths(int killed,int survivors)
        {
            if(State.Phase!=ClockPhase.QuickHands || killed==0)return;
            var s=State;var seats=new List<int>();
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(killed,i))seats.Add(i);
            // Equal death batches stay tied until the quick-hands round has ended.
            for(int i=0;i<seats.Count;i++){var e=s.ForSeat(seats[i]);e.Rank=survivors+1;s.Set(seats[i],e);}
            if(s.Last<0)s.Last=seats[seats.Count-1];
            State=s;
        }
        public void FinishQuick(int winner,int loser,double now)
        {
            var s=State;s.First=winner;s.Last=loser;
            if(winner>=0){var e=s.ForSeat(winner);e.Rank=1;s.Set(winner,e);}State=s;
            var order=new List<int>();for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.ParticipantsMask,i))order.Add(i);
            BeginDiceRanking(order,i=>s.ForSeat(i).Rank>0?s.ForSeat(i).Rank:int.MaxValue,now);
        }
        public bool HasQuickTies
        {get{int best=int.MaxValue,count=0;for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)if(RouletteRules.Has(State.ParticipantsMask,seat))
            {int rank=State.ForSeat(seat).Rank;if(rank<=0)return false;if(rank<best){best=rank;count=1;}else if(rank==best)count++;}return count>1;}}
        void BeginDiceRanking(List<int> order,Func<int,int> key,double now)
        {
            order.Sort((a,b)=>key(a).CompareTo(key(b)));var groups=new List<List<int>>();
            for(int begin=0;begin<order.Count;)
            {int end=begin+1;while(end<order.Count&&key(order[end])==key(order[begin]))end++;groups.Add(order.GetRange(begin,end-begin));begin=end;}
            if(dice.Begin(groups,now,true)){var s=State;s.Phase=ClockPhase.Dice;s.PhaseAt=now;s.Dice=dice.State;State=s;}
            else FinishDiceRanking(now);
        }
        void FinishDiceRanking(double now)
        {
            var s=State;var order=dice.Order;bool quick=s.MiniGame==PartyMiniGame.QuickHands;
            for(int rank=0;rank<order.Count;rank++)
            {int seat=order[rank];var e=s.ForSeat(seat);var roll=dice.State.ForSeat(seat);e.Rank=rank+1;e.Dice=roll.Face;e.Rolls=roll.Rolls;s.Set(seat,e);}
            // Failed ties still earn a shooting opportunity; countdown timeouts never enter this order.
            s.First=order.Count>0?order[0]:-1;
            s.Last=order.Count>1?order[order.Count-1]:-1;
            if(quick)s.EliminatedMask=s.Last>=0?1<<s.Last:0;
            s.Dice=dice.State;s.Phase=quick?ClockPhase.QuickResults:ClockPhase.Ranking;s.PhaseAt=now;State=s;
        }
        public bool StopDice(int seat,int iteration,int round,double now)
        {
            if(State.Phase!=ClockPhase.Dice||iteration!=State.Iteration)return false;
            if(State.MiniGame==PartyMiniGame.Memory){if(!memory.StopDice(seat,round,now))return false;SyncMemory(now);return true;}
            if(!dice.Stop(seat,round,now))return false;var s=State;s.Dice=dice.State;State=s;return true;
        }
        public int ScoreAt(double sample)=>GreedScoreAt(State.StartedAt,sample);
        public int MemoryProgress(int seat)=>seat<0||seat>=RouletteRules.MaxPlayers?0:memory.State.ForSeat(seat).Count;
        public int MemoryDirection(int index)=>DuelArrowMemory.DirectionAt(memory.State.AnswerSeed,index);
        public bool InputMemory(int seat,int iteration,int round,int index,int direction,double now)
        {
            if(State.Phase!=ClockPhase.Memory||iteration!=State.Iteration||!memory.Input(seat,round,index,direction,now))return false;
            SyncMemory(now);return true;
        }
        private void SyncMemory(double now)
        {
            var s=State;s.Memory=memory.State;s.Dice=memory.Dice;
            if(s.Memory.Phase==ArrowMemoryPhase.Dice){s.Phase=ClockPhase.Dice;s.PhaseAt=now;}
            if(s.Memory.Phase==ArrowMemoryPhase.Finished&&s.Phase!=ClockPhase.Ranking)
            {
                s.Phase=ClockPhase.Ranking;s.PhaseAt=now;s.First=s.Memory.Winner;s.Last=-1;int worstRank=0;
                for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.ParticipantsMask,i))
                {
                    var entry=s.Memory.ForSeat(i);s.Set(i,new ClockEntry{Rank=entry.Rank,Points=entry.Count,Error=entry.Errors,Dice=entry.Dice,Rolls=entry.Rolls});
                    if(i!=s.First&&entry.Rank>worstRank){s.Last=i;worstRank=entry.Rank;}
                }
            }
            State=s;
        }
        private void Fail(int seat,MiniFailure failure)
        {var s=State;var e=s.ForSeat(seat);e.Failure=failure;e.Points=0;e.Error=int.MaxValue;e.Centiseconds=-1;s.Set(seat,e);State=s;stopped|=1<<seat;}
        public bool Stop(int seat,int iteration,double sample,double now)
        {
            if(State.Phase!=ClockPhase.Counting || iteration!=State.Iteration || !RouletteRules.Has(State.ParticipantsMask,seat) || RouletteRules.Has(stopped,seat) || double.IsNaN(sample) || double.IsInfinity(sample) || sample<State.StartedAt || sample<now-.3 || sample>now+.05) return false;
            var s=State;var entry=new ClockEntry();
            if(s.MiniGame==PartyMiniGame.Countdown)
            {if(sample>Deadline)return false;int ticks=Quantize(sample-s.StartedAt);entry.Centiseconds=ticks;entry.Error=Math.Abs(ticks-s.Target);}
            else if(s.MiniGame==PartyMiniGame.Signal)
            {if(sample>Deadline)return false;if(sample<goAt){Fail(seat,MiniFailure.EarlyInput);}else{entry.Centiseconds=Quantize(sample-goAt);entry.Error=entry.Centiseconds;}}
            else if(s.MiniGame==PartyMiniGame.Greed)
            {if(now>burstAt+.3)return false;if(sample>=burstAt){Fail(seat,MiniFailure.Burst);}else{entry.Points=ScoreAt(sample);entry.Centiseconds=Quantize(sample-s.StartedAt);entry.Error=-entry.Points;}}
            else return false;
            if(!RouletteRules.Has(stopped,seat)){s=State;s.Set(seat,entry);State=s;stopped|=1<<seat;}
            if(stopped==State.ParticipantsMask)Resolve(now);
            return true;
        }
        private bool TickMiniGame(double now)
        {
            var s=State;bool changed=false;
            if(s.MiniGame==PartyMiniGame.Signal)
            {
                MiniSignal cue=now>=goAt?MiniSignal.Go:MiniSignal.Wait;double at=cue==MiniSignal.Go?goAt:s.StartedAt;
                if(cue!=MiniSignal.Go)for(int i=0;i<fakeCount;i++)if(now>=fakeAt[i]&&now<fakeAt[i]+.35){cue=MiniSignal.Fake;at=fakeAt[i];}
                if(cue!=s.Signal||at!=s.SignalAt){s.Signal=cue;s.SignalAt=at;State=s;changed=true;}
                if(now>=Deadline+.3){for(int i=0;i<6;i++)if(RouletteRules.Has(s.ParticipantsMask&~stopped,i))Fail(i,MiniFailure.MissedSignal);Resolve(now);return true;}
            }
            else if(s.MiniGame==PartyMiniGame.Greed)
            {
                int points=ScoreAt(Math.Min(now,burstAt));if(points!=s.Points){s.Points=points;changed=true;}
                if(now>=burstAt){s.Signal=MiniSignal.Burst;s.SignalAt=burstAt;changed|=State.Signal!=MiniSignal.Burst;}State=s;
                if(now>=burstAt+.3)for(int i=0;i<6;i++)if(RouletteRules.Has(s.ParticipantsMask&~stopped,i)){Fail(i,MiniFailure.Burst);changed=true;}
                if(now>=burstAt+.9){Resolve(now);return true;}
            }
            else if(now>=Deadline+.3){Resolve(now);return true;}
            return changed;
        }
        private void Resolve(double now)
        {
            var s=State;var order=new List<int>();s.EliminatedMask=s.MiniGame==PartyMiniGame.Countdown?s.ParticipantsMask&~stopped:0;
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.MiniGame==PartyMiniGame.Countdown?s.ParticipantsMask&stopped:s.ParticipantsMask,i))order.Add(i);
            int Key(int i) => s.ForSeat(i).Failure!=MiniFailure.None||s.ForSeat(i).Centiseconds<0?int.MaxValue:s.ForSeat(i).Error;
            State=s;BeginDiceRanking(order,Key,now);
        }
        public void Return(double now) { var s=State;s.Phase=ClockPhase.Returning;s.PhaseAt=now;State=s; }
        public bool Tick(double now,int alive,double shotLimit=ShotDuration,bool shotInFlight=false,bool manualSelection=false,bool beginChosenGame=false,int connected=63)
        {
            var s=State;bool changed=false;
            int current=s.MiniGame==PartyMiniGame.QuickHands?s.ParticipantsMask&connected:s.ParticipantsMask&(alive|s.EliminatedMask);
            if(current!=s.ParticipantsMask)
            {
                s.ParticipantsMask=current;stopped&=current;State=s;changed=true;
                if(s.Phase==ClockPhase.Counting && RouletteRules.Count(current)>=2 && stopped==current)Resolve(now);
                else if((s.Phase==ClockPhase.Ranking||s.Phase==ClockPhase.Shooting||s.Phase==ClockPhase.ItemPreparation&&s.MiniGame!=PartyMiniGame.QuickHands)&&(!RouletteRules.Has(alive,s.First)||RouletteRules.Count(alive)<2)&&(s.Phase!=ClockPhase.Shooting||!shotInFlight))Return(now);
            }
            s=State;
            if(s.Phase==ClockPhase.Dice)
            {
                if(s.MiniGame==PartyMiniGame.Memory)
                {if(memory.Tick(now,alive)){SyncMemory(now);return true;}return changed;}
                if(dice.Tick(now,connected))
                {if(dice.State.Phase==DuelDicePhase.Finished)FinishDiceRanking(now);else{s=State;s.Dice=dice.State;State=s;}return true;}
                return changed;
            }
            if(s.Phase==ClockPhase.Shooting&&!shotInFlight&&(!RouletteRules.Has(alive,s.First)||RouletteRules.Count(alive)<2)){Return(now);return true;}
            if(s.Phase==ClockPhase.Selecting && (beginChosenGame || !manualSelection && now-s.PhaseAt>=PartyGames.SelectionDuration))
            {if(s.MiniGame==PartyMiniGame.QuickHands)PrepareItems(now);else Begin(current,now,s.MiniGame);return true;}
            if(s.Phase==ClockPhase.ItemPreparation&&now>=s.StartedAt)
            {if(s.MiniGame==PartyMiniGame.QuickHands)PrepareQuickHands(now);else{s.Phase=ClockPhase.Shooting;s.PhaseAt=now;State=s;}return true;}
            if(s.Phase==ClockPhase.QuickCountdown && now>=s.StartedAt){BeginQuickHands(now);return true;}
            if(s.Phase==ClockPhase.Memory)
            {if(memory.Tick(now,alive)){SyncMemory(now);return true;}return changed;}
            if(s.Phase==ClockPhase.Counting)return TickMiniGame(now)||changed;
            if(s.Phase==ClockPhase.Ranking&&now-s.PhaseAt>=RevealFor(s.MiniGame)){if(s.First<0||s.Last<0){Return(now);return true;}PrepareItems(now);return true;}
            if(s.Phase==ClockPhase.Shooting&&!shotInFlight&&shotLimit>0&&now-ShootingStartsAt(s)>=shotLimit){Return(now);return true;}
            return changed;
        }
    }
}
