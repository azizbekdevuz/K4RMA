using System;
using System.IO;
using UnityEngine;
using KarmaPrototype;

// Native render of the exact runtime orientation/material policy, in Edit Mode.
public static class PreviewBossEffects
{
    public static string Main()
    {
        if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Preview in Edit Mode.");
        var root = new GameObject("Boss direction preview");
        var cameraObject = new GameObject("Preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(1280, 960, 24);
        var previous = RenderTexture.active;
        Texture2D capture = null;
        try
        {
            var directions = new[] { Vector2.right, Vector2.left, Vector2.right, Vector2.left,
                Vector2.up, Vector2.down, new Vector2(1, 1), new Vector2(-1, -1),
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
            var names = new[] { "Wave", "Wave", "AimedWave", "AimedWave", "Wave", "Wave", "Blade", "Blade", "Cleave", "Cleave", "Pillar", "Pillar" };
            for (int i = 0; i < names.Length; i++)
            {
                var item = new GameObject(names[i]); item.transform.SetParent(root.transform);
                item.transform.position = new Vector3((i % 4 - 1.5f) * 5, (1 - i / 4) * 4.2f, 0);
                var picture = new GameObject("Artwork"); picture.transform.SetParent(item.transform, false);
                var sr = picture.AddComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(sr); sr.sortingOrder = 26;
                KarmaBossAttack.ConfigureVisual(sr, directions[i], new Vector2(3, 3), i % 2 == 0 ? 1 : -1, names[i]);
                sr.color = new Color(1, 0.9f, 0.85f);
                KarmaCombatVFX.AttachAttack(sr, directions[i], new Color(1, 0.45f, 0.1f), root.transform);
            }
            foreach (Transform item in root.GetComponentsInChildren<Transform>()) item.gameObject.layer = 31;
            camera.orthographic = true; camera.orthographicSize = 7.5f; camera.aspect = 4f / 3;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f, 1);
            camera.cullingMask = 1 << 31; camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); capture.Apply();
            Directory.CreateDirectory("output/validation/combat");
            File.WriteAllBytes("output/validation/combat/boss-directions.png", capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(capture); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(root);
        }
        return "PASS: 12 runtime boss effect direction/material samples rendered.";
    }
}
