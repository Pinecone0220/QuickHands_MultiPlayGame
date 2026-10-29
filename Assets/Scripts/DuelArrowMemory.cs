using System;
using System.Collections.Generic;
namespace HelloWorld
{
    public enum ArrowMemoryPhase : byte { None, Showing, Input, Results, Finished, Dice }
    public struct ArrowMemoryEntry : IEquatable<ArrowMemoryEntry>
    {
        public int Count,Errors,Dice,Rolls,Rank;
        public bool Locked;
        public bool Equals(ArrowMemoryEntry o)=>Count==o.Count&&Errors==o.Errors&&Dice==o.Dice&&Rolls==o.Rolls&&Rank==o.Rank&&Locked==o.Locked;
    }
    public struct ArrowMemoryState : IEquatable<ArrowMemoryState>
    {
        public ArrowMemoryPhase Phase;
        public int Round,Length,ActiveMask,RoundMask,EliminatedMask,AnswerSeed,Winner;
        public double PhaseAt,InputEndsAt;
        public ArrowMemoryEntry Seat0,Seat1,Seat2,Seat3,Seat4,Seat5;
        public ArrowMemoryEntry ForSeat(int seat)=>seat==0?Seat0:seat==1?Seat1:seat==2?Seat2:seat==3?Seat3:seat==4?Seat4:seat==5?Seat5:default;
        public void Set(int seat,ArrowMemoryEntry entry)
        {if(seat==0)Seat0=entry;else if(seat==1)Seat1=entry;else if(seat==2)Seat2=entry;else if(seat==3)Seat3=entry;else if(seat==4)Seat4=entry;else if(seat==5)Seat5=entry;}
        public bool Equals(ArrowMemoryState o)=>Phase==o.Phase&&Round==o.Round&&Length==o.Length&&ActiveMask==o.ActiveMask&&RoundMask==o.RoundMask&&EliminatedMask==o.EliminatedMask&&AnswerSeed==o.AnswerSeed&&Winner==o.Winner&&PhaseAt==o.PhaseAt&&InputEndsAt==o.InputEndsAt&&Seat0.Equals(o.Seat0)&&Seat1.Equals(o.Seat1)&&Seat2.Equals(o.Seat2)&&Seat3.Equals(o.Seat3)&&Seat4.Equals(o.Seat4)&&Seat5.Equals(o.Seat5);
    }
    // Server-owned answers and partial records. No HP or party lives are changed here.
    public sealed class DuelArrowMemory
    {
        public const double ShowDuration=3,InputDuration=10,ResultDuration=DuelClockContest.MiniResultsDuration;
        public ArrowMemoryState State { get; private set; }
        readonly Random random;
        readonly DuelDiceContest dice;
        public DuelDiceState Dice=>dice.State;
        public DuelArrowMemory(Random random){this.random=random;dice=new DuelDiceContest(random);}
        public static int DirectionAt(int seed,int index)
        {
            uint value=unchecked((uint)seed+(uint)(index+1)*0x9e3779b9u);
            value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;value^=value>>16;
            return (int)(value&3);
        }
        public void Reset(){State=default;dice.Reset();}
        public ArrowMemoryState PublicState()
        {
            var s=State;
            if(s.Phase==ArrowMemoryPhase.Input||s.Phase==ArrowMemoryPhase.None)
                s.AnswerSeed=0;
            if(s.Phase==ArrowMemoryPhase.Input||s.Phase==ArrowMemoryPhase.Showing)
                for(int i=0;i<RouletteRules.MaxPlayers;i++)s.Set(i,new ArrowMemoryEntry{Errors=-1,Rank=s.ForSeat(i).Rank});
            return s;
        }
        public void Begin(int alive,double now)
        {
            dice.Reset();
            State=new ArrowMemoryState{Round=1,Length=3,ActiveMask=alive&63,Winner=-1};
            Show(now);
            if(RouletteRules.Count(State.ActiveMask)<2)Finish(now);
        }
        void Show(double now)
        {
            var s=State;s.Phase=ArrowMemoryPhase.Showing;s.PhaseAt=now;s.InputEndsAt=0;
            s.RoundMask=s.ActiveMask;s.EliminatedMask=0;s.AnswerSeed=random.Next(1,int.MaxValue);
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.ActiveMask,i))s.Set(i,default);
            State=s;
        }
        public bool Input(int seat,int round,int index,int direction,double now)
        {
            var s=State;
            if(seat<0||seat>=RouletteRules.MaxPlayers||direction<0||direction>3||round!=s.Round||s.Phase!=ArrowMemoryPhase.Input||!RouletteRules.Has(s.ActiveMask,seat)||double.IsNaN(now)||double.IsInfinity(now)||now<s.PhaseAt||now>=s.InputEndsAt)return false;
            var e=s.ForSeat(seat);if(e.Locked||index!=e.Count||index<0||index>=s.Length)return false;
            if(direction!=DirectionAt(s.AnswerSeed,index))e.Errors++;
            e.Count++;e.Locked=e.Count==s.Length;s.Set(seat,e);State=s;
            if(AllLocked())Resolve(now);
            return true;
        }
        bool AllLocked()
        {
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(State.ActiveMask,i)&&!State.ForSeat(i).Locked)return false;
            return true;
        }
        public bool StopDice(int seat,int round,double now)=>State.Phase==ArrowMemoryPhase.Dice&&dice.Stop(seat,round,now);
        void FinishDice(double now)
        {
            var s=State;var order=dice.Order;
            for(int i=0;i<order.Count;i++){var e=s.ForSeat(order[i]);var d=dice.State.ForSeat(order[i]);e.Rank=i==0?1:2;e.Dice=d.Face;e.Rolls=d.Rolls;s.Set(order[i],e);}
            s.EliminatedMask|=order.Count>0?s.ActiveMask&~(1<<order[0]):s.ActiveMask;
            s.ActiveMask=order.Count>0?1<<order[0]:0;State=s;Finish(now);
        }
        void Resolve(double now)
        {
            var s=State;var incorrect=new List<int>();var participants=new List<int>();int least=int.MaxValue;
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.ActiveMask,i))
            {
                var e=s.ForSeat(i);e.Errors+=s.Length-e.Count;e.Locked=true;s.Set(i,e);
                participants.Add(i);if(e.Errors>0)incorrect.Add(i);least=Math.Min(least,e.Errors);
            }
            s.EliminatedMask=0;
            // If everyone failed, only those with the fewest errors can win.
            if(incorrect.Count>0&&incorrect.Count==participants.Count)
            {
                var contenders=new List<int>();
                foreach(int seat in participants)
                {
                    var e=s.ForSeat(seat);
                    if(e.Errors==least)contenders.Add(seat);
                    else{e.Rank=2;s.Set(seat,e);s.ActiveMask&=~(1<<seat);s.EliminatedMask|=1<<seat;}
                }
                if(contenders.Count==1){State=s;Finish(now);return;}
                s.Phase=ArrowMemoryPhase.Dice;s.PhaseAt=now;State=s;
                dice.Begin(new List<List<int>>{contenders},now,true);return;
            }
            foreach(int loser in incorrect)
            {
                var e=s.ForSeat(loser);e.Rank=2;s.Set(loser,e);
                s.ActiveMask&=~(1<<loser);s.EliminatedMask|=1<<loser;
            }
            s.Phase=ArrowMemoryPhase.Results;s.PhaseAt=now;State=s;
            if(RouletteRules.Count(s.ActiveMask)<2)Finish(now);
        }
        void Finish(double now)
        {
            var s=State;s.Phase=ArrowMemoryPhase.Finished;s.PhaseAt=now;s.Winner=-1;
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.ActiveMask,i)){s.Winner=i;var e=s.ForSeat(i);e.Rank=1;s.Set(i,e);}
            State=s;
        }
        public bool Tick(double now,int alive)
        {
            var s=State;if(s.Phase==ArrowMemoryPhase.None||s.Phase==ArrowMemoryPhase.Finished)return false;
            bool changed=false;int gone=s.ActiveMask&~alive;
            if(gone!=0)
            {
                for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(gone,i)){var e=s.ForSeat(i);e.Rank=2;s.Set(i,e);}
                s.ActiveMask&=alive;s.RoundMask&=alive;s.EliminatedMask&=alive;State=s;changed=true;
                if(RouletteRules.Count(s.ActiveMask)<2){Finish(now);return true;}
                if(s.Phase==ArrowMemoryPhase.Input&&AllLocked()){Resolve(now);return true;}
            }
            if(s.Phase==ArrowMemoryPhase.Showing&&now>=s.PhaseAt+ShowDuration)
            {s.Phase=ArrowMemoryPhase.Input;s.PhaseAt=now;s.InputEndsAt=now+InputDuration;State=s;return true;}
            if(s.Phase==ArrowMemoryPhase.Input&&now>=s.InputEndsAt){Resolve(now);return true;}
            if(s.Phase==ArrowMemoryPhase.Dice&&dice.Tick(now,alive))
            {if(dice.State.Phase==DuelDicePhase.Finished)FinishDice(now);return true;}
            if(s.Phase==ArrowMemoryPhase.Results&&now>=s.PhaseAt+ResultDuration)
            {s.Round++;s.Length++;State=s;Show(now);return true;}
            return changed;
        }
    }
}
