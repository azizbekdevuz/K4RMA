#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

// Runs the real player -> LoadRoom -> guardian transition for each essence.
[InitializeOnLoad]
public static class KarmaTransitionBackgroundChecks
{
    const string Key = "K4RMA.TransitionBackdropChecks";
    const string Output = "output/validation/transition-background";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static KarmaGame game;
    static int scenario, step, assertions, samples, captured;
    static float began, lastTime;
    static double deadline;
    static Sprite plate;
    static bool initialized, priorRunInBackground;
    static KarmaTransitionBackgroundChecks() { EditorApplication.update += Tick; }
    [MenuItem("K4RMA/검증/전체 고유화 배경")]
    public static void Start()
    {
        SessionState.SetBool(Key, true);
        initialized = false;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }
    static void Check(bool ok, string message)
    {
        assertions++;
        if (!ok) throw new Exception(message);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (!initialized)
            {
                game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
                if (game == null) return;
                priorRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                Directory.CreateDirectory(Output);
                scenario = step = assertions = captured = samples = 0;
                plate = KarmaArtwork.Settings.mountainPanorama;
                Check(plate != null && plate.name == "MountainDojo_Extended", "Expected outpainted Mountain Dojo background");
                Check(plate.pixelsPerUnit == 100, "Preserve PPU");
                // Cover every stage selection, and actual viewport letterboxing at different ratios.
                for (int stage = 0; stage < 4; stage++)
                {
                    var root = new GameObject("Backdrop selection check");
                    try
                    {
                        KarmaArtwork.Background(root.transform, stage);
                        var renderer = root.GetComponentInChildren<SpriteRenderer>();
                        var expected = stage == 0 ? KarmaArtwork.Settings.mountainPanorama : KarmaArtwork.Settings.dojoPanorama;
                        if (expected == null) expected = stage == 0 ? KarmaArtwork.Settings.mountainBackground : KarmaArtwork.Settings.dojoBackground;
                        Check(renderer.sprite == expected, "Stage " + stage + " selected another place");
                        Check(root.GetComponentsInChildren<Renderer>().Length == 1, "No overlapping background mesh");
                        Check(!renderer.flipX && !renderer.flipY, "No mirrored background");
                        Check(renderer.drawMode == SpriteDrawMode.Simple && renderer.sortingOrder == -20, "No tiling / original order");
                        var b = renderer.bounds;
                        Check(Mathf.Abs(b.size.x - 140) < 0.01f, "World coverage width");
                        float floorFromTop = stage == 0 ? 0.74f : 0.8f;
                        float centralFloor = 0.5f + floorFromTop / 2f;
                        float floorY = b.max.y - b.size.y * centralFloor;
                        Check(Mathf.Abs(floorY) < 0.02f, "Stage " + stage + " artwork floor aligns with world ground: " + floorY);
                        foreach (float x in new[] { -15f, 20f, 55f })
                        foreach (bool boss in new[] { false, true })
                        {
                            float size = 50 * 941f / 1672 / 2 * (boss ? 0.8f : 0.75f);
                            float y = boss ? KarmaSpace.GuardianGroundY + 2.2f : KarmaSpace.PlayerHalfHeight + 0.6f;
                            Check(x - size * 16 / 9 > b.min.x && x + size * 16 / 9 < b.max.x
                                && y - size > b.min.y && y + size < b.max.y, "Analytic transition edge coverage");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                var viewport = typeof(KarmaGame).GetMethod("ScreenViewport", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (var size in new[] { new Vector2(1024,768), new Vector2(1280,720), new Vector2(2560,1080), new Vector2(720,1280) })
                {
                    var rect = (Rect)viewport.Invoke(null, new object[] { size.x, size.y });
                    Check(Mathf.Abs(rect.width / rect.height - 16f / 9) < 0.001f, "Viewport aspect " + size);
                }
                initialized = true;
                BeginScenario();
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Transition stalled: " + scenario + "/" + step);
            if (Time.time == lastTime) return;
            lastTime = Time.time;
            if (game.World != null) InspectView();
            if (step == 0 && game.Phase == RunPhase.Choosing)
            {
                game.Choose((Essence)scenario); game.ConfirmChoice();
                Check(game.Phase == RunPhase.Transition, "Choice did not start personalization");
                step = 1; samples = 0; began = Time.time;
            }
            if (step == 1)
            {
                CaptureAt(2.4f, "player");
                if (game.GuardianInheriting)
                {
                    Check(samples == 3, "Missing player samples");
                    Check(Background().sprite == ExpectedBackground(game.StageIndex), "LoadRoom selected the wrong stage backdrop");
                    Check(Background().sprite != plate, "Stage progression did not restore the later-stage backdrop");
                    step = 2; samples = 0; began = Time.time;
                }
            }
            if (step == 2)
            {
                CaptureAt(3.2f, "guardian");
                if (game.Phase == RunPhase.Combat)
                {
                    Check(samples == 3, "Missing guardian samples");
                    scenario++;
                    if (scenario == 3)
                    {
                        string result = "PASS: " + assertions + " assertions; all 3 essences/player+guardian; " + captured
                            + " start/mid/end captures; one backdrop renderer; texture bytes="
                            + UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(plate.texture);
                        Finish(result); return;
                    }
                    BeginScenario();
                }
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish("FAIL: " + e); }
    }
    static SpriteRenderer Background() => game.World.GetComponentsInChildren<SpriteRenderer>()
        .First(renderer => renderer.sortingOrder == -20);
    static Sprite ExpectedBackground(int stage)
    {
        if (stage == 0) return KarmaArtwork.Settings.mountainPanorama ?? KarmaArtwork.Settings.mountainBackground;
        return KarmaArtwork.Settings.dojoPanorama ?? KarmaArtwork.Settings.dojoBackground;
    }
    static void BeginScenario()
    {
        game.StartRun();
        if (!game.Player.TestInvincible) game.Player.ToggleTestInvincibility();
        game.Player.ResetForRoom(new Vector2(new[] { -12f, 20f, 52f }[scenario], KarmaSpace.PlayerHalfHeight), true);
        game.Enemies[0].TakeDamage(1000000);
        step = 0; deadline = EditorApplication.timeSinceStartup + 30; lastTime = -1;
    }
    static void InspectView()
    {
        var bg = Background(); var b = bg.bounds; var camera = game.GameCamera; var p = camera.transform.position;
        float halfY = camera.orthographicSize, halfX = halfY * camera.aspect;
        Check(bg.sprite == ExpectedBackground(game.StageIndex), "Background identity changed");
        Check(p.x - halfX >= b.min.x - 0.01f && p.x + halfX <= b.max.x + 0.01f
            && p.y - halfY >= b.min.y - 0.01f && p.y + halfY <= b.max.y + 0.01f, "Camera left painted image " + p);
        var ground = game.World.Find("Ground").GetComponent<BoxCollider2D>();
        Check(Mathf.Abs(ground.bounds.max.y) < 0.001f, "Gameplay floor changed");
    }
    static void CaptureAt(float duration, string actor)
    {
        float elapsed = Time.time - began;
        float due = samples == 0 ? 0 : samples == 1 ? duration / 2 : duration - 0.12f;
        if (samples >= 3 || elapsed < due) return;
        var camera = game.GameCamera;
        var target = RenderTexture.GetTemporary(960, 540, 16);
        var previous = camera.targetTexture; var active = RenderTexture.active; var rect = camera.rect;
        var image = new Texture2D(960,540,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = target; camera.rect = new Rect(0,0,1,1); camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0,0,960,540),0,0); image.Apply();
            File.WriteAllBytes(Output + "/" + (Essence)scenario + "-" + actor + "-" + samples + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; camera.rect = rect; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image);
        }
        samples++; captured++;
    }
    static void Finish(string result)
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/result.txt", result); Debug.Log(result);
        SessionState.SetBool(Key, false); initialized = false;
        Application.runInBackground = priorRunInBackground;
        game.ReturnToTitle(); EditorApplication.isPlaying = false;
    }
}
#endif
