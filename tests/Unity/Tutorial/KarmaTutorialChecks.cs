#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;
[InitializeOnLoad]
public static class KarmaTutorialChecks
{
    const string Entry = "K4RMA.TutorialEntryChecks";
    const string Pending = "K4RMA.TutorialChecks";
    static int assertions;
    static Keyboard keyboard;
    static KarmaGame game;
    static KarmaTutorial tutorial;
    static bool previousBackground;
    static float tutorialCameraSize;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static KarmaTutorialChecks()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += () =>
        {
            if (!SessionState.GetBool("K4RMA.TutorialBatch", false) || !EditorApplication.isPlaying) return;
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        };
    }
    public static void RunBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/K4RMA/Scenes/KarmaPrototype.unity");
        SessionState.SetBool("K4RMA.TutorialBatch", true); Run();
    }
    public static void RunExitBatch() { SessionState.SetBool("K4RMA.TutorialExit", true); RunBatch(); }
    [MenuItem("K4RMA/검증/플레이 가능한 오프닝")]
    public static void Run() { SessionState.SetBool(Pending, true); if (EditorApplication.isPlaying) Changed(PlayModeStateChange.EnteredPlayMode); else EditorApplication.EnterPlaymode(); }
    [MenuItem("K4RMA/검증/오프닝 시작·재시작·대화")]
    public static void RunEntry() { SessionState.SetBool(Entry, true); Run(); }
    static async void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        string report;
        try
        {
            bool entry = SessionState.GetBool(Entry, false), exit = SessionState.GetBool("K4RMA.TutorialExit", false);
            SessionState.SetBool(Entry, false); SessionState.SetBool("K4RMA.TutorialExit", false);
            if (exit)
            {
                previousBackground = Application.runInBackground; Application.runInBackground = true;
                await Wait(500); game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); keyboard = InputSystem.AddDevice<Keyboard>();
                await CheckGuardExit();
            }
            else if (entry) await CheckEntry(); else await CheckRun();
            report = "PASS: " + assertions + " tutorial " + (exit ? "guard hold and title exit" : "flow, hits, guard freeze, pause, cleanup and combat-entry") + " assertions";
        }
        catch (Exception e) { report = "FAIL: " + e; Debug.LogException(e); }
        finally { if (keyboard != null) { ((InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).devices = null; InputSystem.RemoveDevice(keyboard); } Application.runInBackground = previousBackground; }
        Directory.CreateDirectory("output/validation"); File.WriteAllText("output/validation/tutorial.txt", report); Debug.Log(report);
        if (SessionState.GetBool("K4RMA.TutorialBatch", false)) EditorApplication.Exit(report.StartsWith("PASS:") ? 0 : 1);
    }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static async Task Wait(int ms = 120)
    {
        float real = Time.realtimeSinceStartup, simulation = Time.time;
        int frame = Time.frameCount;
        do { await Task.Delay(16); }
        while ((Time.realtimeSinceStartup - real < ms / 1000f || Time.frameCount < frame + 2
            || Time.timeScale > 0 && Time.time - simulation < ms / 1000f)
            && Time.realtimeSinceStartup - real < 8);
    }
    static Key KeyFor(KarmaAction action)
    {
        var map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        string path = map.FindAction(action.ToString()).bindings[0].effectivePath;
        return ((KeyControl)keyboard[path.Substring(path.LastIndexOf('/') + 1)]).keyCode;
    }
    static async Task Press(KarmaAction action)
    {
        async Task InputFrame()
        {
            int frame = Time.frameCount; await Wait();
            await WaitFor(() => Time.frameCount > frame + 1, "Input reaches the player loop", 5000);
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(action))); await InputFrame();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await InputFrame();
    }
    static void Place(float x, float y = KarmaSpace.PlayerHalfHeight)
    {
        var body = game.Player.GetComponent<Rigidbody2D>(); body.position = new Vector2(x, y); game.Player.transform.position = body.position; body.linearVelocity = Vector2.zero;
    }
    static async Task CheckRun()
    {
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        previousBackground = Application.runInBackground; Application.runInBackground = true;
        int frame = Time.frameCount;
        await WaitFor(() => Time.frameCount > frame + 3, "Play loop advances before testing: frame=" + frame + ", paused=" + EditorApplication.isPaused, 5000);
        await Wait(300); game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        keyboard = InputSystem.AddDevice<Keyboard>();
        ((InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).devices = new[] { keyboard }; game.ReturnToTitle();
        var bossCache = typeof(KarmaArtwork).GetField("bossLibraries", BindingFlags.Static | BindingFlags.NonPublic);
        bossCache.SetValue(null, null);
        var startup = System.Diagnostics.Stopwatch.StartNew(); game.StartOpening(); startup.Stop();
        tutorial = game.Tutorial;
        Check(bossCache.GetValue(null) == null, "Starting training does not synchronously load all boss sprite libraries");
        Debug.Log("Tutorial synchronous start: " + startup.ElapsedMilliseconds + " ms");
        Check(tutorial != null && game.StageIndex == 0 && game.Enemies.Count == 0, "Opening starts without guardian");
        var targets = game.World.GetComponentsInChildren<KarmaTrainingTarget>();
        Check(targets.Length == 4, "Each of the four pictures has a training dummy");
        Check(game.World.Find("Master") != null && Mathf.Abs(game.World.Find("Master").position.x + 12) < 0.01f, "Master stands in front of the dojo steps");
        foreach (var name in new[] { "01_Dojo_NoMaster", "04_Boss_Gate" })
            Check(game.World.Find(name) != null, "Supplied background loaded: " + name);
        foreach (var target in targets)
            Check(Mathf.Abs(target.GetComponent<SpriteRenderer>().bounds.min.y) < 0.01f, "Dummy stands on the ground");
        float floorEnd = -26;
        for (int segment = 0; segment < 2; segment++)
        {
            var floor = game.World.Find("Ground · " + segment).GetComponent<BoxCollider2D>().bounds;
            Check(floor.min.x <= floorEnd + 0.01f && floor.max.y >= 0, "Tutorial floor segment is continuous and walkable " + segment);
            floorEnd = floor.max.x;
        }
        Check(floorEnd >= KarmaTutorialStage.Right, "Tutorial floor reaches the final boundary");
        await Wait(); Check(Time.timeScale == 1 && tutorial.Step == KarmaTutorialStep.Travel, "Opening starts with movement and jump guidance");
        Check(UnityEngine.Object.FindObjectsByType<KarmaGame>(FindObjectsSortMode.None).Length == 1, "Canonical tutorial scene has one director");
        Check(Mathf.Abs(game.GameCamera.orthographicSize - 50f * 9 / 32) < 0.01f && Mathf.Abs(game.GameCamera.transform.position.x) < 0.01f, "First supplied image fills the complete viewport: " + game.GameCamera.orthographicSize + " / " + game.GameCamera.transform.position);
        foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1024, 768) })
        {
            var viewport = (Rect)typeof(KarmaGame).GetMethod("ScreenViewport", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { size.x, size.y });
            Check(viewport.x >= 0 && viewport.y >= 0 && viewport.xMax <= size.x && viewport.yMax <= size.y && Mathf.Abs(viewport.width / viewport.height - 16f / 9) < 0.01f, "UI viewport remains bounded at " + size);
        }
        tutorialCameraSize = game.GameCamera.orthographicSize; Capture("start");
        Check(!tutorial.Learned(Essence.SwordWave), "Unlearned originals stay locked");
        game.Player.Essences.TryUse(Essence.SwordWave); Check(game.Projectiles.Count == 0, "Locked wave does not spawn");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Left))); await Wait(400);
        Check(game.Player.Position.x < -7, "First room permits leftward movement: x=" + game.Player.Position.x + ", held=" + KarmaInput.Held(KarmaAction.Left) + ", movable=" + game.CanMove + ", scale=" + Time.timeScale);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Left), KeyFor(KarmaAction.Jump))); await Wait(180);
        Check(game.Player.Position.y > KarmaSpace.PlayerHalfHeight + 0.3f, "Jump input produces actual airborne motion");
        await Wait(1200);
        Check(game.Player.Position.y < KarmaSpace.PlayerHalfHeight + 0.2f, "Player falls beside the left boundary without sticking");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        for (int lesson = 0; lesson < 4; lesson++)
        {
            Place(0); await Wait();
            Check(Mathf.Abs(game.GameCamera.transform.position.x) < 0.01f, "All lessons stay on the master screen " + lesson);
            Check(tutorial.Step == KarmaTutorialStep.Lesson && tutorial.Lesson == lesson, "Spatial lesson trigger and teacher dialogue " + lesson);
            await Press(KarmaAction.DialogueConfirm);
            float roomEnd = 0.5f * KarmaTutorialStage.RoomWidth;
            float barrierX = roomEnd - 0.3f;
            Check(Mathf.Abs(game.World.GetComponentsInChildren<KarmaTutorialGate>()[lesson].transform.position.x - barrierX) < 0.01f,
                "Training barrier sits at the room exit " + lesson);
            Place(roomEnd - 4);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Right)));
            await Wait(550);
            Check(game.Player.Position.x < barrierX, "Unfinished training blocks the room exit " + lesson);
            Check(tutorial.BlockedMessage.Contains("먼저 훈련"), "Blocked travel explains required training " + lesson);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Place(0); await Wait();
            Capture("lesson-" + lesson);
            if (lesson < 3)
            {
                Place(13 - (lesson == 1 ? 12 : 2.7f)); await Wait();
                if (lesson > 0) { await Press(KarmaAction.Attack); Check(tutorial.Successes == 0, "Wrong attack excluded " + lesson); }
                if (lesson == 1)
                {
                    Place(KarmaTutorial.Stations[lesson] + 12); await Press(KarmaAction.SwordWave); await Wait(700);
                    Check(tutorial.Successes == 0, "Missed wave excluded");
                    Place(KarmaTutorial.Stations[lesson] - 12);
                }
                var action = lesson == 0 ? KarmaAction.Attack : lesson == 1 ? KarmaAction.SwordWave : KarmaAction.RisingSlash;
                for (int count = 1; count <= 3; count++)
                {
                    game.Player.Essences.ResetTemporaryState(); Place(13 - (lesson == 1 ? 12 : 2.7f));
                    await Press(action); await Wait(600);
                    Check(tutorial.Successes == count, "Real skill hit " + lesson + "/" + count);
                    if (count == 1)
                    {
                        int execution = lesson == 1 ? (int)typeof(KarmaProjectile).GetField("nextTrainingId", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)
                            : (int)typeof(KarmaPlayer).GetField("trainingExecution", Private).GetValue(game.Player);
                        game.World.GetComponentsInChildren<KarmaTrainingTarget>()[lesson].Hit((KarmaTrainingAttack)lesson, execution);
                        Check(tutorial.Successes == count, "Repeated collision from same execution excluded " + lesson);
                        if (lesson > 0) { await Press(action); Check(tutorial.Successes == count, "Cooldown input excluded " + lesson); }
                    }
                }
            }
            else
            {
                Place(KarmaTutorial.Stations[3] - 5); InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Guard)));
                await WaitFor(() => typeof(KarmaTutorial).GetField("strikeCharge", Private).GetValue(tutorial) != null, "Dummy begins visible charge after guard cooldown", 12000);
                var charge = (SpriteRenderer)typeof(KarmaTutorial).GetField("strikeCharge", Private).GetValue(tutorial);
                Check(charge.enabled && Mathf.Abs(charge.transform.position.x - 13) < 1, "Charge is visibly anchored to the shared dummy");
                Check(!((SpriteRenderer)typeof(KarmaTutorial).GetField("strike", Private).GetValue(tutorial)).enabled, "Wave launches after gathering energy");
                Capture("guard-charge");
                await WaitFor(() => tutorial.WaitingForGuard, "Guard warning", 4000);
                var strike = (SpriteRenderer)typeof(KarmaTutorial).GetField("strike", Private).GetValue(tutorial);
                var reference = new GameObject("Wave appearance check").AddComponent<SpriteRenderer>();
                KarmaRemnantVFX.ProjectileLook(reference, Vector2.right);
                Check(strike.sprite == reference.sprite && strike.transform.localScale == reference.transform.localScale && strike.GetComponent<KarmaCombatVFX>() != null, "Guard training uses the player's projectile sprite, size and attack effects");
                UnityEngine.Object.Destroy(reference.gameObject);
                await Wait(400); Check(tutorial.WaitingForGuard, "Previously held guard cannot release hold");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait(); Capture("guard");
                float time = Time.time; Vector2 player = game.Player.Position;
                await Press(KarmaAction.Attack); await Wait(500);
                Check(tutorial.WaitingForGuard && Time.time == time && game.Player.Position == player && tutorial.Successes == 0, "Wrong input cannot resume frozen world");
                await Press(KarmaAction.Pause); Check(game.Paused, "Esc pause over guard wait");
                game.OpenSettings(); await Press(KarmaAction.Guard); game.CloseSettings();
                Check(tutorial.WaitingForGuard, "Settings input cannot release guard wait");
                await Press(KarmaAction.Pause); Check(!game.Paused && tutorial.WaitingForGuard, "Esc resumes tutorial hold");
                for (int count = 1; count <= 3; count++)
                {
                    await Press(KarmaAction.Guard); await Wait(400);
                    Check(tutorial.Successes == count && !tutorial.WaitingForGuard, "Actual shield blocks strike " + count);
                    tutorial.GuardBlocked(); Check(tutorial.Successes == count, "No duplicate defense contact");
                    if (count < 3)
                    {
                        Check(game.Player.Essences.Cooldown(Essence.Guard) > 0, "Successful defense starts its real cooldown");
                        await WaitFor(() => game.Player.Essences.Cooldown(Essence.Guard) <= 0, "Defense cooldown ends", 10000);
                        float readyAt = Time.time;
                        await Wait(250);
                        Check(typeof(KarmaTutorial).GetField("strikeCharge", Private).GetValue(tutorial) == null, "Dummy waits after cooldown before gathering energy");
                        await WaitFor(() => typeof(KarmaTutorial).GetField("strikeCharge", Private).GetValue(tutorial) != null, "Next charge", 2500);
                        Check(Time.time - readyAt >= 0.45f, "Charge begins half a second after cooldown");
                        await WaitFor(() => tutorial.WaitingForGuard, "Next defense", 4000);
                    }
                }
            }
            await Wait(1200); Check(tutorial.Step == (lesson < 3 ? KarmaTutorialStep.Lesson : KarmaTutorialStep.FinalDialogue), "Next lesson stays in the same room; final lesson opens travel");
            Check(!game.World.GetComponentsInChildren<KarmaTutorialGate>()[lesson].GetComponent<BoxCollider2D>().enabled, "Completed training opens its barrier " + lesson);
            Check(string.IsNullOrEmpty(tutorial.BlockedMessage), "Completion clears blocked warning " + lesson);
        }
        Check(tutorial.Step == KarmaTutorialStep.FinalDialogue && game.Player.Position.x < 25 && Time.timeScale == 0, "Final advice starts immediately after all training in the master room");
        for (int i = 0; i < 3; i++) await Press(KarmaAction.DialogueConfirm);
        Check(tutorial.Step == KarmaTutorialStep.Gate && Time.timeScale == 1, "Final advice resumes travel to the gate");
        Place(24); InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Right))); await Wait(600);
        Check(game.Player.Position.x > 25 && game.Player.Position.y > 0, "Completed training permits walking directly into the gate courtyard without falling");
        Check(game.World.Find("04_Boss_Gate").GetComponent<SpriteRenderer>().bounds.min.x <= 25.01f, "Gate background immediately follows the training background");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Check(game.Player.Essences.PersonalizationOrder.Count == 0 && game.Elapsed == 0 && game.DamageTaken == 0, "Tutorial excluded from progression and records");
        Place(KarmaTutorialStage.FinalGate - 4); await Wait();
        Check(tutorial.Step == KarmaTutorialStep.Gate, "Three teacher dialogue lines enable gate");
        Check(KarmaInput.BindingLabel(KarmaAction.Interact) == "W", "Final gate displays the W interaction key");
        Capture("gate"); Place(KarmaTutorialStage.FinalGate - 7); await Press(KarmaAction.Interact);
        Check(tutorial.Step == KarmaTutorialStep.Gate && game.Tutorial != null, "F outside the gate does not skip the interaction");
        Place(KarmaTutorialStage.FinalGate - 4); await Press(KarmaAction.Interact); await Wait(1000);
        Check(game.Tutorial == null && game.IsCombat && game.StageIndex == 0 && game.Enemies.Count == 1, "Gate enters original first guardian");
        Check(!string.IsNullOrEmpty(game.OpeningArrivalSpeech) && game.OpeningArrivalSpeech.Contains("나만의 기술"), "First boss entry shows the player's resolve");
        game.TogglePause(); await Press(KarmaAction.DialogueConfirm);
        Check(!string.IsNullOrEmpty(game.OpeningArrivalSpeech), "Pause does not dismiss the arrival speech");
        game.TogglePause();
        float arrivalY = game.Player.Position.y;
        await Press(KarmaAction.DialogueConfirm);
        Check(string.IsNullOrEmpty(game.OpeningArrivalSpeech), "Space dismisses the first boss arrival speech");
        Check(game.Player.Position.y <= arrivalY + 0.1f, "Dismissing arrival speech does not trigger a jump");
        Capture("combat"); await Wait(150); Check(Mathf.Abs(game.GameCamera.orthographicSize - tutorialCameraSize) < 0.01f, "Tutorial and first combat have identical character screen size");
        Check(game.Enemies[0].PrimaryType == null, "First guardian has no inherited technique");
        Check(game.Player.Essences.HasActive(Essence.SwordWave) && game.Player.Essences.HasActive(Essence.RisingSlash) && game.Player.Essences.HasActive(Essence.Guard), "All three originals available");
        Check(game.Player.Essences.Count == 0 && game.Player.Health == game.Config.playerHealth && game.DamageTaken == 0 && Time.timeScale == 1, "Combat starts with clean original skills and records");
        Check(game.World.GetComponentsInChildren<KarmaTrainingTarget>().Length == 0, "Tutorial actors cleaned");
        game.Player.ToggleTestInvincibility();
        KarmaStage.RefreshBoundaries(game.World);
        Place(KarmaStage.ArenaLeft + 3);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Left))); await Wait(700);
        Check(game.Player.GetComponent<BoxCollider2D>().bounds.min.x >= KarmaStage.ArenaLeft + 0.45f, "Boss arena left wall stops the player inside the background edge");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Left), KeyFor(KarmaAction.Jump))); await Wait(180);
        Check(game.Player.Position.y > KarmaSpace.PlayerHalfHeight + 0.3f, "Player can jump against the boss arena wall");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Left))); await Wait(1300);
        Check(game.Player.Position.y < KarmaSpace.PlayerHalfHeight + 0.2f, "Player falls naturally while pressing against the boss wall");
        Place(KarmaStage.ArenaRight - 3);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Right))); await Wait(700);
        Check(game.Player.GetComponent<BoxCollider2D>().bounds.max.x <= KarmaStage.ArenaRight - 0.45f, "Boss arena right wall stops the player inside the background edge");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        game.Player.ToggleTestInvincibility();
        game.StartOpening(); Check(game.Tutorial != null && game.Phase == RunPhase.Tutorial, "Title start enters opening");
        game.RestartSession(); Check(game.Tutorial == null && game.IsCombat, "Restart skips opening");
        game.StartRun(); Check(game.IsCombat && game.Tutorial == null, "Practice and test direct entry preserved");
        await CheckGuardExit();
    }
    static async Task CheckGuardExit()
    {
        game.ReturnToTitle(); game.StartOpening(); tutorial = game.Tutorial;
        typeof(KarmaTutorial).GetProperty("Lesson").SetValue(tutorial, 3);
        typeof(KarmaTutorial).GetProperty("Step").SetValue(tutorial, KarmaTutorialStep.Practice);
        Place(KarmaTutorial.Stations[3] - 5); await WaitFor(() => tutorial.WaitingForGuard, "Title branch defense hold", 4000);
        await Press(KarmaAction.Pause); Check(game.Paused, "Pause opens before title exit"); await Press(KarmaAction.Title);
        Check(game.Phase == RunPhase.Title && game.Tutorial == null && Time.timeScale == 1 && game.Projectiles.Count == 0, "Title exit cleans hold and restores time");
    }
    static async Task CheckEntry()
    {
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        previousBackground = Application.runInBackground; Application.runInBackground = true;
        await Wait(300); game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); keyboard = InputSystem.AddDevice<Keyboard>();
        game.ReturnToTitle(); game.StartOpening(); await Wait();
        Check(game.Tutorial != null && game.Enemies.Count == 0, "Title start enters tutorial");
        game.TogglePause(); game.RestartSession(); await Wait();
        Check(game.IsCombat && game.StageIndex == 0 && game.Tutorial == null && game.Enemies.Count == 1, "Pause restart during tutorial enters first guardian");
        game.Player.TakeDamage(game.Player.Health + 1);
        await WaitFor(() => game.Phase == RunPhase.Defeat, "Boss death reaches defeat", 4000);
        await Press(KarmaAction.Restart);
        Check(game.IsCombat && game.StageIndex == 0 && game.Tutorial == null, "Death restart enters first guardian");
        game.TogglePause(); await Press(KarmaAction.Restart);
        Check(game.IsCombat && game.StageIndex == 0 && game.Tutorial == null, "Combat pause restart enters first guardian");
        game.ReturnToTitle(); game.StartOpening();
        Check(game.Tutorial != null && game.Phase == RunPhase.Tutorial, "Returning to title still enters tutorial");
        tutorial = game.Tutorial;
        typeof(KarmaTutorial).GetProperty("Lesson").SetValue(tutorial, 1);
        typeof(KarmaTutorial).GetProperty("Step").SetValue(tutorial, KarmaTutorialStep.Lesson);
        Place(51); await Wait();
        await Press(KarmaAction.Confirm);
        Check(tutorial.Step == KarmaTutorialStep.Lesson, "Enter does not advance teacher dialogue");
        Check(KarmaInput.BindingLabel(KarmaAction.DialogueConfirm) == "Space", "Dialogue defaults to physical Space");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Wait(300);
        Check(tutorial.Step == KarmaTutorialStep.Practice && game.Player.Position.y < KarmaSpace.PlayerHalfHeight + 0.1f, "Space advances without jumping while held");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait(); await Press(KarmaAction.Jump);
        game.ReturnToTitle();
    }
    static void Capture(string label)
    { Directory.CreateDirectory("output/validation"); ScreenCapture.CaptureScreenshot("output/validation/tutorial-" + label + ".png"); }
    static async Task WaitFor(Func<bool> condition, string message, int limit)
    {
        float until = Time.realtimeSinceStartup + limit / 1000f;
        while (!condition() && Time.realtimeSinceStartup < until) await Wait(50);
        Check(condition(), message);
    }
}
#endif
