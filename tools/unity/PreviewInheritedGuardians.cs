using System;
using System.IO;
using System.Linq;
using UnityEngine;
using KarmaPrototype;

public static class PreviewInheritedGuardians
{
    public static string Main()
    {
        if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Preview in Edit Mode.");
        const string folder = "output/validation/inheritance";
        Directory.CreateDirectory(folder);
        foreach (string form in new[] { "WaveRise", "WaveWard", "WaveFinal", "RiseWave", "RiseWard", "RiseFinal", "WardWave", "WardRise", "WardFinal" })
        {
            string type = form.StartsWith("Wave", StringComparison.Ordinal) ? "Wave" : form.StartsWith("Rise", StringComparison.Ordinal) ? "Rise" : "Ward";
            var names = KarmaArtwork.Settings.spriteLibrary.All(type).Select(s => s.name)
                .Concat(new[] { "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand" }).ToArray();
            var sprites = KarmaArtwork.InheritedPoses(form).ToDictionary(s => s.name);
            var root = new GameObject("Inherited pose preview") { hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("Inherited preview camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1280, 1280, 24); var previous = RenderTexture.active; Texture2D capture = null;
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var go = new GameObject(names[i]); go.layer = 31; go.transform.SetParent(root.transform);
                    go.transform.position = new Vector3((i % 4 - 1.5f) * 5.2f, 6.7f - i / 4 * 5.2f, 0);
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprites[names[i]]; KarmaVisuals.ApplyMaterial(renderer);
                }
                camera.orthographic = true; camera.orthographicSize = 10.4f; camera.aspect = 1;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 1); camera.cullingMask = 1 << 31; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                capture = new Texture2D(1280, 1280, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0); capture.Apply();
                File.WriteAllBytes(folder + "/" + form + ".png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
            }
        }
        return "Rendered all 144 inherited poses to " + folder;
    }
}
