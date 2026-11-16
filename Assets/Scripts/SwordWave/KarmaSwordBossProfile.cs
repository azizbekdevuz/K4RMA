using UnityEngine;
namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/Bosses/SwordWave Settings")]
    public sealed class KarmaSwordBossProfile : ScriptableObject
    {
        public KarmaSwordBossSettings settings = new KarmaSwordBossSettings();
        public static KarmaSwordBossSettings LoadSettings()
        {
            var profile = Resources.Load<KarmaSwordBossProfile>("KarmaSwordBossProfile");
            // Each game config gets a private copy; play-mode tuning never edits the asset.
            return profile == null ? new KarmaSwordBossSettings()
                : JsonUtility.FromJson<KarmaSwordBossSettings>(JsonUtility.ToJson(profile.settings));
        }
    }
}
