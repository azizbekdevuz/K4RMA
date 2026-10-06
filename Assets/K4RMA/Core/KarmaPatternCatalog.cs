using System.Collections.Generic;
namespace KarmaPrototype
{
    public static class KarmaPatternCatalog
    {
        public static string Name(Essence e) { return e == Essence.Flame ? "검기" : e == Essence.Dash ? "상승베기" : "방어"; }
        public static string UniqueName(Essence e) { return e == Essence.Flame ? "관통베기" : e == Essence.Dash ? "도약베기" : "반격"; }
        public static Essence? Primary(IReadOnlyList<Essence> order) { return order.Count == 0 ? (Essence?)null : order[0]; }
        public static string TypeName(IReadOnlyList<Essence> order) { return order.Count == 0 ? "기본형" : Name(order[0]) + "형"; }
        // Index into the 3x2 reference sheet. Sword-primary uses its separate atlas.
        public static int GuardianVisual(IReadOnlyList<Essence> order)
        {
            var primary = Primary(order);
            if (primary == Essence.Flame) return -1;
            int basic = primary == Essence.Dash ? 3 : 0;
            if (order.Count < 2) return basic;
            // Preserve the first secondary silhouette at the final trial.
            if (order[1] == Essence.Flame) return basic + 1;
            return basic + 2;
        }
        public static bool IsFusion(EnemyAttack a) { return a == EnemyAttack.AirWave || a == EnemyAttack.GuardCounter; }
        public static bool IsCharge(EnemyAttack a) { return false; }
        static EnemyAttack Original(Essence e) { return e == Essence.Flame ? EnemyAttack.SwordWave : e == Essence.Dash ? EnemyAttack.RisingSlash : EnemyAttack.Ward; }
        public static List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> returned)
        {
            var result = new List<EnemyAttack>();
            var owned = new HashSet<Essence>(returned);
            if (returned.Count == 0) { result.Add(EnemyAttack.GroundStrike); return result; }
            // First inheritance remains the center; later originals are inserted in selection order.
            var main = Original(returned[0]);
            result.Add(main); result.Add(EnemyAttack.GroundStrike);
            var added = new HashSet<Essence> { returned[0] };
            for (int i = 1; i < returned.Count; i++)
                if (added.Add(returned[i])) { result.Add(Original(returned[i])); result.Add(main); }
            if (owned.Contains(Essence.Flame) && owned.Contains(Essence.Dash)) result.Add(EnemyAttack.AirWave);
            if (owned.Contains(Essence.Ward) && owned.Count > 1) result.Add(EnemyAttack.GuardCounter);
            return result;
        }
        public static string Label(EnemyAttack a)
        {
            switch (a)
            {
                case EnemyAttack.GroundStrike: return "근접 베기 예고 · 검 앞에서 벗어나세요";
                case EnemyAttack.SwordWave: return "검기 예고 · 점프로 피하거나 타이밍에 반격";
                case EnemyAttack.RisingSlash: return "상승베기 예고 · 도약 경로에서 벗어나세요";
                case EnemyAttack.AirWave: return "공중 검기 예고 · 높이와 발사 방향을 읽으세요";
                case EnemyAttack.Ward: return "방어 · 공격을 멈추고 빈틈을 기다리세요";
                case EnemyAttack.GuardCounter: return "방어 후 계승 기술 연계 · 준비 동작을 읽으세요";
                default: return "공격 예고";
            }
        }
        public static string FusionSummary(IReadOnlyList<Essence> order)
        {
            var names = new List<string>(); foreach (var e in order) names.Add(Name(e));
            return names.Count == 0 ? "없음" : string.Join(" → ", names);
        }
    }
}
