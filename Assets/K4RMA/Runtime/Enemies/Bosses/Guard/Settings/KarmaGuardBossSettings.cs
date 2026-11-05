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
        [Min(0.1f)] public float heavyStrikeDamageMultiplier = 1.5f, shieldThrowDamageMultiplier = 0.8f, shieldFragmentDamageMultiplier = 0.8f;
        [Min(2)] public float autoShieldPeriod = 8;
        [Min(0.3f)] public float autoShieldWarning = 0.8f;
        [Min(0.1f)] public float parryStunSeconds = 2, fragmentStunSeconds = 1;
        [Min(0.1f)] public float heavyParryCueSeconds = 0.5f, heavyHitStunSeconds = 1;
        [Range(2, 5)] public int shieldFragments = 4;
        [Min(0.2f)] public float fragmentAimWarning = 0.5f;
        [Min(0.15f)] public float fragmentLaunchGap = 0.3f;
        [Min(1)] public float fragmentChaseSpeed = 15 * KarmaSpace.Scale;
        [Min(0.1f)] public float fragmentReturnSeconds = 0.4f;
        [Range(3, 8)] public int barrierSteps = 6;
        [Min(0.12f)] public float barrierStepGap = 0.22f;
        [Min(0.3f)] public float barrierRecovery = 1.2f;
        [Min(0)] public float barrierHeatDamageMultiplier = 0.25f;
        [Min(0.05f)] public float barrierHeatGrace = 0.3f, barrierHeatInterval = 0.5f;
        [Header("GD05 결계")]
        [Range(2, 25)] public float trialDuration = 9;
        [Min(1)] public float trialShieldSpeed = 10, trialChaseSpeed = 7.5f;
        [Min(0.1f)] public float trialDamageMultiplier = 0.65f, trialParryStunSeconds = 1.1f;
    }
}
