namespace HelloWorld
{
    public enum DuelWeapon : byte { Revolver, SawedOff, Taser, BAS }
    public enum DuelItem : byte { Recovery, Taser, Ink, Bullet, NoruFace }

    public static class WeaponRules
    {
        public const int Count=4;
        public static readonly DuelWeapon[] Selectable={DuelWeapon.Revolver,DuelWeapon.SawedOff,DuelWeapon.BAS};
        public static bool IsValid(DuelWeapon weapon) => IsSelectable(weapon) || weapon == DuelWeapon.Taser;
        public static bool IsSelectable(DuelWeapon weapon)=>weapon==DuelWeapon.Revolver||weapon==DuelWeapon.SawedOff||weapon==DuelWeapon.BAS;
        public static bool IsPrecision(DuelWeapon weapon)=>weapon==DuelWeapon.Revolver||weapon==DuelWeapon.BAS;
        public static bool SupportsAds(DuelWeapon weapon)=>weapon==DuelWeapon.BAS;
        public static bool IsHitscan(DuelWeapon weapon)=>weapon==DuelWeapon.Taser||weapon==DuelWeapon.BAS;
        public static bool CanFireLoaded(DuelWeapon weapon,DuelAction action,int ammo)=>ammo>0&&(action==DuelAction.Aim||weapon==DuelWeapon.Revolver);
        public static string Name(DuelWeapon weapon) => weapon == DuelWeapon.BAS ? "윈체스터" : weapon == DuelWeapon.Taser ? "테이저건" : weapon == DuelWeapon.SawedOff ? "단발 소드오프" : "6연발 리볼버";
        public static int InputLength(DuelWeapon weapon) => weapon == DuelWeapon.SawedOff || weapon==DuelWeapon.BAS ? 4 : 6;
        public static int InputLength(DuelWeapon weapon,DuelAction action)=>weapon==DuelWeapon.Revolver&&action==DuelAction.Load?3:InputLength(weapon);
        public static int Capacity(DuelWeapon weapon)=>weapon==DuelWeapon.Revolver?6:1;
        public static DuelAction ReloadAction(DuelWeapon weapon)=>weapon==DuelWeapon.Revolver?DuelAction.CloseBarrel:weapon==DuelWeapon.SawedOff||weapon==DuelWeapon.BAS?DuelAction.OpenBarrel:DuelAction.Pickup;
        public static int Chamber(int mask,int start)
        {for(int i=0;i<6;i++){int slot=(start+i)%6;if((mask&(1<<slot))!=0)return slot;}return -1;}
        public static int Damage(DuelWeapon weapon,DuelHitPart part) => part < DuelHitPart.Head || part > DuelHitPart.Legs ? 0 :
            weapon == DuelWeapon.BAS ? (part==DuelHitPart.Head?100:0) : weapon == DuelWeapon.Taser ? 5 : weapon == DuelWeapon.SawedOff ? 75 : part == DuelHitPart.Head ? 100 : part == DuelHitPart.Body ? 50 : part == DuelHitPart.Legs ? 35 : 0;
        public static int LivesLost(DuelWeapon weapon,DuelHitPart part)=>weapon==DuelWeapon.BAS&&part==DuelHitPart.Head?2:1;
        public static DuelAction NextAction(DuelWeapon weapon, DuelAction action)
        {
            if(weapon==DuelWeapon.BAS)
            {
                if(action==DuelAction.Pickup)return DuelAction.OpenBarrel;
                if(action==DuelAction.OpenBarrel)return DuelAction.Load;
                if(action==DuelAction.Load)return DuelAction.CloseBarrel;
                if(action==DuelAction.CloseBarrel)return DuelAction.Aim;
            }
            if(weapon==DuelWeapon.Revolver)
            {
                if(action==DuelAction.Pickup)return DuelAction.CockHammer;
                if(action==DuelAction.CockHammer)return DuelAction.UnlockBarrel;
                if(action==DuelAction.UnlockBarrel)return DuelAction.OpenBarrel;
                if(action==DuelAction.OpenBarrel)return DuelAction.CloseBarrel;
                if(action==DuelAction.CloseBarrel)return DuelAction.Load;
                if(action==DuelAction.Load)return DuelAction.Aim;
            }
            if (action == DuelAction.Pickup) return weapon == DuelWeapon.SawedOff ? DuelAction.OpenBarrel : DuelAction.UnlockBarrel;
            if (action == DuelAction.UnlockBarrel) return DuelAction.OpenBarrel;
            if (action == DuelAction.OpenBarrel) return DuelAction.Load;
            if (action == DuelAction.Load) return DuelAction.CloseBarrel;
            if (action == DuelAction.CloseBarrel) return weapon == DuelWeapon.SawedOff ? DuelAction.Aim : DuelAction.CockHammer;
            if (action == DuelAction.CockHammer) return DuelAction.Aim;
            if (action == DuelAction.Aim) return DuelAction.Fire;
            if (action == DuelAction.Fire) return ReloadAction(weapon);
            return DuelAction.Pickup;
        }
        // Numbered neighbours only: seats 1 and 6 do not share splash. Never skip empty seats.
        public static bool AreAdjacent(int a, int b) => a >= 0 && a < RouletteRules.MaxPlayers &&
            b >= 0 && b < RouletteRules.MaxPlayers &&
            System.Math.Abs(a - b) == 1;
    }
}
