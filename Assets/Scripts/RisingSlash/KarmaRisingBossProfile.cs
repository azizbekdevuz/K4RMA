using UnityEngine;
namespace KarmaPrototype
{
    [CreateAssetMenu(menuName = "K4RMA/Bosses/RisingSlash Settings")]
    public sealed class KarmaRisingBossProfile : ScriptableObject
    {
        public KarmaRisingBossSettings settings = new KarmaRisingBossSettings();
        public static KarmaRisingBossSettings LoadSettings()
        {
            var profile = Resources.Load<KarmaRisingBossProfile>("KarmaRisingBossProfile");
            // Each game config gets a private copy; play-mode tuning never edits the asset.
            return profile == null ? new KarmaRisingBossSettings()
                : JsonUtility.FromJson<KarmaRisingBossSettings>(JsonUtility.ToJson(profile.settings));
        }
    }
}
