using System;
using HelloWorld;

public static class SixPlayerRulesTests
{
    private static int checks;
    private static void Check(bool value, string label)
    { checks++; if (!value) throw new Exception(label); }
    private sealed class RepeatingRandom : Random
    { public override int Next(int min, int max) => min; public override int Next(int max) => 0; }
    private sealed class SplashTargets : Random
    {
        private readonly int[] picks = { 2, 0, 0, 3, 3, 1 }; private int next;
        public override int Next(int max) => picks[next++];
        public override int Next(int min, int max) => min;
    }
    private static void Correct(DuelDirector d, int seat, double now)
    { var a = d.ForSeat(seat); Check(d.Input(seat, a.Digit(a.Progress), a.Version, now), "statistics correct input"); }
    private static void CheckStatistics()
    {
        var d = new DuelDirector(DuelMode.PvP, new RepeatingRandom());
        for (int seat = 0; seat < 6; seat++) d.Join(seat, 0);
        for (int seat = 0; seat < 6; seat++) d.Ready(seat, 0);
        d.Tick(3);
        Check(d.RoundParticipantsMask == 63 && d.RaceStartedAt == 3, "round roster and start time");
        Correct(d, 0, 3); Correct(d, 0, 3);
        var a = d.ForSeat(0);
        Check(a.CorrectInputs == 2 && a.BestStreak == 2 && a.CurrentStreak == 2, "correct and streak counts");
        Check(!d.Input(0, a.Digit(a.Progress), a.Version - 1, 3), "stale statistics request");
        Check(d.ForSeat(0).Equals(a), "rejected input not counted");
        d.Input(0, a.Digit(a.Progress) % 5 + 1, a.Version, 3); a = d.ForSeat(0);
        Check(a.Mistakes == 1 && a.CorrectInputs == 2 && a.CurrentStreak == 0 && a.BestStreak == 2, "mistake resets streak only");
        for (int i = 0; i < 12; i++) Correct(d, 0, 3.46);
        a = d.ForSeat(0);
        Check(a.CorrectInputs == 14 && a.CurrentStreak == 12 && a.BestStreak == 12, "streak carries across actions");
        d.Disconnect(1, 3.46);
        Check(d.RoundParticipantsMask == 63 && d.ForSeat(0).Kills == 0, "departed player retained, no kill for leaving");
        while (d.Game.Phase == DuelPhase.Playing) Correct(d, 0, 3.46);
        Check(d.ForSeat(0).Kills == 4, "only actual deaths award kills");
        Check(d.ForSeat(0).DamageDealt == 400 && d.ForSeat(0).ShotsFired == 4 && d.ForSeat(0).BestMultiKill == 1, "damage and firing statistics");
        d.Tick(6);
        for (int seat = 0; seat < 6; seat++) if (RouletteRules.Has(d.Game.ConnectedMask, seat)) d.Ready(seat, 6);
        for (int seat = 0; seat < 6; seat++)
        { a = d.ForSeat(seat); Check(a.CorrectInputs == 0 && a.Mistakes == 0 && a.Kills == 0 && a.BestStreak == 0 && a.CurrentStreak == 0 && a.DamageDealt == 0 && a.ShotsFired == 0 && a.BestMultiKill == 0, "new round clears statistics"); }
        var splash = new DuelDirector(DuelMode.PvP, new SplashTargets());
        for (int seat = 0; seat < 6; seat++) splash.Join(seat, 0);
        splash.SelectWeapon(5, DuelWeapon.SawedOff, 0);
        for (int seat = 0; seat < 6; seat++) splash.Ready(seat, 0);
        splash.Tick(3);
        while (splash.Game.Phase == DuelPhase.Playing) Correct(splash, 5, 3);
        Check(splash.Game.Shots == 6 && splash.Game.LastKilledMask == 14 && splash.ForSeat(5).Kills == 5, "splash triple kill counted per victim");
        Check(splash.ForSeat(5).CorrectInputs == 144 && splash.ForSeat(5).BestStreak == 144, "longest streak and correct inputs include all shots");
        Check(splash.ForSeat(5).DamageDealt == 500 && splash.ForSeat(5).ShotsFired == 6 && splash.ForSeat(5).BestMultiKill == 3, "clamped splash damage and triple-kill highlight statistics");
    }
    private static void CheckMistakes(DuelWeapon weapon, Random random)
    {
        var d = new DuelDirector(DuelMode.PvP, random);
        for (int i = 0; i < 6; i++) d.Join(i, 0);
        if (weapon == DuelWeapon.SawedOff) d.SelectWeapon(0, weapon, 0);
        for (int i = 0; i < 6; i++) d.Ready(i, 0);
        d.Tick(3); double now = 3;
        int stages = weapon == DuelWeapon.Revolver ? 8 : 6;
        for (int stage = 0; stage < stages; stage++)
        {
            for (int i = 0; i < 2; i++)
            { var a = d.ForSeat(0); Check(d.Input(0, a.Digit(a.Progress), a.Version, now), "prefix before mistake"); }
            var before = d.ForSeat(0); var other = d.ForSeat(5);
            Check(d.Input(0, before.Digit(before.Progress) % 5 + 1, before.Version, now), "mistake accepted");
            var after = d.ForSeat(0);
            Check(after.Progress == 0 && after.Mistakes == before.Mistakes + 1 && after.Action == before.Action &&
                after.Target == before.Target && d.Game.Shots == 0, "restart current action, preserve aim and no shot");
            Check(d.ForSeat(5).Equals(other), "mistake does not modify another player");
            for (int i = 0; i < after.Length; i++)
                Check(after.Digit(i) >= 1 && after.Digit(i) <= 5 &&
                    (weapon == DuelWeapon.Revolver ? after.Digit(i) != before.Digit(i) : after.Digit(i) == before.Digit(i)), "all positions rerolled only for Revolver");
            Check(!d.Input(0, after.Digit(0), before.Version, now + 1), "stale input rejected after reroll");
            Check(!d.Input(0, after.Digit(0), after.Version, now + .44), "existing penalty retained");
            now += .46;
            for (int i = 0; i < after.Length; i++)
            { var a = d.ForSeat(0); Check(d.Input(0, a.Digit(a.Progress), a.Version, now), "replacement pattern can finish action"); }
        }
        Check(d.Game.Shots == 1, "reroll in firing action still fires once");
    }
    public static void Main()
    {
        Check(RouletteRules.MaxPlayers == 6, "six seats");
        CheckStatistics();
        for (int seed = 0; seed < 16; seed++)
        { CheckMistakes(DuelWeapon.Revolver, new Random(seed)); CheckMistakes(DuelWeapon.SawedOff, new Random(seed)); }
        CheckMistakes(DuelWeapon.Revolver, new RepeatingRandom());
        for (int a = -1; a <= 6; a++) for (int b = -1; b <= 6; b++)
            Check(WeaponRules.AreAdjacent(a, b) == (a >= 0 && a < 6 && b >= 0 && b < 6 && Math.Abs(a - b) == 1), "numbered neighbours without wrap");
        for (int mask = 1; mask < 64; mask++)
        {
            if (RouletteRules.Count(mask) < 2) continue;
            for (int actor = 0; actor < 6; actor++) for (int target = 0; target < 6; target++)
            {
                if (actor == target || !RouletteRules.Has(mask, actor) || !RouletteRules.Has(mask, target)) continue;
                var g = new RouletteRules(DuelMode.PvP);
                for (int seat = 0; seat < 6; seat++) if (RouletteRules.Has(mask, seat)) Check(g.Join(seat), "join subset");
                Check(!g.Join(6) && !g.Join(-1), "capacity bounds");
                for (int seat = 0; seat < 6; seat++) if (RouletteRules.Has(mask, seat)) g.SetReady(seat);
                Check(g.Start(), "subset all ready starts");
                Check(g.Shoot(actor, target, DuelWeapon.SawedOff), "shot valid");
                for (int seat = 0; seat < 6; seat++)
                {
                    bool neighbour = Math.Abs(seat - target) == 1;
                    int damage = seat == target ? 50 : seat != actor && neighbour ? 25 : 0;
                    Check(g.HealthForSeat(seat) == (RouletteRules.Has(mask, seat) ? 100 - damage : 0), "damage matches physical seat, no skipping");
                }
                Check(g.Shoot(actor, target, DuelWeapon.SawedOff) && g.HealthForSeat(target) == 0, "cumulative direct kill");
                Check(!g.Shoot(target, actor), "dead actor rejected");
            }
        }
        var multi = new RouletteRules(DuelMode.PvP);
        for (int seat = 0; seat < 6; seat++) multi.Join(seat);
        for (int seat = 0; seat < 6; seat++) multi.SetReady(seat);
        multi.Start();
        foreach (int target in new[] { 2, 0, 0, 4, 4, 2 }) Check(multi.Shoot(5, target, DuelWeapon.SawedOff), "prepare interior triple kill");
        Check(multi.LastKilledMask == 14 && multi.AliveMask == 32 && multi.Winner == 5, "interior triple kill resolves atomically");
        for (int count = 2; count <= 6; count++) for (int firingSeat = 0; firingSeat < count; firingSeat++)
        {
            var d = new DuelDirector(DuelMode.PvP, new Random(31));
            for (int s = 0; s < count; s++) Check(d.Join(s, 0), "director join");
            for (int s = 0; s < count - 1; s++) d.Ready(s, 0);
            Check(d.Stage == DuelStage.Idle, "wait for every player");
            d.Ready(count - 1, 0); d.Tick(3);
            Check(d.Stage == DuelStage.Racing, "2 through 6 start");
            int other = (firingSeat + 1) % count;
            var a = d.ForSeat(other); d.Input(other, a.Digit(0), a.Version, 3);
            a = d.ForSeat(other);
            int entered = 0;
            while (d.ForSeat(firingSeat).Action != DuelAction.Fire)
            {
                var step = d.ForSeat(firingSeat);
                Check(d.Input(firingSeat, step.Digit(step.Progress), step.Version, 3), "valid independent command"); entered++;
                Check(entered <= 42, "eight-step derringer");
            }
            Check(entered == 42 && d.ForSeat(firingSeat).Target != firingSeat, "seven commands before fire");
            int victim = d.ForSeat(firingSeat).Target;
            for (int digit = 0; digit < 6; digit++)
            { var step = d.ForSeat(firingSeat); Check(d.Input(firingSeat, step.Digit(step.Progress), step.Version, 3), "fire command"); }
            Check(d.Game.HealthForSeat(victim) == 0 && d.ForSeat(victim).DiedAt == 3, "death stamp incl seats 5/6");
            if (other != victim) Check(d.ForSeat(other).Version == a.Version && d.ForSeat(other).Progress == a.Progress, "survivor input preserved");
            while (d.Game.Phase == DuelPhase.Playing) Check(d.Game.Shoot(firingSeat, d.Game.NextTarget(firingSeat)), "finish rules round");
            for (int s = 0; s < count - 1; s++) d.Game.SetReady(s);
            Check(!d.Game.CanStart, "dead player must ready"); d.Game.SetReady(count - 1);
            Check(d.Game.Start() && d.Game.Round == 2, "rematch");
            for (int s = 0; s < count; s++) Check(d.Game.HealthForSeat(s) == 100, "restore all health");
        }
        for (int bots = 1; bots <= 5; bots++) for (int level = 0; level < 3; level++)
        {
            var d = new DuelDirector(DuelMode.AI, new Random(17), (AiDifficulty)level);
            for (int s = 0; s <= bots; s++) d.Join(s, 0);
            Check(d.Game.ReadyMask == ((1 << (bots + 1)) - 2), "every AI auto ready");
            d.Ready(0, 0); double now = 3;
            while (d.Game.Phase == DuelPhase.Playing && now < 500) { d.Tick(now); now += .05; }
            Check(d.Game.Phase == DuelPhase.Finished && RouletteRules.Count(d.Game.AliveMask) == 1, "AI last survivor");
            d.Tick(now + 2);
            Check(d.Game.ReadyMask == d.Game.ConnectedMask - 1 && d.Stage == DuelStage.Idle, "all AI incl dead auto ready after round");
            d.Ready(0, now + 2); Check(d.Game.Round == 2, "AI rematch");
        }
        Console.WriteLine("PASS six-player rules " + checks + " assertions");
    }
}
