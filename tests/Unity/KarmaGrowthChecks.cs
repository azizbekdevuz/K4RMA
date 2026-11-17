#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;

// Exercises the real defeat -> choice -> inheritance -> Space release path.
// Deliberate attack invocation is confined to this test, never the selector.
[InitializeOnLoad]
public static class KarmaGrowthChecks
{
    const string Key = "K4RMA.GrowthChecks";
    const string Output = "output/validation/growth";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static bool started;
    static KarmaGame game;
    static int checks, route, capture;
    static string failure;
    static Keyboard keyboard;
    static bool recordVideo, playerOnly, focusOnly;
    static string recording;
    static float lastRecorded = -1;
    static int videoFrame;
    static readonly List<string> observations = new List<string>();
    static KarmaGrowthChecks() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.isPlaying) { Application.runInBackground = true; EditorApplication.isPaused = false; }
        if (EditorApplication.isPlaying && !started && Time.frameCount > 3) { started = true; Run(); }
        if (recording != null && game != null && Time.time > lastRecorded)
        {
            lastRecorded = Time.time;
            Capture(recording + "/" + (videoFrame++).ToString("D5"));
        }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        view.position = new Rect(50, 50, 960, 580); view.Show(); view.Focus();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    public static void RunVideoBatch()
    {
        SessionState.SetBool(Key + ".Video", true);
        RunBatch();
    }
    public static void RunPlayerProofBatch()
    {
        SessionState.SetBool(Key + ".PlayerOnly", true);
        RunVideoBatch();
    }
    public static void RunFocusBatch()
    {
        SessionState.SetBool(Key + ".FocusOnly", true);
        RunBatch();
    }
    public static void RunFocusVideoBatch()
    {
        SessionState.SetBool(Key + ".Video", true);
        RunFocusBatch();
    }
    public static void RunFocusInputBatch()
    {
        SessionState.SetBool(Key + ".PlayerOnly", true);
        RunFocusBatch();
    }
    static object Get(object value, string name) => value.GetType().GetField(name, F).GetValue(value);
    static object Property(object value, string name) => value.GetType().GetProperty(name, F).GetValue(value);
    static void Set(object value, string name, object state) => value.GetType().GetField(name, F).SetValue(value, state);
    static void Check(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
    static async Task Until(Func<bool> done, float timeout = 20)
    {
        double deadline = EditorApplication.timeSinceStartup + timeout;
        while (!done())
        {
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out in " + game.Phase);
            await Task.Delay(10);
        }
    }
    static async Task Frames(int count = 2)
    { int frame = Time.frameCount + count; await Until(() => Time.frameCount >= frame); }
    static async Task Space(bool held)
    {
        InputSystem.QueueStateEvent(keyboard, held ? new KeyboardState(UnityEngine.InputSystem.Key.Space) : new KeyboardState());
        await Until(() => keyboard.spaceKey.isPressed == held); await Frames();
    }
    static async Task StartCombat()
    {
        await Space(false); await Space(true);
        await Until(() => Property(game, "CurrentEncounterPrompt").ToString() == "WaitForRelease");
        Check(!game.IsCombat, "Confirmation waits for release");
        await Space(false); await Until(() => game.IsCombat);
    }
    static async Task Transition(Essence essence, int stage)
    {
        var defeated = game.Enemies[0];
        defeated.TakeDamage(10000000);
        await Until(() => game.Phase == RunPhase.Choosing);
        Vector2 player = game.Player.Position;
        if (focusOnly && route == 0 && stage == 2)
        {
            // Reproduce the reported overlap without changing production spacing.
            var fallen = (Transform)Get(game, "fallenGuardian");
            var overlap = (Vector3)Get(game, "fallenGuardianPosition"); overlap.x = player.x;
            fallen.position += Vector3.right * (player.x - fallen.position.x);
            Set(game, "fallenGuardianPosition", overlap);
        }
        var playerMaterial = game.Player.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
        game.Choose(essence); game.ConfirmChoice();
        var playerPoses = new HashSet<string>();
        game.StartCoroutine(ObservePlayerProof(player, playerPoses));
        if (recordVideo && route == 0)
        {
            recording = "player-route" + route + "-stage" + stage;
            Directory.CreateDirectory(Output + "/" + recording); videoFrame = 0; lastRecorded = -1;
        }
        // Queue the held input before rendering video frames. Capture itself can
        // change Editor focus; it must not be part of the input-delivery test.
        if (!recordVideo) await Space(true);
        Check(game.Player.Essences.Count == stage, "Exactly one existing choice is committed");
        await Until(() => game.GuardianInheriting);
        recording = null;
        Check(game.Player.GetComponentInChildren<SpriteRenderer>().sharedMaterial == playerMaterial,
            "Player demonstration restores the combat material");
        string expectedProof = essence == Essence.SwordWave ? "Personalized/Pierce" : essence == Essence.Guard ? "Personalized/Riposte" : "Personalized/AirDiagonal";
        Check(playerPoses.Contains(expectedProof) || essence == Essence.RisingSlash && playerPoses.Contains("Personalized/AirSlash"), "Player demonstrates the actual personalized move: " + expectedProof);
        var boss = game.Enemies[0];
        var growth = UnityEngine.Object.FindAnyObjectByType<KarmaGuardianGrowth>();
        Check(growth != null && growth.Stage == stage, "Growth is attached to real transition");
        Check(growth.Duration == KarmaGuardianGrowth.DurationFor(stage), "Stage-specific duration");
        Check(!game.AllowsPlayerActions && !game.CanMove, "Input locked during rehearsal");
        Check(boss.PrimaryType == game.Player.Essences.PersonalizationOrder[0], "First choice still determines type");
        Vector2 bossPosition = boss.Position;
        float fight = boss.FightSeconds, health = game.Player.Health;
        var techniques = (KarmaGuardianGrowth.Technique[])Get(growth, "techniques");
        Check(techniques.Take(stage).All(x => x.Effect != null), "Every preview uses a real existing effect");
        Check(((SpriteRenderer)Get(growth, "original")).sprite != null, "Original player effect exists");
        var reflection = (Sprite)Get(growth, "reflection");
        Check(reflection != null && reflection.texture.width >= 1024, "Reference-based reflection art loads at full resolution");
        Check(growth.GetComponentsInChildren<Collider2D>().Length == 0
            && growth.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "Rehearsal cannot hit");
        var beats = new HashSet<KarmaGuardianGrowth.Beat>();
        if (recordVideo && route < 3)
        {
            recording = "route" + route + "-stage" + stage;
            Directory.CreateDirectory(Output + "/" + recording); videoFrame = 0; lastRecorded = -1;
        }
        game.StartCoroutine(ObserveGrowth(growth, beats));
        bool captionCaptured = false;
        while (growth != null)
        {
            if (recordVideo && route < 3 && !captionCaptured && growth.CaptionAlpha > 0.9f)
            {
                ScreenCapture.CaptureScreenshot(Output + "/ui-" + route + "-" + stage + ".png");
                captionCaptured = true;
            }
            Check(boss.FightSeconds == fight && game.Player.Health == health, "No combat advances during growth");
            Check((boss.Position - bossPosition).sqrMagnitude < 0.001f, "Visual motion never moves the combat body");
            Check((game.Player.Position - player).sqrMagnitude < 0.01f, "Player stays at ceremony position");
            await Frames();
        }
        Check(beats.Contains(KarmaGuardianGrowth.Beat.Revive), "Revival continuity");
        recording = null;
        if (stage < 3)
            Check(beats.Contains(KarmaGuardianGrowth.Beat.Observe) && beats.Contains(KarmaGuardianGrowth.Beat.Imitate)
                && beats.Contains(KarmaGuardianGrowth.Beat.Adapt), "Original -> copy -> own interpretation");
        Check(beats.Contains(KarmaGuardianGrowth.Beat.Falter) == (stage == 2)
            && beats.Contains(KarmaGuardianGrowth.Beat.Inspect) == (stage == 2), "Trial and reflection only at second inheritance");
        if (stage == 3) Check(beats.Contains(KarmaGuardianGrowth.Beat.Combine)
            && beats.Contains(KarmaGuardianGrowth.Beat.Foreshadow)
            && !beats.Contains(KarmaGuardianGrowth.Beat.Imitate), "Mastery has no repeated trial");
        await Until(() => (bool)Property(game, "EncounterPromptReady"));
        Check(Property(game, "CurrentEncounterPrompt").ToString() == "BossName", "Held Space cannot skip reveal");
        var animator = boss.GetComponentInChildren<KarmaCharacterAnimator>();
        Check(!(bool)Get(animator, "growthActive"), "Animation override released before combat");
        var focus = UnityEngine.Object.FindAnyObjectByType<KarmaGrowthFocus>();
        Check(focus != null && (float)Property(focus, "PlayerOpacity") > 0.99f
            && (float)Property(focus, "GuardianOpacity") > 0.99f, "Both actors return before confirmation");
        float clock = game.Elapsed; await Frames(5);
        Check(Mathf.Abs(game.Elapsed - clock) < 0.001f, "Confirmation doesn't advance run clock");
        if (recordVideo)
        {
            // Screen capture can steal synthetic keyboard focus in the Editor.
            // Input is covered by RunBatch and the existing entrance suite; the
            // video run advances only the confirmation gate after the full scene.
            var prompt = typeof(KarmaGame).GetProperty("CurrentEncounterPrompt", F);
            prompt.SetValue(game, Enum.Parse(prompt.PropertyType, "WaitForRelease"));
            typeof(KarmaGame).GetMethod("AdvanceEncounterPrompt", F).Invoke(game, null);
        }
        else await StartCombat();
        await Frames();
        Check(UnityEngine.Object.FindAnyObjectByType<KarmaGrowthFocus>() == null, "Combat releases camera visibility owner");
        Check(!game.Player.GetComponentInChildren<SpriteRenderer>().forceRenderingOff,
            "Player rendering restored for combat");
        Check(game.StageIndex == stage, "Stage advances exactly once");
    }
    static IEnumerator ObservePlayerProof(Vector2 bodyPosition, HashSet<string> poses)
    {
        var art = game.Player.GetComponentInChildren<KarmaCharacterAnimator>();
        var picture = game.Player.GetComponentInChildren<SpriteRenderer>();
        var baseColor = (Color)Get(art, "pictureColor");
        bool captured = false;
        while (game.Phase == RunPhase.Transition && !game.GuardianInheriting)
        {
            Check((game.Player.Position - bodyPosition).sqrMagnitude < 0.01f, "Player demonstration leaves physics body in place");
            poses.Add(art.CurrentPose ?? "");
            Check(picture.color == baseColor, "Personalization preserves the player's original skin and clothing colours");
            var ceremony = UnityEngine.Object.FindAnyObjectByType<KarmaPersonalizationVFX>();
            if (ceremony != null)
            {
                float age = (float)Get(ceremony, "age");
                var completion = ceremony.GetComponentsInChildren<SpriteRenderer>()
                    .Single(p => p.name == "Personalization completion");
                Check(completion.sprite == KarmaArtwork.Sprite("PersonalizationVFX", "Awaken"),
                    "Completion uses the original awakening art");
                if (age < 5.45f) Check(completion.color.a == 0, "Awakening waits until the gesture is complete");
                if (age > 5.8f && age < 6.3f) Check(completion.color.a > 0.3f, "Original awakening effect follows the skill");
            }
            if (recordVideo && route < 3 && !captured && art.CurrentPose != null
                && (art.CurrentPose == "Personalized/Pierce" || art.CurrentPose == "Personalized/AirDiagonal" || art.CurrentPose == "Personalized/Riposte"))
            {
                ScreenCapture.CaptureScreenshot(Output + "/ui-player-" + route + "-" + game.Player.Essences.Count + ".png");
                captured = true;
            }
            if (art.CurrentPose != null && art.CurrentPose.StartsWith("Personalized/"))
            {
                Check((bool)Get(art, "personalizationProofActive") && (bool)Get(art, "presentingEnding"), "Authored player proof is active");
                var focus = UnityEngine.Object.FindAnyObjectByType<KarmaGrowthFocus>();
                Check(focus != null && (float)Property(focus, "GuardianOpacity") < 0.001f,
                    "Fallen guardian cannot overlap the player demonstration");
            }
            yield return null;
        }
    }
    // Sample each player-loop frame; an Editor task continuation can resume
    // after several frames when the GPU/importer is busy and miss a short beat.
    static IEnumerator ObserveGrowth(KarmaGuardianGrowth growth, HashSet<KarmaGuardianGrowth.Beat> beats)
    {
        int stage = growth.Stage;
        var attempts = new HashSet<int>(); var staggers = new HashSet<int>();
        bool checkedRender = false, checkedFall = false;
        while (growth != null)
        {
            if (growth.Age > 0.6f)
            {
                var focus = UnityEngine.Object.FindAnyObjectByType<KarmaGrowthFocus>();
                Check(focus != null && (float)Property(focus, "PlayerOpacity") < 0.001f,
                    "Actual player is hidden during the guardian story");
                if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Inspect)
                    Check(game.GameCamera.orthographicSize >= 10.5f, "Guardian framing retains full body and weapon");
                if (!checkedRender)
                {
                    checkedRender = true;
                    var picture = game.Player.GetComponentInChildren<SpriteRenderer>();
                    var color = picture.color; bool hidden = picture.forceRenderingOff;
                    typeof(KarmaGrowthFocus).GetMethod("BeforeCamera", F).Invoke(focus, new object[] { game.GameCamera });
                    Check(picture.forceRenderingOff, "Render callback actually hides the inactive actor");
                    typeof(KarmaGrowthFocus).GetMethod("AfterCamera", F).Invoke(focus, new object[] { game.GameCamera });
                    Check(picture.color == color && picture.forceRenderingOff == hidden,
                        "Render callback restores exact original visibility and colour");
                }
            }
            if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Imitate) attempts.Add(growth.CurrentAttempt);
            if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Stagger) staggers.Add(growth.CurrentAttempt);
            if (stage == 2 && growth.CurrentBeat == KarmaGuardianGrowth.Beat.Falter && !checkedFall)
            {
                var animator = game.Enemies[0].GetComponentInChildren<KarmaCharacterAnimator>();
                var frames = (Sprite[])Get(animator, "beforeRevivalFrames");
                if (game.Enemies[0].GetComponentInChildren<SpriteRenderer>().sprite == frames[0]) checkedFall = true;
            }
            if (beats.Add(growth.CurrentBeat))
            {
                observations.Add(route + "/" + growth.Stage + ": " + growth.CurrentBeat);
                if (route < 3) Capture(route + "-" + growth.Stage + "-" + growth.CurrentBeat);
            }
            yield return null;
        }
        if (stage == 2)
        {
            Check(attempts.SetEquals(new[] { 1, 2, 3 }), "Second inheritance makes three visible attempts");
            Check(staggers.SetEquals(new[] { 1, 2 }) && checkedFall,
                "First two attempts wobble; only the third falls before reflection");
        }
    }
    static async Task Ending()
    {
        // A fresh room restores the boss after the explicit ultimate cancel test.
        typeof(KarmaGame).GetMethod("LoadRoom", F).Invoke(game, new object[] { true });
        game.Enemies[0].TakeDamage(10000000);
        await Until(() => game.Phase == RunPhase.Ending);
        if (recordVideo) { recording = "ending"; Directory.CreateDirectory(Output + "/" + recording); videoFrame = 0; lastRecorded = -1; }
        await Until(() => game.EndingTime >= 1.6f);
        if (recordVideo) ScreenCapture.CaptureScreenshot(Output + "/ui-ending-reflect.png");
        Check(game.EndingSpeaker == "마지막 수호자" && game.EndingLine.Contains("내 것으로"), "Guardian acknowledges the player");
        var art = (Transform)Get(game, "fallenGuardian");
        Check(art != null && art.GetComponent<KarmaCharacterAnimator>().CurrentPose == "Ending/Reflect", "Guardian reflects on the mastered technique");
        var picture = art.GetComponentInChildren<SpriteRenderer>();
        var block = new MaterialPropertyBlock(); picture.GetPropertyBlock(block);
        Check(block.GetFloat("_Progress") == 0, "Acknowledgment precedes dissolve");
        float at = game.EndingTime; game.TogglePause(); await Frames(5);
        Check(game.EndingTime == at, "Ending pause holds dialogue and gesture"); game.TogglePause();
        await Until(() => game.EndingTime >= 3.2f);
        if (recordVideo) ScreenCapture.CaptureScreenshot(Output + "/ui-ending-acknowledge.png");
        Check(game.EndingLine.Contains("넘지 못했구나")
            && art.GetComponent<KarmaCharacterAnimator>().CurrentPose == "Ending/Acknowledge", "Guardian raises gaze and accepts defeat");
        await Until(() => game.EndingTime >= 4.2f);
        picture.GetPropertyBlock(block); Check(block.GetFloat("_Progress") > 0 && block.GetFloat("_Progress") < 1, "Existing dissolve follows recognition");
        await Until(() => game.EndingTime >= 5.6f);
        Check(art == null, "Guardian disappears at existing endpoint");
        recording = null;
        typeof(KarmaGame).GetMethod("SkipEnding", F).Invoke(game, null);
        await Until(() => game.Phase == RunPhase.Victory);
        Check(game.EndingCompleted, "Ending skip still completes cleanly");
    }
    static async Task Defeats()
    {
        var types = new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard };
        for (int stage = 0; stage <= 3; stage++)
        for (int type = 0; type < (stage == 0 ? 1 : 3); type++)
        {
            game.StartRun();
            var order = new List<Essence> { types[type] };
            foreach (var essence in types) if (essence != types[type]) order.Add(essence);
            for (int i = 0; i < stage; i++) { game.Player.Essences.Preview(order[i]); game.Player.Essences.Confirm(out _); }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, stage);
            typeof(KarmaGame).GetMethod("LoadRoom", F).Invoke(game, new object[] { true });
            var boss = game.Enemies[0]; float fight = boss.FightSeconds;
            Set(game.Player, "invulnerableUntil", 0f);
            game.Player.TakeDamage(1000000);
            Check(game.Phase == RunPhase.PlayerFallen, "Fatal hit enters defeat scene before results");
            if (recordVideo && stage == 2 && type == 0)
            {
                recording = "player-defeat"; Directory.CreateDirectory(Output + "/" + recording); videoFrame = 0; lastRecorded = -1;
            }
            await Until(() => (float)Get(game, "playerFallAge") >= 2f);
            Check(game.Phase == RunPhase.PlayerFallen && !game.AllowsPlayerActions, "Death gesture and speech precede results");
            Check(boss.FightSeconds == fight, "Defeat scene cannot resume attacks");
            Check((float)Property(game, "DefeatLineAlpha") > 0 && !string.IsNullOrEmpty((string)Property(game, "DefeatLine")), "Stage and owner defeat line is visible");
            Check(boss.GetComponentInChildren<KarmaCharacterAnimator>().CurrentPose.StartsWith("Victory/"), "Winning boss settles its weapon");
            if (recordVideo) ScreenCapture.CaptureScreenshot(Output + "/ui-defeat-" + stage + "-" + type + ".png");
            await Until(() => game.Phase == RunPhase.Defeat);
            recording = null;
            Check((float)Get(game, "playerFallAge") >= 4.5f, "Results wait for the full losing scene");
            game.RetryRoom(); await Frames();
            Check(game.IsCombat && game.StageIndex == 0 && game.Player.Essences.Count == 0, "Defeat retry retains the existing full restart rule");
            game.ReturnToTitle(); await Frames();
        }
    }
    static async Task Ultimate()
    {
        var boss = game.Enemies[0];
        var behavior = Get(boss, "behavior");
        var attacks = (List<EnemyAttack>)Get(boss, "attacks");
        var method = behavior.GetType().GetMethod("IsUltimate", F);
        var attack = attacks.Single(x => (bool)method.Invoke(behavior, new object[] { x }));
        Set(boss, "selected", attack); Set(boss, "running", true); Set(boss, "patternStarted", boss.FightSeconds);
        boss.StartCoroutine((IEnumerator)typeof(KarmaEnemy).GetMethod("RunPattern", F).Invoke(boss, null));
        await Frames();
        var recall = boss.GetComponentInChildren<KarmaGuardianGrowth>();
        Check(recall != null && (bool)Get(recall, "recall"), "Actual ultimate reuses learned stances");
        Check(recall.GetComponentsInChildren<Collider2D>().Length == 0, "Recall has no hitbox");
        if (route < 3) Capture(route + "-ultimate");
        game.TogglePause(); float age = recall.Age; await Frames(6);
        Check(recall != null && recall.Age == age, "Pause freezes recall");
        game.TogglePause();
        boss.CancelCombat(); await Frames(3);
        Check(boss.GetComponentInChildren<KarmaGuardianGrowth>() == null, "Combat cancellation clears recall");
    }
    static void Capture(string label)
    {
        var camera = game.GameCamera;
        var target = RenderTexture.GetTemporary(960, 540, 16);
        var old = camera.targetTexture; var active = RenderTexture.active; var rect = camera.rect;
        var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/" + label + ".png", image.EncodeToPNG()); capture++;
        }
        finally
        {
            camera.targetTexture = old; camera.rect = rect; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(image);
        }
    }
    static async void Run()
    {
        int code = 0; string verdict;
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        Application.LogCallback log = (m, s, t) => { if (t == LogType.Exception || t == LogType.Error) failure = m; };
        try
        {
            // Keep synthetic keyboard events in the Game View even when a test
            // capture or the user's other Editor window changes desktop focus.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Directory.CreateDirectory(Output); keyboard = InputSystem.AddDevice<Keyboard>();
            playerOnly = SessionState.GetBool(Key + ".PlayerOnly", false);
            SessionState.EraseBool(Key + ".PlayerOnly");
            focusOnly = SessionState.GetBool(Key + ".FocusOnly", false);
            SessionState.EraseBool(Key + ".FocusOnly");
            recordVideo = SessionState.GetBool(Key + ".Video", false);
            SessionState.EraseBool(Key + ".Video");
            Application.logMessageReceived += log; Application.runInBackground = true;
            Time.captureDeltaTime = 1f / 24;
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            var routes = new[] {
                new[] { Essence.SwordWave, Essence.Guard, Essence.RisingSlash },
                new[] { Essence.RisingSlash, Essence.SwordWave, Essence.Guard },
                new[] { Essence.Guard, Essence.RisingSlash, Essence.SwordWave },
                new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard },
                new[] { Essence.RisingSlash, Essence.Guard, Essence.SwordWave },
                new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash } };
            for (route = 0; route < (playerOnly ? 1 : focusOnly ? 3 : routes.Length); route++)
            {
                game.StartRun(); game.Player.ToggleTestInvincibility();
                for (int stage = 1; stage <= 3; stage++) await Transition(routes[route][stage - 1], stage);
                if (!focusOnly) { await Ultimate(); if (route == 2) await Ending(); }
                game.ReturnToTitle(); await Frames();
                Check(UnityEngine.Object.FindObjectsByType<KarmaGuardianGrowth>(FindObjectsSortMode.None).Length == 0, "Title cleans all growth objects");
            }
            if (!playerOnly && !focusOnly) await Defeats();
            // Abort a real second inheritance during the failure beat, then restart.
            game.StartRun(); game.Player.ToggleTestInvincibility();
            await Transition(Essence.Guard, 1);
            game.Enemies[0].TakeDamage(10000000); await Until(() => game.Phase == RunPhase.Choosing);
            game.Choose(Essence.SwordWave); game.ConfirmChoice();
            await Until(() => game.GuardianInheriting);
            var interrupted = UnityEngine.Object.FindAnyObjectByType<KarmaGuardianGrowth>();
            await Until(() => interrupted.CurrentBeat == KarmaGuardianGrowth.Beat.Falter);
            game.ReturnToTitle(); await Frames();
            Check(UnityEngine.Object.FindAnyObjectByType<KarmaGuardianGrowth>() == null, "Mid-scene title return clears ownership");
            Check(UnityEngine.Object.FindAnyObjectByType<KarmaGrowthFocus>() == null, "Mid-scene title restores render scope");
            game.StartRun(); await Frames();
            Check(game.IsCombat && game.Player.Essences.Count == 0, "Restart restores combat and original skills");
            game.ReturnToTitle();
            Check(failure == null, "No runtime errors");
            verdict = "PASS: " + checks + (focusOnly ? (playerOnly ? " checks; one route / three growth scenes; " : " checks; three boss types / nine growth scenes; ")
                    + "overlapping actors; focus / pacing / render restoration; "
                : playerOnly ? " checks; focused authored player demonstrations / material restoration; "
                : " checks; six routes / 18 inheritance transitions; six ultimate activations; player personalized demonstrations; ten defeat scenes; recognition/dissolve/ending skip; ")
                + (recordVideo ? "video capture; " : "input; ")
                + (focusOnly ? "cancellation, restart; " : "pause, cancellation, restart; ") + capture + " captures";
        }
        catch (Exception e) { code = 1; verdict = "FAIL: " + e; Debug.LogException(e); }
        Time.captureDeltaTime = 0; if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.editorInputBehaviorInPlayMode = routing;
        InputSystem.settings.backgroundBehavior = background;
        Application.logMessageReceived -= log; SessionState.SetBool(Key, false);
        File.WriteAllText(Output + "/result.txt", verdict + "\n" + string.Join("\n", observations));
        Debug.Log(verdict); EditorApplication.Exit(code);
    }
}
#endif
