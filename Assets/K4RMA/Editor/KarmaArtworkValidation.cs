#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    public static class KarmaArtworkValidation
    {
        public static void Run()
        {
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(KarmaArtworkSetup.SettingsPath);
                if (settings == null || KarmaArtwork.Settings != settings)
                    throw new InvalidOperationException("Resources 이미지 설정 에셋 연결 실패");
                string[] names = { "WorldOverview", "PlayerAnimation", "PlayerSheet", "GuardianSprites", "DojoBackground", "PlayerSwordWave", "PlayerRisingSlash", "WardGuardian", "RisingGuardian", "SwordGuardian", "GuardianEvolution" };
                foreach (string name in names)
                {
                    var texture = KarmaArtwork.Texture(name);
                    if (texture == null || texture.width < 2 || texture.height < 2)
                        throw new InvalidOperationException("이미지 로딩 실패: " + name);
                }
                var frames = KarmaArtwork.PlayerFrames();
                if (frames.Length != 9) throw new InvalidOperationException("플레이어 프레임 수 오류");
                foreach (var frame in frames) if (frame == null || !AssetDatabase.Contains(frame) || frame.rect.width <= 0 || frame.rect.height <= 0)
                    throw new InvalidOperationException("플레이어 Sprite 에셋 참조 오류");
                foreach (Essence type in Enum.GetValues(typeof(Essence)))
                    if (settings.Guardian(type) == null || !AssetDatabase.Contains(settings.Guardian(type)))
                        throw new InvalidOperationException("수호자 Sprite 에셋 참조 오류: " + type);
                if (settings.dojoBackground == null || !AssetDatabase.Contains(settings.dojoBackground))
                    throw new InvalidOperationException("배경 Sprite 에셋 참조 오류");
                // The runtime must follow Inspector references, regardless of the PNG filename.
                var original = settings.worldOverview;
                try
                {
                    settings.worldOverview = settings.playerSwordWave;
                    if (KarmaArtwork.Texture("WorldOverview") != settings.playerSwordWave)
                        throw new InvalidOperationException("Inspector 텍스처 교체가 반영되지 않습니다.");
                }
                finally { settings.worldOverview = original; }
                var root = new GameObject("Artwork validation");
                if (KarmaArtwork.Player(root.transform) == null || !KarmaArtwork.Background(root.transform))
                    throw new InvalidOperationException("플레이어/배경 연결 실패");
                foreach (Essence type in Enum.GetValues(typeof(Essence)))
                    KarmaArtwork.Guardian(root.transform, new[] { type }, false);
                var reset = typeof(KarmaArtwork).GetMethod("ResetCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                reset.Invoke(null, null);
                if (frames[0] == null || original == null || KarmaArtwork.Settings != settings)
                    throw new InvalidOperationException("캐시 초기화 시 공유 이미지 에셋이 파괴되었습니다.");
                UnityEngine.Object.DestroyImmediate(root);
                File.WriteAllText("artwork-validation.txt", "PASS: shared Inspector settings, 11 textures, 9 native player sprites, 3 guardian sprites, background, reference replacement and cache reset.\n");
                Debug.Log("K4RMA 이미지 연결 검사 PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText("artwork-validation.txt", "FAIL: " + error + "\n");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
#endif
