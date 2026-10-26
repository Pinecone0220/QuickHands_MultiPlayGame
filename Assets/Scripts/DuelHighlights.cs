using System;
namespace HelloWorld
{
    // The server freezes one unusual, factual moment. Routine stats are not embarrassments.
    public static class DuelHighlights
    {
        public static string Select(DuelRoundResults data)
        {
            if (!data.IsValid) return $"게임이 {data.Duration:F1}초 만에 종료되었습니다.";
            if(data.Winner<0)return "모두 탈락해 최종 생존자 없이 게임이 끝났습니다.";
            int bestScore = -1, shots = 0, kills = 0;
            string best = null;
            void Pick(int score, string message)
            { if (score > bestScore) { bestScore = score; best = message; } }
            int participants = RouletteRules.Count(data.ParticipantsMask);
            bool hasInputs=false;
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)if(RouletteRules.Has(data.ParticipantsMask,seat)&&(data.ForSeat(seat).CorrectInputs>0||data.ForSeat(seat).Mistakes>0))hasInputs=true;
            for (int seat = 0; seat < RouletteRules.MaxPlayers; seat++)
            {
                if (!RouletteRules.Has(data.ParticipantsMask, seat)) continue;
                var a = data.ForSeat(seat); shots += a.ShotsFired; kills += a.Kills;
                if (a.Kills == 0 && a.DamageDealt >= RouletteRules.MaxHealth)
                    Pick(100 + Math.Min(15, a.DamageDealt / 50),
                        $"{a.Name}님은 {a.DamageDealt} 피해를 줬지만 아무도 처치하지 못했습니다.");
                if (a.BestMultiKill >= 2)
                    Pick(a.BestMultiKill >= 3 ? 120 + a.BestMultiKill : 75,
                        $"{a.Name}님은 단 한 발로 {a.BestMultiKill}킬을 기록했습니다.");
                if (a.Eliminated && a.ShotsFired >= 3 && a.DamageDealt == 0)
                    Pick(90 + Math.Min(10, a.ShotsFired - 3),
                        $"{a.Name}님은 {a.ShotsFired}발을 쐈지만 아무에게도 피해를 주지 못했습니다.");
                if (a.Kills >= 2 && a.DamageDealt > 0 && a.DamageDealt <= a.Kills * 35)
                    Pick(105 + a.Kills,
                        $"{a.Name}님은 단 {a.DamageDealt} 피해로 {a.Kills}킬을 기록했습니다.");
                if (a.Eliminated && a.Mistakes >= 10 && a.Mistakes * 3 >= a.CorrectInputs)
                    Pick(60 + Math.Min(20, a.Mistakes / 2),
                        $"{a.Name}님은 입력을 {a.Mistakes}번 틀리고 탈락했습니다.");
                if (hasInputs && participants >= 3 && data.Duration <= 10 && a.Eliminated && a.CorrectInputs == 0 && a.Mistakes == 0)
                    Pick(70, $"{a.Name}님은 첫 숫자를 입력하기도 전에 탈락했습니다.");
                if(a.ClockWins>=3&&a.ClockStops>=3&&a.ClockErrorTotal<=a.ClockStops*2)
                    Pick(90,$"{a.Name}님은 평균 오차 {a.ClockErrorTotal/(a.ClockStops*100.0):F2}초로 초읽기에서 {a.ClockWins}회 1등을 차지했습니다.");
            }
            var winner = data.ForSeat(data.Winner);
            if (winner.RemainingHealth > 0 && winner.RemainingHealth <= 10 && winner.Kills > 0)
                Pick(110 + 10 - winner.RemainingHealth,
                    $"{winner.Name}님은 체력 {winner.RemainingHealth}만 남기고 최종 승리했습니다.");
            if (participants >= 4 && winner.Kills >= 3 && winner.Mistakes == 0 && winner.CorrectInputs > 0)
                Pick(72, $"{winner.Name}님은 입력을 한 번도 틀리지 않고 {winner.Kills}킬을 기록했습니다.");
            if (participants >= 4 && winner.Kills >= 3 && winner.RemainingHealth == RouletteRules.MaxHealth)
                Pick(68, $"{winner.Name}님은 {winner.Kills}킬을 기록하고 체력 100으로 최종 승리했습니다.");
            if (participants >= 3 && winner.ShotsFired == 0 && kills >= participants - 2 && kills > 0)
                Pick(95, $"{winner.Name}님은 한 발도 쏘지 않고 최종 승리했습니다.");
            if (participants >= 4 && data.Duration <= 12 && kills >= participants - 1)
                Pick(80 + (int)(12 - data.Duration), $"게임이 {data.Duration:F1}초 만에 종료되었습니다.");
            if (data.Duration >= 120 && shots >= 10)
                Pick(55, $"{data.Duration:F1}초 동안 {shots}발의 총성이 울렸습니다.");
            return best ?? $"게임이 {data.Duration:F1}초 만에 종료되었습니다.";
        }
    }
}
