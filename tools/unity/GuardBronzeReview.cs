using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;
using KarmaPrototype.Editor;

// Pause a real practice pattern for a separate Pipeline capture command.
public static class GuardBronzeReview
{
    public static async Task<string> All()
    {
        Directory.CreateDirectory("output/validation");
        var results = new System.Collections.Generic.List<string>();
        for (int pattern = 1; pattern <= 5; pattern++)
        {
            results.Add(await Capture(pattern));
            var camera = Camera.main;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("output/validation/bronze-gd0" + pattern + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(pixels);
                EditorApplication.isPaused = false;
            }
        }
        File.WriteAllLines("output/validation/bronze-review.txt", results);
        KarmaGuardPractice.Start(5);
        return string.Join("\n", results);
    }

    public static async Task<string> Capture(int pattern)
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first.");
        EditorApplication.isPaused = false;
        int firstFrame = Time.frameCount;
        KarmaGuardPractice.Start(pattern);
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        game.Player.ResetForRoom(new Vector2(13, KarmaSpace.Units(1)), true);
        double deadline = EditorApplication.timeSinceStartup + 12;
        string expected = pattern == 2 ? "GuardBash" : pattern == 3 ? "GuardShard"
            : pattern == 4 ? "GuardPillar" : "GuardWave";
        while (true)
        {
            var enemy = game.Enemies[0];
            // Attacks live under World, outside the enemy hierarchy.
            var attacks = UnityEngine.Object.FindObjectsByType<KarmaBossAttack>();
            var halo = (SpriteRenderer)typeof(KarmaEnemy).GetField("shieldHalo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemy);
            bool ready = pattern == 1 ? halo.enabled && enemy.Armored
                : attacks.Any(a => a.Visual != null && a.Visual.sprite.texture.name == expected)
                    && (pattern != 3 || attacks.Length > 1);
            if (ready && Time.frameCount > firstFrame + 2)
            {
                EditorApplication.isPaused = true;
                return "GD0" + pattern + " real frames advanced: " + (Time.frameCount - firstFrame)
                    + "; paused with " + attacks.Length + " attacks; halo=" + halo.sprite.texture.name;
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Capture state not reached: " + enemy.Intent);
            await Task.Delay(10);
        }
    }
}
