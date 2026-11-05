using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public bool SettingsOpen { get; private set; }
        bool pausedBeforeSettings;
        public void OpenSettings()
        {
            if (SettingsOpen) return;
            pausedBeforeSettings = Paused; SettingsOpen = true; Paused = true;
        }
        public void CloseSettings()
        {
            KarmaInput.CancelRebinding(); KarmaPreferences.Save();
            SettingsOpen = false; Paused = pausedBeforeSettings;
        }
    }
}
