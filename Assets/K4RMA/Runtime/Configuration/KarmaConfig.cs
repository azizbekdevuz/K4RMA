using UnityEngine;

namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/게임 설정", fileName = "KarmaConfig")]
    public sealed class KarmaConfig : ScriptableObject
    {
        [Header("15~20분 목표 난이도 — 실제 보스 체력은 이 프로필 사용")]
        [InspectorName("보스 난이도·화상 설정")] public KarmaBossTuning bossTuning = new KarmaBossTuning();
        public KarmaBossTuning BossTuning
        {
            get { if (bossTuning == null) bossTuning = new KarmaBossTuning(); return bossTuning; }
        }
        [Header("플레이어")]
        [Min(1)] [InspectorName("플레이어 최대 체력")] public float playerHealth = 100;
        [Min(1)] [InspectorName("이동 속도")] public float moveSpeed = 7;
        [Min(1)] [InspectorName("점프 속도")] public float jumpSpeed = 13;
        [Min(0.1f)] [InspectorName("중력 배율")] public float gravityScale = 3;
        [Min(0)] [InspectorName("피격 후 무적 시간")] public float hitInvulnerability = 0.7f;
        [Header("검 공격")]
        [Min(1)] [InspectorName("검 공격력")] public float swordDamage = 16;
        [Min(0.1f)] [InspectorName("검 공격 간격")] public float swordCooldown = 0.32f;
        [Min(0.5f)] [InspectorName("검 사거리")] public float swordReach = 2.1f;
        [Header("액티브 기술")]
        [Min(0.1f)] [InspectorName("검기 쿨타임")] public float flameCooldown = 2.2f;
        [Min(1)] [InspectorName("검기 피해")] public float flameDamage = 32;
        [Min(1)] [InspectorName("검기 속도")] public float flameSpeed = 14;
        [Min(0.1f)] [InspectorName("상승베기 쿨타임")] public float dashCooldown = 1.6f;
        [Min(1)] [InspectorName("돌진 속도")] public float dashSpeed = 21;
        [Min(0.05f)] [InspectorName("돌진 지속 시간")] public float dashDuration = 0.18f;
        [Min(0.1f)] [InspectorName("보호막 쿨타임")] public float wardCooldown = 5;
        [Min(0.1f)] [InspectorName("보호막 지속 시간")] public float wardDuration = 1.5f;
        [Header("원형·고유화 기술 조정")]
        public KarmaTechniqueTuning techniques = new KarmaTechniqueTuning();
        public KarmaTechniqueTuning Techniques { get { if (techniques == null) techniques = new KarmaTechniqueTuning(); return techniques; } }
        [Header("구버전 호환 (전투에 미사용)")]
        [Min(1)] [InspectorName("불씨 피해")] public float emberDamage = 9;
        [Min(1)] [InspectorName("검기 피해")] public float waveDamage = 12;
        [Min(1)] [InspectorName("검기 발동에 필요한 적중 수")] public int waveEveryHits = 3;
        [Range(0, 0.9f)] [InspectorName("보호막 무의식 피해 감소율")] public float wardDamageReduction = 0.25f;
        [Min(1)] [InspectorName("충격파 발동에 필요한 적중 수")] public int wardBurstEveryHits = 4;
        [Min(1)] [InspectorName("충격파 피해")] public float wardBurstDamage = 18;
        [Min(1)] [InspectorName("충격파 반경")] public float wardBurstRadius = 3;
        [Header("구버전 호환 체력 (현재 난이도에는 미사용)")]
        [Min(1)] [InspectorName("구버전 시험 상대 체력")] public float summonHealth = 170;
        [Min(0)] [InspectorName("스테이지당 추가 체력")] public float healthPerStage = 55;
        [Min(1)] [InspectorName("구버전 최종 보스 체력")] public float witchHealth = 420;
        [Header("적 기본 능력 — 난이도 프로필의 배율 적용")]
        [Min(1)] [InspectorName("적 공격력")] public float enemyDamage = 12;
        [Min(0.1f)] [InspectorName("적 이동 속도")] public float enemyMoveSpeed = 2.4f;
        [Min(0.2f)] [InspectorName("적 공격 선택 간격")] public float enemyDecisionDelay = 1.2f;
        [Min(0.2f)] [InspectorName("기본 공격 예고 시간")] public float enemyTelegraphTime = 0.75f;
        [Range(0.1f, 1)] [InspectorName("적 보호막 중 받는 피해 배율")] public float enemyWardDamageMultiplier = 0.3f;
        [Min(0)] [InspectorName("고유화 후 체력 회복")] public float healAfterSacrifice = 30;
        [Header("적 합성 패턴")]
        [Min(0.2f)] [InspectorName("합성 공격 예고 시간")] public float fusionTelegraphTime = 1.15f;
        [Min(0.2f)] [InspectorName("불길 지속 시간")] public float fireTrailLifetime = 2.4f;
        [Min(0.05f)] [InspectorName("불길 생성 간격")] public float fireTrailInterval = 0.12f;
        [Min(1)] [InspectorName("불길 피해")] public float fireTrailDamage = 9;
        [Min(0.1f)] [InspectorName("화염 반격 쿨타임")] public float flameCounterCooldown = 0.65f;
        [Min(4)] [InspectorName("합성 고리 탄환 수")] public int fusionRingProjectiles = 12;
        [Min(1)] [InspectorName("합성 탄환 속도")] public float fusionProjectileSpeed = 7;
        [Header("화면 표시")]
        [InspectorName("한글 UI 폰트 (비우면 자동)")] public Font koreanFont;
        [InspectorName("2D 표시 재질 (빌드에 포함)")] public Material spriteMaterial;
        [InspectorName("검기 색상")] public Color flameColor = new Color(1, 0.38f, 0.18f);
        [InspectorName("상승베기 색상")] public Color dashColor = new Color(0.25f, 0.9f, 0.9f);
        [InspectorName("보호막 색상")] public Color wardColor = new Color(0.67f, 0.48f, 1);
        public Color ColorOf(Essence essence)
        {
            return essence == Essence.Flame ? flameColor : essence == Essence.Dash ? dashColor : wardColor;
        }
    }
}
