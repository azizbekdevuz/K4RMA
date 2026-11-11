#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class KarmaPresentationChecks
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
    const string SymbolKey = "K4RMA.UnifiedSymbolChecks", SymbolOutput = "output/symbol-review";
    static bool symbolsStarted;
    static string symbolError;
    static KarmaGame symbolGame;
    static int assertions;
    static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    static float Support(Sprite sprite) => (float)typeof(KarmaCharacterAnimator)
        .GetMethod("FallenSupportHeight", Hidden).Invoke(null, new object[] { sprite });

    static KarmaPresentationChecks()
    {
        EditorApplication.update += () => {
            if (!SessionState.GetBool(SymbolKey, false)) return;
            Application.runInBackground = true; EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (!symbolsStarted && EditorApplication.isPlaying && Time.frameCount > 3)
            { symbolsStarted = true; RunSymbols(); }
        };
    }
    [MenuItem("K4RMA/검증/고유화 문양 통일")]
    public static void RunSymbolsFromMenu()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before validation.");
        SessionState.SetBool(SymbolKey + ".Background", Application.runInBackground);
        SessionState.SetBool(SymbolKey, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static async Task SymbolsUntil(Func<bool> done)
    {
        double deadline = EditorApplication.timeSinceStartup + 40;
        while (!done())
        {
            if (symbolError != null) throw new Exception(symbolError);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Symbol presentation timeout");
            await Task.Delay(10);
        }
    }
    static async Task SymbolCapture(string name)
    {
        string path = SymbolOutput + "/" + name + ".png";
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        await SymbolsUntil(() => File.Exists(path));
    }
    static void CheckCeremony(bool guardian, Essence essence)
    {
        var root = symbolGame.World.Find(guardian ? "Guardian inheritance ceremony" : "Player personalization ceremony");
        Check(root != null, "Real ceremony exists for " + essence);
        var pictures = root.GetComponentsInChildren<SpriteRenderer>();
        var marks = pictures.Where(p => p.name == "Inherited power" || p.name == "Absorbed sigil" || p.name == "Awakening pulse").ToArray();
        Check(marks.Length == 5 && marks.All(p => p.sprite == KarmaArtwork.SkillIcon(essence)),
            "Absorbed, inherited and expanding symbols share the selection icon for " + essence);
        Check(marks.All(p => Mathf.Abs(p.transform.localScale.x - p.transform.localScale.y) < 0.001f),
            "Ceremony preserves the source symbol proportions");
    }
    static async void RunSymbols()
    {
        string verdict;
        Application.LogCallback log = (message, stack, type) => {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Assets/Scripts/")) symbolError = message;
        };
        try
        {
            Directory.CreateDirectory(SymbolOutput); assertions = 0;
            symbolGame = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            Application.logMessageReceived += log;
            var icons = KarmaArtwork.Settings.skillIcons;
            Check(icons.Length == 3, "Only the three canonical selection symbols are configured");
            for (int i = 0; i < 3; i++) Check(icons[i] != null && icons[i].name == new[] { "Wave", "Rise", "Ward" }[i],
                "Approved selection sprite identity and order remain unchanged");
            foreach (Essence essence in Enum.GetValues(typeof(Essence)))
            {
                symbolGame.StartRun(); symbolGame.Enemies[0].TakeDamage(1000000);
                await SymbolsUntil(() => symbolGame.Phase == RunPhase.Choosing);
                if (essence == Essence.SwordWave) await SymbolCapture("choice");
                symbolGame.Choose(essence); symbolGame.ConfirmChoice();
                CheckCeremony(false, essence);
                await Task.Delay(350); await SymbolCapture("personalize-" + essence);
                await SymbolsUntil(() => symbolGame.GuardianInheriting);
                CheckCeremony(true, essence);
                await Task.Delay(350); await SymbolCapture("inherit-" + essence);
                await SymbolsUntil(() => symbolGame.IsCombat);
                symbolGame.ReturnToTitle();
            }
            symbolGame.StartRun();
            foreach (Essence essence in Enum.GetValues(typeof(Essence)))
            { symbolGame.Player.Essences.Preview(essence); symbolGame.Player.Essences.Confirm(out _); }
            await SymbolCapture("owned-hud");
            foreach (Essence essence in Enum.GetValues(typeof(Essence)))
            {
                symbolGame.Player.Essences.NotifyUnique(essence);
                var proc = symbolGame.World.Find("Personalization symbol " + essence);
                Check(proc != null && proc.GetComponentsInChildren<SpriteRenderer>().Where(p => p.name != "Spark")
                    .All(p => p.sprite == KarmaArtwork.SkillIcon(essence)), "Proc core, glow and echo use the same canonical symbol");
                await SymbolCapture("proc-" + essence);
                await SymbolsUntil(() => proc == null);
            }
            var members = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(symbolGame, 3);
            typeof(KarmaGame).GetMethod("LoadRoom", members).Invoke(symbolGame, new object[] { true });
            symbolGame.Enemies[0].TakeDamage(1000000);
            Check(symbolGame.Phase == RunPhase.Ending, "Real final defeat starts ending");
            var ending = symbolGame.World.Find("Ending presentation");
            for (int i = 0; i < 3; i++)
            {
                var symbol = ending.Find("Completed technique " + (Essence)i).GetComponent<SpriteRenderer>();
                var united = ending.Find("Three techniques united/United " + (Essence)i).GetComponent<SpriteRenderer>();
                Check(symbol.sprite == icons[i] && united.sprite == icons[i], "Ending and convergence retain the same selection symbol");
            }
            await SymbolsUntil(() => symbolGame.EndingTime >= 14.5f); await SymbolCapture("ending-symbols");
            await SymbolsUntil(() => symbolGame.EndingTime >= 17.7f); await SymbolCapture("ending-united");
            symbolGame.ReturnToTitle(); await Task.Delay(100);
            Check(!UnityEngine.Object.FindAnyObjectByType<KarmaPersonalizationVFX>() && !UnityEngine.Object.FindAnyObjectByType<KarmaEndingPresentation>(),
                "Return to title removes ceremony and ending symbols");
            Check(symbolError == null, "No presentation runtime errors");
            verdict = "PASS: " + assertions + " canonical selection symbols across HUD, three real personalizations, inheritance, unique procs and ending";
        }
        catch (Exception error) { verdict = "FAIL: " + error; Debug.LogException(error); }
        Application.logMessageReceived -= log;
        Directory.CreateDirectory(SymbolOutput); File.WriteAllText(SymbolOutput + "/result.txt", verdict); Debug.Log(verdict);
        SessionState.SetBool(SymbolKey, false);
        Application.runInBackground = SessionState.GetBool(SymbolKey + ".Background", false);
        EditorApplication.ExitPlaymode();
    }

    [MenuItem("K4RMA/검증/배경과 사망 접지")]
    public static void RunFromMenu()
    {
        string result;
        try { Run(); result = "PASS: " + assertions + " background/original texture and fallen-body support assertions"; }
        catch (Exception error) { result = "FAIL: " + error; Debug.LogException(error); }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/presentation-ground.txt", result);
        Debug.Log(result);
    }
    public static void RunBatch()
    {
        RunFromMenu();
        EditorApplication.Exit(File.ReadAllText("output/validation/presentation-ground.txt").StartsWith("PASS:") ? 0 : 1);
    }
    static void Run()
    {
        assertions = 0;
        var stage = new GameObject("Presentation checks");
        try
        {
            var fallen = Resources.LoadAll<Sprite>("Neutral/Revival/Neutral")
                .OrderBy(s => s.name).First();
            float silhouetteBottom = fallen.vertices.Min(v => v.y);
            float supportHeight = Support(fallen);
            Check(supportHeight > silhouetteBottom + 0.05f,
                "Actual fallen body's support must be higher than its protruding blade tip: " +
                supportHeight + " bottom=" + silhouetteBottom + " vertices=" + fallen.vertices.Length + " triangles=" + fallen.triangles.Length);

            var artwork = new GameObject("Detached fallen guardian"); artwork.transform.SetParent(stage.transform);
            artwork.transform.position = new Vector3(8, 12, 0);
            artwork.transform.rotation = Quaternion.Euler(0, 0, 18);
            artwork.transform.localScale = new Vector3(-1.7f, 1.7f, 1);
            var picture = new GameObject("Guardian sprite").AddComponent<SpriteRenderer>();
            picture.transform.SetParent(artwork.transform, false);
            picture.transform.localPosition = new Vector3(0, -KarmaSpace.GuardianGroundY / KarmaSpace.GuardianArtScale, 0);
            picture.transform.localScale = Vector3.one * KarmaSpace.Scale;
            var animator = artwork.AddComponent<KarmaCharacterAnimator>();
            animator.ConfigureSprites(picture, new[] { KarmaArtwork.Settings.neutralGuardian });
            animator.ConfigureSheet("Neutral");
            animator.Die();
            float support = Support(picture.sprite);
            Check(animator.CurrentPose == "Death", "Death pose is selected immediately");
            Check(Mathf.Abs(picture.transform.TransformPoint(new Vector3(0, support, 0)).y) < 0.001f,
                "Fallen body lands on the floor after an elevated, mirrored, tilted pose");
            Check(Quaternion.Angle(artwork.transform.rotation, Quaternion.identity) < 0.01f, "Death clears retained tilt");
            var settled = artwork.transform.position;
            animator.Pose(0, false);
            Check(Vector3.Distance(settled, artwork.transform.position) < 0.001f, "Repeated death updates do not drift");

            KarmaArtwork.Background(stage.transform, 0);
            var background = stage.transform.Find("Trial hall artwork");
            var renderer = background.GetComponent<SpriteRenderer>();
            Check(renderer.sprite == KarmaArtwork.Settings.dojoPanorama, "Stage zero uses the same extended temple");
            Check(background.GetComponentsInChildren<Renderer>().Length == 1, "Single plate has no overlay seam");
            Check(Mathf.Abs(renderer.bounds.size.x - KarmaStage.BackgroundWidth * 2) < 0.001f, "Two-width outpaint coverage");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stage);
        }
    }
}
#endif
