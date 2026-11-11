using System;
using UnityEngine;
namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/Bosses/Guard Pattern Weights")]
    public sealed class KarmaGuardPatternWeights : ScriptableObject
    {
        [Serializable]
        public sealed class GuardPatternWeightValues
        {
            [Header("패턴 선택 가중치 · 발동 조건을 만족한 후보끼리 추첨")]
            [Min(0)] public float basicWeight = 3f;
            [Min(0)] public float gd02Weight = 2f;
            [Min(0)] public float gd03Weight = 2f;
            [Min(0)] public float gd04Weight = 1.5f;
            [Range(0, 1)] public float repeatWeightScale = 0.25f;
        }
        public GuardPatternWeightValues weights = new GuardPatternWeightValues();
        public static GuardPatternWeightValues LoadCopy()
        {
            var asset = Resources.Load<KarmaGuardPatternWeights>("KarmaGuardPatternWeights");
            return asset == null ? new GuardPatternWeightValues() : JsonUtility.FromJson<GuardPatternWeightValues>(JsonUtility.ToJson(asset.weights));
        }
    }
}
