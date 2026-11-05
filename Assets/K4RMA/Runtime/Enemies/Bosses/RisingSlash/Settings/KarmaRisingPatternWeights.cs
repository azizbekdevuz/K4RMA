using System;
using UnityEngine;
namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/Bosses/RisingSlash Pattern Weights")]
    public sealed class KarmaRisingPatternWeights : ScriptableObject
    {
        [Serializable]
        public sealed class RisingPatternWeightValues
        {
            [Header("패턴 선택 가중치 · 발동 조건을 만족한 후보끼리 추첨")]
            [Min(0)] public float basicWeight = 3f;
            [Min(0)] public float up01Weight = 3f;
            [Min(0)] public float up02Weight = 2f;
            [Min(0)] public float up03Weight = 2f;
            [Min(0)] public float up04Weight = 1.5f;
            [Min(0)] public float up05Weight = 0.8f;
            [Range(0, 1)] public float repeatWeightScale = 0.25f;
            [Min(0), Tooltip("플레이어가 공중에 있을 때 UP02 가중치 배율")] public float airborneUp02Multiplier = 2f;
        }
        public RisingPatternWeightValues weights = new RisingPatternWeightValues();
        public static RisingPatternWeightValues LoadCopy()
        {
            var asset = Resources.Load<KarmaRisingPatternWeights>("KarmaRisingPatternWeights");
            return asset == null ? new RisingPatternWeightValues() : JsonUtility.FromJson<RisingPatternWeightValues>(JsonUtility.ToJson(asset.weights));
        }
    }
}
