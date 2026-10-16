using System;
namespace HelloWorld
{
    // The server freezes one unusual, factual moment. Routine stats are not embarrassments.
    public static class DuelHighlights
    {
        public static string Select(DuelRoundResults data)
        {
            if (!data.IsValid) return $"게임이 {data.Duration:F1}초 만에 종료되었습니다.";
            if(data.Winner<0)return "모두 탈락했습니다. 이번 경기에는 생존자가 없습니다.";
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
                bool alternate = (data.Round + seat) % 2 == 0;
                if (a.Kills == 0 && a.DamageDealt >= RouletteRules.MaxHealth)
                    Pick(100 + Math.Min(15, a.DamageDealt / 50), alternate ?
                        $"{a.Name}님은 {a.DamageDealt} 피해, 0킬. 총알은 일했는데 킬은 퇴근했습니다." :
                        $"{a.Name}님은 {a.DamageDealt} 피해를 줬지만 처치 0명. 마무리 한 발이 끝내 없었습니다.");
                if (a.BestMultiKill >= 2)
                    Pick(a.BestMultiKill >= 3 ? 120 + a.BestMultiKill : 75, alternate ?
                        $"{a.Name}님은 탄 한 발로 {a.BestMultiKill}명의 관전석을 예약했습니다." :
                        $"{a.Name}님의 한 발에 {a.BestMultiKill}명이 동시에 관전석으로 이동했습니다.");
                if (a.Eliminated && a.ShotsFired >= 3 && a.DamageDealt == 0)
                    Pick(90 + Math.Min(10, a.ShotsFired - 3),
                        $"{a.Name}님은 {a.ShotsFired}발을 쏘고 피해 0. 조준과 끝내 합의하지 못했습니다.");
                if (a.Kills >= 2 && a.DamageDealt > 0 && a.DamageDealt <= a.Kills * 35)
                    Pick(105 + a.Kills,
                        $"{a.Name}님은 {a.DamageDealt} 피해로 {a.Kills}킬을 챙겼습니다. 마무리만큼은 확실했습니다.");
                if (a.Eliminated && a.Mistakes >= 10 && a.Mistakes * 3 >= a.CorrectInputs)
                    Pick(60 + Math.Min(20, a.Mistakes / 2),
                        $"{a.Name}님은 숫자와 {a.Mistakes}번 싸우다 관전석으로 갔습니다.");
                if (hasInputs && participants >= 3 && data.Duration <= 10 && a.Eliminated && a.CorrectInputs == 0 && a.Mistakes == 0)
                    Pick(70, $"{a.Name}님은 첫 숫자보다 사망창을 먼저 만났습니다. 입력 기록 0회.");
                if(a.ClockWins>=3&&a.ClockStops>=3&&a.ClockErrorTotal<=a.ClockStops*2)
                    Pick(90,$"{a.Name}님은 초읽기 {a.ClockWins}회 1등, 평균 오차 {a.ClockErrorTotal/(a.ClockStops*100.0):F2}초. 머릿속 시계가 정확했습니다.");
            }
            var winner = data.ForSeat(data.Winner);
            if (winner.RemainingHealth > 0 && winner.RemainingHealth <= 10 && winner.Kills > 0)
                Pick(110 + 10 - winner.RemainingHealth,
                    $"{winner.Name}님은 체력 {winner.RemainingHealth}로 우승했습니다. 의자는 끝내 비워 주지 않았습니다.");
            if (participants >= 4 && winner.Kills >= 3 && winner.Mistakes == 0 && winner.CorrectInputs > 0)
                Pick(72, $"{winner.Name}님은 {winner.Kills}킬을 기록하는 동안 숫자도 한 번 틀리지 않았습니다.");
            if (participants >= 4 && winner.Kills >= 3 && winner.RemainingHealth == RouletteRules.MaxHealth)
                Pick(68, $"{winner.Name}님은 {winner.Kills}킬을 챙기고도 체력 100. 다친 곳 없이 돌아갔습니다.");
            if (participants >= 3 && winner.ShotsFired == 0 && kills >= participants - 2 && kills > 0)
                Pick(95, $"{winner.Name}님은 한 발도 쏘지 않고 우승했습니다. 끝까지 자리를 지킨 보람이 있었습니다.");
            if (participants >= 4 && data.Duration <= 12 && kills >= participants - 1)
                Pick(80 + (int)(12 - data.Duration), $"{participants}명이 앉았지만, {data.Duration:F1}초 뒤에는 한 명만 남았습니다.");
            if (data.Duration >= 120 && shots >= 10)
                Pick(55, $"{data.Duration:F1}초 동안 {shots}발이 오갔습니다. 테이블이 오래 버텼습니다.");
            return best ?? $"{participants}인 총격전 · 총 {shots}발 · {data.Duration:F1}초";
        }
    }
}
