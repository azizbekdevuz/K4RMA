using UnityEngine;
namespace KarmaPrototype
{
    public static class KarmaPreferences
    {
        const string Prefix = "K4RMA.Options.";
        public static float MasterVolume { get => Get("Master", 1); set { Put("Master", value); ApplyAudio(); } }
        public static float MusicVolume { get => Get("Music", 0.8f); set => Put("Music", value); }
        public static float EffectsVolume { get => Get("Effects", 1); set => Put("Effects", value); }
        public static float ScreenShake { get => Get("Shake", 1); set => Put("Shake", value); }
        public static bool Muted { get => PlayerPrefs.GetInt(Prefix + "Muted", 0) != 0; set { PlayerPrefs.SetInt(Prefix + "Muted", value ? 1 : 0); ApplyAudio(); } }
        public static float EffectiveMusicVolume => Muted ? 0 : MasterVolume * MusicVolume;
        public static float EffectiveEffectsVolume => Muted ? 0 : MasterVolume * EffectsVolume;
        static float Get(string key, float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, fallback));
        static void Put(string key, float value) => PlayerPrefs.SetFloat(Prefix + key, Mathf.Clamp01(value));
        public static void ApplyAudio() { AudioListener.volume = Muted ? 0 : MasterVolume; }
        public static void Save() { PlayerPrefs.Save(); }
    }
}
