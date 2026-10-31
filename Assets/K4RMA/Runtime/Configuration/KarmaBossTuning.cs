using System;
using UnityEngine;
namespace KarmaPrototype
{
    [Serializable]
    public sealed class KarmaBossTuning
    {
        [Header("보스 공통")]
        [Min(1)] public float stageOneHealth = 1050, stageTwoHealth = 1550, stageThreeHealth = 2050, finalHealth = 2800;
        [Min(0.05f)] public float healthMultiplier = 1;
        [Min(0.1f)] public float damageMultiplier = 1.15f, moveMultiplier = 1.7f, decisionMultiplier = 0.42f;
        [Range(0.4f, 2)] public float recoveryMultiplier = 0.72f;
        [Header("체력 절반 이하 · 강화 페이즈")]
        [Range(0.1f, 0.8f)] public float enrageThreshold = 0.5f;
        [Range(0.65f, 1)] public float enrageTempo = 0.82f;
        [Range(0.65f, 1.5f)] public float warningMultiplier = 0.9f;
        [Min(10)] public float ultimateCooldown = 20;
        [Min(5)] public float patternTimeout = 30;
        [Header("검기형 · 초기 밸런스")]
        [Min(1)] public float swordSpeed = 16 * KarmaSpace.Scale;
        [Min(0.2f)] public float swordLifetime = 1.65f;
        [Min(0.1f)] public float swordWidth = 1.2f * KarmaSpace.Scale, swordHeight = 0.5f * KarmaSpace.Scale;
        [Min(0.3f)] public float counterWarning = 0.45f;
        [Min(0.6f)] public float swordWardDuration = 3;
        [Min(0.5f)] public float swordWardResponseCooldown = 0.8f;
        [Range(1, 8)] public int aerialSwordCount = 7;
        [Min(0.25f)] public float aerialSwordGap = 0.4f;
        [Header("상승베기형")]
        [Min(1)] public float riseHeight = 4 * KarmaSpace.Scale;
        [Min(0.5f)] public float groundBladeWidth = 5 * KarmaSpace.Scale, groundBladeHeight = 6 * KarmaSpace.Scale;
        [Min(0.4f)] public float crossingWarning = 0.65f, crossingReset = 0.65f;
        [Header("방어형 · 미정 항목의 조정 가능한 초기값")]
        [Min(2)] public float autoShieldPeriod = 8;
        [Min(0.3f)] public float autoShieldWarning = 0.8f;
        [Range(0, 1)] public float shieldDamageMultiplier = 0.3f;
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
        [Header("GD05 결계")]
        [Range(3, 8)] public int trialAttackCount = 7;
        [Range(0, 5)] public int trialAllowedHits = 1;
        [Min(0.4f)] public float trialAttackGap = 0.65f;
        [Min(1)] public float trialStunSeconds = 4;
        [Min(1)] public float trialExplosionMultiplier = 2.5f;
        public float HealthFor(int stage)
        {
            float hp = stage == 0 ? stageOneHealth : stage == 1 ? stageTwoHealth : stage == 2 ? stageThreeHealth : finalHealth;
            return Mathf.Max(1, hp * Mathf.Max(0.05f, healthMultiplier));
        }
    }
}
