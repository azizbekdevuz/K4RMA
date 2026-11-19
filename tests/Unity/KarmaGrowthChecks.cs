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
    static async Task PauseRehearsal(Func<float> readAge, SpriteRenderer picture)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
        await Until(() => game.Paused);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Frames();
        float age = readAge(), remaining = (float)Get(game, "transitionLeft");
        var sprite = picture.sprite;
        var camera = game.GameCamera.transform.position;
        float size = game.GameCamera.orthographicSize;
        await Space(true); await Space(false);
        Check(game.Paused && readAge() == age && (float)Get(game, "transitionLeft") == remaining,
            "Esc freezes rehearsal and confirmation input cannot advance it");
        Check(picture.sprite == sprite && camera == game.GameCamera.transform.position && size == game.GameCamera.orthographicSize,
            "The paused pose and camera remain still");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape));
        await Until(() => !game.Paused);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Until(() => readAge() > age);
    }
    static float HudOpacity => (float)Property(game.GetComponent<KarmaHUD>(), "CombatHudOpacity");
    static bool Framed(float x, float margin)
    {
        float u = game.GameCamera.WorldToViewportPoint(new Vector3(x, 2, 0)).x;
        return u >= margin && u <= 1 - margin;
    }

    static async Task PlaceEdgeKill(bool left, bool ranged)
    {
        float wall = left ? KarmaStage.BossLeftWall : KarmaStage.BossRightWall;
        float player = ranged ? KarmaStage.ArenaCenter * 2 - wall : wall + (left ? 3 : -3);
        game.Player.ResetForRoom(new Vector2(player, KarmaSpace.PlayerHalfHeight), false);
        await Frames(20); // Start from the ordinary combat camera at the player's position.
        game.Enemies[0].transform.position = new Vector3(wall, KarmaSpace.GuardianGroundY, 0);
    }

    static async Task Transition(Essence essence, int stage)
    {
        var defeated = game.Enemies[0];
        bool edge = focusOnly && stage == 1 && route < 2;
        if (edge) await PlaceEdgeKill(route == 0, route == 1);
        float fallenX = defeated.Position.x;
        defeated.TakeDamage(10000000);
        if (edge)
        {
            await Until(() => HudOpacity > 0.12f && HudOpacity < 0.88f);
            await Until(() => HudOpacity == 0);
            Check(Framed(fallenX, 0.16f), "The fallen guardian is framed before skill selection");
        }
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
        if (!recordVideo)
        {
            if (route == 0 && stage == 1)
            {
                var ceremony = UnityEngine.Object.FindAnyObjectByType<KarmaPersonalizationVFX>();
                await Until(() => (float)Get(ceremony, "age") > KarmaPersonalizationVFX.CeremonyStart + 0.25f);
                await PauseRehearsal(() => (float)Get(ceremony, "age"), game.Player.GetComponentInChildren<SpriteRenderer>());
            }
            await Space(true);
        }
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
        var reflection = (Sprite)Get(growth, "inspectionPose");
        if (stage == 2) Check(reflection != null && reflection.texture.width >= 1024, "Second inheritance loads the guardian's weapon inspection");
        if (stage == 1) Check(KarmaArtResource.Frames("Motions12/Neutral/GrowthHesitate").Length == 3, "Neutral reaching sequence");
        Check(growth.GetComponentsInChildren<Collider2D>().Length == 0
            && growth.GetComponentsInChildren<KarmaBossAttack>().Length == 0, "Rehearsal cannot hit");
        var beats = new HashSet<KarmaGuardianGrowth.Beat>();
        if (recordVideo && route < 3)
        {
            recording = "route" + route + "-stage" + stage;
            Directory.CreateDirectory(Output + "/" + recording); videoFrame = 0; lastRecorded = -1;
        }
        game.StartCoroutine(ObserveGrowth(growth, beats));
        if (!recordVideo && route == 0 && stage == 1)
        {
            await Until(() => Get(growth, "formMaterial") is Material m && m.GetFloat("_FormAmount") > 0.2f);
            var reveal = (SpriteRenderer)Get(growth, "formReveal");
            float radius = reveal.sharedMaterial.GetFloat("_FormAmount");
            await PauseRehearsal(() => growth.Age, reveal);
            Check(reveal.sharedMaterial.GetFloat("_FormAmount") >= radius, "The hand reveal resumes after Esc");
            await Space(true);
        }
        if (!recordVideo && route == 0 && stage == 3)
        {
            await Until(() => growth.CurrentBeat == KarmaGuardianGrowth.Beat.Foreshadow);
            await PauseRehearsal(() => growth.Age, boss.GetComponentInChildren<SpriteRenderer>());
            await Space(true);
        }
        var capturedCaptions = new HashSet<string>();
        while (growth != null)
        {
            if (recordVideo && route < 3 && growth.CaptionAlpha > 0.9f && capturedCaptions.Add(growth.Caption))
            {
                string suffix = growth.CurrentBeat == KarmaGuardianGrowth.Beat.TakeStance ? "-opening"
                    : growth.Caption == "네 움직임…" ? "-transform" : "";
                ScreenCapture.CaptureScreenshot(Output + "/ui-" + route + "-" + stage + suffix + ".png");
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
                && beats.Contains(stage == 1 ? KarmaGuardianGrowth.Beat.Remember : KarmaGuardianGrowth.Beat.Adapt),
                "First copy is remembered; second inheritance is reinterpreted after failure");
        if (stage == 1) Check(beats.Contains(KarmaGuardianGrowth.Beat.Adapt), "Guardian uses its signature skill after remembering the successful copy");
        Check(beats.Contains(KarmaGuardianGrowth.Beat.Falter) == (stage == 2)
            && beats.Contains(KarmaGuardianGrowth.Beat.Inspect) == (stage == 2), "Trial and reflection only at second inheritance");
        if (stage == 3) Check(beats.Contains(KarmaGuardianGrowth.Beat.Combine)
            && beats.Contains(KarmaGuardianGrowth.Beat.Foreshadow)
            && !beats.Contains(KarmaGuardianGrowth.Beat.Imitate), "Mastery has no repeated trial");
        if (edge) await Until(() => HudOpacity > 0.05f && HudOpacity < 0.95f);
        await Until(() => (bool)Property(game, "EncounterPromptReady"));
        if (edge) Check(HudOpacity > 0.99f && Framed(boss.Position.x, 0.025f) && Framed(player.x, 0.025f),
            "Both fighters and the restored HUD fit the name shot");
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
        bool captured = false, opaqueProof = false;
        float previousAdvance = 0;
        while (game.Phase == RunPhase.Transition && !game.GuardianInheriting)
        {
            Check((game.Player.Position - bodyPosition).sqrMagnitude < 0.01f, "Player demonstration leaves physics body in place");
            poses.Add(art.CurrentPose ?? "");
            Check(picture.color == baseColor, "Personalization preserves the player's original skin and clothing colours");
            var ceremony = UnityEngine.Object.FindAnyObjectByType<KarmaPersonalizationVFX>();
            if (ceremony != null)
            {
                float age = (float)Get(ceremony, "age");
                var layers = ceremony.GetComponentsInChildren<SpriteRenderer>();
                var memory = (SpriteRenderer)Get(ceremony, "memoryPicture");
                var source = (SpriteRenderer)Get(ceremony, "sourceEffect");
                var proof = (SpriteRenderer)Get(ceremony, "proofEffect");
                Check(HudOpacity == 0, "Combat UI stays hidden during the whole player demonstration");
                if (age > 0.45f && memory.enabled)
                    Check(Framed(memory.bounds.min.x, 0.015f) && Framed(memory.bounds.max.x, 0.015f),
                        "The complete player memory is inside the viewport");
                if (age > 0.35f && age < 1.3f)
                    Check(Mathf.Abs(memory.color.a - 0.65f) < 0.001f && Mathf.Abs(source.color.a - 0.65f) < 0.001f,
                        "Player memory matches the guardian's 65 percent opacity");
                bool inGap = age > KarmaPersonalizationVFX.MemoryDuration + 0.05f && age < KarmaPersonalizationVFX.CeremonyStart - 0.05f
                    || age > KarmaPersonalizationVFX.CeremonyStart + KarmaPersonalizationVFX.CeremonyDuration + 0.05f
                        && age < KarmaPersonalizationVFX.ProofStart - 0.05f;
                if (inGap) Check(layers.All(p => !p.enabled || p.color.a < 0.001f), "Quiet intervals contain no overlapping presentation effects");
                var original = layers.Single(p => p.name == "Absorbed sigil");
                Check(original.sprite == KarmaArtwork.Sprite("PersonalizationVFX", "Awaken"), "Original awakening art is reused");
                if (age < KarmaPersonalizationVFX.CeremonyStart || age > KarmaPersonalizationVFX.CeremonyStart + KarmaPersonalizationVFX.CeremonyDuration + 0.05f)
                    Check(original.color.a == 0, "Original ceremony is confined to the middle sequence");
                if (age > KarmaPersonalizationVFX.CeremonyStart + 0.3f && age < KarmaPersonalizationVFX.CeremonyStart + 1.7f)
                    Check(art.CurrentPose == "Transition" || art.CurrentPose.StartsWith("Personalize"), "Original personalization gesture plays after the memory");
                if (proof.color.a > 0.99f) opaqueProof = true;
            }
            if (recordVideo && route < 3 && !captured && art.CurrentPose != null
                && (art.CurrentPose == "Personalized/Pierce" || art.CurrentPose == "Personalized/AirDiagonal" || art.CurrentPose == "Personalized/Riposte"))
            {
                ScreenCapture.CaptureScreenshot(Output + "/ui-player-" + route + "-" + game.Player.Essences.Count + ".png");
                captured = true;
            }
            if (art.CurrentPose != null && art.CurrentPose.StartsWith("Personalized/"))
            {
                float advance = picture.transform.localPosition.x - ((Vector3)Get(art, "pictureHome")).x;
                Check(advance + 0.001f >= previousAdvance, "Player holds the finishing position without sliding backwards");
                previousAdvance = advance;
                Check((bool)Get(art, "personalizationProofActive") && (bool)Get(art, "presentingEnding"), "Authored player proof is active");
                var focus = UnityEngine.Object.FindAnyObjectByType<KarmaGrowthFocus>();
                Check(focus != null && (float)Property(focus, "GuardianOpacity") < 0.001f,
                    "Fallen guardian cannot overlap the player demonstration");
            }
            yield return null;
        }
        Check(opaqueProof, "New personalized technique is demonstrated at full opacity");
        Check(!poses.Contains("Personalized/Walk"), "No backwards walk follows the player's demonstration");
    }
    // Sample each player-loop frame; an Editor task continuation can resume
    // after several frames when the GPU/importer is busy and miss a short beat.
    static IEnumerator ObserveGrowth(KarmaGuardianGrowth growth, HashSet<KarmaGuardianGrowth.Beat> beats)
    {
        int stage = growth.Stage;
        var attempts = new HashSet<int>(); var staggers = new HashSet<int>();
        bool checkedRender = false, checkedFall = false, inspectedWeapon = false;
        var releases = new List<int>();
        var demos = (SpriteRenderer[])Get(growth, "demonstrations");
        bool hintedBeforeLine = false;
        float rememberHeld = 0, reachHeld = 0, revealHeld = 0;
        bool sawNewForm = false, checkedRevealShader = false;
        var captions = new List<string>();
        var source = (SpriteRenderer)Get(growth, "sourcePicture");
        var guardianArt = game.Enemies[0].GetComponentInChildren<KarmaCharacterAnimator>();
        var body = guardianArt.GetComponentInChildren<SpriteRenderer>();
        var bodyMaterial = body.sharedMaterial;
        var halo = (SpriteRenderer)Get(growth, "inheritedHalo");
        if (stage == 1 && game.Enemies[0].PrimaryType == Essence.Guard)
            Check(halo != null && game.Enemies[0].AutoShieldActive, "The guard shield remains active during its visual introduction");
        var previousForm = (string)Get(guardianArt, "previousMotionForm");
        bool PreviousBody(Sprite sprite) => sprite != null && (sprite == KarmaArtwork.StageIdle(previousForm)
            || AssetDatabase.GetAssetPath(sprite.texture) == KarmaPrototype.EditorTools.KarmaMotionSheetImport.SheetPath(previousForm, sprite.texture.name));
        while (growth != null)
        {
            Check(HudOpacity == 0, "Combat UI cannot reveal the new guardian's name before the entrance");
            if (growth.Age > 0.6f)
            {
                Check(Framed(body.bounds.min.x, 0.015f) && Framed(body.bounds.max.x, 0.015f),
                    "The active guardian's silhouette fits in the shot");
                if (source.enabled) Check(Framed(source.bounds.min.x, 0.015f) && Framed(source.bounds.max.x, 0.015f),
                    "The guardian's memory stays visible at the wall");
            }
            if (stage == 1 && (growth.CurrentBeat == KarmaGuardianGrowth.Beat.TakeStance ||
                growth.CurrentBeat == KarmaGuardianGrowth.Beat.Imitate))
            {
                Check(body.enabled && PreviousBody(body.sprite) && Get(growth, "formReveal") == null,
                    "The original body performs the question and the entire copy");
            }
            if (stage == 1 && growth.CurrentBeat == KarmaGuardianGrowth.Beat.Remember)
            {
                var reveal = (SpriteRenderer)Get(growth, "formReveal");
                if (reveal != null)
                {
                    Check(body.enabled && reveal.enabled && PreviousBody(body.sprite) && !PreviousBody(reveal.sprite)
                        && body.sharedMaterial == reveal.sharedMaterial && growth.Caption == "네 움직임…",
                        "The two real sprite outlines share the hand reveal during the first phrase");
                    if (!checkedRevealShader)
                    {
                        Check(reveal.transform.parent == body.transform && reveal.sharedMaterial.shader.isSupported
                            && !ShaderUtil.ShaderHasError(reveal.sharedMaterial.shader), "Foot-relative reveal shader is compiled and supported");
                        checkedRevealShader = true;
                    }
                    revealHeld += Time.deltaTime;
                }
                if (growth.Caption == "네 움직임… 기억했다." && growth.CaptionAlpha > 0.9f)
                {
                    Check(body.enabled && reveal == null && body.sharedMaterial == bodyMaterial && !PreviousBody(body.sprite),
                        "The completed phrase holds the inherited body after releasing the overlay");
                    sawNewForm = true;
                }
            }
            if (halo != null && growth.Caption != "네 움직임… 기억했다.")
                Check(halo.color.a == 0, "The inherited shield is invisible on the neutral guardian");
            if (halo != null && growth.CurrentBeat == KarmaGuardianGrowth.Beat.Adapt)
                Check(halo.color == (Color)Get(growth, "haloColor"), "The normal shield is restored before the signature");
            if (growth.CaptionAlpha > 0.9f && captions.LastOrDefault() != growth.Caption) captions.Add(growth.Caption);
            if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Hesitate)
            {
                Check(stage == 1 && !source.enabled && growth.CaptionAlpha == 0, "The guardian has a silent beat after the memory disappears");
                if (body.sprite.texture.name == "GrowthHesitate") reachHeld += Time.deltaTime;
            }
            if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.TakeStance)
                Check(!source.enabled && demos.All(d => d == null || !d.enabled), "The stance and its opening line precede the attack");
            if (stage == 3)
            {
                Check(!source.enabled, "Mastery begins without a player demonstration");
                for (int i = 0; i < demos.Length; i++)
                    if (demos[i].enabled && demos[i].color.a > 0.2f && !releases.Contains(i)) releases.Add(i);
                if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Foreshadow && growth.CaptionAlpha == 0)
                    hintedBeforeLine |= ((SpriteRenderer[])Get(growth, "ultimateHints")).Any(p => p.enabled && p.sprite != null);
            }
            if (stage == 1 && growth.CurrentBeat == KarmaGuardianGrowth.Beat.Adapt)
            {
                Check(beats.Contains(KarmaGuardianGrowth.Beat.Remember), "Signature skill follows the memory hold");
                Check(growth.CaptionAlpha == 0, "Signature skill begins after the memory caption has cleared");
            }
            if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Remember) rememberHeld += Time.deltaTime;
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
                if (body.sprite.texture.name == "GrowthFalter" && body.sprite.name == "GrowthFalter_02") checkedFall = true;
            }
            if (growth.CurrentBeat == KarmaGuardianGrowth.Beat.Inspect && guardianArt.CurrentPose == "Growth/InspectWeapon")
            {
                string path = AssetDatabase.GetAssetPath(body.sprite);
                string owner = previousForm == "Wave" ? "SwordWave" : previousForm == "Ward" ? "Guard" : "RisingSlash";
                Check(path == "Assets/Arts/" + owner + "/" + previousForm + "/InspectWeapon.png", "Original weapon inspection is displayed by the previous guardian");
                inspectedWeapon = true;
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
            Check(staggers.SetEquals(new[] { 1, 2 }) && checkedFall && inspectedWeapon,
                "First two attempts wobble; only the third falls before reflection");
        }
        else if (stage == 1) Check(attempts.SetEquals(new[] { 1 }) && staggers.Count == 0,
            "First inheritance attempts once and remembers without failure");
        if (stage == 1)
        {
            Check(reachHeld > 0.8f && captions.SequenceEqual(new[] { "이렇게…?", "네 움직임…", "네 움직임… 기억했다." }),
                "The silent reaching pose precedes the question and the successful copy is acknowledged");
            Check(revealHeld >= 1.15f && sawNewForm, "A continuous hand reveal finishes before the signature attack");
            Check(rememberHeld >= 2.5f, "First inheritance holds the successful stance before its signature skill");
        }
        if (stage == 3) Check(captions.SequenceEqual(new[] { "이번엔… 내가 먼저.", "이제 나만의 움직임이다…" }),
            "The guardian announces the first move and acknowledges mastery after the ultimate hint");
        if (stage == 3) Check(releases.SequenceEqual(new[] { 0, 1, 2 }) && hintedBeforeLine,
            "Each inherited technique appears in acquisition order and the ultimate appears before the line");
    }
    static async Task Ending()
    {
        // A fresh room restores the boss after the explicit ultimate cancel test.
        typeof(KarmaGame).GetMethod("LoadRoom", F).Invoke(game, new object[] { true });
        await PlaceEdgeKill(route % 2 == 0, true);
        game.Enemies[0].TakeDamage(10000000);
        await Until(() => game.Phase == RunPhase.Ending);
        if (recordVideo) { recording = "ending"; Directory.CreateDirectory(Output + "/" + recording); videoFrame = 0; lastRecorded = -1; }
        await Until(() => game.EndingTime >= 1.6f);
        if (recordVideo) ScreenCapture.CaptureScreenshot(Output + "/ui-ending-reflect.png");
        Check(game.EndingSpeaker == "마지막 수호자" && game.EndingLine.Contains("내 것으로"), "Guardian acknowledges the player");
        var art = (Transform)Get(game, "fallenGuardian");
        Check(art != null && art.GetComponent<KarmaCharacterAnimator>().CurrentPose == "Ending/Reflect", "Guardian reflects on the mastered technique");
        var picture = art.GetComponentInChildren<SpriteRenderer>();
        Check(HudOpacity == 0 && Framed(picture.bounds.min.x, 0.07f) && Framed(picture.bounds.max.x, 0.07f),
            "The final guardian's acknowledgment is fully visible after a ranged edge kill");
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
            Time.captureDeltaTime = 1f / 12;
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
