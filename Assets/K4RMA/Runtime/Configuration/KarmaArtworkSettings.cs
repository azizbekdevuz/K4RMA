using UnityEngine;

namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/이미지 설정", fileName = "KarmaArtworkSettings")]
    public sealed class KarmaArtworkSettings : ScriptableObject
    {
        [Header("플레이어 동작 — Sprite를 연결하세요")]
        [InspectorName("대기")] public Sprite playerIdle;
        [InspectorName("이동 1 / 공중")] public Sprite playerMove1;
        [InspectorName("이동 2")] public Sprite playerMove2;
        [InspectorName("검 공격 1")] public Sprite playerSlash1;
        [InspectorName("검 공격 2")] public Sprite playerSlash2;
        [InspectorName("상승 공격")] public Sprite playerRising;
        [InspectorName("하강 공격")] public Sprite playerFalling;
        [InspectorName("피격")] public Sprite playerHurt;
        [InspectorName("사망")] public Sprite playerDeath;

        [Header("수호자 외형 — Sprite를 연결하세요")]
        [InspectorName("기본 수호자")] public Sprite neutralGuardian;
        [InspectorName("검 수호자")] public Sprite swordGuardian;
        [InspectorName("상승 수호자")] public Sprite risingGuardian;
        [InspectorName("결계 수호자")] public Sprite wardGuardian;

        [Header("스테이지 — Sprite를 연결하세요")]
        [InspectorName("도장 배경")] public Sprite dojoBackground;

        [InspectorName("실외 도장 배경")] public Sprite mountainBackground;

        [Header("Production UI / VFX")]
        public Texture2D menuBackground;
        public Sprite[] skillIcons;
        public Sprite selectionCrescent;
        public Sprite hitEffect;

        [Header("UI 일러스트 — PNG 텍스처를 연결하세요")]
        [InspectorName("타이틀 / 세계관")] public Texture2D worldOverview;
        [Tooltip("왼쪽 위 기준 0~1 비율. 그림 전체를 표지로 쓰려면 X=0, Y=0, W=1, H=1")]
        [InspectorName("타이틀 표시 영역")] public Rect titleCrop = new Rect(0, 0, 0.5f, 0.5f);
        [InspectorName("플레이어 소개")] public Texture2D playerSheet;
        [InspectorName("검기 설명")] public Texture2D playerSwordWave;
        [InspectorName("상승베기 설명")] public Texture2D playerRisingSlash;
        [InspectorName("검 수호자 소개")] public Texture2D swordGuardianIllustration;
        [InspectorName("상승 수호자 소개")] public Texture2D risingGuardianIllustration;
        [InspectorName("결계 수호자 소개")] public Texture2D wardGuardianIllustration;
        [Tooltip("가로 3칸 × 세로 2칸의 계승 일러스트 시트")]
        [InspectorName("수호자 계승 일러스트")] public Texture2D guardianEvolution;

        public Sprite[] PlayerFrames()
        {
            return new[] { playerIdle, playerMove1, playerMove2, playerSlash1, playerSlash2,
                playerRising, playerFalling, playerHurt, playerDeath };
        }

        public Rect TitleSource
        {
            get
            {
                if (worldOverview == null) return new Rect();
                return new Rect(titleCrop.x * worldOverview.width, titleCrop.y * worldOverview.height,
                    titleCrop.width * worldOverview.width, titleCrop.height * worldOverview.height);
            }
        }

        public Sprite Guardian(Essence? type)
        {
            if (!type.HasValue && neutralGuardian != null) return neutralGuardian;
            return type == Essence.Flame ? swordGuardian : type == Essence.Dash ? risingGuardian : wardGuardian;
        }

        public Texture2D Texture(string name)
        {
            switch (name)
            {
                case "MenuBackground": return menuBackground;
                case "WorldOverview": return worldOverview;
                case "PlayerSheet": return playerSheet;
                case "PlayerSwordWave": return playerSwordWave;
                case "PlayerRisingSlash": return playerRisingSlash;
                case "SwordGuardian": return swordGuardianIllustration;
                case "RisingGuardian": return risingGuardianIllustration;
                case "WardGuardian": return wardGuardianIllustration;
                case "GuardianEvolution": return guardianEvolution;
                case "PlayerAnimation": return playerIdle != null ? playerIdle.texture : null;
                case "GuardianSprites": return wardGuardian != null ? wardGuardian.texture : null;
                case "DojoBackground": return dojoBackground != null ? dojoBackground.texture : null;
                default: return null;
            }
        }
    }
}
