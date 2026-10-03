using System;
using UnityEngine;

namespace KarmaPrototype
{
    // Separate serialized profile: existing Config assets acquire the new profile
    // without depending on their previously saved quick-test HP values.
    [Serializable]
    public sealed class KarmaBossTuning
    {
        [InspectorName("1구역 체력")] [Min(1)] public float stageOneHealth = 4800;
        [InspectorName("2구역 체력")] [Min(1)] public float stageTwoHealth = 8000;
        [InspectorName("3구역 체력")] [Min(1)] public float stageThreeHealth = 11000;
        [InspectorName("마지막 수호자 체력")] [Min(1)] public float finalHealth = 18000;
        [InspectorName("전체 보스 체력 배율")] [Min(0.05f)] public float healthMultiplier = 1;
        [InspectorName("적 피해 배율")] [Min(0.1f)] public float damageMultiplier = 1.25f;
        [InspectorName("적 이동 속도 배율")] [Min(0.1f)] public float moveMultiplier = 1.45f;
        [InspectorName("공격 사이 대기 시간 배율")] [Range(0.2f, 2)] public float decisionMultiplier = 0.6f;
        [InspectorName("공격 후 빈틈 배율")] [Range(0.2f, 2)] public float recoveryMultiplier = 0.85f;
        [InspectorName("추가 연사 간격")] [Min(0.25f)] public float volleyInterval = 0.6f;
        [InspectorName("연속 돌진 사이 예고")] [Min(0.35f)] public float returnChargeWarning = 0.65f;
        [InspectorName("화상 지속 시간")] [Min(0.5f)] public float burnDuration = 3.6f;
        [InspectorName("화상 피해 간격")] [Min(0.2f)] public float burnTickInterval = 0.6f;
        [InspectorName("화상 1회 피해")] [Min(0)] public float burnTickDamage = 4;
        [InspectorName("화상 넉백 속도")] [Min(0)] public float burnKnockbackSpeed = 7.5f;
        [InspectorName("화상 넉백 지속")] [Range(0.05f, 0.4f)] public float burnKnockbackDuration = 0.18f;
        [Header("화염·반사·최종 합성기")]
        [InspectorName("적 화상 지속 시간")] [Min(0.1f)] public float enemyBurnDuration = 3;
        [InspectorName("적 화상 피해 간격")] [Min(0.2f)] public float enemyBurnInterval = 0.5f;
        [InspectorName("적 화상 1회 피해")] [Min(0)] public float enemyBurnDamage = 6;
        [InspectorName("메테오 낙하 예고")] [Min(0.4f)] public float meteorWarning = 0.9f;
        [InspectorName("메테오 낙하 속도")] [Min(1)] public float meteorSpeed = 18;
        [InspectorName("메테오 폭발 반경")] [Min(0.5f)] public float meteorRadius = 2.2f;
        [InspectorName("메테오 피해 배율")] [Min(0)] public float meteorDamageMultiplier = 1.8f;
        [InspectorName("메테오 넉백 속도")] [Min(0)] public float meteorKnockback = 10;
        [InspectorName("반사 피해 배율")] [Min(0)] public float reflectMultiplier = 1;
        [InspectorName("반사 충격파 충전 시간")] [Min(0.2f)] public float reflectDelay = 0.65f;
        [InspectorName("최종 합성기 준비 시간")] [Min(0.7f)] public float overdriveWindup = 1.6f;
        [InspectorName("최종 합성기 돌진 속도")] [Min(8)] public float overdriveSpeed = 20;
        [InspectorName("벽 충돌 후 기절 시간")] [Min(1)] public float wallStunSeconds = 3.5f;
        [InspectorName("벽 충돌 보스 넉백 거리")] [Min(0.5f)] public float bossRecoilDistance = 3.5f;
        [InspectorName("기절 중 보스 받는 피해 배율")] [Min(1)] public float stunDamageMultiplier = 1.5f;
        [Header("기본 공격 혼합·메테오 연속 낙하")]
        [InspectorName("기본 공격 혼합 사용")] public bool enableAttackOverlap = true;
        [InspectorName("추가 마력탄 준비 대기")] [Min(0.1f)] public float overlayStartDelay = 0.35f;
        [InspectorName("추가 마력탄 조준 예고")] [Min(0.3f)] public float overlayAimWarning = 0.5f;
        [InspectorName("추가 마력탄 사이 대기")] [Min(0.2f)] public float overlayShotGap = 0.65f;
        [InspectorName("추가 마력탄 속도")] [Min(1)] public float overlayBoltSpeed = 5;
        [InspectorName("추가 마력탄 피해 배율")] [Min(0)] public float overlayDamageMultiplier = 0.65f;
        [InspectorName("1페이즈 메테오 수")] [Range(1, 7)] public int meteorCountPhaseOne = 3;
        [InspectorName("2페이즈 메테오 수")] [Range(1, 7)] public int meteorCountPhaseTwo = 5;
        [InspectorName("3페이즈 메테오 수")] [Range(1, 7)] public int meteorCountPhaseThree = 7;
        [InspectorName("메테오 예고 시 플레이어 위치 추적")] public bool meteorTracksPlayer = true;
        [InspectorName("메테오 추가 예고 간격")] [Min(0.2f)] public float meteorSequenceGap = 0.4f;
        public int MeteorCountFor(int phase)
        {
            return Mathf.Clamp(phase <= 1 ? meteorCountPhaseOne : phase == 2 ? meteorCountPhaseTwo : meteorCountPhaseThree, 1, 7);
        }
        public float HealthFor(int stage)
        {
            float hp = stage == 0 ? stageOneHealth : stage == 1 ? stageTwoHealth
                : stage == 2 ? stageThreeHealth : finalHealth;
            return Mathf.Max(1, hp * Mathf.Max(0.05f, healthMultiplier));
        }
        public static float TargetSeconds(int stage)
        {
            return stage == 0 ? 180 : stage == 1 ? 210 : stage == 2 ? 240 : 330;
        }
    }
}
