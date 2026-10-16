using System;
using HelloWorld;
public static class RouletteRulesTests
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static void Correct(DuelDirector d, int seat, double t)
    {
        var a = d.ForSeat(seat); Check(d.Input(seat, a.Digit(a.Progress), a.Version, t), "valid digit");
        Check(!d.Input(seat, a.Digit(a.Progress), a.Version, t), "duplicate digit rejected");
    }
    static DuelDirector Lobby(int count, int seed = 0, DuelMode mode = DuelMode.PvP, AiDifficulty difficulty = AiDifficulty.Normal)
    {
        var d = new DuelDirector(mode, new Random(seed), difficulty);
        for (int i = 0; i < count; i++) Check(d.Join(i, 0), "join unique seat");
        Check(!d.Join(4, 0) && !d.Join(0, 0), "capacity and duplicate seat"); return d;
    }
    static void ReadyAll(DuelDirector d, double now)
    {
        for (int i = 0; i < 4 && d.Stage == DuelStage.Idle; i++) if (RouletteRules.Has(d.Game.ConnectedMask, i) && !RouletteRules.Has(d.Game.ReadyMask, i)) Check(d.Ready(i, now), "ready accepted");
        Check(d.Stage == DuelStage.Countdown, "all ready starts"); d.Tick(now + 2.51);
        Check(d.Stage == DuelStage.Racing, "countdown completed");
    }
    public static void Main()
    {
        int selectedMask = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var d = Lobby(4, seed); ReadyAll(d, 0); double t = 3;
            for (int i = 0; i < 17; i++) Correct(d, 0, t += .1);
            Check(d.ForSeat(0).Action == DuelAction.Aim && d.ForSeat(0).Target == -1, "aim fifth digit has no target");
            Correct(d, 0, t += .1);
            int target = d.ForSeat(0).Target; selectedMask |= 1 << target;
            Check(target > 0 && target < 4, "random excludes self");
            int progress = d.ForSeat(0).Progress;
            Check(d.Disconnect(target, t), "target leaves");
            var a = d.ForSeat(0);
            Check(a.Target != target && a.Target != 0 && RouletteRules.Has(d.Game.AliveMask, a.Target), "retarget living opponent");
            Check(a.Progress == progress, "retarget preserves input progress");
        }
        Check(selectedMask == 14, "random selection reaches all opponents across seeds");
        for (int count = 2; count <= 4; count++) for (int seed = 0; seed < 70; seed++)
        {
            var d = Lobby(count, seed); double now = 0;
            for (int i = 0; i < count - 1; i++) Check(d.Ready(i, now), "partial readiness");
            Check(d.Stage == DuelStage.Idle, "wait for final participant");
            Check(!d.Ready(0, now), "duplicate readiness");
            ReadyAll(d, now); now = 2.6;
            Check(!d.Input(0, 6, d.ForSeat(0).Version, now), "digit range");
            Check(!d.Input(4, 1, 0, now), "invalid seat");
            Check(!d.Join(3, now), "mid-match join rejected");
            int actor = seed % count, target = -1;
            var a = d.ForSeat(actor);
            for (int stage = 0; stage < 3; stage++)
            {
                a = d.ForSeat(actor);
                Check(a.Target == -1, "no target before aim completes");
                Check(a.Length == 6 && (int)a.Action == stage, "six digits per action");
                for (int i = 0; i < 5; i++) Correct(d, actor, now += .1);
                Check((int)d.ForSeat(actor).Action == stage && d.ForSeat(actor).Progress == 5, "five inputs cannot advance");
                Correct(d, actor, now += .1);
                Check((int)d.ForSeat(actor).Action == stage + 1, "sixth input advances");
            }
            a = d.ForSeat(actor); Check(a.Length == 6, "six-digit firing panel");
            target = a.Target; Check(target != actor && RouletteRules.Has(d.Game.AliveMask, target), "random living opponent after aim");
            Correct(d, actor, now += .1); a = d.ForSeat(actor);
            Check(d.Input(actor, a.Digit(a.Progress) % 5 + 1, a.Version, now), "wrong input");
            a = d.ForSeat(actor); Check(a.Progress == 0 && a.Action == DuelAction.Fire, "wrong resets firing panel");
            Check(!d.Input(actor, a.Digit(0), a.Version, now + .44), "mistake lock"); now += .46;
            for (int i = 0; i < 5; i++) Correct(d, actor, now += .1);
            Check(d.Game.Shots == 0 && d.ForSeat(actor).Progress == 5, "five inputs cannot fire");
            Correct(d, actor, now += .1);
            Check(d.Game.Shots == 1 && !RouletteRules.Has(d.Game.AliveMask, target), "one shot kills selected target");
            Check(d.ForSeat(target).DiedAt == now, "death time replicated");
            a = d.ForSeat(target); Check(!d.Input(target, a.Digit(a.Progress), a.Version, now), "dead input forbidden");
            Check(!d.Ready(target, now), "cannot ready during death animation");
            now += 1.66; d.Tick(now);
            while (d.Game.Phase == DuelPhase.Playing)
            {
                while (d.Stage == DuelStage.Racing) Correct(d, actor, now += .1);
                now += 1.66; d.Tick(now);
            }
            Check(d.Game.Winner == actor && d.Game.Shots == count - 1, "last survivor wins");
            Check(RouletteRules.Count(d.Game.AliveMask) == 1, "exactly one survivor");
            int version = d.ForSeat(actor).Version;
            Check(d.Ready(actor, now), "winner ready"); Check(d.Stage == DuelStage.Idle, "dead players must also ready");
            ReadyAll(d, now);
            Check(d.Game.AliveMask == d.Game.ConnectedMask && d.Game.Round == 2, "everyone revives");
            Check(d.ForSeat(actor).Version > version && d.ForSeat(target).DiedAt < 0, "new match clears deaths and stale input");
            now += 3;
            for (int seat = 0; seat < count - 1; seat++) Check(d.Disconnect(seat, now), "disconnect removes exact seat");
            Check(d.Game.Winner == count - 1 && d.Stage == DuelStage.Idle, "last connected survivor wins");
            Check(d.Ready(count - 1, now), "solo ready allowed"); Check(d.Stage == DuelStage.Idle, "solo cannot start");
            Check(d.Join(0, now), "vacant seat joins between matches");
            Check(d.Game.ReadyMask == 0, "roster change clears readiness");
        }
        double[] means = new double[3];
        for (int count = 2; count <= 4; count++) for (int level = 0; level < 3; level++) for (int seed = 0; seed < 20; seed++)
        {
            var d = Lobby(count, seed, DuelMode.AI, (AiDifficulty)level);
            int botMask = (1 << count) - 2;
            Check(d.Game.ReadyMask == botMask, "all connected bots initially ready");
            Check(d.Stage == DuelStage.Idle, "bots wait for human");
            ReadyAll(d, 0);
            double t = 2.6; int[] deathVersion = { -1, -1, -1, -1 };
            while (d.Game.Phase == DuelPhase.Playing && t < 400)
            {
                t += .05; d.Tick(t);
                for (int bot = 1; bot < count; bot++)
                    if (!RouletteRules.Has(d.Game.AliveMask, bot))
                    {
                        if (deathVersion[bot] < 0) deathVersion[bot] = d.ForSeat(bot).Version;
                        Check(deathVersion[bot] == d.ForSeat(bot).Version, "dead bot stops input");
                    }
            }
            Check(d.Game.Phase == DuelPhase.Finished && d.Game.Winner > 0, "multiple bots finish match");
            Check(d.Game.Shots == count - 1 && RouletteRules.Count(d.Game.AliveMask) == 1, "exactly one survivor among all bots");
            for (int bot = 1; bot < count; bot++) Check(d.ForSeat(bot).Version > 8, "every bot entered digits");
            t += 2; d.Tick(t);
            Check(d.Stage == DuelStage.Idle && d.Game.ReadyMask == botMask, "dead and living bots ready for rematch");
            d.Tick(t + 60); Check(d.Stage == DuelStage.Idle, "multiple bots cannot bypass human readiness");
            ReadyAll(d, t + 60);
            Check(d.Game.Round == 2 && d.Game.AliveMask == d.Game.ConnectedMask, "all bots respawn for rematch");
        }
        for (int level = 0; level < 3; level++) for (int seed = 0; seed < 40; seed++)
        {
            var d = Lobby(2, seed, DuelMode.AI, (AiDifficulty)level);
            Check(d.Game.ReadyMask == 2, "AI initial ready"); ReadyAll(d, 0);
            double now = 2.6;
            while (d.Game.Phase == DuelPhase.Playing && now < 200) { now += .025; d.Tick(now); }
            Check(d.Game.Winner == 1 && d.Game.Shots == 1, "AI finishes one six-digit firing set for instant kill");
            means[level] += now; now += 2; d.Tick(now);
            Check(d.Game.ReadyMask == 2 && d.Stage == DuelStage.Idle, "AI ready waits for human");
            d.Tick(now + 60); Check(d.Stage == DuelStage.Idle, "no automatic next match");
            ReadyAll(d, now + 60);
        }
        Check(means[0] > means[1] && means[1] > means[2], "difficulty speed order");
        for (int i = 0; i < 3; i++) Console.WriteLine((AiDifficulty)i + " mean " + (means[i] / 40).ToString("F2") + " sec");
        Console.WriteLine("PASS " + checks + " assertions");
    }
}



