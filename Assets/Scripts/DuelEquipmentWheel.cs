namespace HelloWorld
{
    public enum DuelWheelSlot { Weapon, Taser, Ink }

    // Chooses only equipment that can be held; consumables never activate on scroll.
    public static class DuelEquipmentWheel
    {
        public static DuelWheelSlot NextPreview(DuelActionState a,bool lobby,int direction)
        {
            var current=(DuelWheelSlot)a.PreviewItem;
            if(direction==0)return current;
            bool taser=lobby?DuelLoadout.Has(a.SelectedItems,DuelItem.Taser):a.HasTaser;
            bool ink=lobby?DuelLoadout.Has(a.SelectedItems,DuelItem.Ink):a.Ink.Owned;
            int index=(int)current,step=direction>0?1:-1;
            for(int i=0;i<3;i++){index=(index+step+3)%3;if(index==0||index==1&&taser||index==2&&ink)return (DuelWheelSlot)index;}
            return current;
        }
        public static DuelWheelSlot Current(DuelActionState a)=>
            a.Ink.Armed?DuelWheelSlot.Ink:a.QuickEquipped&&a.QuickItem==2?DuelWheelSlot.Taser:DuelWheelSlot.Weapon;

        public static DuelWheelSlot Next(DuelActionState a,bool practice,bool quick,bool shooting,bool noCooldown,int iteration,double now,int direction)
        {
            var current=Current(a);
            if(direction==0||a.IsStunned(now)||!practice&&!quick&&!shooting)return current;
            bool throwing=DuelInk.Busy(a.Ink,now)&&!a.Ink.Armed;
            bool flight=a.Ink.ThrownAt>=0&&a.Ink.ImpactAt<a.Ink.ThrownAt&&now<a.Ink.ThrownAt+DuelInk.ReleaseDelay+DuelInk.Lifetime;
            bool taser=false,ink=false;
            if(practice)
            {
                taser=DuelLoadout.Has(a.SelectedItems,DuelItem.Taser)&&!throwing;
                ink=DuelLoadout.Has(a.SelectedItems,DuelItem.Ink)&&!throwing&&(!flight||current==DuelWheelSlot.Ink);
            }
            else if(quick)
            {
                taser=a.QuickItem==2&&(current==DuelWheelSlot.Taser||!a.QuickSpent&&a.HasTaser&&(noCooldown||iteration>=a.TaserReadyIteration))&&!throwing;
                ink=a.QuickItem==3&&(current==DuelWheelSlot.Ink||!a.QuickSpent&&a.Ink.Owned&&!flight)&&!throwing;
            }
            else
                ink=a.PreparedItem==(int)DuelItem.Ink+1&&(a.Ink.Armed||a.Ink.Owned&&!flight)&&!throwing;

            var slots=new[]{DuelWheelSlot.Weapon,DuelWheelSlot.Taser,DuelWheelSlot.Ink};
            int index=current==DuelWheelSlot.Taser?1:current==DuelWheelSlot.Ink?2:0;
            int step=direction>0?1:-1;
            for(int i=0;i<3;i++)
            {
                index=(index+step+3)%3;
                var candidate=slots[index];
                if(candidate==DuelWheelSlot.Weapon||candidate==DuelWheelSlot.Taser&&taser||candidate==DuelWheelSlot.Ink&&ink)return candidate;
            }
            return current;
        }
    }
}
