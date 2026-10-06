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
                string[] names = { "WorldOverview", "PlayerAnimation", "PlayerSheet", "GuardianSprites", "DojoBackground", "PlayerSwordWave", "PlayerRisingSlash", "WardGuardian", "RisingGuardian", "SwordGuardian", "GuardianEvolution" };
                foreach (string name in names)
                {
                    var texture = KarmaArtwork.Texture(name);
                    if (texture == null || texture.width < 2 || texture.height < 2)
                        throw new InvalidOperationException("이미지 로딩 실패: " + name);
                }
                var cover = KarmaArtwork.Texture("WorldOverview");
                if (cover.width != 1280 || cover.height != 854) throw new InvalidOperationException("표지 원본 크기가 변했습니다.");
                var frames = KarmaArtwork.PlayerFrames();
                if (frames.Length != 9) throw new InvalidOperationException("플레이어 프레임 수 오류");
                foreach (var frame in frames) if (frame == null || frame.rect.width != 418 || frame.rect.height != 418)
                    throw new InvalidOperationException("플레이어 스프라이트 생성/크기 오류");
                var root = new GameObject("Artwork validation");
                if (KarmaArtwork.Player(root.transform) == null || !KarmaArtwork.Background(root.transform))
                    throw new InvalidOperationException("플레이어/배경 연결 실패");
                KarmaArtwork.Guardian(root.transform, new Essence[] { Essence.Ward, Essence.Flame }, false);
                UnityEngine.Object.DestroyImmediate(root);
                File.WriteAllText("artwork-validation.txt", "PASS: Unity loaded 11 original-size PNGs, created 9 sprites, player, guardian and background.\n");
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
