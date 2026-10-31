using System.Collections.Generic;
namespace KarmaPrototype
{
    public static class KarmaPatternCatalog
    {
        // Boss-to-tip distance, shared by selection and the GD02 hit box (unscaled units).
        public const float GuardHeavyReach = 4;
        public static string Name(Essence e) { return e == Essence.Flame ? "검기" : e == Essence.Dash ? "상승베기" : "방어"; }
        public static string UniqueName(Essence e) { return e == Essence.Flame ? "관통베기" : e == Essence.Dash ? "도약베기" : "반격"; }
        public static Essence? Primary(IReadOnlyList<Essence> order) { return order.Count == 0 ? (Essence?)null : order[0]; }
        public static string TypeName(IReadOnlyList<Essence> order) { return order.Count == 0 ? "기본형" : Name(order[0]) + "형"; }
        public static bool IsUltimate(EnemyAttack a)
        { return a == EnemyAttack.SW05 || a == EnemyAttack.UP05 || a == EnemyAttack.GD05; }
        public static List<EnemyAttack> BuildCycle(IReadOnlyList<Essence> returned)
        {
            var result = new List<EnemyAttack>();
            var owned = new HashSet<Essence>(returned);
            if (returned.Count == 0) { result.Add(EnemyAttack.GroundStrike); return result; }
            switch (returned[0])
            {
                case Essence.Flame:
                    result.Add(EnemyAttack.SW01); result.Add(EnemyAttack.SW02);
                    if (owned.Contains(Essence.Dash)) result.Add(EnemyAttack.SW03);
                    if (owned.Contains(Essence.Ward)) result.Add(EnemyAttack.SW04);
                    if (owned.Count == 3) result.Add(EnemyAttack.SW05);
                    break;
                case Essence.Dash:
                    result.Add(EnemyAttack.UP01); result.Add(EnemyAttack.UP02);
                    if (owned.Contains(Essence.Flame)) result.Add(EnemyAttack.UP03);
                    if (owned.Contains(Essence.Ward)) result.Add(EnemyAttack.UP04);
                    if (owned.Count == 3) result.Add(EnemyAttack.UP05);
                    break;
                case Essence.Ward:
                    // GD01 persists separately; after a parry it re-forms outside this attack cycle.
                    result.Add(EnemyAttack.GroundStrike); result.Add(EnemyAttack.GD02);
                    if (owned.Contains(Essence.Flame)) result.Add(EnemyAttack.GD03);
                    if (owned.Contains(Essence.Dash)) result.Add(EnemyAttack.GD04);
                    if (owned.Count == 3) result.Add(EnemyAttack.GD05);
                    break;
            }
            return result;
        }
        public static bool CanSelect(EnemyAttack attack, EnemyAttack previous, float distance,
            bool airborne, bool lineOfSight, bool shield, bool escapeRoute, bool ultimateReady)
        {
            if (IsUltimate(attack)) return ultimateReady && !IsUltimate(previous);
            if (attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03) return distance >= 4;
            if (attack == EnemyAttack.SW02) return lineOfSight;
            if (attack == EnemyAttack.UP02) return previous != attack && (airborne || distance <= 8);
            if (attack == EnemyAttack.UP03 || attack == EnemyAttack.GD04) return escapeRoute;
            if (attack == EnemyAttack.GD02) return shield && distance <= GuardHeavyReach;
            return true;
        }
        public static string Label(EnemyAttack a)
        {
            switch (a)
            {
                case EnemyAttack.SW01: return "SW01 교차 연사 · 낮은 검기/높은 검기를 반복 회피";
                case EnemyAttack.SW02: return "SW02 조준 일섬 · 조준 고정 후 사선 이탈";
                case EnemyAttack.SW03: return "SW03 검흔 상승 · 검기 후 바닥 검흔에서 이탈";
                case EnemyAttack.SW04: return "SW04 반격 검막 · 사출 유도 후 회피";
                case EnemyAttack.SW05: return "SW05 공중 검 사출 · 순차 조준을 피하고 착지 반격";
                case EnemyAttack.UP01: return "UP01 상승 내려찍기 · 착지 후 낮은 충격파";
                case EnemyAttack.UP02: return "UP02 공중 추격 · 목표 고정 후 수평 이동/낙하";
                case EnemyAttack.UP03: return "UP03 지면 검기 상승 · 등 뒤 또는 범위 밖으로";
                case EnemyAttack.UP04: return "UP04 방어막 내려찍기 · 착지 파편을 점프로";
                case EnemyAttack.UP05: return "UP05 연속 횡단 · 지상/공중/지상/내려찍기";
                case EnemyAttack.GD01: return "GD01 자동방어막 생성";
                case EnemyAttack.GD02: return "GD02 강공격 · K 반격 성공 시 방어막 파괴";
                case EnemyAttack.GD03: return "GD03 방패 순차 추적 · 복귀 경로도 회피";
                case EnemyAttack.GD04: return "GD04 계단식 지면 상승 · 진행 방향에서 이탈";
                case EnemyAttack.GD05: return "GD05 결계 생존 시험";
                default: return "연속 베기 · 접근 후 연타 / 마무리 뒤 반격";
            }
        }
        public static string FusionSummary(IReadOnlyList<Essence> order)
        {
            var names = new List<string>(); foreach (var e in order) names.Add(Name(e));
            return names.Count == 0 ? "없음" : string.Join(" → ", names);
        }
    }
}
