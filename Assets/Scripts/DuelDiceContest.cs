using System;
using System.Collections.Generic;
namespace HelloWorld
{
    public enum DuelDicePhase:byte { None, Rolling, Comparing, Finished }
    public struct DuelDiceEntry:IEquatable<DuelDiceEntry>
    {
        public int Face,Rolls;
        public bool Equals(DuelDiceEntry other)=>Face==other.Face&&Rolls==other.Rolls;
    }
    public struct DuelDiceState:IEquatable<DuelDiceState>
    {
        public DuelDicePhase Phase;
        public int Round,ParticipantsMask,PendingMask,LockedMask;
        public double StartedAt,EndsAt;
        public DuelDiceEntry Seat0,Seat1,Seat2,Seat3,Seat4,Seat5;
        public DuelDiceEntry ForSeat(int seat)=>seat==0?Seat0:seat==1?Seat1:seat==2?Seat2:seat==3?Seat3:seat==4?Seat4:seat==5?Seat5:default;
        public void Set(int seat,DuelDiceEntry e)
        {if(seat==0)Seat0=e;else if(seat==1)Seat1=e;else if(seat==2)Seat2=e;else if(seat==3)Seat3=e;else if(seat==4)Seat4=e;else if(seat==5)Seat5=e;}
        public bool CanStop(int seat)=>Phase==DuelDicePhase.Rolling&&RouletteRules.Has(PendingMask&~LockedMask,seat);
        public bool Equals(DuelDiceState o)=>Phase==o.Phase&&Round==o.Round&&ParticipantsMask==o.ParticipantsMask&&PendingMask==o.PendingMask&&LockedMask==o.LockedMask&&StartedAt==o.StartedAt&&EndsAt==o.EndsAt&&Seat0.Equals(o.Seat0)&&Seat1.Equals(o.Seat1)&&Seat2.Equals(o.Seat2)&&Seat3.Equals(o.Seat3)&&Seat4.Equals(o.Seat4)&&Seat5.Equals(o.Seat5);
    }
    // The server chooses the final face on a validated stop, never from a client's number.
    // Ordered groups preserve the original score/error/death order through repeated ties.
    public sealed class DuelDiceContest
    {
        public const double InputDuration=10,CompareDuration=1.2;
        readonly Random random;
        List<List<int>> groups=new List<List<int>>();
        int allMask;
        bool firstPlaceOnly;
        public DuelDiceState State {get;private set;}
        public List<int> Order {get;private set;}=new List<int>();
        public DuelDiceContest(Random random){this.random=random;}
        public void Reset(){State=default;groups.Clear();Order.Clear();allMask=0;firstPlaceOnly=false;}
        public bool Begin(List<List<int>> orderedGroups,double now,bool firstPlaceOnly=false)
        {
            this.firstPlaceOnly=firstPlaceOnly;
            groups=new List<List<int>>();Order.Clear();int pending=0;allMask=0;
            foreach(var group in orderedGroups)
            {
                if(group.Count==0)continue;groups.Add(new List<int>(group));
                foreach(int seat in group)allMask|=1<<seat;
                if(group.Count>1&&(!firstPlaceOnly||groups.Count==1))foreach(int seat in group)pending|=1<<seat;
            }
            State=new DuelDiceState{Round=State.Round+1,ParticipantsMask=pending,PendingMask=pending,Phase=DuelDicePhase.Rolling,StartedAt=now,EndsAt=now+InputDuration};
            if(pending==0){Complete();return false;}return true;
        }
        public bool Stop(int seat,int round,double now)
        {
            if(round!=State.Round||!State.CanStop(seat)||double.IsNaN(now)||double.IsInfinity(now)||now<State.StartedAt||now>=State.EndsAt)return false;
            Lock(seat);if((State.LockedMask&State.PendingMask)==State.PendingMask)Compare(now);return true;
        }
        void Lock(int seat)
        {
            var s=State;var e=s.ForSeat(seat);e.Face=random.Next(1,9);e.Rolls++;s.Set(seat,e);s.LockedMask|=1<<seat;State=s;
        }
        void Compare(double now){var s=State;s.Phase=DuelDicePhase.Comparing;s.StartedAt=now;s.EndsAt=now+CompareDuration;State=s;}
        void Complete(){Order=new List<int>();foreach(var group in groups)Order.AddRange(group);var s=State;s.Phase=DuelDicePhase.Finished;s.PendingMask=0;State=s;}
        public bool Tick(double now,int connected)
        {
            if(State.Phase!=DuelDicePhase.Rolling&&State.Phase!=DuelDicePhase.Comparing)return false;
            bool changed=false;int gone=allMask&~connected;
            if(gone!=0)
            {
                foreach(var group in groups)group.RemoveAll(seat=>!RouletteRules.Has(connected,seat));
                allMask&=connected;
                groups.RemoveAll(group=>group.Count==0);
                int tiedMask=0;foreach(var group in groups)if(group.Count>1)foreach(int seat in group)tiedMask|=1<<seat;
                var s=State;s.ParticipantsMask&=connected;s.PendingMask&=connected&tiedMask;s.LockedMask&=s.PendingMask;State=s;changed=true;
                if(s.Phase==DuelDicePhase.Rolling&&(s.PendingMask&~s.LockedMask)==0)Compare(now);
            }
            if(State.Phase==DuelDicePhase.Rolling&&now>=State.EndsAt)
            {
                for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)if(State.CanStop(seat))Lock(seat);
                Compare(now);return true;
            }
            if(State.Phase==DuelDicePhase.Comparing&&now>=State.EndsAt)
            {
                var split=new List<List<int>>();int pending=0;var snapshot=State;
                foreach(var group in groups)
                {
                    group.Sort((a,b)=>snapshot.ForSeat(b).Face.CompareTo(snapshot.ForSeat(a).Face));
                    for(int begin=0;begin<group.Count;)
                    {
                        int end=begin+1;while(end<group.Count&&snapshot.ForSeat(group[end]).Face==snapshot.ForSeat(group[begin]).Face)end++;
                        var tied=group.GetRange(begin,end-begin);split.Add(tied);
                        if(tied.Count>1)foreach(int seat in tied)pending|=1<<seat;
                        begin=end;
                    }
                }
                groups=split;
                if(firstPlaceOnly)
                {
                    pending=0;
                    if(groups.Count>0&&groups[0].Count>1)foreach(int seat in groups[0])pending|=1<<seat;
                }
                if(pending==0)Complete();
                else{var s=State;s.Round++;s.Phase=DuelDicePhase.Rolling;s.ParticipantsMask|=pending;s.PendingMask=pending;s.LockedMask=0;s.StartedAt=now;s.EndsAt=now+InputDuration;
                    for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)if(RouletteRules.Has(pending,seat)){var e=s.ForSeat(seat);e.Face=0;s.Set(seat,e);}State=s;}
                return true;
            }
            return changed;
        }
    }
}
