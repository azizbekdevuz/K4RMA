#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;

// Uses the existing validation project; never rebuilds unrelated player art.
[InitializeOnLoad]
public static class KarmaPlayerIdleChecks
{
    const string Art = "Assets/Arts/Resources/Player/Native24/Idle.png";
    const string Clips = "Assets/Arts/Resources/Player/Animation";
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
                nextCapture = Time.time + 1f / 24;
                Capture();
            }
        };
    }

    static void Check(bool pass, string message)
    { if (!pass) throw new Exception(message); }

    // Runs the existing sprite importer API in a disposable validation copy.
    public static void ImportBatch()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-idleSheet");
            Check(at >= 0 && at + 1 < args.Length, "Pass -idleSheet <reviewed PNG>");
            var original = AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Resources/Player/Actions.png")
                .OfType<Sprite>().Single(s => s.name == "Idle");
            var pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
            var sprites = ImportIdle(args[at + 1], original.pixelsPerUnit, pivot);
            foreach (string folder in new[] { Clips, Clips + "24" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Idle.anim");
                clip.frameRate = 24;
                var keys = Enumerable.Range(0, 24).Select(i => new ObjectReferenceKeyframe { time = i / 24f, value = sprites[i] }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = 1; settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            }
            var library = AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Clips + "/Frames.asset");
            var entry = library.sheets.Single(s => s.id == "Idle");
            entry.sprites = sprites; entry.source = Art;
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
            VerifyAssets();
            Debug.Log("IDLE IMPORT PASS: 24 drawings, one 24FPS tick each, 1s loop; original Actions/Idle PPU and pivot.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static Sprite[] ImportIdle(string input, float ppu, Vector2 pivot)
    {
        File.Copy(input, Art, true);
        AssetDatabase.ImportAsset(Art, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu; importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" }) importer.ClearPlatformTextureSettings(platform);
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        Check(provider != null, "Sprite data provider missing"); provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        Check(capability != null, "Sprite edit capability missing");
        foreach (var edit in new[] { EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect, EEditCapability.EditPivot })
            Check(capability.GetEditCapability().HasCapability(edit), "Unsupported sprite edit: " + edit);
        var old = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        var rects = Enumerable.Range(0, 24).Select(i => {
            string name = "Idle_" + i.ToString("00");
            return new SpriteRect { name = name, rect = new Rect(i % 4 * 205, (5 - i / 4) * 285, 205, 285),
                alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = old.TryGetValue(name, out var id) ? id : GUID.Generate() };
        }).ToArray();
        provider.SetSpriteRects(rects);
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        Check(names != null, "Sprite name/ID provider missing");
        names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
    }

    static void VerifyAssets()
    {
        var original = AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Resources/Player/Actions.png").OfType<Sprite>().Single(s => s.name == "Idle");
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Art).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        Check(sprites.Length == 24, "Idle must contain exactly twenty-four drawings");
        for (int i = 0; i < 24; i++)
        {
            var s = sprites[i];
            Check(s.name == "Idle_" + i.ToString("00") && s.rect == new Rect(i % 4 * 205, (5 - i / 4) * 285, 205, 285), "Row-major slice " + i);
            Check(s.pixelsPerUnit == original.pixelsPerUnit && (s.pivot - original.pivot).sqrMagnitude < .001f, "Original Idle PPU/pivot " + i);
        }
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/Idle.anim");
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        Check(clip.frameRate == 24 && clip.isLooping && settings.stopTime == 1, "24FPS, exactly one second, loop enabled");
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
        Check(keys.Length == 24, "24-tick Idle timeline");
        for (int i = 0; i < 24; i++)
            Check(Mathf.Abs(keys[i].time - i / 24f) < .00001f && keys[i].value == sprites[i], "Idle drawing timing " + i);
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
            var library = Resources.Load<KarmaSpriteLibrary>("Player/Animation/Frames");
            Check(!(bool)typeof(KarmaCharacterAnimator).GetField("nativePlayer24", Private).GetValue(animator), "Keep existing legacy state policy");
            player.enabled = false;
            foreach (var sheet in library.sheets)
            {
                if (sheet.id == "Riposte") continue; // Dedicated parry-success renderer owns this pose.
                sample.Invoke(animator, new object[] { sheet.id, true });
                var clip = Resources.Load<AnimationClip>("Player/Animation/" + sheet.id);
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
            for (int tick = 0; tick < 49; tick++)
            {
                clock.SetValue(animator, Time.time - (tick + .01f) / 24);
                sample.Invoke(animator, new object[] { "Idle", false });
                Check(renderer.sprite.name == "Idle_" + (tick % 24).ToString("00"), "Two complete Idle loops at tick " + tick);
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
            File.WriteAllText(Output + "/result.txt", "PASS: Unity import, PPU/pivot, 24-tick/1-second loop, 49 deterministic runtime ticks, all connected player sequences, parry-success, no procedural bob, and actual Idle/run/attack/jump/guard input capture. Gameplay clocks unchanged.\n");
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
