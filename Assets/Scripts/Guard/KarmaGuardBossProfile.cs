using UnityEngine;
namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/Bosses/Guard Settings")]
    public sealed class KarmaGuardBossProfile : ScriptableObject
    {
        public KarmaGuardBossSettings settings = new KarmaGuardBossSettings();
        public static KarmaGuardBossSettings LoadSettings()
        {
            var profile = Resources.Load<KarmaGuardBossProfile>("KarmaGuardBossProfile");
            // Each game config gets a private copy; play-mode tuning never edits the asset.
            return profile == null ? new KarmaGuardBossSettings()
                : JsonUtility.FromJson<KarmaGuardBossSettings>(JsonUtility.ToJson(profile.settings));
        }
    }
}
