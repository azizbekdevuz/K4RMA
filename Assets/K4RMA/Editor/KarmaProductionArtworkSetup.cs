#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    // Reuse the canonical, reviewed library instead of creating duplicate pose atlases.
    public static class KarmaProductionArtworkSetup
    {
        const string Folder = "Assets/K4RMA/Art/Resources/K4RMAArtwork/";
        internal static void Assign(KarmaArtworkSettings settings)
        {
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>("Assets/K4RMA/Resources/KarmaSpriteLibrary.asset");
            if (library == null) throw new InvalidOperationException("먼저 동작 시트를 가져오세요: tools/unity/ImportSpriteSheets.cs");
            Func<string, string, Sprite> pose = (sheet, name) => library.Get(sheet, name)
                ?? throw new InvalidOperationException("Sprite 연결 누락: " + sheet + "/" + name);
            settings.spriteLibrary = library;
            settings.playerIdle = pose("Player", "Idle"); settings.playerMove1 = pose("Player", "Run");
            settings.playerMove2 = pose("Player", "Jump"); settings.playerSlash1 = pose("Player", "Slash");
            settings.playerSlash2 = pose("Player", "FollowThrough"); settings.playerRising = pose("Player", "Rising");
            settings.playerFalling = pose("Player", "AirSlash"); settings.playerHurt = pose("Player", "Hurt");
            settings.playerDeath = pose("Player", "Death");
            settings.neutralGuardian = pose("Neutral", "Idle"); settings.swordGuardian = pose("Wave", "Idle");
            settings.risingGuardian = pose("Rise", "Idle"); settings.guardGuardian = pose("Ward", "Idle");
            settings.mountainBackground = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Environments/MountainDojo/Background.png");
            settings.dojoBackground = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Environments/TrialHall/Background.png");
            settings.mountainPanorama = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Environments/MountainDojo/Panorama.png");
            settings.dojoPanorama = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Environments/TrialHall/Panorama.png");
            settings.menuBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "UI/MenuBackground.png");
            settings.skillIcons = new[] { pose("UI", "Wave"), pose("UI", "Rise"), pose("UI", "Ward"),
                pose("UI", "Pierce"), pose("UI", "Air"), pose("UI", "Parry") };
            settings.selectionCrescent = pose("UI", "Crescent");
            settings.hitEffect = pose("HitVFX", "Spark");
        }
        [MenuItem("K4RMA/현재 게임 이미지 연결 적용")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료하세요.");
            var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(KarmaArtworkSetup.SettingsPath);
            if (settings == null) throw new InvalidOperationException("기본 이미지 설정이 없습니다.");
            Undo.RecordObject(settings, "Apply canonical artwork");
            Assign(settings);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            Debug.Log("K4RMA 공용 동작 시트 연결 완료");
        }
        public static void ApplyAndValidate() { Apply(); KarmaArtworkValidation.Run(); }
    }
}
#endif
