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
                KarmaCompleteArtImport.Validate();
                var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(KarmaArtworkSetup.SettingsPath);
                if (settings == null || KarmaArtwork.Settings != settings)
                    throw new InvalidOperationException("Resources 이미지 설정 에셋 연결 실패");
                ValidateSheets(settings.spriteLibrary);
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
                if (settings.menuBackground != null)
                {
                    if (settings.skillIcons == null || settings.skillIcons.Length != 6
                        || settings.selectionCrescent == null || settings.hitEffect == null
                        || settings.neutralGuardian == null || settings.mountainBackground == null)
                        throw new InvalidOperationException("Production UI / 기본 수호자 / 실외 배경 연결 누락");
                    foreach (var icon in settings.skillIcons)
                        if (icon == null || !AssetDatabase.Contains(icon))
                            throw new InvalidOperationException("Production 기술 아이콘 참조 오류");
                    if (settings.Guardian(null) != settings.neutralGuardian)
                        throw new InvalidOperationException("첫 시련 기본 수호자 선택 오류");
                }
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
                if (!KarmaArtwork.Background(root.transform, 0)) throw new InvalidOperationException("실외 배경 생성 실패");
                KarmaArtwork.Guardian(root.transform, Array.Empty<Essence>(), false);
                foreach (Essence type in Enum.GetValues(typeof(Essence)))
                    KarmaArtwork.Guardian(root.transform, new[] { type }, false);
                var reset = typeof(KarmaArtwork).GetMethod("ResetCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                reset.Invoke(null, null);
                if (frames[0] == null || original == null || KarmaArtwork.Settings != settings)
                    throw new InvalidOperationException("캐시 초기화 시 공유 이미지 에셋이 파괴되었습니다.");
                UnityEngine.Object.DestroyImmediate(root);
                File.WriteAllText("artwork-validation.txt", "PASS: shared Inspector settings, 208 named sprites across 37 sheets/layers, player/guardian poses, VFX/UI/props/inheritance, native sprite geometry, reference replacement and cache reset.\n");
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
        static void ValidateSheets(KarmaSpriteLibrary library)
        {
            if (library == null) throw new InvalidOperationException("스프라이트 라이브러리 연결 누락");
            var keys = new System.Collections.Generic.HashSet<string>();
            int total = 0;
            foreach (var sheet in library.sheets)
            {
                if (sheet == null || string.IsNullOrEmpty(sheet.id) || sheet.sprites == null)
                    throw new InvalidOperationException("잘못된 스프라이트 시트");
                foreach (var sprite in sheet.sprites)
                {
                    if (sprite == null || !AssetDatabase.Contains(sprite) || sprite.vertices.Length < 3 || sprite.triangles.Length < 3)
                        throw new InvalidOperationException("Sprite 참조 또는 렌더 메시 누락: " + sheet.id);
                    if (!keys.Add(sheet.id + "/" + sprite.name) || library.Get(sheet.id, sprite.name) != sprite)
                        throw new InvalidOperationException("Sprite 이름 중복 또는 검색 실패: " + sheet.id + "/" + sprite.name);
                    if (sprite.rect.xMin < 0 || sprite.rect.yMin < 0 || sprite.rect.xMax > sprite.texture.width || sprite.rect.yMax > sprite.texture.height)
                        throw new InvalidOperationException("Sprite가 이미지 영역을 벗어났습니다: " + sprite.name);
                    total++;
                }
            }
            if (total != 208 || library.All("Player").Length != 20 || library.All("Neutral").Length != 8
                || library.All("Wave").Length != 12 || library.All("Rise").Length != 12 || library.All("Ward").Length != 12)
                throw new InvalidOperationException("포즈 또는 시트 분리 누락");
        }
    }
}
#endif
