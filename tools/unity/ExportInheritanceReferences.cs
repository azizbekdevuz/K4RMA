using System;
using System.IO;
using UnityEngine;
using KarmaPrototype;

public static class ExportInheritanceReferences
{
    public static string Main()
    {
        if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        const string folder = "output/validation/inheritance-references";
        Directory.CreateDirectory(folder);
        foreach (string form in new[] { "WaveRise", "WaveWard", "WaveFinal", "RiseWave", "RiseWard", "RiseFinal", "WardWave", "WardRise", "WardFinal" })
        {
            var root = new GameObject("Inheritance reference"); root.hideFlags = HideFlags.HideAndDontSave; root.layer = 31;
            var sr = root.AddComponent<SpriteRenderer>(); sr.sprite = KarmaArtwork.Sprite("Inheritance", form); KarmaVisuals.ApplyMaterial(sr);
            var cameraObject = new GameObject("Reference camera"); cameraObject.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(768, 768, 24); var previous = RenderTexture.active; Texture2D capture = null;
            try
            {
                float scale = 5.4f / Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
                root.transform.localScale = Vector3.one * scale; root.transform.position = -sr.sprite.bounds.center * scale;
                camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 1;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 31; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                capture = new Texture2D(768, 768, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); capture.Apply();
                File.WriteAllBytes(folder + "/" + form + ".png", capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
            }
        }
        return "Exported nine exact inherited guardian identity references to " + folder;
    }
}
