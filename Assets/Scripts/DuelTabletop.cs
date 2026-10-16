using System;
namespace HelloWorld
{
    public enum DuelTableProp : byte { None, Bottle, Glass }
    public enum DuelDrinkAction : byte { None, Pour, Sip, Inspect }
    public struct DuelTableEntry : IEquatable<DuelTableEntry>
    {
        public DuelDrinkAction Action;
        public double StartedAt,BottleBrokenAt,GlassBrokenAt;
        public bool Equals(DuelTableEntry o)=>Action==o.Action&&StartedAt==o.StartedAt&&BottleBrokenAt==o.BottleBrokenAt&&GlassBrokenAt==o.GlassBrokenAt;
    }
    public struct DuelTabletopState : IEquatable<DuelTabletopState>
    {
        public int BottlesBrokenMask,GlassesBrokenMask,FilledMask,DrankMask;
        public DuelTableEntry Seat0,Seat1,Seat2,Seat3,Seat4,Seat5;
        public DuelTableEntry ForSeat(int seat)=>seat==0?Seat0:seat==1?Seat1:seat==2?Seat2:seat==3?Seat3:seat==4?Seat4:Seat5;
        public void SetSeat(int seat,DuelTableEntry e)
        {switch(seat){case 0:Seat0=e;break;case 1:Seat1=e;break;case 2:Seat2=e;break;case 3:Seat3=e;break;case 4:Seat4=e;break;case 5:Seat5=e;break;}}
        public bool Broken(int seat,DuelTableProp kind)=>RouletteRules.Has(kind==DuelTableProp.Bottle?BottlesBrokenMask:GlassesBrokenMask,seat);
        public bool Equals(DuelTabletopState o)=>BottlesBrokenMask==o.BottlesBrokenMask&&GlassesBrokenMask==o.GlassesBrokenMask&&FilledMask==o.FilledMask&&DrankMask==o.DrankMask&&Seat0.Equals(o.Seat0)&&Seat1.Equals(o.Seat1)&&Seat2.Equals(o.Seat2)&&Seat3.Equals(o.Seat3)&&Seat4.Equals(o.Seat4)&&Seat5.Equals(o.Seat5);
    }
    // Server-owned tabletop and one-drink-per-party activity; no input/aim effects.
    public sealed class DuelTabletop
    {
        public DuelTabletopState State { get; private set; }
        readonly double[] next=new double[RouletteRules.MaxPlayers];
        public static double Duration(DuelDrinkAction action)=>action==DuelDrinkAction.Pour?4.6:action==DuelDrinkAction.Sip?3.8:4.8;
        public static AimVector Rest(int seat,DuelTableProp kind)=>DuelGunplay.Local(seat,kind==DuelTableProp.Bottle?.66f:-.18f,kind==DuelTableProp.Bottle?1.10f:.995f,(kind==DuelTableProp.Bottle?.82f:.86f)-DuelGunplay.SeatAdvance);
        public void Reset(double now)
        {State=default;for(int i=0;i<next.Length;i++)next[i]=now+1.5+i*.55;}
        public void Joined(int seat,double now)
        {var s=State;s.DrankMask&=~(1<<seat);var e=s.ForSeat(seat);e.Action=DuelDrinkAction.None;s.SetSeat(seat,e);State=s;next[seat]=now+1.8+seat*.35;}
        public bool Break(int seat,DuelTableProp kind,double now)
        {
            if(seat<0||seat>=RouletteRules.MaxPlayers||(kind!=DuelTableProp.Bottle&&kind!=DuelTableProp.Glass)||State.Broken(seat,kind))return false;
            var s=State;var e=s.ForSeat(seat);int bit=1<<seat;
            if(kind==DuelTableProp.Bottle){s.BottlesBrokenMask|=bit;e.BottleBrokenAt=now;if(e.Action!=DuelDrinkAction.Sip)e.Action=DuelDrinkAction.None;}
            else{s.GlassesBrokenMask|=bit;s.FilledMask&=~bit;e.GlassBrokenAt=now;e.Action=DuelDrinkAction.None;}
            s.SetSeat(seat,e);State=s;next[seat]=now+1;return true;
        }
        public bool Tick(int eligible,bool allowed,double now)
        {
            var before=State;var s=State;
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
            {
                int bit=1<<seat;var e=s.ForSeat(seat);
                if(!allowed||!RouletteRules.Has(eligible,seat))
                {if(e.Action!=DuelDrinkAction.None){e.Action=DuelDrinkAction.None;next[seat]=now+1;s.SetSeat(seat,e);}continue;}
                if(e.Action!=DuelDrinkAction.None)
                {
                    double progress=(now-e.StartedAt)/Duration(e.Action);
                    if(e.Action==DuelDrinkAction.Pour&&progress>=.62)s.FilledMask|=bit;
                    if(e.Action==DuelDrinkAction.Sip&&progress>=.62){s.DrankMask|=bit;s.FilledMask&=~bit;}
                    if(progress>=1){e.Action=DuelDrinkAction.None;next[seat]=now+(RouletteRules.Has(s.GlassesBrokenMask,seat)?8+seat:.35);s.SetSeat(seat,e);}
                    continue;
                }
                if(now<next[seat]||RouletteRules.Has(s.DrankMask,seat))continue;
                bool bottle= !RouletteRules.Has(s.BottlesBrokenMask,seat),glass= !RouletteRules.Has(s.GlassesBrokenMask,seat);
                e.Action=glass?(RouletteRules.Has(s.FilledMask,seat)?DuelDrinkAction.Sip:bottle?DuelDrinkAction.Pour:DuelDrinkAction.None):bottle?DuelDrinkAction.Inspect:DuelDrinkAction.None;
                if(e.Action!=DuelDrinkAction.None){e.StartedAt=now;s.SetSeat(seat,e);}
            }
            State=s;return !s.Equals(before);
        }
    }
}
