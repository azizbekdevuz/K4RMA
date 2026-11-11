#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;

[InitializeOnLoad]
public static class KarmaCinematicSkipChecks
{
    const string Pending = "K4RMA.CinematicSkipChecks";
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static bool started;
    static int checks;
    static KarmaGame game;
    static Keyboard keyboard;
    static string runtimeError;
    static KarmaCinematicSkipChecks() { EditorApplication.update += () => {
        if (!SessionState.GetBool(Pending, false)) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2) { started = true; Run(); }
    }; }
    [MenuItem("K4RMA/Tutorial/Validate skip and practice recovery")]
    public static void RunInEditor() { Begin(false); }
    public static void RunBatch() { Begin(true); }
    static void Begin(bool batch)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
        SessionState.SetBool(Pending + ".Batch", batch);
        SessionState.SetBool(Pending + ".Background", Application.runInBackground);
        SessionState.SetBool(Pending, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static async Task Until(Func<bool> condition, string message)
    {
        double end = EditorApplication.timeSinceStartup + 15;
        while (!condition()) {
            if (runtimeError != null) throw new Exception(runtimeError);
            if (EditorApplication.timeSinceStartup > end) throw new Exception(message);
            await Task.Yield();
        }
    }
    static async Task Frames(int count = 2)
    { int end = Time.frameCount + count; await Until(() => Time.frameCount >= end, "Player loop stopped"); }
    static async Task Wait(float seconds)
    { float end = Time.realtimeSinceStartup + seconds; await Until(() => Time.realtimeSinceStartup >= end, "Realtime stopped"); }
    static async Task Keys(params Key[] keys)
    { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); await Frames(); }
    static async Task Tutorial()
    { await Keys(); game.StartOpening(); await Frames(); Check(game.CanSkipCinematic, "Tutorial offers skip"); }
    static void FreezeLesson()
    {
        typeof(KarmaTutorial).GetProperty("Lesson").SetValue(game.Tutorial, 0);
        typeof(KarmaTutorial).GetProperty("Step").SetValue(game.Tutorial, KarmaTutorialStep.Lesson);
        Time.timeScale = 0;
    }
    static async Task FinalBoss()
    {
        await Keys(); game.StartRun();
        foreach (Essence e in Enum.GetValues(typeof(Essence))) { game.Player.Essences.Preview(e); game.Player.Essences.Confirm(out _); }
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        typeof(KarmaGame).GetMethod("LoadRoom", Fields).Invoke(game, new object[] { true });
        game.Enemies[0].TakeDamage(game.Enemies[0].MaxHealth * 100);
        await Frames(); Check(game.Phase == RunPhase.Ending, "Lethal final boss contact starts real ending");
    }
    static async Task CheckEarlyTutorialSkip()
    {
        await Tutorial();
        Check(game.Tutorial.Step == KarmaTutorialStep.Travel && game.Tutorial.Lesson == -1,
            "Actual opening starts before the first lesson is selected");
        await Keys(Key.F);
        await Until(() => game.Tutorial != null && game.Tutorial.Step == KarmaTutorialStep.Fade,
            "Early F hold never reaches fade");
        Check(game.Tutorial.Lesson == -1, "Early fade renders safely without a lesson index");
        await Until(() => game.Tutorial == null, "Early skip froze before reaching combat");
        await CheckArrivalLock();
        await Wait(0.25f); await Keys();
        float x = game.Player.Position.x;
        await Keys(Key.D); await Wait(0.25f); await Keys();
        Check(game.Player.Position.x > x + 0.1f, "Player moves after skipping before the first lesson");
    }
    static async Task CheckArrivalLock()
    {
        await Frames();
        Check(game.Phase == RunPhase.Combat && game.OpeningArrivalSpeech != null && game.GameplayFrozen
            && !game.CanMove && !game.AllowsPlayerActions && !game.IsCombat && Time.timeScale == 0,
            "Arrival speech freezes movement, attacks and combat until acknowledged");
        Vector2 at = game.Player.Position;
        var animator = game.Player.GetComponentInChildren<KarmaCharacterAnimator>();
        Check(animator.CurrentPose == "Idle" && !game.Player.Airborne
            && Mathf.Abs(at.y - KarmaSpace.PlayerHalfHeight) <= Physics2D.defaultContactOffset * 2 + 0.001f,
            $"Arrival starts grounded in Idle, not a frozen airborne pose (pose={animator.CurrentPose}, airborne={game.Player.Airborne}, position={at})");
        float health = game.Enemies[0].Health, fight = game.Enemies[0].FightSeconds;
        await Keys(Key.D, Key.J, Key.L, Key.LeftShift, Key.K);
        await Wait(0.2f);
        Check(Vector2.Distance(game.Player.Position, at) < 0.001f
            && game.Player.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
            "Movement and buffered skill inputs cannot move the player during arrival speech");
        Check(animator.CurrentPose == "Idle", "Held input cannot replace arrival Idle with an action pose");
        Check(game.Enemies[0].Health == health && game.Enemies[0].FightSeconds == fight && game.Projectiles.Count == 0,
            "Dialogue does not allow attacks or advance the boss clock");
        await Keys();
        game.TogglePause(); await Keys(Key.Space);
        Check(game.OpeningArrivalSpeech != null, "Paused Space cannot acknowledge arrival speech");
        game.TogglePause(); await Keys();
        await Until(() => game.OpeningArrivalFade < 0.45f, "Arrival fade did not finish while gameplay was frozen");
        string capture = "output/validation/arrival-idle-" + checks + ".png";
        Directory.CreateDirectory("output/validation");
        ScreenCapture.CaptureScreenshot(capture);
        await Until(() => File.Exists(capture), "Arrival Idle screenshot was not captured");
        await Keys(Key.Space); await Keys(); await Frames();
        Check(game.OpeningArrivalSpeech == null && game.CanMove && game.AllowsPlayerActions && game.IsCombat
            && !game.GameplayFrozen && Time.timeScale == 1, "Space acknowledgement resumes normal play");
        Check(game.Player.Position.y <= at.y + 0.05f && game.Player.GetComponent<Rigidbody2D>().linearVelocity.y <= 0.1f,
            "Acknowledgement Space does not leak into a jump");
    }
    static async Task CheckPracticeRecovery()
    {
        await Keys();
        EditorApplication.isPaused = true;
        KarmaPrototype.EditorTools.KarmaBossPractice.Request(Essence.Guard,
            KarmaPrototype.EditorTools.KarmaBossPractice.Mode.Single, 1);
        Check(!EditorApplication.isPaused, "New practice request clears an inherited Editor pause immediately");
        await Frames(3);
        await Until(() => game.Phase == RunPhase.Combat && game.Enemies.Count == 1 && game.StageIndex == 1,
            "Practice never resumes combat after Editor pause");
        float x = game.Player.Position.x, fight = game.Enemies[0].FightSeconds;
        await Keys(Key.D); await Wait(0.3f); await Keys();
        Check(game.Player.Position.x > x + 0.1f && game.Enemies[0].FightSeconds > fight && Time.timeScale == 1,
            "Practice player movement and boss clock advance after recovery");
    }
    static async Task CheckTutorialSkip()
    {
        await Tutorial(); FreezeLesson(); await Frames();
        await Keys(Key.Space); await Wait(0.25f);
        Check(game.Phase == RunPhase.Tutorial && game.Tutorial.Step == KarmaTutorialStep.Practice,
            "Short Space still advances one teacher line");
        Check(game.Player.Position.y < KarmaSpace.PlayerHalfHeight + 0.15f, "Dialogue confirmation does not jump");
        await Wait(1.4f);
        Check(game.Phase == RunPhase.Tutorial && game.CinematicSkipProgress == 0, "Holding Space confirms dialogue without skipping");
        Check(KarmaInput.BindingLabel(KarmaAction.SkipCinematic) == "F", "Skip hint and input both use F");
        await Keys(); Check(game.CinematicSkipProgress == 0, "Release clears hold progress");
        await Keys(Key.F); await Wait(0.45f);
        Check(game.CinematicSkipProgress > 0.1f && game.CinematicSkipProgress < 0.8f, "Partial hold reports progress");
        await Keys(); await Keys(Key.F); await Wait(0.3f);
        Check(game.Phase == RunPhase.Tutorial, "Separate partial holds never accumulate"); await Keys();
        game.TogglePause(); await Keys(Key.F); await Wait(1.65f);
        Check(game.Paused && game.Phase == RunPhase.Tutorial && game.CinematicSkipProgress == 0, "Pause blocks skip");
        game.TogglePause(); await Wait(0.3f);
        Check(game.CinematicSkipProgress == 0, "Unpausing with F held requires release"); await Keys();
        FreezeLesson(); await Frames();
        // F skips independently of Space dialogue confirmation, including while scaled time is frozen.
        await Keys(Key.F); await Frames();
        Check(game.Tutorial.Step == KarmaTutorialStep.Lesson, "F does not confirm teacher dialogue");
        Check(Time.timeScale == 0, "Frozen dialogue uses unscaled hold time");
        await Until(() => game.Tutorial == null, "Frozen tutorial never skips");
        Check(game.Phase == RunPhase.Combat, "Skip fades into first stage");
        Check(game.StageIndex == 0 && game.Player.Essences.Count == 0 && game.Player.Health == game.Config.playerHealth,
            "Skipped tutorial preserves initial skills and health");
        Check(game.World.GetComponentsInChildren<KarmaTrainingTarget>().Length == 0, "Skip removes tutorial actors");
        Check(game.Player.GetComponent<Rigidbody2D>().linearVelocity.y <= 0.1f, "Held skip does not leak a jump into combat");
        await CheckArrivalLock();
        await Keys(); foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
        await Keys(Key.F); await Wait(1.65f);
        Check(game.Phase == RunPhase.Combat && game.CinematicSkipProgress == 0, "Ordinary combat F never skips");
        await Keys();
        if (!string.IsNullOrEmpty(game.OpeningArrivalSpeech)) { await Keys(Key.Space); await Keys(); }
        await Keys(Key.Space); await Wait(0.12f);
        Check(game.Player.Airborne && game.CinematicSkipProgress == 0, "Space still jumps during combat");
        await Keys();
        await Tutorial();
        typeof(KarmaTutorial).GetProperty("Lesson").SetValue(game.Tutorial, 3);
        typeof(KarmaTutorial).GetProperty("Step").SetValue(game.Tutorial, KarmaTutorialStep.Practice);
        var body = game.Player.GetComponent<Rigidbody2D>();
        body.position = game.Player.transform.position = new Vector2(3, KarmaSpace.PlayerHalfHeight);
        await Until(() => game.Tutorial.WaitingForGuard, "Guard training did not freeze");
        Check(Time.timeScale == 0, "Real guard training freeze reached");
        await Keys(Key.F); await Until(() => game.Tutorial == null, "Guard hold never skips");
        Check(game.Phase == RunPhase.Combat, "Guard freeze skip reaches the first stage");
        await CheckArrivalLock();
        await Keys();
    }
    static async Task CheckEndingAndRestart()
    {
        await FinalBoss(); var presentation = game.World.Find("Ending presentation");
        int soundStage = (int)typeof(KarmaGame).GetField("endingSoundStage", Fields).GetValue(game);
        await Keys(Key.F); await Until(() => game.CinematicSkipProgress > 0.15f, "Ending hold did not start");
        game.TogglePause(); float pausedAt = game.EndingTime; await Wait(1.65f);
        Check(game.Paused && game.EndingTime == pausedAt && !game.EndingCompleted, "Paused ending cannot advance or skip");
        game.TogglePause(); await Wait(0.3f); Check(game.CinematicSkipProgress == 0, "Ending hold resets on pause");
        await Keys(); await Keys(Key.F);
        await Until(() => game.EndingFade > 0.1f, "Skip fade missing");
        Check(game.Phase == RunPhase.Ending && game.EndingTime < 8, "Skip fades from current shot without fast-forwarding cues");
        await Until(() => game.EndingCompleted, "Ending skip never completes"); await Frames();
        Check(game.Phase == RunPhase.Victory && game.EndingFade == 1 && game.EndingTime == 40, "Skip reaches normal victory endpoint");
        Check(presentation == null && game.World.Find("Ending presentation") == null, "Early skip removes presentation objects");
        Check((Transform)typeof(KarmaGame).GetField("fallenGuardian", Fields).GetValue(game) == null, "Early skip removes undissolved boss");
        Check((int)typeof(KarmaGame).GetField("endingSoundStage", Fields).GetValue(game) == soundStage, "Skipping never bursts pending sound cues");
        await Keys(Key.F, Key.R); await Keys(Key.F); await Wait(1.65f);
        Check(game.Phase == RunPhase.Tutorial && game.Tutorial != null && !game.EndingCompleted && game.EndingTime == 0,
            "Victory R starts tutorial and clears ending state");
        Check(game.CinematicSkipProgress == 0, "F carried through restart cannot skip new tutorial"); await Keys();
        await FinalBoss(); await Keys(Key.R);
        Check(game.Phase == RunPhase.Tutorial && game.Tutorial != null && Time.timeScale == 1, "Mid-ending R starts tutorial"); await Keys();
        game.TogglePause(); await Keys(Key.R);
        Check(game.Phase == RunPhase.Tutorial && !game.Paused && game.Enemies.Count == 0, "Paused tutorial R starts fresh tutorial"); await Keys();
        game.StartRun(); game.Player.TakeDamage(game.Player.Health * 2);
        await Until(() => game.Phase == RunPhase.Defeat, "Actual defeat never completes"); await Keys(Key.R);
        Check(game.Phase == RunPhase.Tutorial && game.Tutorial != null, "Defeat R starts tutorial"); await Keys();
        game.StartRun(); game.TogglePause(); await Keys(Key.R);
        Check(game.Phase == RunPhase.Tutorial && !game.Paused, "Paused combat R starts tutorial");
    }
    static async void Run()
    {
        string result; InputActionMap map = null; string bindings = null;
        Application.LogCallback log = null;
        try
        {
            game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); keyboard = InputSystem.AddDevice<Keyboard>();
            map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            map.devices = new[] { keyboard }; bindings = map.SaveBindingOverridesAsJson(); map.RemoveAllBindingOverrides();
            log = (message, stack, type) => {
                if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Assets/Scripts/")
                    && !message.StartsWith("Not allowed to override geometry")) runtimeError = message;
            };
            Application.logMessageReceived += log;
            await CheckEarlyTutorialSkip(); await CheckTutorialSkip(); await CheckEndingAndRestart();
            await CheckPracticeRecovery();
            Check(runtimeError == null, "No new gameplay errors"); game.ReturnToTitle();
            result = "PASS: " + checks + " early F skip, frozen tutorial, pause recovery, movement and tutorial-first restart checks";
        }
        catch (Exception ex) { result = "FAIL: " + ex; Debug.LogException(ex); }
        finally
        {
            if (map != null) { map.devices = null; if (bindings != null) map.LoadBindingOverridesFromJson(bindings); }
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (log != null) Application.logMessageReceived -= log;
            Time.timeScale = 1; SessionState.SetBool(Pending, false);
            Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
        }
        Directory.CreateDirectory("output/validation"); File.WriteAllText("output/validation/cinematic-skip.txt", result);
        Debug.Log(result);
        if (SessionState.GetBool(Pending + ".Batch", false)) EditorApplication.Exit(result.StartsWith("PASS:") ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
#endif
