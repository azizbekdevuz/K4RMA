using System;
using UnityEngine;
using UnityEngine.Serialization;
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
        // Only the idle between completed regular patterns; warnings/recovery stay unchanged.
        [Range(0.25f, 1.5f)] public float swordDecisionMultiplier = 0.75f;
        [Min(0.05f)] public float swordSelectionRetry = 0.1f;
        [Min(0.5f)] public float swordBasicCooldown = 2.4f;
        [Tooltip("World-unit spacing for SW01/SW02. Basic waves remain limited to ten units when the player closes in.")]
        [Range(9, 14)] public float swordPreferredDistance = 11.75f;
        [Tooltip("Completed sword skills between priority basic attacks. Cooldown and actual reach still apply.")]
        [Range(1, 6)] public int swordSkillsBetweenBasics = 1;
        [Min(4.5f)] public float swordRetreatCooldown = 5;
        [Header("검기형 · 거리 유지 (월드 단위)")]
        [Range(5.5f, 6.5f)] public float swordRetreatStartDistance = 6.25f;
        [Range(9, 14)] public float swordRetreatStopDistance = 11.75f;
        [Range(2, 18)] public float swordRetreatDistance = 16;
        [Range(0.35f, 0.75f)] public float swordRetreatDuration = 0.6f;
        [Min(1)] public float swordRetreatSpeed = 32;
        [Range(0.25f, 1.25f)] public float swordMeleeSpacingMargin = 0.65f;
        [Tooltip("이 거리에서 접근을 시작하고 선호 교전 거리까지 이동한 뒤 공격합니다.")]
        [Range(10, 12)] public float swordApproachStartDistance = 10.5f;
        [Header("검기형 · 공격")]
        [Min(1)] public float swordSpeed = 16 * KarmaSpace.Scale;
        [Min(0.2f)] public float swordLifetime = 1.65f;
        [Min(0.1f)] public float swordWidth = 1.2f * KarmaSpace.Scale, swordHeight = 0.5f * KarmaSpace.Scale;
        [Min(0.3f)] public float counterWarning = 0.45f;
        [FormerlySerializedAs("swordWardDuration")]
        [Min(0.6f)] public float swordGuardDuration = 3;
        [FormerlySerializedAs("swordWardResponseCooldown")]
        [Min(0.5f)] public float swordGuardResponseCooldown = 0.8f;
        [Header("SW05 · 무작위 공중 난사")]
        // Legacy serialized totals are preserved, but no longer limit seven volleys.
        [HideInInspector] public int aerialSwordMinCount = 18, aerialSwordCount = 30;
        [HideInInspector] public int aerialSwordMinGroup = 4, aerialSwordMaxGroup = 8;
        [Tooltip("각 7회 사출의 검기 수. 동일한 값이면 매회 고정 수량입니다.")]
        [Range(1, 18)] public int aerialSwordVolleyMin = 15, aerialSwordVolleyMax = 15;
        [Range(0, 0.45f)] public float aerialSwordAngleJitter = 0.3f;
        [Tooltip("0: 매 공격마다 무작위, 그 외: 동일 물리 상태에서 재현 가능한 시드")]
        public int aerialSwordSeed;
        [Tooltip("검기 사출 시작 사이의 간격. 전조 시간과 별도로 계산합니다.")]
        [Min(1)] public float aerialSwordInterval = 1;
        [HideInInspector] public float aerialSwordGap = 0.4f;
        [Header("상승베기형")]
        [Min(1)] public float riseHeight = 4 * KarmaSpace.Scale;
        [Min(0.5f)] public float groundBladeWidth = 5 * KarmaSpace.Scale, groundBladeHeight = 6 * KarmaSpace.Scale;
        [Min(0.4f)] public float crossingWarning = 0.65f, crossingReset = 0.65f;
        [Header("방어형 · 미정 항목의 조정 가능한 초기값")]
        [Min(2)] public float autoShieldPeriod = 8;
        [Min(0.3f)] public float autoShieldWarning = 0.8f;
        [Min(1)] public float autoShieldDuration = 5;
        [Range(0, 1)] public float shieldDamageMultiplier = 0.3f;
        public bool dodgeRefreshesShield = false;
        [Min(0.1f)] public float parryStunSeconds = 2, fragmentStunSeconds = 1;
        [Range(2, 5)] public int shieldFragments = 4;
        [Min(0.2f)] public float fragmentAimWarning = 0.5f;
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
