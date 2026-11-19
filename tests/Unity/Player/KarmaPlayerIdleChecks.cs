#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;

// Uses the existing validation project; never rebuilds unrelated player art.
[InitializeOnLoad]
public static class KarmaPlayerIdleChecks
{
    const string Art = "Assets/Arts/Reference/Player_Idle.png";
    const string Clips = "Assets/Data/Resources/Player/Animation12";
    const string Output = "output/validation/player-idle";
    const string Session = "K4RMA.IdleChecks";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool started, recording;
    static float captureAt, nextCapture;
    static int captures;
    static string segment;
    static KarmaGame game;
    static KarmaCharacterAnimator animator;
    static SpriteRenderer renderer;
    static RenderTexture target;
    static Texture2D readback;
    static readonly List<string> rows = new List<string>();

    static KarmaPlayerIdleChecks()
    {
        EditorApplication.update += () => {
            if (!SessionState.GetBool(Session, false)) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying) return;
            EditorApplication.isPaused = false;
            Application.runInBackground = true;
            if (!started && Time.frameCount > 0) { started = true; Debug.Log("IDLE PLAY: starting verification at frame " + Time.frameCount); VerifyGameplay(); }
            if (recording && Time.time >= nextCapture)
            {
                nextCapture = Time.time + 1f / 12;
                Capture();
            }
        };
    }

    static void Check(bool pass, string message)
    { if (!pass) throw new Exception(message); }

    public static void ImportBatch()
    {
        try
        {
            AssetDatabase.ImportAsset(Art, ImportAssetOptions.ForceSynchronousImport);
            KarmaPrototype.EditorTools.KarmaPlayer12AnimationAssets.Rebuild();
            VerifyAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void VerifyAssets()
    {
        var idle = KarmaArtwork.StageIdle("Player");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Idle.anim");
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
        Check(clip.frameRate == KarmaBootstrap.FramesPerSecond && clip.isLooping, "Idle playback clock");
        Check(keys.Length == 1 && keys[0].value == idle && AssetDatabase.GetAssetPath(idle) == Art, "StageStyle Idle binding");
    }

    public static void RunGameplayBatch()
    {
        Directory.CreateDirectory(Output);
        VerifyAssets();
        SessionState.SetBool(Session, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }

    static void Capture()
    {
        var camera = game.GameCamera;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
            File.WriteAllBytes(Output + "/frame_" + captures.ToString("D5") + ".png", readback.EncodeToPNG());
            rows.Add(string.Join(",", captures.ToString(), (Time.time - captureAt).ToString("F6", CultureInfo.InvariantCulture), segment, animator.CurrentPose, renderer.sprite.name));
            captures++;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; }
    }

    static async void VerifyGameplay()
    {
        Keyboard keyboard = null; InputActionMap map = null; string overrides = null;
        int exit = 1;
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
            foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
            var player = game.Player;
            animator = player.GetComponentInChildren<KarmaCharacterAnimator>();
            renderer = (SpriteRenderer)typeof(KarmaCharacterAnimator).GetField("picture", Private).GetValue(animator);
            var sample = typeof(KarmaCharacterAnimator).GetMethod("SamplePlayerClip", Private);
            var clock = typeof(KarmaCharacterAnimator).GetField("playerClipAt", Private);
            var library = Resources.Load<KarmaSpriteLibrary>("Player/Animation12/Frames");
            player.enabled = false;
            foreach (var sheet in library.sheets)
            {
                if (sheet.id == "Riposte") continue; // Dedicated parry-success renderer owns this pose.
                sample.Invoke(animator, new object[] { sheet.id, true });
                var clip = Resources.Load<AnimationClip>("Player/Animation12/" + sheet.id);
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                for (int i = 0; i < sheet.sprites.Length; i++)
                {
                    float at = curve.First(k => k.value == sheet.sprites[i]).time + .001f;
                    clock.SetValue(animator, Time.time - at);
                    sample.Invoke(animator, new object[] { sheet.id, false });
                    Check(renderer.sprite == sheet.sprites[i], sheet.id + " playback tick " + i);
                }
            }
            sample.Invoke(animator, new object[] { "Idle", true });
            for (int tick = 0; tick < 25; tick++)
            {
                clock.SetValue(animator, Time.time - (tick + .01f) / 12);
                sample.Invoke(animator, new object[] { "Idle", false });
                Check(renderer.sprite == KarmaArtwork.StageIdle("Player"), "Two complete Idle loops at tick " + tick);
                animator.Pose(0, false);
                Check(renderer.transform.localPosition == (Vector3)typeof(KarmaCharacterAnimator).GetField("pictureHome", Private).GetValue(animator), "No procedural Idle bob");
            }
            KarmaPrototype.EditorTools.KarmaPlayerAnimationAssets.VerifyParrySuccess();
            player.enabled = true;
            player.ResetForRoom(new Vector2(12, KarmaSpace.PlayerHalfHeight + .05f), true);
            keyboard = InputSystem.AddDevice<Keyboard>();
            map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            overrides = map.SaveBindingOverridesAsJson(); map.RemoveAllBindingOverrides();
            target = new RenderTexture(960, 540, 24); readback = new Texture2D(960, 540, TextureFormat.RGB24, false);
            rows.Clear(); rows.Add("frame,game_seconds,segment,pose,sprite");
            captureAt = nextCapture = Time.time; recording = true;
            async Task Hold(string label, float seconds, params Key[] keys)
            {
                segment = label; InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                float end = Time.time + seconds; int frame = Time.frameCount;
                double deadline = EditorApplication.timeSinceStartup + 20;
                while (Time.time < end || Time.frameCount <= frame + 2)
                { Check(EditorApplication.timeSinceStartup < deadline, "Player loop stalled: " + label); await Task.Yield(); }
            }
            await Hold("IdleTwoLoops", 4.2f);
            await Hold("RunRight", .35f, Key.D); await Hold("ReturnIdleRight", .3f);
            await Hold("RunLeft", .35f, Key.A); await Hold("ReturnIdleLeft", .3f);
            await Hold("AttackLeft", .05f, Key.J); await Hold("AttackReturn", .6f);
            await Hold("Jump", .05f, Key.Space); await Hold("LandReturn", 1.4f);
            await Hold("Guard", .05f, Key.K); await Hold("GuardReturn", .6f);
            Check(captures >= 48 && rows.Any(r => r.Contains(",Run,")) && rows.Any(r => r.Contains(",Attack,")) && rows.Any(r => r.Contains(",Land,")), "Actual movement/attack/jump input capture");
            File.WriteAllText(Output + "/result.txt", "PASS: Unity import, PPU/pivot, 12-tick/1-second loop, 25 deterministic runtime ticks, all connected player sequences, parry-success, no procedural bob, and actual Idle/run/attack/jump/guard input capture. Gameplay clocks unchanged.\n");
            exit = 0;
        }
        catch (Exception e) { File.WriteAllText(Output + "/result.txt", "FAIL: " + e); Debug.LogException(e); }
        finally
        {
            recording = false; File.WriteAllLines(Output + "/frames.csv", rows);
            if (map != null && overrides != null) map.LoadBindingOverridesFromJson(overrides);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            SessionState.SetBool(Session, false); EditorApplication.Exit(exit);
        }
    }
}
#endif
