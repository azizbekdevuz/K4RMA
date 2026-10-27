#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    // Deliberate import command: never re-slices or replaces Inspector assignments on startup.
    public static class KarmaProductionArtworkSetup
    {
        const string Prefix = "Production/";
        static SpriteRect Pose(string name, float x, float y, float w, float h, float footX, float footY)
        {
            // Reviewed top-left pixel rectangles, not an assumed uniform animation grid.
            return KarmaArtworkSetup.Frame(name, new Rect(x, 1024 - y - h, w, h),
                new Vector2((footX - x) / w, 1 - (footY - y) / h));
        }
        static Sprite Sprite(string sheet, string pose) { return KarmaArtworkSetup.Sprite(Prefix + sheet, pose); }

        [MenuItem("K4RMA/image 폴더 Production 원화 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료하세요.");
            var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(KarmaArtworkSetup.SettingsPath);
            if (settings == null) throw new InvalidOperationException("기본 이미지 설정이 없습니다.");
            KarmaArtworkSetup.ImportSprite(Prefix + "PlayerKeyposes", 130, new[] {
                Pose("Idle", 45, 15, 205, 285, 153, 290),
                Pose("Move1", 330, 45, 265, 250, 477, 282),
                Pose("Move2", 625, 30, 270, 260, 765, 278),
                Pose("Slash1", 610, 300, 375, 215, 745, 503),
                Pose("Slash2", 1180, 295, 355, 225, 1320, 507),
                Pose("Rising", 355, 510, 245, 280, 480, 776),
                Pose("Falling", 645, 800, 285, 220, 780, 995),
                Pose("Hurt", 1215, 550, 295, 235, 1370, 758),
                Pose("Death", 5, 910, 345, 105, 155, 992)
            });
            settings.playerIdle = Sprite("PlayerKeyposes", "Idle");
            settings.playerMove1 = Sprite("PlayerKeyposes", "Move1");
            settings.playerMove2 = Sprite("PlayerKeyposes", "Move2");
            settings.playerSlash1 = Sprite("PlayerKeyposes", "Slash1");
            settings.playerSlash2 = Sprite("PlayerKeyposes", "Slash2");
            settings.playerRising = Sprite("PlayerKeyposes", "Rising");
            settings.playerFalling = Sprite("PlayerKeyposes", "Falling");
            settings.playerHurt = Sprite("PlayerKeyposes", "Hurt");
            settings.playerDeath = Sprite("PlayerKeyposes", "Death");
            KarmaArtworkSetup.ImportSprite(Prefix + "NeutralGuardian", 145, new[] { Pose("Idle", 5, 90, 350, 420, 242, 500) });
            KarmaArtworkSetup.ImportSprite(Prefix + "WaveGuardian", 125, new[] { Pose("Idle", 45, 5, 310, 350, 246, 345) });
            KarmaArtworkSetup.ImportSprite(Prefix + "RiseGuardian", 125, new[] { Pose("Idle", 80, 5, 335, 310, 255, 305) });
            KarmaArtworkSetup.ImportSprite(Prefix + "WardGuardian", 125, new[] { Pose("Idle", 10, 45, 395, 305, 222, 339) });
            settings.neutralGuardian = Sprite("NeutralGuardian", "Idle");
            settings.swordGuardian = Sprite("WaveGuardian", "Idle");
            settings.risingGuardian = Sprite("RiseGuardian", "Idle");
            settings.guardGuardian = Sprite("WardGuardian", "Idle");
            foreach (var name in new[] { "MountainDojo", "TrialHall" }) KarmaArtworkSetup.ImportSprite(Prefix + name, 100, null);
            settings.mountainBackground = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/K4RMA/Resources/K4RMAArtwork/Production/MountainDojo.png");
            settings.dojoBackground = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/K4RMA/Resources/K4RMAArtwork/Production/TrialHall.png");
            settings.menuBackground = KarmaArtworkSetup.ImportTexture(Prefix + "MenuBackground");
            KarmaArtworkSetup.ImportSprite(Prefix + "UIComponents", 100, new[] {
                Pose("Wave", 20, 340, 315, 160, 177, 420),
                Pose("Rise", 355, 280, 180, 265, 445, 412),
                Pose("Ward", 550, 335, 190, 180, 645, 425),
                Pose("Pierce", 785, 290, 150, 250, 860, 415),
                Pose("Air", 1000, 345, 235, 155, 1117, 422),
                Pose("Parry", 1260, 335, 260, 180, 1390, 425),
                Pose("Crescent", 490, 115, 75, 95, 527, 162)
            });
            settings.skillIcons = new[] { Sprite("UIComponents", "Wave"), Sprite("UIComponents", "Rise"), Sprite("UIComponents", "Ward"),
                Sprite("UIComponents", "Pierce"), Sprite("UIComponents", "Air"), Sprite("UIComponents", "Parry") };
            settings.selectionCrescent = Sprite("UIComponents", "Crescent");
            // Hit sheet is 1792x896; use one isolated impact, preserving its original alpha.
            KarmaArtworkSetup.ImportSprite(Prefix + "HitVFX", 350, new[] {
                KarmaArtworkSetup.Frame("Impact", new Rect(15, 896 - 420, 390, 340), Vector2.one * 0.5f)
            });
            settings.hitEffect = Sprite("HitVFX", "Impact");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("K4RMA Production 원화 적용 완료");
        }
        public static void ApplyAndValidate() { Apply(); KarmaArtworkValidation.Run(); }
    }
}
#endif
