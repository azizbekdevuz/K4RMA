using System.Collections.Generic;
namespace KarmaPrototype
{
    public static class KarmaPatternCatalog
    {
        public static string Name(Essence e) { return e == Essence.SwordWave ? "검기" : e == Essence.RisingSlash ? "상승베기" : "방어"; }
        public static string UniqueName(Essence e) { return e == Essence.SwordWave ? "관통베기" : e == Essence.RisingSlash ? "도약베기" : "반격"; }
        public static Essence? Primary(IReadOnlyList<Essence> order) { return order.Count == 0 ? (Essence?)null : order[0]; }
        public static string TypeName(IReadOnlyList<Essence> order) { return order.Count == 0 ? "기본형" : Name(order[0]) + "형"; }
        public static string FusionSummary(IReadOnlyList<Essence> order)
        {
            var names = new List<string>(); foreach (var e in order) names.Add(Name(e));
            return names.Count == 0 ? "없음" : string.Join(" → ", names);
        }
    }
}
