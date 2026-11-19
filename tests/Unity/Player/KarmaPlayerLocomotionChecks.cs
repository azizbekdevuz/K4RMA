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

[InitializeOnLoad]
public static class KarmaPlayerLocomotionChecks
{
    const string Output = "output/validation/player-locomotion";
    const string Session = "K4RMA.LocomotionChecks";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool started, recording;
    static float nextCapture, captureAt;
    static int captures;
    static string segment;
    static KarmaGame game;
    static KarmaPlayer player;
    static KarmaCharacterAnimator animator;
    static SpriteRenderer picture;
    static RenderTexture target;
    static Texture2D readback;
    static readonly List<string> rows = new List<string>();
    static readonly HashSet<string> observed = new HashSet<string>();
    static KarmaPlayerLocomotionChecks()
    {
        EditorApplication.update += () => {
            if (!SessionState.GetBool(Session, false)) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying) return;
            EditorApplication.isPaused = false; Application.runInBackground = true;
            if (!started && Time.frameCount > 0) { started = true; VerifyGameplay(); }
            if (!recording) return;
            observed.Add(segment + "/" + animator.CurrentPose + "/" + picture.sprite.name);
            if (Time.time < nextCapture) return;
            nextCapture = Time.time + 1f / 12; Capture();
        };
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void ImportBatch()
    {
        try
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-locomotionSheets");
            Check(at >= 0 && at + 1 < args.Length, "Pass -locomotionSheets <folder>");
            KarmaPrototype.EditorTools.KarmaPlayerAnimationAssets.ImportLocomotion(args[at + 1]);
            VerifyAssets();
            Debug.Log("LOCOMOTION IMPORT PASS: four sheets, exact grid/PPU/pivot, seven existing pose keys; Idle untouched.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void VerifyAssets()
    {
        foreach (string folder in new[] { "Player/Animation", "Player/Animation12" })
        {
            var library = Resources.Load<KarmaSpriteLibrary>(folder + "/Frames");
            foreach (string name in new[] { "Run", "JumpStart", "Jump", "Apex", "Fall", "Land", "Crouch" })
            {
                var sequence = library.All(name);
                if (sequence.Length == 0 && folder == "Player/Animation") continue;
                var motion = KarmaPlayerTimeline.Find(name);
                var clip = Resources.Load<AnimationClip>(folder + "/" + name);
                Check(sequence.SequenceEqual(motion.Load()), folder + "/" + name + " drawings");
                Check(clip.frameRate == KarmaBootstrap.FramesPerSecond &&
                    Mathf.Abs(AnimationUtility.GetAnimationClipSettings(clip).stopTime - motion.Duration) < .00001f, name + " duration");
                Check(clip.isLooping == motion.Loop, name + " loop");
            }
        }
    }
    public static void RunBatch()
    {
        Directory.CreateDirectory(Output); VerifyAssets();
        SessionState.SetBool(Session, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void VerifySelector()
    {
        var library = Resources.Load<KarmaSpriteLibrary>("Player/Animation/Frames");
        var probe = new GameObject("Locomotion selector probe", typeof(SpriteRenderer));
        try
        {
            var renderer = probe.GetComponent<SpriteRenderer>();
            var a = probe.AddComponent<KarmaCharacterAnimator>(); a.ConfigureSprites(renderer, new[] { library.All("Idle")[0] }); a.ConfigureSheet("Player");
            void Field(string name, float value) => typeof(KarmaCharacterAnimator).GetField(name, Private).SetValue(a, value);
            void At(float seconds) => Field("playerClipAt", Time.time - seconds);
            void Expect(string pose, string sprite) => Check(a.CurrentPose == pose && renderer.sprite.name == sprite, "Expected " + pose + "/" + sprite + ", got " + a.CurrentPose + "/" + renderer.sprite.name);
            a.Pose(1, false); At(.502f); a.Pose(1, false); Expect("Run", "Run_06");
            At(.543f); a.Pose(1, false); Expect("Run", "Run_00");
            a.Pose(0, true, 8); Expect("Jump", "Jump_00");
            At(.17f); a.Pose(0, true, 5); Expect("Jump", "Jump_02");
            At(.251f); a.Pose(0, true, 4); Expect("Jump", "Jump_03");
            At(.334f); a.Pose(0, true, 3); Expect("Jump", "Jump_04");
            a.Pose(0, true, 0); Expect("Apex", "Apex_00"); At(1); a.Pose(0, true, 0); Expect("Apex", "Apex_00");
            a.Pose(0, true, -4); Expect("Fall", "Fall_00"); At(.251f); a.Pose(0, true, -5); Expect("Fall", "Fall_03");
            a.Pose(1, false); Expect("Land", "Land_00");
            At(.049f); a.Pose(1, false); Expect("Land", "Land_00"); At(.085f); a.Pose(1, false); Expect("Land", "Land_01");
            Field("landedUntil", Time.time - .001f); a.Pose(0, false); Check(a.CurrentPose == "Idle", "Land to preserved Idle");
            a.Pose(0, false, 0, true); Expect("Crouch", "Crouch_00"); At(.043f); a.Pose(0, false, 0, true); Expect("Crouch", "Crouch_00");
            At(.126f); a.Pose(0, false, 0, true); Expect("Crouch", "Crouch_01"); At(2); a.Pose(0, false, 0, true); Expect("Crouch", "Crouch_01");
            a.Pose(0, false); Check(a.CurrentPose == "Idle", "Crouch release restores Idle immediately");
            a.Attack(); a.Pose(0, false, 0, true); Check(a.CurrentPose == "Attack", "Attack wins over crouch");
            a.Hurt(); a.Pose(1, true, 4); Check(a.CurrentPose == "AirKnockback", "Hurt wins over attack and jump");
            a.Die(); a.Pose(1, false, 0, true); Check(a.CurrentPose == "Death", "Death remains highest priority");
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); }
    }
    static void Capture()
    {
        var camera = game.GameCamera; var previous = camera.targetTexture; var active = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply();
            File.WriteAllBytes(Output + "/frame_" + captures.ToString("D5") + ".png", readback.EncodeToPNG());
            rows.Add(string.Join(",", captures.ToString(), (Time.time - captureAt).ToString("F6", CultureInfo.InvariantCulture), segment, animator.CurrentPose, picture.sprite.name)); captures++;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; }
    }
    static async void VerifyGameplay()
    {
        Keyboard keyboard = null; InputActionMap map = null; string overrides = null; int exit = 1;
        float previousCaptureDelta = Time.captureDeltaTime;
        try
        {
            Time.captureDeltaTime = 1f / 60; // Validation-only: observe both 0.05s landing drawings even on a slow Editor.
            VerifySelector(); KarmaPrototype.EditorTools.KarmaPlayerAnimationAssets.Validate();
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
            foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
            player = game.Player; animator = player.GetComponentInChildren<KarmaCharacterAnimator>();
            picture = (SpriteRenderer)typeof(KarmaCharacterAnimator).GetField("picture", Private).GetValue(animator);
            player.ResetForRoom(new Vector2(12, KarmaSpace.PlayerHalfHeight + .05f), true);
            keyboard = InputSystem.AddDevice<Keyboard>();
            map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            overrides = map.SaveBindingOverridesAsJson(); map.RemoveAllBindingOverrides();
            target = new RenderTexture(960, 540, 24); readback = new Texture2D(960, 540, TextureFormat.RGB24, false);
            rows.Clear(); rows.Add("frame,game_seconds,segment,pose,sprite"); observed.Clear();
            captureAt = nextCapture = Time.time; recording = true;
            async Task Hold(string label, float seconds, params Key[] keys)
            {
                segment = label; InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                float end = Time.time + seconds; int frame = Time.frameCount; double deadline = EditorApplication.timeSinceStartup + 30;
                while (Time.time < end || Time.frameCount <= frame + 2)
                { Check(EditorApplication.timeSinceStartup < deadline, "Play Mode stalled: " + label); await Task.Yield(); }
            }
            await Hold("Idle", .35f);
            await Hold("RunRight", .65f, Key.D); Check(animator.CurrentPose == "Run", "Actual right movement");
            await Hold("StopRight", .2f); Check(animator.CurrentPose == "Idle", "Run release to Idle");
            await Hold("RunLeft", .65f, Key.A); await Hold("StopLeft", .2f); Check(animator.CurrentPose == "Idle", "Left movement to Idle");
            await Hold("Crouch", .4f, Key.S); Check(player.Crouching && picture.sprite.name == "Crouch_01", "Actual crouch held on final drawing");
            await Hold("CrouchRelease", .15f); Check(!player.Crouching && animator.CurrentPose == "Idle", "Actual crouch release");
            await Hold("JumpPress", .05f, Key.Space); await Hold("JumpAndLand", 1.5f);
            Check(!player.Airborne && animator.CurrentPose == "Idle", "Jump and landing return to Idle");
            await Hold("MovingJumpPress", .05f, Key.D, Key.Space); await Hold("MovingJumpAndLand", 1.5f, Key.D);
            await Hold("FinalIdle", .3f); Check(animator.CurrentPose == "Idle", "Moving jump returns to Idle");
            Check(observed.Any(s => s.Contains("/Jump/Jump_")) && observed.Any(s => s.Contains("/Apex/Apex_00")) && observed.Any(s => s.Contains("/Fall/Fall_")), "Actual jump ascent/apex/descent");
            Check(observed.Any(s => s.Contains("JumpAndLand/Land/Land_00")) && observed.Any(s => s.Contains("JumpAndLand/Land/Land_01")), "Actual landing displays both drawings, including moving landing");
            Check(observed.Any(s => s.StartsWith("MovingJumpAndLand/Land/")), "Landing is visible while movement continues");
            File.WriteAllText(Output + "/result.txt", "PASS: exact import settings and phase mapping; Run 7 frames at 12 FPS, Jump velocity phases, Land 0.10s, Crouch 0.125s/hold/release; attack/hurt/death priorities; actual Play Mode movement both directions, stationary/moving jump, both landing drawings and return to preserved Idle. Validation capture uses a 1/60s simulation step only in the test.\n");
            exit = 0;
        }
        catch (Exception e) { File.WriteAllText(Output + "/result.txt", "FAIL: " + e); Debug.LogException(e); }
        finally
        {
            recording = false; Time.captureDeltaTime = previousCaptureDelta;
            File.WriteAllLines(Output + "/frames.csv", rows); File.WriteAllLines(Output + "/observed.txt", observed.OrderBy(s => s));
            if (map != null && overrides != null) map.LoadBindingOverridesFromJson(overrides);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            SessionState.SetBool(Session, false); EditorApplication.Exit(exit);
        }
    }
}
#endif
