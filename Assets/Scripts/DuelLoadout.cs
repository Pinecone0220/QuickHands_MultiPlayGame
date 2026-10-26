namespace HelloWorld
{
    public static class DuelLoadout
    {
        public const int Required=2;
        public const int ItemCount=3,Default=(1<<(int)DuelItem.Recovery)|(1<<(int)DuelItem.Taser),All=(1<<ItemCount)-1;
        public static bool Selectable(DuelItem item)=>item>=DuelItem.Recovery&&item<=DuelItem.Ink;
        public static int AiSelection(int seat)=>seat%3==1?5:seat%3==2?6:Default;
        public static bool Has(int mask,DuelItem item)=>Selectable(item)&&(mask&(1<<(int)item))!=0;
        public static int Count(int mask)
        {int count=0;for(int bits=mask&All;bits!=0;bits&=bits-1)count++;return count;}
        public static bool Complete(int mask)=>(mask&~All)==0&&Count(mask)>=Required;
    }
}
