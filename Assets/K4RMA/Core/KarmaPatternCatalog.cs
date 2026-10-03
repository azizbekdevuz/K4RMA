using System.Collections.Generic;

namespace KarmaPrototype
{
    // Pure recipe/eligibility module. No scene, physics or MonoBehaviour dependency.
    public static class KarmaPatternCatalog
    {
        public static bool IsFusion(EnemyAttack attack)
        {
            return attack == EnemyAttack.BlazingCharge || attack == EnemyAttack.GuardedCharge
                || attack == EnemyAttack.EmberAegis || attack == EnemyAttack.Overdrive;
        }
        public static bool IsCharge(EnemyAttack attack)
        {
            return attack == EnemyAttack.Charge || attack == EnemyAttack.BlazingCharge
                || attack == EnemyAttack.GuardedCharge || attack == EnemyAttack.Overdrive;
        }
        public static List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> returned)
        {
            bool flame = false, dash = false, ward = false;
            foreach (var e in returned)
            {
                if (e == Essence.Flame) flame = true;
                if (e == Essence.Dash) dash = true;
                if (e == Essence.Ward) ward = true;
            }
            var result = new List<EnemyAttack> { EnemyAttack.GroundStrike };
            if (flame && dash && ward) result.Add(EnemyAttack.Overdrive);
            // Show unlocked fusions early so they are visible in a short prototype fight.
            if (flame && dash) result.Add(EnemyAttack.BlazingCharge);
            if (dash && ward) result.Add(EnemyAttack.GuardedCharge);
            if (flame && ward) result.Add(EnemyAttack.EmberAegis);
            result.Add(EnemyAttack.Bolt);
            foreach (var e in returned)
            {
                var attack = e == Essence.Flame ? EnemyAttack.FlameFan
                    : e == Essence.Dash ? EnemyAttack.Charge : EnemyAttack.Ward;
                if (!result.Contains(attack)) result.Add(attack);
            }
            return result;
        }
        public static string Label(EnemyAttack attack)
        {
            switch (attack)
            {
                case EnemyAttack.GroundStrike: return "지면 강타 / 이동하거나 점프";
                case EnemyAttack.Bolt: return "마력탄 / 회피";
                case EnemyAttack.FlameFan: return "메테오 / 바닥의 낙하 지점에서 벗어나세요";
                case EnemyAttack.Charge: return "돌진 / 점프";
                case EnemyAttack.Ward: return "반사 보호막 / 공격하면 차단한 피해를 사방으로 반사";
                case EnemyAttack.BlazingCharge: return "합성: 화염 + 돌진 / 지나간 자리에 불길";
                case EnemyAttack.GuardedCharge: return "합성: 돌진 + 보호막 / 수호 돌진 후 충격파";
                case EnemyAttack.Overdrive: return "삼중 합성 / 벽 방향으로 유도 후 점프로 회피!";
                default: return "합성: 화염 + 보호막 / 공격 시 화상·반복 넉백";
            }
        }
        public static string FusionSummary(IReadOnlyList<Essence> returned)
        {
            var result = new List<string>();
            foreach (var attack in BuildCycle(returned))
            {
                if (attack == EnemyAttack.Overdrive) result.Add("화염 + 돌진 + 보호막");
                if (attack == EnemyAttack.BlazingCharge) result.Add("화염 + 돌진");
                if (attack == EnemyAttack.GuardedCharge) result.Add("돌진 + 보호막");
                if (attack == EnemyAttack.EmberAegis) result.Add("화염 + 보호막");
            }
            return result.Count == 0 ? "없음" : string.Join(" / ", result);
        }
    }
}
