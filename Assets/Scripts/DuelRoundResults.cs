using System;
using Unity.Collections;
using Unity.Netcode;

namespace HelloWorld
{
    public struct DuelResultEntry : INetworkSerializable, IEquatable<DuelResultEntry>
    {
        public FixedString64Bytes Name;
        public int Mistakes, Kills, CorrectInputs, BestStreak;
        public int DamageDealt, ShotsFired, BestMultiKill, RemainingHealth;
        public int ClockWins, ClockStops, ClockErrorTotal;
        public bool Eliminated;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Name); s.SerializeValue(ref Mistakes); s.SerializeValue(ref Kills);
            s.SerializeValue(ref CorrectInputs); s.SerializeValue(ref BestStreak);
            s.SerializeValue(ref DamageDealt); s.SerializeValue(ref ShotsFired); s.SerializeValue(ref BestMultiKill);
            s.SerializeValue(ref RemainingHealth); s.SerializeValue(ref Eliminated);
            s.SerializeValue(ref ClockWins);s.SerializeValue(ref ClockStops);s.SerializeValue(ref ClockErrorTotal);
        }
        public bool Equals(DuelResultEntry o) => Name.Equals(o.Name) && Mistakes == o.Mistakes &&
            Kills == o.Kills && CorrectInputs == o.CorrectInputs && BestStreak == o.BestStreak &&
            DamageDealt == o.DamageDealt && ShotsFired == o.ShotsFired && BestMultiKill == o.BestMultiKill &&
            RemainingHealth == o.RemainingHealth && Eliminated == o.Eliminated && ClockWins==o.ClockWins && ClockStops==o.ClockStops && ClockErrorTotal==o.ClockErrorTotal;
    }

    // Frozen by the server once per round. Joining/reusing a seat cannot rewrite past results.
    public struct DuelRoundResults : INetworkSerializable, IEquatable<DuelRoundResults>
    {
        public int Round, ParticipantsMask, Winner;
        public double Duration;
        public FixedString512Bytes Highlight;
        public DuelResultEntry Seat0, Seat1, Seat2, Seat3, Seat4, Seat5;
        public bool IsValid => Round > 0 && Winner >= -1 && Winner < RouletteRules.MaxPlayers && ParticipantsMask != 0;
        public DuelResultEntry ForSeat(int seat) => seat < 0 || seat >= RouletteRules.MaxPlayers ? default :
            seat == 0 ? Seat0 : seat == 1 ? Seat1 : seat == 2 ? Seat2 : seat == 3 ? Seat3 : seat == 4 ? Seat4 : Seat5;
        private static DuelResultEntry Entry(DuelDirector d, FixedString64Bytes[] names, int seat)
        {
            var a = d.ForSeat(seat);
            return new DuelResultEntry { Name = names[seat], Mistakes = a.Mistakes, Kills = a.Kills,
                CorrectInputs = a.CorrectInputs, BestStreak = a.BestStreak, DamageDealt = a.DamageDealt,
                ShotsFired = a.ShotsFired, BestMultiKill = a.BestMultiKill, RemainingHealth = a.Health, Eliminated = a.DiedAt >= 0,
                ClockWins=a.ClockWins,ClockStops=a.ClockStops,ClockErrorTotal=a.ClockErrorTotal };
        }
        public static DuelRoundResults Capture(DuelDirector d, FixedString64Bytes[] names, double now)
        {
            var data = new DuelRoundResults
            {
                Round = d.Game.Round, ParticipantsMask = d.RoundParticipantsMask, Winner = d.Game.Winner,
                Duration = Math.Max(0, now - d.RaceStartedAt),
                Seat0 = Entry(d, names, 0), Seat1 = Entry(d, names, 1), Seat2 = Entry(d, names, 2),
                Seat3 = Entry(d, names, 3), Seat4 = Entry(d, names, 4), Seat5 = Entry(d, names, 5)
            };
            data.Highlight = new FixedString512Bytes(DuelHighlights.Select(data));
            return data;
        }
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Round); s.SerializeValue(ref ParticipantsMask); s.SerializeValue(ref Winner);
            s.SerializeValue(ref Duration); s.SerializeValue(ref Seat0); s.SerializeValue(ref Seat1); s.SerializeValue(ref Seat2);
            s.SerializeValue(ref Seat3); s.SerializeValue(ref Seat4); s.SerializeValue(ref Seat5);
            s.SerializeValue(ref Highlight);
        }
        public bool Equals(DuelRoundResults o) => Round == o.Round && ParticipantsMask == o.ParticipantsMask &&
            Winner == o.Winner && Duration == o.Duration && Highlight.Equals(o.Highlight) && Seat0.Equals(o.Seat0) && Seat1.Equals(o.Seat1) &&
            Seat2.Equals(o.Seat2) && Seat3.Equals(o.Seat3) && Seat4.Equals(o.Seat4) && Seat5.Equals(o.Seat5);
    }
}
