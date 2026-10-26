namespace HelloWorld
{
    public enum DuelMode : byte { AI, PvP }
    public enum DuelPhase : byte { Waiting, Playing, Finished }
    public enum AiDifficulty : byte { Easy, Normal, Hard }
    public sealed class RouletteRules
    {
        public const int MaxPlayers = 6;
        public const int MaxHealth = 100;
        public const int StartingLives = 3;
        public DuelMode Mode { get; }
        public DuelPhase Phase { get; private set; }
        public int ConnectedMask { get; private set; }
        public int AliveMask { get; private set; }
        public int ReadyMask { get; private set; }
        public int Round { get; private set; }
        public int Winner { get; private set; } = -1;
        public int LastActor { get; private set; } = -1;
        public int LastTarget { get; private set; } = -1;
        public int Shots { get; private set; }
        public float ShotTimeLimit { get; private set; } = 10;
        public bool UnlimitedItems { get; private set; }
        public bool AutoSelectItems { get; private set; }
        public bool ManualMiniGame { get; private set; }
        public bool ResetDeveloperSettings()
        {
            bool changed=ShotTimeLimit!=10||UnlimitedItems||ManualMiniGame||AutoSelectItems;
            ShotTimeLimit=10;UnlimitedItems=ManualMiniGame=AutoSelectItems=false;return changed;
        }
        public bool SetAutoSelectItems(bool enabled)
        {if(Phase==DuelPhase.Playing||AutoSelectItems==enabled)return false;AutoSelectItems=enabled;return true;}
        public bool SetUnlimitedItems(bool enabled)
        {if(Phase==DuelPhase.Playing||UnlimitedItems==enabled)return false;UnlimitedItems=enabled;return true;}
        public bool SetManualMiniGame(bool enabled)
        {if(Phase==DuelPhase.Playing||ManualMiniGame==enabled)return false;ManualMiniGame=enabled;return true;}
        public bool Heal(int seat,int amount)
        {if(Phase!=DuelPhase.Playing||!Has(AliveMask,seat)||amount<=0||health[seat]>=MaxHealth)return false;health[seat]=System.Math.Min(MaxHealth,health[seat]+amount);return true;}
        public bool SetShotTime(float seconds)
        {
            if(Phase==DuelPhase.Playing || float.IsNaN(seconds)||float.IsInfinity(seconds) || (seconds!=0&&(seconds<1||seconds>60)) || seconds==ShotTimeLimit)return false;
            ShotTimeLimit=seconds;return true;
        }
        public int LastHitMask { get; private set; }
        public int LastKilledMask { get; private set; }
        public int LastDamage { get; private set; }
        public DuelWeapon LastWeapon { get; private set; }
        private readonly int[] health = new int[MaxPlayers];
        private readonly int[] lastDamage = new int[MaxPlayers];
        public int LastDamageForSeat(int seat) => seat >= 0 && seat < MaxPlayers ? lastDamage[seat] : 0;
        public int HealthForSeat(int seat) => seat >= 0 && seat < MaxPlayers ? health[seat] : 0;
        public static bool Has(int mask, int seat) => seat >= 0 && seat < MaxPlayers && (mask & (1 << seat)) != 0;
        public static int Count(int mask) { int n = 0; for (int i = 0; i < MaxPlayers; i++) if (Has(mask, i)) n++; return n; }
        public RouletteRules(DuelMode mode) { Mode = mode; }
        public bool Join(int seat)
        {
            if (seat < 0 || seat >= MaxPlayers || Phase == DuelPhase.Playing || Has(ConnectedMask, seat)) return false;
            ConnectedMask |= 1 << seat; health[seat] = MaxHealth; ReadyMask &= ~(1 << seat); return true;
        }
        public bool SetReady(int seat)
        {
            if (Phase == DuelPhase.Playing || !Has(ConnectedMask, seat) || Has(ReadyMask, seat)) return false;
            ReadyMask |= 1 << seat; return true;
        }
        public bool CancelReady(int seat)
        {
            if(Phase==DuelPhase.Playing||!Has(ConnectedMask,seat)||!Has(ReadyMask,seat))return false;
            ReadyMask&=~(1<<seat);return true;
        }
        public bool CanStart => Count(ConnectedMask) >= 2 && ReadyMask == ConnectedMask && Phase != DuelPhase.Playing;
        public bool Start()
        {
            if (!CanStart) return false;
            AliveMask = ConnectedMask; ReadyMask = 0; Round++; Shots = 0;
            LastHitMask = LastKilledMask = LastDamage = 0; LastWeapon = DuelWeapon.Revolver;
            System.Array.Clear(lastDamage, 0, lastDamage.Length);
            for (int seat = 0; seat < MaxPlayers; seat++) health[seat] = Has(ConnectedMask, seat) ? MaxHealth : 0;
            Winner = LastActor = LastTarget = -1; Phase = DuelPhase.Playing; return true;
        }
        public int NextTarget(int actor)
        {
            for (int i = 1; i < MaxPlayers; i++) { int seat = (actor + i) % MaxPlayers; if (Has(AliveMask, seat)) return seat; }
            return -1;
        }
        public bool Shoot(int actor, int target, DuelWeapon weapon = DuelWeapon.Revolver, DuelHitPart part = DuelHitPart.Body, bool projectile = false)
        {
            if (Phase != DuelPhase.Playing || !WeaponRules.IsValid(weapon) || !Has(projectile?ConnectedMask:AliveMask, actor) ||
                part < DuelHitPart.None || part > DuelHitPart.Legs ||
                (part != DuelHitPart.None && (actor == target || !Has(AliveMask, target))) || (part == DuelHitPart.None && target != -1)) return false;
            LastHitMask = LastKilledMask = LastDamage = 0; LastWeapon = weapon;
            System.Array.Clear(lastDamage, 0, lastDamage.Length);
            if (part != DuelHitPart.None) Damage(target, WeaponRules.Damage(weapon,part));
            if (part != DuelHitPart.None && weapon == DuelWeapon.SawedOff)
                for (int seat = 0; seat < MaxPlayers; seat++)
                    if (seat != actor && WeaponRules.AreAdjacent(target, seat)) Damage(seat, 25);
            LastActor = actor; LastTarget = target; Shots++;
            // Resolve victory after the whole shot, including both neighbouring chairs.
            if(!projectile)EvaluateWinner(); return true;
        }
        private void Damage(int seat, int amount)
        {
            if (amount<=0 || !Has(AliveMask, seat)) return;
            LastHitMask |= 1 << seat;
            int dealt = System.Math.Min(health[seat], amount);
            health[seat] -= dealt; LastDamage += dealt;
            lastDamage[seat] = dealt;
            if (health[seat] == 0) { AliveMask &= ~(1 << seat); LastKilledMask |= 1 << seat; }
        }
        private void EvaluateWinner()
        {
            if (Phase != DuelPhase.Playing || Count(AliveMask) > 1) return;
            Winner = -1; for (int i = 0; i < MaxPlayers; i++) if (Has(AliveMask, i)) Winner = i;
            Phase = DuelPhase.Finished; ReadyMask = 0;
        }
        public int Eliminate(int mask)
        {
            if(Phase!=DuelPhase.Playing)return 0;
            int eliminated=mask&AliveMask;
            for(int seat=0;seat<MaxPlayers;seat++)if(Has(eliminated,seat))health[seat]=0;
            AliveMask&=~eliminated;
            // All timeouts form one batch: an all-timeout round has no fabricated survivor.
            EvaluateWinner();return eliminated;
        }
        public void ContinueParty() { Phase=DuelPhase.Playing;Winner=-1;ReadyMask=0; }
        public void RestoreParty(int mask,bool healSurvivors=true)
        {
            int previousAlive=AliveMask;
            AliveMask=mask&ConnectedMask;
            for(int seat=0;seat<MaxPlayers;seat++)health[seat]=Has(AliveMask,seat)?healSurvivors||!Has(previousAlive,seat)?MaxHealth:health[seat]:0;
            ContinueParty();
        }
        public void FinishParty(int mask)
        {AliveMask&=mask;Winner=-1;for(int seat=0;seat<MaxPlayers;seat++)if(Has(mask&ConnectedMask,seat))Winner=seat;Phase=DuelPhase.Finished;ReadyMask=0;}
        public void FinishIfSingle()=>EvaluateWinner();
        public bool Leave(int seat)
        {
            if (!Has(ConnectedMask, seat)) return false;
            ConnectedMask &= ~(1 << seat); AliveMask &= ~(1 << seat); health[seat] = 0; ReadyMask &= ~(1 << seat);
            EvaluateWinner(); return true;
        }
    }
}
