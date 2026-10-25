using System;
using System.IO;
using UnityEngine;
using KarmaPrototype;

// Render actual imported sprite meshes, rather than rectangular PNG crops.
public static class PreviewSpriteSheets
{
    public static string Main()
    {
        if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Preview in Edit Mode.");
        string folder = "output/validation/sprites"; Directory.CreateDirectory(folder);
        var library = KarmaArtwork.Settings.spriteLibrary;
        foreach (string sheet in new[] { "Player", "Neutral", "Wave", "Rise", "Ward", "Inheritance", "PlayerVFX", "GuardianVFX", "PersonalizationVFX", "HitVFX" })
        {
            var sprites = library.All(sheet);
            int columns = sheet == "Player" ? 5 : 4, rows = (sprites.Length + columns - 1) / columns;
            var root = new GameObject("Sprite mesh preview"); root.hideFlags = HideFlags.HideAndDontSave;
            var cameraObject = new GameObject("Preview camera"); cameraObject.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(columns * 240, rows * 280, 24);
            var previous = RenderTexture.active;
            Texture2D capture = null;
            try
            {
                camera.orthographic = true; camera.orthographicSize = rows * 1.75f;
                camera.aspect = (float)target.width / target.height;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 1);
                camera.cullingMask = 1 << 31; camera.targetTexture = target;
                for (int i = 0; i < sprites.Length; i++)
                {
                    var go = new GameObject(sprites[i].name); go.layer = 31; go.transform.SetParent(root.transform);
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprites[i]; KarmaVisuals.ApplyMaterial(renderer);
                    var size = sprites[i].bounds.size;
                    float scale = Mathf.Min(2.7f / size.x, 3.1f / size.y);
                    go.transform.localScale = Vector3.one * scale;
                    go.transform.position = new Vector3((i % columns - (columns - 1) / 2f) * 3,
                        ((rows - 1) / 2f - i / columns) * 3.5f, 0) - sprites[i].bounds.center * scale;
                }
                camera.Render(); RenderTexture.active = target;
                capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); capture.Apply();
                File.WriteAllBytes(folder + "/Unity-" + sheet + ".png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
            }
        }
        return "Rendered imported player/guardian/inheritance meshes to " + folder;
    }
}
