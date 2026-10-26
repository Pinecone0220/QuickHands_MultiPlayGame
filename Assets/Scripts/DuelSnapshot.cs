using System;
using Unity.Netcode;
using Unity.Collections;
namespace HelloWorld
{
    public struct DuelSnapshot : INetworkSerializable, IEquatable<DuelSnapshot>
    {
        public DuelMode Mode; public DuelPhase Phase; public AiDifficulty Difficulty;
        public ClockState Clock;
        public DuelTabletopState Tabletop;
        public int ConnectedMask, AliveMask, PartyAliveMask, ReadyMask, Round, Winner, LastActor, LastTarget, Shots, Sequence;
        public DuelStage Stage; public double StageStartedAt;
        public float ShotTimeLimit;
        public bool ManualMiniGame,UnlimitedItems,AutoSelectItems;
        public DuelActionState Seat0, Seat1, Seat2, Seat3, Seat4, Seat5;
        public FixedString64Bytes Name0, Name1, Name2, Name3, Name4, Name5;
        public DuelRoundResults Results;
        public string NameForSeat(int seat) => seat < 0 || seat >= RouletteRules.MaxPlayers ? "" :
            (seat == 0 ? Name0 : seat == 1 ? Name1 : seat == 2 ? Name2 : seat == 3 ? Name3 : seat == 4 ? Name4 : Name5).ToString();
        public bool Connected(int seat) => RouletteRules.Has(ConnectedMask, seat);
        public bool Alive(int seat) => RouletteRules.Has(AliveMask, seat);
        public int PracticeMask=>Stage==DuelStage.Idle&&Phase!=DuelPhase.Playing?ConnectedMask&ReadyMask:0;
        public bool IsPracticing(int seat)=>RouletteRules.Has(PracticeMask,seat);
        public bool InParty(int seat)=>RouletteRules.Has(PartyAliveMask,seat);
        public DuelActionState ForSeat(int seat) => seat < 0 || seat >= RouletteRules.MaxPlayers ? default :
            seat == 0 ? Seat0 : seat == 1 ? Seat1 : seat == 2 ? Seat2 : seat == 3 ? Seat3 : seat == 4 ? Seat4 : Seat5;
        public static DuelSnapshot From(DuelDirector d)
        {
            var g = d.Game;
            return new DuelSnapshot { Mode = g.Mode, Phase = g.Phase, Difficulty = d.Difficulty, Clock=d.Clock,Tabletop=d.Tabletop,
                ConnectedMask = g.ConnectedMask, AliveMask = g.AliveMask, PartyAliveMask=d.PartyAliveMask, ReadyMask = g.ReadyMask,
                Round = g.Round, Winner = g.Winner, LastActor = g.LastActor, LastTarget = g.LastTarget, Shots = g.Shots,
                Stage = d.Stage, StageStartedAt = d.StartedAt, Sequence = d.Sequence,
                ShotTimeLimit=g.ShotTimeLimit,ManualMiniGame=g.ManualMiniGame,UnlimitedItems=g.UnlimitedItems,AutoSelectItems=g.AutoSelectItems,
                Seat0 = d.ForSeat(0), Seat1 = d.ForSeat(1), Seat2 = d.ForSeat(2), Seat3 = d.ForSeat(3), Seat4 = d.ForSeat(4), Seat5 = d.ForSeat(5) };
        }
        private static void ActionSerialize<T>(BufferSerializer<T> s, ref DuelActionState a) where T : IReaderWriter
        {
            s.SerializeValue(ref a.Action); s.SerializeValue(ref a.Code); s.SerializeValue(ref a.Progress);
            s.SerializeValue(ref a.Version); s.SerializeValue(ref a.Mistakes); s.SerializeValue(ref a.Target);
            s.SerializeValue(ref a.StartedAt); s.SerializeValue(ref a.BlockedUntil); s.SerializeValue(ref a.DiedAt);
            s.SerializeValue(ref a.FiredAt); s.SerializeValue(ref a.FiredTarget);
            s.SerializeValue(ref a.FiredYaw);s.SerializeValue(ref a.FiredPitch);s.SerializeValue(ref a.FiredSpread);s.SerializeValue(ref a.FiredAliveMask);
            s.SerializeValue(ref a.FiredSeed);s.SerializeValue(ref a.FiredOrigin.X);s.SerializeValue(ref a.FiredOrigin.Y);s.SerializeValue(ref a.FiredOrigin.Z);
            s.SerializeValue(ref a.FiredAds);s.SerializeValue(ref a.FiredWeapon);
            s.SerializeValue(ref a.FiredBottleMask);s.SerializeValue(ref a.FiredGlassMask);
            s.SerializeValue(ref a.Weapon); s.SerializeValue(ref a.Health); s.SerializeValue(ref a.HitAt);
            s.SerializeValue(ref a.SelectedWeapon);
            s.SerializeValue(ref a.SelectedItems);
            s.SerializeValue(ref a.LoadoutLocked);
            s.SerializeValue(ref a.Life);
            s.SerializeValue(ref a.LivesRemaining);s.SerializeValue(ref a.ReviveAt);
            s.SerializeValue(ref a.HasRecovery);s.SerializeValue(ref a.HasTaser);s.SerializeValue(ref a.TaserArmed);s.SerializeValue(ref a.Stunned);s.SerializeValue(ref a.TaserReadyIteration);s.SerializeValue(ref a.HealAt);s.SerializeValue(ref a.StunnedAt);
            InkSerialize(s,ref a.Ink);
            s.SerializeValue(ref a.QuickItem);s.SerializeValue(ref a.QuickItemVersion);s.SerializeValue(ref a.QuickEquipped);s.SerializeValue(ref a.QuickSpent);
            s.SerializeValue(ref a.LoadedMask);s.SerializeValue(ref a.SpentMask);s.SerializeValue(ref a.NextChamber);
            s.SerializeValue(ref a.Kills); s.SerializeValue(ref a.CorrectInputs); s.SerializeValue(ref a.CurrentStreak); s.SerializeValue(ref a.BestStreak);
            s.SerializeValue(ref a.DamageDealt); s.SerializeValue(ref a.ShotsFired); s.SerializeValue(ref a.BestMultiKill);
            s.SerializeValue(ref a.PracticeShotsFired);s.SerializeValue(ref a.PracticeSession);
            s.SerializeValue(ref a.AimYaw); s.SerializeValue(ref a.AimPitch); s.SerializeValue(ref a.Aiming);
            s.SerializeValue(ref a.AimStartedAt); s.SerializeValue(ref a.LastHitPart);
            s.SerializeValue(ref a.ShootingStartedAt);
            s.SerializeValue(ref a.ClockWins); s.SerializeValue(ref a.ClockStops); s.SerializeValue(ref a.ClockErrorTotal);
            s.SerializeValue(ref a.DeathCause);
        }
        private static void InkSerialize<T>(BufferSerializer<T> s,ref DuelInkState i) where T:IReaderWriter
        {
            s.SerializeValue(ref i.Owned);s.SerializeValue(ref i.Armed);s.SerializeValue(ref i.Version);s.SerializeValue(ref i.Serial);s.SerializeValue(ref i.Target);
            s.SerializeValue(ref i.ThrownAt);s.SerializeValue(ref i.ImpactAt);s.SerializeValue(ref i.InkedAt);
            s.SerializeValue(ref i.Origin.X);s.SerializeValue(ref i.Origin.Y);s.SerializeValue(ref i.Origin.Z);
            s.SerializeValue(ref i.Direction.X);s.SerializeValue(ref i.Direction.Y);s.SerializeValue(ref i.Direction.Z);
            s.SerializeValue(ref i.Impact.X);s.SerializeValue(ref i.Impact.Y);s.SerializeValue(ref i.Impact.Z);
        }
        private static void ClockEntrySerialize<T>(BufferSerializer<T> s,ref ClockEntry e) where T:IReaderWriter
        {s.SerializeValue(ref e.Centiseconds);s.SerializeValue(ref e.Error);s.SerializeValue(ref e.Rank);s.SerializeValue(ref e.Dice);s.SerializeValue(ref e.Rolls);s.SerializeValue(ref e.Points);s.SerializeValue(ref e.Failure);}
        private static void MemoryEntrySerialize<T>(BufferSerializer<T> s,ref ArrowMemoryEntry e) where T:IReaderWriter
        {s.SerializeValue(ref e.Count);s.SerializeValue(ref e.Errors);s.SerializeValue(ref e.Dice);s.SerializeValue(ref e.Rolls);s.SerializeValue(ref e.Rank);s.SerializeValue(ref e.Locked);}
        private static void MemorySerialize<T>(BufferSerializer<T> s,ref ArrowMemoryState m) where T:IReaderWriter
        {
            s.SerializeValue(ref m.Phase);s.SerializeValue(ref m.Round);s.SerializeValue(ref m.Length);s.SerializeValue(ref m.ActiveMask);s.SerializeValue(ref m.RoundMask);s.SerializeValue(ref m.EliminatedMask);
            s.SerializeValue(ref m.AnswerSeed);s.SerializeValue(ref m.Winner);s.SerializeValue(ref m.PhaseAt);s.SerializeValue(ref m.InputEndsAt);
            MemoryEntrySerialize(s,ref m.Seat0);MemoryEntrySerialize(s,ref m.Seat1);MemoryEntrySerialize(s,ref m.Seat2);MemoryEntrySerialize(s,ref m.Seat3);MemoryEntrySerialize(s,ref m.Seat4);MemoryEntrySerialize(s,ref m.Seat5);
        }
        private static void DiceEntrySerialize<T>(BufferSerializer<T> s,ref DuelDiceEntry e) where T:IReaderWriter
        {s.SerializeValue(ref e.Face);s.SerializeValue(ref e.Rolls);}
        private static void DiceSerialize<T>(BufferSerializer<T> s,ref DuelDiceState d) where T:IReaderWriter
        {
            s.SerializeValue(ref d.Phase);s.SerializeValue(ref d.Round);s.SerializeValue(ref d.ParticipantsMask);s.SerializeValue(ref d.PendingMask);s.SerializeValue(ref d.LockedMask);
            s.SerializeValue(ref d.StartedAt);s.SerializeValue(ref d.EndsAt);
            DiceEntrySerialize(s,ref d.Seat0);DiceEntrySerialize(s,ref d.Seat1);DiceEntrySerialize(s,ref d.Seat2);DiceEntrySerialize(s,ref d.Seat3);DiceEntrySerialize(s,ref d.Seat4);DiceEntrySerialize(s,ref d.Seat5);
        }
        private static void ClockSerialize<T>(BufferSerializer<T> s,ref ClockState c) where T:IReaderWriter
        {
            s.SerializeValue(ref c.Phase);s.SerializeValue(ref c.Iteration);s.SerializeValue(ref c.ParticipantsMask);s.SerializeValue(ref c.Target);s.SerializeValue(ref c.First);s.SerializeValue(ref c.Last);
            s.SerializeValue(ref c.MiniGame);s.SerializeValue(ref c.EliminatedMask);
            s.SerializeValue(ref c.StartedAt);s.SerializeValue(ref c.PhaseAt);s.SerializeValue(ref c.HideAfter);s.SerializeValue(ref c.Signal);s.SerializeValue(ref c.Points);s.SerializeValue(ref c.SignalAt);
            ClockEntrySerialize(s,ref c.Seat0);ClockEntrySerialize(s,ref c.Seat1);ClockEntrySerialize(s,ref c.Seat2);ClockEntrySerialize(s,ref c.Seat3);ClockEntrySerialize(s,ref c.Seat4);ClockEntrySerialize(s,ref c.Seat5);
            MemorySerialize(s,ref c.Memory);
            DiceSerialize(s,ref c.Dice);
        }
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Mode); s.SerializeValue(ref Phase); s.SerializeValue(ref Difficulty);
            ClockSerialize(s,ref Clock);
            TableSerialize(s,ref Tabletop);
            s.SerializeValue(ref Name0); s.SerializeValue(ref Name1); s.SerializeValue(ref Name2); s.SerializeValue(ref Name3);
            s.SerializeValue(ref Name4); s.SerializeValue(ref Name5);
            s.SerializeValue(ref ConnectedMask); s.SerializeValue(ref AliveMask); s.SerializeValue(ref ReadyMask); s.SerializeValue(ref Round);
            s.SerializeValue(ref PartyAliveMask);
            s.SerializeValue(ref Winner); s.SerializeValue(ref LastActor); s.SerializeValue(ref LastTarget); s.SerializeValue(ref Shots);
            s.SerializeValue(ref Sequence); s.SerializeValue(ref Stage); s.SerializeValue(ref StageStartedAt);s.SerializeValue(ref ShotTimeLimit);s.SerializeValue(ref ManualMiniGame);s.SerializeValue(ref UnlimitedItems);s.SerializeValue(ref AutoSelectItems);
            ActionSerialize(s, ref Seat0); ActionSerialize(s, ref Seat1); ActionSerialize(s, ref Seat2); ActionSerialize(s, ref Seat3);
            ActionSerialize(s, ref Seat4); ActionSerialize(s, ref Seat5);
            s.SerializeValue(ref Results);
        }
        private static void TableEntrySerialize<T>(BufferSerializer<T> s,ref DuelTableEntry e) where T:IReaderWriter
        {s.SerializeValue(ref e.Action);s.SerializeValue(ref e.StartedAt);s.SerializeValue(ref e.BottleBrokenAt);s.SerializeValue(ref e.GlassBrokenAt);}
        private static void TableSerialize<T>(BufferSerializer<T> s,ref DuelTabletopState t) where T:IReaderWriter
        {
            s.SerializeValue(ref t.BottlesBrokenMask);s.SerializeValue(ref t.GlassesBrokenMask);s.SerializeValue(ref t.FilledMask);s.SerializeValue(ref t.DrankMask);
            TableEntrySerialize(s,ref t.Seat0);TableEntrySerialize(s,ref t.Seat1);TableEntrySerialize(s,ref t.Seat2);TableEntrySerialize(s,ref t.Seat3);TableEntrySerialize(s,ref t.Seat4);TableEntrySerialize(s,ref t.Seat5);
        }
        public bool Equals(DuelSnapshot o) => Mode == o.Mode && Phase == o.Phase && Difficulty == o.Difficulty && Clock.Equals(o.Clock) && Tabletop.Equals(o.Tabletop)&&
            Name0.Equals(o.Name0) && Name1.Equals(o.Name1) && Name2.Equals(o.Name2) && Name3.Equals(o.Name3) &&
            Name4.Equals(o.Name4) && Name5.Equals(o.Name5) &&
            ConnectedMask == o.ConnectedMask && AliveMask == o.AliveMask && PartyAliveMask==o.PartyAliveMask && ReadyMask == o.ReadyMask && Round == o.Round && Winner == o.Winner &&
            LastActor == o.LastActor && LastTarget == o.LastTarget && Shots == o.Shots && Sequence == o.Sequence && ShotTimeLimit==o.ShotTimeLimit && ManualMiniGame==o.ManualMiniGame && UnlimitedItems==o.UnlimitedItems && AutoSelectItems==o.AutoSelectItems &&
            Stage == o.Stage && StageStartedAt == o.StageStartedAt && Seat0.Equals(o.Seat0) && Seat1.Equals(o.Seat1) && Seat2.Equals(o.Seat2) && Seat3.Equals(o.Seat3) && Seat4.Equals(o.Seat4) && Seat5.Equals(o.Seat5) && Results.Equals(o.Results);
    }
}

