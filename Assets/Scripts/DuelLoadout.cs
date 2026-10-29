namespace HelloWorld
{
    public static class DuelLoadout
    {
        public const int Required=2;
        public const int ItemCount=5,Default=(1<<(int)DuelItem.Recovery)|(1<<(int)DuelItem.Taser),All=(1<<ItemCount)-1;
        public static bool CanPreview(DuelPhase phase,DuelStage stage,ClockPhase clock,bool connected,bool alive,bool practice,bool dead)
        {
            if(!connected||practice||dead||phase==DuelPhase.Finished)return false;
            if(phase==DuelPhase.Waiting)return stage==DuelStage.Idle;
            return phase==DuelPhase.Playing&&alive&&(stage==DuelStage.Countdown||stage==DuelStage.Racing&&
                clock!=ClockPhase.Shooting&&clock!=ClockPhase.QuickHands);
        }
        public static bool Selectable(DuelItem item)=>item>=DuelItem.Recovery&&item<=DuelItem.NoruFace;
        private static readonly int[] aiPairs={9,18,5,6,17,10,20,3,12,24};
        public static int AiSelection(int seat)=>AiSelection(seat,1);
        public static int AiSelection(int seat,int round)=>seat<=0?Default:aiPairs[(seat-1+(System.Math.Max(1,round)-1)%aiPairs.Length*3)%aiPairs.Length];
        public static bool Has(int mask,DuelItem item)=>Selectable(item)&&(mask&(1<<(int)item))!=0;
        public static int Count(int mask)
        {int count=0;for(int bits=mask&All;bits!=0;bits&=bits-1)count++;return count;}
        public static bool Complete(int mask)=>(mask&~All)==0&&Count(mask)>=Required;
    }
}
