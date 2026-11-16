#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

// Exercises PR presentation together with the current native player and reset lifecycle.
[InitializeOnLoad]
public static class KarmaIntegrationChecks
{
    const string Key = "K4RMA.IntegrationChecks";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static bool started;
    static int assertions;
    static string runtimeError;
    static void Check(bool ok, string message)
    { assertions++; if (!ok) throw new Exception(message); }
    static KarmaIntegrationChecks() { EditorApplication.update += Pump; }
    static void Pump()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!started && EditorApplication.isPlaying && Time.frameCount > 2)
        { started = true; Run(); }
    }
    public static void RunBatch()
    {
        SessionState.SetBool(Key, true);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeError = message; }
    static async void Run()
    {
        string verdict;
        Application.logMessageReceived += Log;
        try
        {
            KarmaSettingsChecks.CheckBindingPersistence(Check);
            var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
            game.StartRun();
            foreach (var essence in new[] { Essence.SwordWave, Essence.RisingSlash, Essence.Guard })
            { game.Player.Essences.Preview(essence); game.Player.Essences.Confirm(out _); }
            typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
            typeof(KarmaGame).GetMethod("LoadRoom", Hidden).Invoke(game, new object[] { true });
            foreach (var enemy in game.Enemies) enemy.gameObject.SetActive(false);
            await Task.Delay(100);
            KarmaPresentationChecks.CheckCeremonyLayers(game, Check);
            KarmaPrototype.EditorTools.KarmaPlayerAnimationAssets.VerifyParrySuccess();
            Check(true, "Parry input, frames, facing, expiry, hurt and death");
            var player = game.Player;
            var animator = player.GetComponentInChildren<KarmaCharacterAnimator>(true);
            var nativeMode = typeof(KarmaCharacterAnimator).GetField("nativePlayer24", Hidden);
            bool originalNative = (bool)nativeMode.GetValue(animator);
            Check(originalNative == (Resources.Load<TextAsset>("Player/Animation24/Ready") != null),
                "Preserve the repository's native animation activation gate");
            try
            {
                foreach (bool native in new[] { false, true })
                {
                    nativeMode.SetValue(animator, native);
                    for (int i = 0; i < 3; i++)
                    {
                        player.ResetForRoom(new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.05f), true);
                        typeof(KarmaPlayer).GetField("invulnerableUntil", Hidden).SetValue(player, 0f);
                        animator.PlayPose("Riposte", 0.3f);
                        Check(animator.ParrySuccessVisible, "Parry can replay after room reset");
                        player.ClearStatus();
                        typeof(KarmaPlayer).GetField("invulnerableUntil", Hidden).SetValue(player, 0f);
                        Check(player.ApplyShock(0.75f), "A new shock is accepted");
                        animator.SendMessage("LateUpdate");
                        Check(!animator.ParrySuccessVisible, "Shock cancels parry without changing CC timing");
                        var flash = animator.transform.GetComponentInChildren<SpriteRenderer>(true).transform.Find("Parry success flash");
                        Check(flash != null && !flash.GetComponent<SpriteRenderer>().enabled, "No stale parry flash after shock");
                        animator.Die();
                        player.ResetForRoom(new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.05f), true);
                        animator.PlayPose("Riposte", 0.3f);
                        Check(animator.ParrySuccessVisible, "Death state cannot cross a room reset");
                        player.ResetForRoom(new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.05f), true);
                        Check(!animator.ParrySuccessVisible, "Room reset clears an active parry");
                    }
                }
            }
            finally { nativeMode.SetValue(animator, originalNative); }
            KarmaGuardIntegrationChecks.Run(game, Check);
            KarmaSwordCacheChecks.Run(Check);
            KarmaPhysicsQueryChecks.Run(player, Check);
            var library = ScriptableObject.CreateInstance<KarmaSpriteLibrary>();
            try
            {
                library.sheets = null;
                Check(library.All("empty").Length == 0 && library.Get(null, null) == null, "Null library is safe");
                library.sheets = new[] { null, new KarmaSpriteLibrary.Sheet { id = "empty" } };
                typeof(KarmaSpriteLibrary).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(library, null);
                Check(library.All("empty").Length == 0 && library.Get("empty", "missing") == null, "Null entries are safe");
            }
            finally { UnityEngine.Object.DestroyImmediate(library); }
            Check(runtimeError == null, "No runtime error: " + runtimeError);
            verdict = "PASS: " + assertions + " integration assertions, plus 16 parry frame/facing samples";
        }
        catch (Exception error) { verdict = "FAIL: " + error; }
        finally { Application.logMessageReceived -= Log; }
        Directory.CreateDirectory("output/validation");
        File.WriteAllText("output/validation/integration.txt", verdict);
        SessionState.SetBool(Key, false); Debug.Log(verdict);
        EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
    }
}
#endif
