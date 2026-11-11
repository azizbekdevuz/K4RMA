using System;
using UnityEngine;
namespace KarmaPrototype
{
    // Independent tuning for this boss. Shared combat rules read the balance scales through BossBehavior.
    [Serializable]
    public sealed class KarmaSwordBossSettings : KarmaBossBalance
    {
        [NonSerialized] KarmaSwordPatternWeights.SwordPatternWeightValues patternWeights;
        public KarmaSwordPatternWeights.SwordPatternWeightValues Weights => patternWeights ?? (patternWeights = KarmaSwordPatternWeights.LoadCopy());
        [Header("검기형 · 초기 밸런스")]
        // Post-action selection; committed warnings keep their full dodge window.
        [Range(0.25f, 1.5f)] public float swordDecisionMultiplier = 0.35f;
        [Min(0.05f)] public float swordSelectionRetry = 0.1f;
        [Min(0.5f)] public float swordBasicCooldown = 2.4f;
        [Min(0.5f)] public float meleeBasicCooldown = 1.1f, waveBasicCooldown = 1.5f;
        [Tooltip("근접 공격 진입에만 적용하는 여유. 종료 경계는 실제 칼질 판정입니다.")]
        [Range(0.1f, 0.6f)] public float swordMeleeHysteresis = 0.2f;
        [Tooltip("World-unit spacing for SW01/SW02. Basic waves remain limited to ten units when the player closes in.")]
        [Range(9, 14)] public float swordPreferredDistance = 11.75f;
        [Tooltip("Completed sword skills between priority basic attacks. Cooldown and actual reach still apply.")]
        [Range(1, 6)] public int swordSkillsBetweenBasics = 1;
        [Min(4.5f)] public float swordRetreatCooldown = 5;
        [Header("검기형 · 거리 유지 (월드 단위)")]
        [Range(5.5f, 6.5f)] public float swordRetreatStartDistance = 6.25f;
        [Range(9, 14)] public float swordRetreatStopDistance = 11.75f;
        [Range(2, 64)] public float swordRetreatDistance = 12;
        [Range(0.35f, 0.75f)] public float swordRetreatDuration = 0.6f;
        [Min(1)] public float swordRetreatSpeed = 48;
        [Tooltip("기존 씬의 빠른 후퇴 설정에도 적용하는 실제 속도 상한 (월드 유닛/초)")]
        [Min(1)] public float swordRetreatSpeedLimit = 48;
        [Min(1)] public float swordRetreatAcceleration = 56;
        [Min(1)] public float swordRetreatDeceleration = 72;
        [Tooltip("실제 압박 대응 거리·재사용 간격·이동 시간. 기존 저장값을 덮어쓰지 않습니다.")]
        [Min(1)] public float pressureRetreatStartDistance = 9.2f;
        [Range(2.1f, 3.5f)] public float pressureRetreatCooldown = 2.4f;
        [Range(0.7f, 1.2f)] public float pressureRetreatDuration = 1.05f;
        [Header("검기형 · 슬라이딩 백스텝 / 잔상")]
        [Range(0, 0.08f)] public float swordBackstepPreparation = 0.045f;
        [Min(1)] public float swordBackstepAcceleration = 420;
        [Min(1)] public float swordBackstepDeceleration = 360;
        [Tooltip("실제 방향 대쉬 거리보다 멀리 이동합니다. 기존 유효 이동 예산도 하한으로 유지합니다.")]
        [Min(1f)] public float swordRetreatDashMultiplier = 1.75f;
        [Tooltip("후퇴 후 추가 후퇴·접근을 보류하는 공격 기회. 다음 후퇴에는 공격 완료도 필요합니다.")]
        [Range(0.5f, 0.8f)] public float swordRetreatAttackWindow = 0.65f;
        [Tooltip("Wall-return delay after an escape. A completed attack is still required before evading again.")]
        [Range(2, 3)] public float swordWallReturnCooldown = 2.5f;
        [Range(0.03f, 0.07f)] public float swordAfterimageInterval = 0.03f;
        [Range(0.2f, 0.3f)] public float swordAfterimageLifetime = 0.3f;
        [Range(0.45f, 0.65f)] public float swordAfterimageAlpha = 0.58f;
        [Range(5, 8)] public int swordAfterimageCapacity = 8;
        [Range(0.25f, 1.25f)] public float swordMeleeSpacingMargin = 0.65f;
        [Tooltip("이 거리에서 접근을 시작하고 선호 교전 거리까지 이동한 뒤 공격합니다.")]
        [Range(10, 12)] public float swordApproachStartDistance = 10.5f;
        [Header("검기형 · 공격")]
        [Min(0.1f)] public float basicDamageMultiplier = 0.5f, aimedDamageMultiplier = 1.15f;
        [Min(1)] public float swordSpeed = 16 * KarmaSpace.Scale;
        [Min(0.2f)] public float swordLifetime = 1.65f;
        [Min(0.1f)] public float swordWidth = 1.2f * KarmaSpace.Scale, swordHeight = 0.5f * KarmaSpace.Scale;
        [Min(0.3f)] public float counterWarning = 0.45f;
        [Min(0.6f)] public float swordGuardDuration = 3;
        [Min(0.5f)] public float swordGuardResponseCooldown = 0.8f;
        [Tooltip("Editor/development only: SW04 volley, contact result and ordered cancellation diagnostics.")]
        public bool sw04DebugLogging;
        [Header("SW03 · 16구간 중 10구간 검흔 상승")]
        [Min(0.65f)] public float scarWarningSeconds = 0.95f;
        [Range(0.25f, 0.8f)] public float scarActiveSeconds = 0.65f;
        [Range(0.3f, 0.8f)] public float scarRoundGap = 0.32f;
        [Header("SW05 · 체력 50% / 25% · 검 소나기 3연격")]
        [Tooltip("일반 공격 대비 검 소나기 명중 피해 배율")]
        [Min(1)] public float aerialSwordDamageMultiplier = 2.5f;
        [Tooltip("회차별 무작위 검 수. 매회 이동 가능한 회피 경로를 확보합니다.")]
        [Range(1, 18)] public int aerialSwordVolleyMin = 16, aerialSwordVolleyMax = 18;
        [Tooltip("0: 매 공격마다 무작위, 그 외: 동일 물리 상태에서 재현 가능한 시드")]
        public int aerialSwordSeed;
    }
}
