#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    public static class KarmaArtworkSetup
    {
        public const string SettingsPath = "Assets/K4RMA/Resources/KarmaArtworkSettings.asset";
        [MenuItem("K4RMA/이미지 설정 선택")]
        public static void SelectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(SettingsPath);
            if (settings == null) throw new InvalidOperationException("기본 이미지 설정이 없습니다.");
            Selection.activeObject = settings; EditorGUIUtility.PingObject(settings);
        }
        [MenuItem("K4RMA/기본 이미지 설정 생성 (최초 1회)")]
        public static void CreateDefaults()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이 모드를 종료하세요.");
            if (AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(SettingsPath) != null) { SelectSettings(); return; }
            var settings = ScriptableObject.CreateInstance<KarmaArtworkSettings>();
            KarmaProductionArtworkSetup.Assign(settings);
            AssetDatabase.CreateAsset(settings, SettingsPath); AssetDatabase.SaveAssets(); SelectSettings();
        }
        public static void InitializeAndValidate() { CreateDefaults(); KarmaArtworkValidation.Run(); }
    }
}
#endif
