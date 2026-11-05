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
            Check(KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "U", "Unique sword input defaults to U");
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
            KarmaInput.ResetBindings(); Check(KarmaInput.BindingLabel(KarmaAction.Pierce).ToUpperInvariant() == "U", "Reset restores default U binding");
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
