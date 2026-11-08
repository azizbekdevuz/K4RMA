using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public bool SettingsOpen { get; private set; }
        bool pausedBeforeSettings;
        const string DisplayKey = "K4RMA.Options.Display.";
        readonly List<Vector2Int> displayResolutions = new List<Vector2Int>();
        public IReadOnlyList<Vector2Int> DisplayResolutions => displayResolutions;
        public FullScreenMode[] DisplayModes { get; private set; }
        public int SelectedDisplayMode { get; private set; }
        public int SelectedDisplayResolution { get; private set; }
        public bool DisplayConfirmationPending { get; private set; }
        public float DisplayConfirmationSeconds => Mathf.Max(0, displayConfirmUntil - Time.realtimeSinceStartup);
        float displayConfirmUntil;
        Vector2Int activeDisplaySize, previousDisplaySize;
        FullScreenMode activeDisplayMode, previousDisplayMode;

        void InitializeDisplaySettings()
        {
            bool windows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
            DisplayModes = windows
                ? new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed }
                : new[] { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
            activeDisplayMode = Screen.fullScreenMode;
            activeDisplaySize = new Vector2Int(Screen.width, Screen.height);
            if (!Application.isEditor)
            {
                var native = NativeDisplaySize();
                var mode = (FullScreenMode)PlayerPrefs.GetInt(DisplayKey + "Mode", (int)FullScreenMode.FullScreenWindow);
                if (System.Array.IndexOf(DisplayModes, mode) < 0) mode = FullScreenMode.FullScreenWindow;
                activeDisplayMode = mode;
                activeDisplaySize = new Vector2Int(PlayerPrefs.GetInt(DisplayKey + "Width", native.x),
                    PlayerPrefs.GetInt(DisplayKey + "Height", native.y));
                SelectActiveDisplay();
                // A saved monitor mode may no longer exist after changing displays.
                activeDisplaySize = displayResolutions[SelectedDisplayResolution];
                SetDisplay(activeDisplaySize, activeDisplayMode);
            }
            SelectActiveDisplay();
        }
        static Vector2Int NativeDisplaySize()
        {
            var resolution = Screen.currentResolution;
            return new Vector2Int(Mathf.Max(1, resolution.width), Mathf.Max(1, resolution.height));
        }
        void SelectActiveDisplay()
        {
            SelectedDisplayMode = Mathf.Max(0, System.Array.IndexOf(DisplayModes, activeDisplayMode));
            BuildDisplayResolutions(activeDisplaySize);
        }
        void BuildDisplayResolutions(Vector2Int preferred)
        {
            displayResolutions.Clear();
            var native = NativeDisplaySize();
            void Add(int width, int height)
            {
                var size = new Vector2Int(width, height);
                if (width > 0 && height > 0 && !displayResolutions.Contains(size)) displayResolutions.Add(size);
            }
            if (DisplayModes[SelectedDisplayMode] == FullScreenMode.Windowed)
            {
                foreach (int width in new[] { 960, 1280, 1600, 1920, 2560, 3840 })
                {
                    int height = width * 9 / 16;
                    if (width <= native.x && height <= native.y) Add(width, height);
                }
                if (activeDisplaySize.x <= native.x && activeDisplaySize.y <= native.y)
                    Add(activeDisplaySize.x, activeDisplaySize.y);
            }
            else
            {
                foreach (var resolution in Screen.resolutions) Add(resolution.width, resolution.height);
            }
            Add(native.x, native.y);
            displayResolutions.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            SelectedDisplayResolution = displayResolutions.IndexOf(preferred);
            if (SelectedDisplayResolution < 0) SelectedDisplayResolution = displayResolutions.IndexOf(native);
        }
        public void CycleDisplayMode(int direction)
        {
            if (DisplayConfirmationPending) return;
            var preferred = displayResolutions[SelectedDisplayResolution];
            SelectedDisplayMode = (SelectedDisplayMode + direction + DisplayModes.Length) % DisplayModes.Length;
            BuildDisplayResolutions(preferred);
        }
        public void CycleDisplayResolution(int direction)
        {
            if (DisplayConfirmationPending) return;
            SelectedDisplayResolution = (SelectedDisplayResolution + direction + displayResolutions.Count) % displayResolutions.Count;
        }
        public void ApplyDisplaySettings()
        {
            if (!SettingsOpen || DisplayConfirmationPending) return;
            // Capture the actual window too, in case it was resized manually.
            previousDisplaySize = Application.isEditor ? activeDisplaySize : new Vector2Int(Screen.width, Screen.height);
            previousDisplayMode = Application.isEditor ? activeDisplayMode : Screen.fullScreenMode;
            activeDisplaySize = displayResolutions[SelectedDisplayResolution];
            activeDisplayMode = DisplayModes[SelectedDisplayMode];
            DisplayConfirmationPending = true;
            displayConfirmUntil = Time.realtimeSinceStartup + 15;
            SetDisplay(activeDisplaySize, activeDisplayMode);
        }
        static void SetDisplay(Vector2Int size, FullScreenMode mode)
        {
            // The Editor's Game view is a preview; only a standalone player changes OS window modes.
            if (!Application.isEditor) Screen.SetResolution(size.x, size.y, mode);
        }
        public void KeepDisplaySettings()
        {
            if (!DisplayConfirmationPending) return;
            if (DisplayConfirmationSeconds <= 0) { RevertDisplaySettings(); return; }
            DisplayConfirmationPending = false;
            PlayerPrefs.SetInt(DisplayKey + "Width", activeDisplaySize.x);
            PlayerPrefs.SetInt(DisplayKey + "Height", activeDisplaySize.y);
            PlayerPrefs.SetInt(DisplayKey + "Mode", (int)activeDisplayMode);
            PlayerPrefs.Save();
        }
        public void RevertDisplaySettings()
        {
            if (!DisplayConfirmationPending) return;
            DisplayConfirmationPending = false;
            activeDisplaySize = previousDisplaySize;
            activeDisplayMode = previousDisplayMode;
            SetDisplay(activeDisplaySize, activeDisplayMode);
            SelectActiveDisplay();
        }
        void UpdateDisplaySettings()
        {
            if (DisplayConfirmationPending && DisplayConfirmationSeconds <= 0) RevertDisplaySettings();
        }
        public void OpenSettings()
        {
            if (SettingsOpen) return;
            if (!Application.isEditor)
            {
                activeDisplaySize = new Vector2Int(Screen.width, Screen.height);
                activeDisplayMode = Screen.fullScreenMode;
            }
            SelectActiveDisplay();
            pausedBeforeSettings = Paused; SettingsOpen = true; Paused = true;
            KarmaAudio.Play(KarmaSfxId.UiConfirm);
        }
        public void CloseSettings()
        {
            if (SettingsOpen) KarmaAudio.Play(KarmaSfxId.UiCancel);
            RevertDisplaySettings();
            KarmaInput.CancelRebinding(); KarmaPreferences.Save();
            SettingsOpen = false; Paused = pausedBeforeSettings;
        }
    }
}
