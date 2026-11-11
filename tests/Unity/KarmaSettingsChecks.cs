#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using KarmaPrototype;
public static class KarmaSettingsChecks
{
    const string Bindings = "K4RMA.InputBindings.v1";
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    public static void CheckBindingPersistence(Action<bool, string> verify)
    {
        // Exercise genuine Input System JSON across separately created maps,
        // without reading or writing the user's PlayerPrefs.
        var restore = typeof(KarmaInput).GetMethod("RestoreSavedBindings", BindingFlags.Static | BindingFlags.NonPublic);
        using (var first = new InputActionMap("K4RMA"))
        using (var restarted = new InputActionMap("K4RMA"))
        {
            foreach (var map in new[] { first, restarted })
            {
                map.AddAction("Left", binding: "<Keyboard>/a").AddBinding("<Keyboard>/leftArrow");
                map.AddAction("Jump", binding: "<Keyboard>/space");
            }
            first["Left"].ApplyBindingOverride(0, "<Keyboard>/h");
            first["Jump"].ApplyBindingOverride(0, "<Keyboard>/rightShift");
            string saved = first.SaveBindingOverridesAsJson();
            verify(first["Left"].bindings[0].id != restarted["Left"].bindings[0].id, "Restart fixture changes binding GUIDs");
            restore.Invoke(null, new object[] { restarted, saved });
            verify(restarted["Left"].bindings[0].effectivePath == "<Keyboard>/h"
                && restarted["Jump"].bindings[0].effectivePath == "<Keyboard>/rightShift", "Existing v1 settings survive a fresh map");
            verify(restarted["Left"].bindings[1].effectivePath == "<Keyboard>/leftArrow", "Secondary default alias is preserved");
            string roundTrip = restarted.SaveBindingOverridesAsJson();
            restore.Invoke(null, new object[] { restarted, roundTrip });
            verify(restarted.SaveBindingOverridesAsJson() == roundTrip, "Same-map IDs and repeated restoration retain v1 data");
            foreach (string invalid in new[] { "{", "{}", saved.Replace("K4RMA/Jump", "K4RMA/Unknown") })
            {
                bool rejected = false;
                try { restore.Invoke(null, new object[] { restarted, invalid }); }
                catch (TargetInvocationException error) when (error.InnerException is FormatException) { rejected = true; }
                verify(rejected && restarted.SaveBindingOverridesAsJson() == roundTrip,
                    "Invalid or unresolvable data cannot partially replace active bindings");
            }
            restore.Invoke(null, new object[] { restarted, "" });
            verify(restarted["Left"].bindings[0].effectivePath == "<Keyboard>/a", "Empty override data restores defaults");
        }
    }
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
        bool hadBindings = PlayerPrefs.HasKey(Bindings); string bindings = PlayerPrefs.GetString(Bindings, "");
        KarmaInput.BindingLabel(KarmaAction.Pierce);
        var map = (InputActionMap)typeof(KarmaInput).GetField("map", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        string activeBindings = map.SaveBindingOverridesAsJson();
        string[] keys = { "Master", "Music", "Effects", "Shake", "Muted" };
        bool[] existed = new bool[5]; float[] values = new float[5];
        for (int i = 0; i < keys.Length; i++) { string k = "K4RMA.Options." + keys[i]; existed[i] = PlayerPrefs.HasKey(k); values[i] = i == 4 ? PlayerPrefs.GetInt(k) : PlayerPrefs.GetFloat(k); }
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            KarmaInput.ResetBindings();
            Check(KarmaInput.BindingLabel(KarmaAction.Left) == "A" && KarmaInput.BindingLabel(KarmaAction.Right) == "D"
                && KarmaInput.BindingLabel(KarmaAction.Jump) == "Space"
                && KarmaInput.BindingLabel(KarmaAction.RisingSlash) == "Left Shift", "Key labels use English physical key names");
            Check(KarmaInput.TrySetBinding(KarmaAction.Pierce, "<Keyboard>/f10", out _) && KarmaInput.BindingLabel(KarmaAction.Pierce) == "F10",
                "Rebound function keys keep their English key name");
            Check(KarmaInput.TrySetBinding(KarmaAction.Pierce, "<Keyboard>/rightShift", out _) && KarmaInput.BindingLabel(KarmaAction.Pierce) == "Right Shift",
                "Rebound modifier keys keep their English key name");
            KarmaInput.ResetBindings();
            Check(KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "L", "Unique sword input defaults to L");
            game.OpenSettings(); Check(game.SettingsOpen && game.Paused, "Opening options pauses combat");
            KarmaInput.BeginRebinding(KarmaAction.Pierce);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I)); InputSystem.Update();
            await Task.Delay(150); InputSystem.Update();
            Check(!KarmaInput.Rebinding && KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "I", "Keyboard capture rebinds the selected action");
            Check(PlayerPrefs.GetString(Bindings).Contains("/i"), "Rebinding is saved to preferences");
            Check(!KarmaInput.TrySetBinding(KarmaAction.Pierce, "<Keyboard>/j", out _), "Conflicting gameplay keys are rejected");
            Check(KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "I", "Conflict preserves the earlier key");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            KarmaInput.BeginRebinding(KarmaAction.Pierce);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
            Check(!KarmaInput.Rebinding && KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "I", "Escape cancels capture without changing the key");
            KarmaInput.ResetBindings(); Check(KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "L", "Reset restores default L binding");
            KarmaPreferences.Muted = false; KarmaPreferences.MasterVolume = 0.4f; KarmaPreferences.MusicVolume = 0.25f; KarmaPreferences.EffectsVolume = 0.75f;
            Check(Mathf.Approximately(AudioListener.volume, 0.4f) && Mathf.Approximately(KarmaPreferences.EffectiveMusicVolume, 0.1f)
                && Mathf.Approximately(KarmaPreferences.EffectiveEffectsVolume, 0.3f), "Master and channel volume values are applied consistently");
            KarmaPreferences.Muted = true; Check(AudioListener.volume == 0 && KarmaPreferences.EffectiveEffectsVolume == 0, "Mute silences audio");
            KarmaPreferences.Save(); Check(Mathf.Approximately(PlayerPrefs.GetFloat("K4RMA.Options.Music"), 0.25f), "Sound settings persist");
            game.CloseSettings(); Check(!game.SettingsOpen && !game.Paused, "Closing combat options resumes combat");
            game.TogglePause(); game.OpenSettings(); game.CloseSettings();
            Check(game.Paused, "Options preserve an existing pause"); game.TogglePause();
            // The unique action uses its actual configured input and still requires personalization.
            var p = game.Player; var f = BindingFlags.Instance | BindingFlags.NonPublic;
            p.ResetForRoom(new Vector2(3, KarmaSpace.PlayerHalfHeight), true);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U)); InputSystem.Update();
            typeof(KarmaPlayer).GetMethod("ReadUniqueSwordInput", f).Invoke(p, null);
            Check(Mathf.Abs(p.Position.x - 3) < 0.01f, "U stays locked before SwordWave personalization");
            p.Essences.Preview(Essence.SwordWave); p.Essences.Confirm(out _);
            typeof(KarmaPlayer).GetField("nextSword", f).SetValue(p, 0f);
            typeof(KarmaPlayer).GetMethod("ReadUniqueSwordInput", f).Invoke(p, null);
            Check(p.Position.x > 3.5f, "U executes personalized directional pierce");
            return "PASS: " + checks + " settings, interactive rebinding, conflicts, persistence, volume, pause and U-input assertions";
        }
        finally
        {
            KarmaInput.CancelRebinding(); InputSystem.RemoveDevice(keyboard);
            if (game.SettingsOpen) game.CloseSettings(); if (game.Paused) game.TogglePause();
            map.RemoveAllBindingOverrides(); map.LoadBindingOverridesFromJson(activeBindings);
            if (hadBindings) PlayerPrefs.SetString(Bindings, bindings); else PlayerPrefs.DeleteKey(Bindings);
            for (int i = 0; i < keys.Length; i++) { string k = "K4RMA.Options." + keys[i]; if (!existed[i]) PlayerPrefs.DeleteKey(k); else if (i == 4) PlayerPrefs.SetInt(k, (int)values[i]); else PlayerPrefs.SetFloat(k, values[i]); }
            KarmaPreferences.ApplyAudio(); PlayerPrefs.Save();
        }
    }
}
#endif
