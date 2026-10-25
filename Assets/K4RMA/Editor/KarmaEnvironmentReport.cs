#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    public static class KarmaEnvironmentReport
    {
        [Serializable]
        sealed class Report
        {
            public string utc, unityVersion, platform, projectVersion, graphicsApi, sourceHash, artworkHash;
            public Vector2 screen;
            public Rect cameraViewport, uiViewport;
            public float cameraAspect, cameraSize;
            public string font, configAsset;
            public string[] warnings, liveGuardians;
            public SpriteInfo[] sprites;
        }
        [Serializable]
        sealed class SpriteInfo
        {
            public string slot, asset, guid, name;
            public Rect rect;
            public Vector2 pivot;
            public float pixelsPerUnit;
            public Vector2 textureSize;
        }
        [MenuItem("K4RMA/환경 진단 보고서 저장")]
        public static void Save()
        {
            string folder = "output/validation";
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "environment-" + Application.platform + ".json");
            File.WriteAllText(path, BuildJson(), Encoding.UTF8);
            Debug.Log("K4RMA 환경 진단 저장: " + Path.GetFullPath(path));
        }
        public static string BuildJson()
        {
            var warnings = new List<string>();
            var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(KarmaArtworkSetup.SettingsPath);
            var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            var config = game != null ? game.Config : AssetDatabase.LoadAssetAtPath<KarmaConfig>("Assets/K4RMA/Settings/KarmaConfig.asset");
            var camera = game != null ? game.GameCamera : Camera.main;
            var sprites = new List<SpriteInfo>();
            if (settings == null) warnings.Add("KarmaArtworkSettings is missing");
            else
            {
                AddSprite(sprites, warnings, "neutralGuardian", settings.neutralGuardian);
                AddSprite(sprites, warnings, "swordGuardian", settings.swordGuardian);
                AddSprite(sprites, warnings, "risingGuardian", settings.risingGuardian);
                AddSprite(sprites, warnings, "wardGuardian", settings.wardGuardian);
                AddSprite(sprites, warnings, "mountainPanorama", settings.mountainPanorama);
                AddSprite(sprites, warnings, "dojoPanorama", settings.dojoPanorama);
            }
            if (config == null) warnings.Add("KarmaConfig is missing");
            if (config == null || config.koreanFont == null) warnings.Add("UI uses OS-specific fallback fonts; Mac and Windows typography may differ");
            string projectVersion = File.ReadAllText("ProjectSettings/ProjectVersion.txt").Split('\n')[0].Trim();
            if (!projectVersion.EndsWith(Application.unityVersion, StringComparison.Ordinal)) warnings.Add("Editor version differs from ProjectVersion.txt");
            var live = new List<string>();
            if (game != null)
                foreach (var guardian in game.Enemies)
                {
                    if (guardian == null) continue;
                    var picture = guardian.transform.Find("Artwork/Guardian sprite");
                    live.Add(guardian.DisplayName + " | root=" + guardian.Position.ToString("F4")
                        + " | footPivotY=" + (picture != null ? picture.position.y.ToString("F4") : "missing")
                        + " | intent=" + guardian.Intent);
                }
            var report = new Report {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(), projectVersion = projectVersion,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), screen = new Vector2(Screen.width, Screen.height),
                uiViewport = UiViewportPixels(Screen.width, Screen.height),
                cameraViewport = camera != null ? camera.pixelRect : new Rect(),
                cameraAspect = camera != null ? camera.aspect : 0, cameraSize = camera != null ? camera.orthographicSize : 0,
                font = config != null && config.koreanFont != null ? AssetDatabase.GetAssetPath(config.koreanFont) : "OS fallback",
                configAsset = config != null ? AssetDatabase.GetAssetPath(config) : "missing",
                sourceHash = HashFiles(Directory.GetFiles("Assets/K4RMA", "*.cs", SearchOption.AllDirectories)),
                artworkHash = HashFiles(Directory.GetFiles("Assets/K4RMA/Resources", "*", SearchOption.AllDirectories)
                    .Concat(new[] { "Assets/K4RMA/Settings/KarmaConfig.asset" })),
                sprites = sprites.ToArray(), warnings = warnings.ToArray(), liveGuardians = live.ToArray()
            };
            return JsonUtility.ToJson(report, true);
        }
        static Rect UiViewportPixels(int width, int height)
        {
            // Match the centered 1280 x 720 layout used by KarmaHUD.OnGUI.
            float scale = Mathf.Min(width / 1280f, height / 720f);
            return new Rect((width - 1280 * scale) / 2, (height - 720 * scale) / 2,
                1280 * scale, 720 * scale);
        }
        static void AddSprite(List<SpriteInfo> list, List<string> warnings, string slot, Sprite sprite)
        {
            if (sprite == null) { warnings.Add("Missing sprite: " + slot); return; }
            string path = AssetDatabase.GetAssetPath(sprite);
            list.Add(new SpriteInfo { slot = slot, asset = path, guid = AssetDatabase.AssetPathToGUID(path), name = sprite.name,
                rect = sprite.rect, pivot = sprite.pivot, pixelsPerUnit = sprite.pixelsPerUnit,
                textureSize = new Vector2(sprite.texture.width, sprite.texture.height) });
        }
        static string HashFiles(IEnumerable<string> files)
        {
            var combined = new StringBuilder();
            using (var hash = SHA256.Create())
            {
                foreach (var file in files.OrderBy(p => p.Replace('\\', '/'), StringComparer.Ordinal))
                {
                    string extension = Path.GetExtension(file);
                    byte[] bytes = extension == ".png" ? File.ReadAllBytes(file)
                        : Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n", "\n"));
                    combined.Append(file.Replace('\\', '/')).Append(':').Append(BitConverter.ToString(hash.ComputeHash(bytes))).Append('\n');
                }
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(combined.ToString()))).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
#endif
