using System;
using UnityEngine;
namespace KarmaPrototype
{
    // Independent tuning for this boss. Shared combat rules read the balance scales through BossBehavior.
    [Serializable]
    public sealed class KarmaGuardBossSettings : KarmaBossBalance
    {
        [NonSerialized] KarmaGuardPatternWeights.GuardPatternWeightValues patternWeights;
        public KarmaGuardPatternWeights.GuardPatternWeightValues Weights => patternWeights ?? (patternWeights = KarmaGuardPatternWeights.LoadCopy());
        [Header("방어형 · 이동 및 사거리")]
        [Min(0.1f)] public float preferredDistance = 3 * KarmaSpace.Scale;
        [Min(0.1f)] public float heavyStrikeReach = 4 * KarmaSpace.Scale;
        [Header("방어형 · 미정 항목의 조정 가능한 초기값")]
        [Min(0.1f)] public float heavyStrikeDamageMultiplier = 1.5f, shieldThrowDamageMultiplier = 0.8f;
        [Min(2)] public float autoShieldPeriod = 8;
        [Min(0.3f)] public float autoShieldWarning = 0.8f;
        [Min(0.1f)] public float parryStunSeconds = 2;
        [Min(0.1f)] public float heavyParryCueSeconds = 0.5f, heavyHitStunSeconds = 1;
        [Min(1)] public float shieldThrowSpeed = 15 * KarmaSpace.Scale;
        [Header("GD04 전방 지면강타")]
        [Min(0.1f)] public float slamRange = 10 * KarmaSpace.Scale, slamHeight = 1.2f * KarmaSpace.Scale;
        [Min(0.1f)] public float slamDamageMultiplier = 1, slamKnockUpSpeed = 9 * KarmaSpace.Scale;
        [Min(0.1f)] public float slamStunSeconds = 0.8f;
        [Header("GD05 결계")]
        [Range(2, 25)] public float trialDuration = 9;
        [Min(1)] public float trialShieldSpeed = 10, trialChaseSpeed = 7.5f;
        [Min(0.1f)] public float trialDamageMultiplier = 0.65f, trialParryStunSeconds = 1.1f;
        [Min(1)] public int trialFailureHits = 3;
        [Min(0.1f)] public float trialExplosionDamageMultiplier = 1, trialSuccessStunSeconds = 4;
    }
}
