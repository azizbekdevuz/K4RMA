using System;
using UnityEngine;
namespace KarmaPrototype
{
    [Serializable]
    public sealed class KarmaBossTuning
    {
        [Header("보스 공통")]
        [Min(1)] public float stageOneHealth = 4800, stageTwoHealth = 8000, stageThreeHealth = 11000, finalHealth = 18000;
        [Min(0.05f)] public float healthMultiplier = 1;
        [Min(0.1f)] public float damageMultiplier = 1.25f, moveMultiplier = 1.45f, decisionMultiplier = 0.8f;
        [Min(1)] public float recoveryMultiplier = 1;
        [Min(10)] public float ultimateCooldown = 28;
        [Min(5)] public float patternTimeout = 30;
        [Header("검기형 · 초기 밸런스")]
        [Min(1)] public float swordSpeed = 11 * KarmaSpace.Scale;
        [Min(0.2f)] public float swordLifetime = 1.4f;
        [Min(0.1f)] public float swordWidth = 1.2f * KarmaSpace.Scale, swordHeight = 0.5f * KarmaSpace.Scale;
        [Min(0.3f)] public float counterWarning = 0.45f;
        [Min(0.6f)] public float swordWardDuration = 3;
        [Min(0.5f)] public float swordWardResponseCooldown = 0.8f;
        [Range(1, 8)] public int aerialSwordCount = 5;
        [Min(0.5f)] public float aerialSwordGap = 0.8f;
        [Header("상승베기형")]
        [Min(1)] public float riseHeight = 4 * KarmaSpace.Scale;
        [Min(0.5f)] public float groundBladeWidth = 5 * KarmaSpace.Scale, groundBladeHeight = 6 * KarmaSpace.Scale;
        [Min(0.5f)] public float crossingWarning = 0.9f, crossingReset = 1.1f;
        [Header("방어형 · 미정 항목의 조정 가능한 초기값")]
        [Min(2)] public float autoShieldPeriod = 12;
        [Min(0.3f)] public float autoShieldWarning = 0.8f;
        [Min(1)] public float autoShieldDuration = 5;
        [Range(0, 1)] public float shieldDamageMultiplier = 0.3f;
        public bool dodgeRefreshesShield = false;
        [Min(0.1f)] public float parryStunSeconds = 2, fragmentStunSeconds = 1;
        [Range(2, 5)] public int shieldFragments = 3;
        [Min(0.2f)] public float fragmentAimWarning = 0.7f;
        [Min(0.3f)] public float barrierRecovery = 1.2f;
        [Header("GD05 결계")]
        [Range(3, 8)] public int trialAttackCount = 5;
        [Range(0, 5)] public int trialAllowedHits = 1;
        [Min(0.8f)] public float trialAttackGap = 1.4f;
        [Min(1)] public float trialStunSeconds = 4;
        [Min(1)] public float trialExplosionMultiplier = 2.5f;
        public float HealthFor(int stage)
        {
            float hp = stage == 0 ? stageOneHealth : stage == 1 ? stageTwoHealth : stage == 2 ? stageThreeHealth : finalHealth;
            return Mathf.Max(1, hp * Mathf.Max(0.05f, healthMultiplier));
        }
    }
}
