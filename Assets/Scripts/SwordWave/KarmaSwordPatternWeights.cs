using System;
using UnityEngine;
namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/Bosses/SwordWave Pattern Weights")]
    public sealed class KarmaSwordPatternWeights : ScriptableObject
    {
        [Serializable]
        public sealed class SwordPatternWeightValues
        {
            [Header("패턴 선택 가중치 · 발동 조건을 만족한 후보끼리 추첨")]
            [Min(0)] public float swBasicWeight = 3f;
            [Min(0)] public float sw01Weight = 3f;
            [Min(0)] public float sw02Weight = 3f;
            [Min(0)] public float sw03Weight = 2f;
            [Min(0)] public float sw04Weight = 1f;
            [Tooltip("다른 후보가 없을 때에만 같은 기술을 연속 사용")] public bool allowRepeatFallback = true;
            [Min(0), Tooltip("근접 시 기본 공격 가중치 배율")] public float closeBasicMultiplier = 2f;
        }
        public SwordPatternWeightValues weights = new SwordPatternWeightValues();
        public static SwordPatternWeightValues LoadCopy()
        {
            var asset = Resources.Load<KarmaSwordPatternWeights>("KarmaSwordPatternWeights");
            return asset == null ? new SwordPatternWeightValues() : JsonUtility.FromJson<SwordPatternWeightValues>(JsonUtility.ToJson(asset.weights));
        }
    }
}
