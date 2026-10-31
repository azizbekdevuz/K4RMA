using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/이미지 설정", fileName = "KarmaArtworkSettings")]
    public sealed class KarmaArtworkSettings : ScriptableObject
    {
        [Header("개별 포즈 · VFX · UI · 소품 스프라이트")]
        public KarmaSpriteLibrary spriteLibrary;
        [Tooltip("이전 원본 카탈로그 표시 경로. 현재는 Sprite Library의 검토된 포즈와 VFX를 사용합니다.")]
        public bool useCompleteArtwork = false;
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
        [FormerlySerializedAs("wardGuardian")]
        [InspectorName("결계 수호자")] public Sprite guardGuardian;

        [Header("검기 주 능력 수호자 · 투명 단일 외형")]
        public Sprite waveGuardianBase;
        public Sprite waveGuardianRise;
        public Sprite waveGuardianWard;
        public Sprite waveGuardianFinal;

        public Sprite WaveGuardian(IReadOnlyList<Essence> order)
        {
            if (KarmaPatternCatalog.Primary(order) != Essence.SwordWave) return null;
            bool rise = false, ward = false;
            foreach (var essence in order) { rise |= essence == Essence.RisingSlash; ward |= essence == Essence.Guard; }
            var configured = rise && ward ? waveGuardianFinal : rise ? waveGuardianRise : ward ? waveGuardianWard : waveGuardianBase;
            string pose = rise && ward ? "Wave_Rise_Ward" : rise ? "Wave_Rise" : ward ? "Wave_Ward" : "Wave_Base";
            return configured != null ? configured : spriteLibrary != null ? spriteLibrary.Get("WaveGuardians", pose) : null;
        }

        [Header("스테이지 — Sprite를 연결하세요")]
        [InspectorName("도장 배경")] public Sprite dojoBackground;

        [InspectorName("실외 도장 배경")] public Sprite mountainBackground;
        [Header("좌우 스크롤용 파노라마")]
        [InspectorName("실외 파노라마")] public Sprite mountainPanorama;
        [InspectorName("도장 파노라마")] public Sprite dojoPanorama;

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
        [FormerlySerializedAs("wardGuardianIllustration")]
        [InspectorName("결계 수호자 소개")] public Texture2D guardGuardianIllustration;
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
            return type == Essence.SwordWave ? swordGuardian : type == Essence.RisingSlash ? risingGuardian : guardGuardian;
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
                case "WardGuardian": return guardGuardianIllustration;
                case "GuardianEvolution": return guardianEvolution;
                case "PlayerAnimation": return playerIdle != null ? playerIdle.texture : null;
                case "GuardianSprites": return guardGuardian != null ? guardGuardian.texture : null;
                case "DojoBackground": return dojoBackground != null ? dojoBackground.texture : null;
                default: return null;
            }
        }
    }
}
