using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] bindingNames = { "왼쪽 이동", "오른쪽 이동", "위쪽 방향", "아래쪽 방향 / 앉기", "점프", "검 공격", "검기", "관통베기", "상승베기", "방어", "반격" };
        bool displaySettingsTab;
        const float SettingsLeft = 100, SettingsRight = 680, SettingsColumnWidth = 500;
        [System.NonSerialized] GUIStyle settingsTitle, settingsHeading, settingsText, settingsCentered, settingsSmall, settingsButton;
        void SettingsStyles()
        {
            if (settingsHeading != null) return;
            settingsTitle = MakeSettingsStyle(title, TextAnchor.MiddleCenter);
            settingsHeading = MakeSettingsStyle(heading, TextAnchor.MiddleCenter);
            settingsText = MakeSettingsStyle(text, TextAnchor.MiddleLeft);
            settingsCentered = MakeSettingsStyle(text, TextAnchor.MiddleCenter);
            settingsSmall = MakeSettingsStyle(small, TextAnchor.MiddleCenter);
            settingsButton = MakeSettingsStyle(menuButton, TextAnchor.MiddleCenter);
        }

        static GUIStyle MakeSettingsStyle(GUIStyle source, TextAnchor alignment)
        {
            var style = new GUIStyle(source) { alignment = alignment };
            style.normal.textColor = style.focused.textColor = HudIvory;
            style.hover.textColor = style.active.textColor = HudBrass;
            return style;
        }

        void SettingsScreen()
        {
            SettingsStyles();
            DrawHudShade(0.72f);
            if (game.DisplayConfirmationPending)
            {
                DisplayConfirmationScreen();
                return;
            }
            Label(80, 35 + (1 - HudReveal) * 6, 1120, 60, "환경설정", settingsTitle);
            Panel(new Rect(81, 128, 538, 522));
            Panel(new Rect(661, 128, 538, 488));
            SpriteArt(new Rect(460, 110, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            Label(SettingsLeft, 135, SettingsColumnWidth, 40, "키 설정", settingsHeading);
            GUI.enabled = !KarmaInput.Rebinding;
            for (int i = 0; i < KarmaInput.ConfigurableActions.Length; i++)
            {
                var action = KarmaInput.ConfigurableActions[i]; float y = 185 + i * 34;
                Fill(new Rect(SettingsLeft, y + 32, SettingsColumnWidth, 1), WithOpacity(HudRule, 0.42f));
                Label(SettingsLeft, y, 330, 31, bindingNames[i], settingsText);
                if (KeyBindingButton(new Rect(450, y, 150, 31), action)) KarmaInput.BeginRebinding(action);
            }
            if (!KarmaInput.Rebinding
                && SettingsAction(new Rect(SettingsLeft, 595, SettingsColumnWidth, 40), "키 기본값 복원")) KarmaInput.ResetBindings();
            GUI.enabled = true;
            if (SettingsAction(new Rect(SettingsRight, 139, SettingsColumnWidth / 2 - 4, 38), "소리", !displaySettingsTab)) displaySettingsTab = false;
            if (SettingsAction(new Rect(SettingsRight + SettingsColumnWidth / 2 + 4, 139, SettingsColumnWidth / 2 - 4, 38), "화면", displaySettingsTab)) displaySettingsTab = true;
            if (displaySettingsTab) DisplaySettingsPanel();
            else
            {
                VolumeRow("전체 볼륨", 185, KarmaPreferences.MasterVolume, v => KarmaPreferences.MasterVolume = v);
                VolumeRow("음악 볼륨", 270, KarmaPreferences.MusicVolume, v => KarmaPreferences.MusicVolume = v);
                VolumeRow("효과음 볼륨", 355, KarmaPreferences.EffectsVolume, v => KarmaPreferences.EffectsVolume = v);
                VolumeRow("화면 흔들림", 440, KarmaPreferences.ScreenShake, v => KarmaPreferences.ScreenShake = v);
                if (SettingsAction(new Rect(SettingsRight, 535, SettingsColumnWidth, 40), KarmaPreferences.Muted ? "음소거 해제" : "음소거", KarmaPreferences.Muted)) KarmaPreferences.Muted = !KarmaPreferences.Muted;
            }
            if (KarmaInput.Rebinding)
            {
                KeyPrompt(new Rect(SettingsLeft, 595, SettingsColumnWidth, 23), 14, true, "키를 누르면 새 조작 키로 설정됩니다.");
                KeyPrompt(new Rect(SettingsLeft, 620, SettingsColumnWidth, 28), 14, true, KarmaAction.Pause, "  키 변경 취소");
            }
            else KeyPrompt(new Rect(SettingsLeft, 567, SettingsColumnWidth, 25), 14, true,
                KarmaInput.RebindMessage ?? "변경할 동작을 선택하면 새 키를 지정할 수 있습니다.");
            if (!KarmaInput.Rebinding
                && ShortcutButton(new Rect(790, 635, 390, 45), "저장하고 돌아가기", KarmaAction.Pause, true)) game.CloseSettings();
        }
        bool SettingsAction(Rect bounds, string label, bool selected = false, bool primary = false)
        {
            DrawHudButtonPlate(bounds, primary, selected, false);
            return GUI.Button(bounds, label, settingsButton);
        }

        static string DisplayModeName(FullScreenMode mode)
        {
            if (mode == FullScreenMode.ExclusiveFullScreen) return "전체 화면";
            return mode == FullScreenMode.Windowed ? "창 모드" : "전체 화면";
        }
        void DisplaySettingsPanel()
        {
            Label(SettingsRight, 220, SettingsColumnWidth, 32, "화면 모드", settingsHeading);
            if (SettingsAction(new Rect(SettingsRight, 267, 45, 45), "〈")) game.CycleDisplayMode(-1);
            Label(SettingsRight + 45, 267, SettingsColumnWidth - 90, 45, DisplayModeName(game.DisplayModes[game.SelectedDisplayMode]), settingsCentered);
            if (SettingsAction(new Rect(SettingsRight + SettingsColumnWidth - 45, 267, 45, 45), "〉")) game.CycleDisplayMode(1);
            Label(SettingsRight, 345, SettingsColumnWidth, 32, "해상도", settingsHeading);
            if (SettingsAction(new Rect(SettingsRight, 392, 45, 45), "〈")) game.CycleDisplayResolution(-1);
            var size = game.DisplayResolutions[game.SelectedDisplayResolution];
            Label(SettingsRight + 45, 392, SettingsColumnWidth - 90, 45, size.x + " × " + size.y, settingsCentered);
            if (SettingsAction(new Rect(SettingsRight + SettingsColumnWidth - 45, 392, 45, 45), "〉")) game.CycleDisplayResolution(1);
            Label(SettingsRight, 450, SettingsColumnWidth, 60, "게임 화면은 16:9를 유지합니다.\n다른 비율에서는 검은 여백이 생깁니다.", settingsSmall);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !KarmaInput.Rebinding;
            if (SettingsAction(new Rect(SettingsRight, 520, SettingsColumnWidth, 45), "화면 설정 적용", primary: true)) game.ApplyDisplaySettings();
            GUI.enabled = enabled;
        }
        void DisplayConfirmationScreen()
        {
            Panel(new Rect(257, 197, 766, 319));
            Label(240, 220, 800, 65, "화면 설정을 변경했습니다.", settingsTitle);
            Label(280, 310, 720, 65,
                Mathf.CeilToInt(game.DisplayConfirmationSeconds) + "초 뒤 이전 설정으로 돌아갑니다.", settingsCentered);
            if (ShortcutButton(new Rect(300, 430, 300, 55), "유지", KarmaAction.Confirm, true)) game.KeepDisplaySettings();
            if (ShortcutButton(new Rect(680, 430, 300, 55), "되돌리기", KarmaAction.Pause)) game.RevertDisplaySettings();
        }
        void VolumeRow(string label, float y, float value, System.Action<float> set)
        {
            Label(SettingsRight, y, SettingsColumnWidth, 30, label, settingsCentered);
            Label(SettingsRight, y + 29, SettingsColumnWidth, 22, Mathf.RoundToInt(value * 100) + "%", settingsSmall);
            var bounds = new Rect(SettingsRight + 25, y + 53, SettingsColumnWidth - 50, 28);
            float next = SettingsSlider(bounds, value);
            if (!Mathf.Approximately(next, value)) set(next);
        }
        float SettingsSlider(Rect bounds, float value)
        {
            int id = GUIUtility.GetControlID(0x4B34534C, FocusType.Keyboard, bounds);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !bounds.Contains(e.mousePosition)) break;
                    GUIUtility.hotControl = GUIUtility.keyboardControl = id;
                    value = Mathf.Clamp01((e.mousePosition.x - bounds.x) / bounds.width); e.Use(); break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    value = Mathf.Clamp01((e.mousePosition.x - bounds.x) / bounds.width); e.Use(); break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.KeyDown:
                    if (GUIUtility.keyboardControl != id || (e.keyCode != KeyCode.LeftArrow && e.keyCode != KeyCode.RightArrow)) break;
                    value = Mathf.Clamp01(value + (e.keyCode == KeyCode.RightArrow ? 0.05f : -0.05f)); e.Use(); break;
            }
            Color cream = HudBrass;
            Fill(new Rect(bounds.x, bounds.center.y - 1, bounds.width, 2), cream * new Color(1, 1, 1, 0.25f));
            Fill(new Rect(bounds.x, bounds.center.y - 1, bounds.width * value, 2), cream);
            SpriteArt(new Rect(bounds.x + bounds.width * value - 7, bounds.center.y - 7, 14, 14), KarmaArtwork.Sprite("UI", "FilledSlot"), cream);
            return value;
        }
    }
}
