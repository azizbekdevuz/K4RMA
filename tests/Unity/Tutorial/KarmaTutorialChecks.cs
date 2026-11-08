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
    static KarmaTutorialChecks() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("K4RMA/검증/플레이 가능한 오프닝")]
    public static void Run() { SessionState.SetBool(Pending, true); if (EditorApplication.isPlaying) Changed(PlayModeStateChange.EnteredPlayMode); else EditorApplication.EnterPlaymode(); }
    [MenuItem("K4RMA/검증/오프닝 시작·재시작·대화")]
    public static void RunEntry() { SessionState.SetBool(Entry, true); Run(); }
    static async void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        string report;
        try { bool entry = SessionState.GetBool(Entry, false); SessionState.SetBool(Entry, false); if (entry) await CheckEntry(); else await CheckRun(); report = "PASS: " + assertions + " tutorial flow, hits, guard freeze, pause, cleanup and combat-entry assertions"; }
        catch (Exception e) { report = "FAIL: " + e; Debug.LogException(e); }
        finally { if (keyboard != null) InputSystem.RemoveDevice(keyboard); Application.runInBackground = previousBackground; }
        Directory.CreateDirectory("output/validation"); File.WriteAllText("output/validation/tutorial.txt", report); Debug.Log(report);
    }
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static async Task Wait(int ms = 120) { await Task.Delay(ms); }
    static Key KeyFor(KarmaAction action)
    {
        var map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        string path = map.FindAction(action.ToString()).bindings[0].effectivePath;
        return ((KeyControl)keyboard[path.Substring(path.LastIndexOf('/') + 1)]).keyCode;
    }
    static async Task Press(KarmaAction action)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(action))); await Wait();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait();
    }
    static void Place(float x, float y = KarmaSpace.PlayerHalfHeight)
    {
        var body = game.Player.GetComponent<Rigidbody2D>(); body.position = new Vector2(x, y); game.Player.transform.position = body.position; body.linearVelocity = Vector2.zero;
    }
    static async Task CheckRun()
    {
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        previousBackground = Application.runInBackground; Application.runInBackground = true;
        await Wait(300); game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        keyboard = InputSystem.AddDevice<Keyboard>(); game.ReturnToTitle(); game.StartOpening(); tutorial = game.Tutorial;
        Check(tutorial != null && game.StageIndex == 0 && game.Enemies.Count == 0, "Opening starts without guardian");
        await Wait(); Check(Time.timeScale == 1 && tutorial.Step == KarmaTutorialStep.Travel, "Opening starts with movement and jump guidance");
        foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1280, 800), new Vector2(1024, 768) })
        {
            var viewport = (Rect)typeof(KarmaGame).GetMethod("ScreenViewport", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { size.x, size.y });
            Check(viewport.x >= 0 && viewport.y >= 0 && viewport.xMax <= size.x && viewport.yMax <= size.y && Mathf.Abs(viewport.width / viewport.height - 16f / 9) < 0.01f, "UI viewport remains bounded at " + size);
        }
        tutorialCameraSize = game.GameCamera.orthographicSize; Capture("start");
        Check(!tutorial.Learned(Essence.SwordWave), "Unlearned originals stay locked");
        game.Player.Essences.TryUse(Essence.SwordWave); Check(game.Projectiles.Count == 0, "Locked wave does not spawn");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Right))); await Wait(1000);
        Check(game.Player.Position.x > -7, "Movement guidance permits real movement");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Right), KeyFor(KarmaAction.Jump))); await Wait(180);
        Check(game.Player.Position.y > KarmaSpace.PlayerHalfHeight + 0.3f, "Jump input produces actual airborne motion"); await Wait(820);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait(700);
        Check(game.Player.Position.x > -7, "Movement and jump guidance without obstacle gate");
        for (int lesson = 0; lesson < 4; lesson++)
        {
            Place(KarmaTutorial.Stations[lesson] - 12); await Wait();
            Check(tutorial.Step == (lesson == 0 ? KarmaTutorialStep.Practice : KarmaTutorialStep.Lesson) && tutorial.Lesson == lesson, "Spatial lesson trigger " + lesson);
            if (lesson > 0) await Press(KarmaAction.DialogueConfirm);
            Capture("lesson-" + lesson);
            if (lesson < 3)
            {
                Place(KarmaTutorial.Stations[lesson] - (lesson == 1 ? 12 : 2.7f)); await Wait();
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
                    game.Player.Essences.ResetTemporaryState(); Place(KarmaTutorial.Stations[lesson] - (lesson == 1 ? 12 : 2.7f));
                    await Press(action); await Wait(850);
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
                Place(KarmaTutorial.Stations[3] - 5); InputSystem.QueueStateEvent(keyboard, new KeyboardState(KeyFor(KarmaAction.Guard))); await WaitFor(() => tutorial.WaitingForGuard, "Guard warning", 4000);
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
                    if (count < 3) await WaitFor(() => tutorial.WaitingForGuard, "Next defense", 4000);
                }
            }
            await Wait(1200); Check(tutorial.Step == KarmaTutorialStep.Travel, "Completed lesson opens path");
        }
        Check(game.Player.Essences.PersonalizationOrder.Count == 0 && game.Elapsed == 0 && game.DamageTaken == 0, "Tutorial excluded from progression and records");
        Place(KarmaTutorialStage.FinalGate - 4); await Wait(); Check(tutorial.Step == KarmaTutorialStep.FinalDialogue, "Final master dialogue");
        for (int i = 0; i < 4; i++) await Press(KarmaAction.DialogueConfirm);
        Check(tutorial.Step == KarmaTutorialStep.Gate, "Four dialogue lines enable gate");
        Capture("gate"); Place(KarmaTutorialStage.FinalGate); await Press(KarmaAction.Interact); await Wait(1000);
        Check(game.Tutorial == null && game.IsCombat && game.StageIndex == 0 && game.Enemies.Count == 1, "Gate enters original first guardian");
        Capture("combat"); await Wait(150); Check(Mathf.Abs(game.GameCamera.orthographicSize - tutorialCameraSize) < 0.01f, "Tutorial and first combat have identical character screen size");
        Check(game.Enemies[0].PrimaryType == null, "First guardian has no inherited technique");
        Check(game.Player.Essences.HasActive(Essence.SwordWave) && game.Player.Essences.HasActive(Essence.RisingSlash) && game.Player.Essences.HasActive(Essence.Guard), "All three originals available");
        Check(game.Player.Essences.Count == 0 && game.Player.Health == game.Config.playerHealth && game.DamageTaken == 0 && Time.timeScale == 1, "Combat starts with clean original skills and records");
        Check(game.World.GetComponentsInChildren<KarmaTrainingTarget>().Length == 0, "Tutorial actors cleaned");
        game.StartOpening(); Check(game.Tutorial != null && game.Phase == RunPhase.Tutorial, "Title start enters opening");
        game.RestartSession(); Check(game.Tutorial == null && game.IsCombat, "Restart skips opening");
        game.StartRun(); Check(game.IsCombat && game.Tutorial == null, "Practice and test direct entry preserved");
        game.ReturnToTitle(); game.StartOpening(); tutorial = game.Tutorial;
        typeof(KarmaTutorial).GetProperty("Lesson").SetValue(tutorial, 3);
        typeof(KarmaTutorial).GetProperty("Step").SetValue(tutorial, KarmaTutorialStep.Practice);
        Place(KarmaTutorial.Stations[3] - 5); await WaitFor(() => tutorial.WaitingForGuard, "Title branch defense hold", 4000);
        await Press(KarmaAction.Pause); await Press(KarmaAction.Title);
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
