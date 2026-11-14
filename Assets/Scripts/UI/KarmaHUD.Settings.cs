using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] bindingNames = { "왼쪽 이동", "오른쪽 이동", "위쪽 방향", "아래쪽 방향 / 앉기", "점프", "검 공격", "관통베기 · 검기 고유화", "반격", "검기 발사", "상승베기", "원형 방어" };
        bool displaySettingsTab;
        void SettingsScreen()
        {
            FillScreen(new Color(0.015f, 0.025f, 0.07f, 0.68f));
            if (game.DisplayConfirmationPending)
            {
                DisplayConfirmationScreen();
                return;
            }
            Label(80, 35, 1120, 60, "환경설정", title);
            SpriteArt(new Rect(460, 110, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            Label(95, 140, 620, 35, "키 설정", heading);
            GUI.enabled = !KarmaInput.Rebinding;
            for (int i = 0; i < KarmaInput.ConfigurableActions.Length; i++)
            {
                var action = KarmaInput.ConfigurableActions[i]; float y = 185 + i * 34;
                Label(100, y, 360, 31, bindingNames[i], text);
                if (KeyBindingButton(new Rect(490, y, 210, 31), action)) KarmaInput.BeginRebinding(action);
            }
            if (GUI.Button(new Rect(110, 595, 570, 40), "키 기본값 복원", menuButton)) KarmaInput.ResetBindings();
            GUI.enabled = true;
            if (GUI.Button(new Rect(790, 135, 190, 40), (displaySettingsTab ? "" : "◆ ") + "소리", menuButton)) displaySettingsTab = false;
            if (GUI.Button(new Rect(985, 135, 190, 40), (displaySettingsTab ? "◆ " : "") + "화면", menuButton)) displaySettingsTab = true;
            if (displaySettingsTab) DisplaySettingsPanel();
            else
            {
                VolumeRow("전체 볼륨", 185, KarmaPreferences.MasterVolume, v => KarmaPreferences.MasterVolume = v);
                VolumeRow("음악 볼륨", 270, KarmaPreferences.MusicVolume, v => KarmaPreferences.MusicVolume = v);
                VolumeRow("효과음 볼륨", 355, KarmaPreferences.EffectsVolume, v => KarmaPreferences.EffectsVolume = v);
                VolumeRow("화면 흔들림", 440, KarmaPreferences.ScreenShake, v => KarmaPreferences.ScreenShake = v);
                if (GUI.Button(new Rect(795, 525, 360, 40), KarmaPreferences.Muted ? "◆ 음소거 켜짐" : "◇ 음소거 꺼짐", menuButton)) KarmaPreferences.Muted = !KarmaPreferences.Muted;
            }
            if (KarmaInput.Rebinding)
            {
                Label(790, 574, 390, 23, "새 키를 누르세요.", small);
                KeyPrompt(new Rect(790, 597, 390, 25), 14, false, KarmaAction.Pause, "  키 변경 취소");
            }
            else Label(790, 580, 390, 42, KarmaInput.RebindMessage ?? "변경할 키를 선택한 뒤 새 키를 누르세요.", small);
            if (ShortcutButton(new Rect(790, 635, 390, 45), "저장하고 돌아가기", KarmaAction.Pause)) game.CloseSettings();
            KeyPrompt(new Rect(100, 650, 620, 28), 14, false, KarmaAction.Pause, "  돌아가기 / 키 변경 취소");
        }
        static string DisplayModeName(FullScreenMode mode)
        {
            if (mode == FullScreenMode.ExclusiveFullScreen) return "전체 화면";
            return mode == FullScreenMode.Windowed ? "창 모드" : "테두리 없는 전체 화면";
        }
        void DisplaySettingsPanel()
        {
            Label(795, 195, 370, 32, "화면 모드", heading);
            if (GUI.Button(new Rect(790, 242, 45, 45), "〈", menuButton)) game.CycleDisplayMode(-1);
            Label(835, 242, 295, 45, DisplayModeName(game.DisplayModes[game.SelectedDisplayMode]), centered);
            if (GUI.Button(new Rect(1130, 242, 45, 45), "〉", menuButton)) game.CycleDisplayMode(1);
            Label(795, 320, 370, 32, "해상도", heading);
            if (GUI.Button(new Rect(790, 367, 45, 45), "〈", menuButton)) game.CycleDisplayResolution(-1);
            var size = game.DisplayResolutions[game.SelectedDisplayResolution];
            Label(835, 367, 295, 45, size.x + " × " + size.y, centered);
            if (GUI.Button(new Rect(1130, 367, 45, 45), "〉", menuButton)) game.CycleDisplayResolution(1);
            Label(795, 425, 370, 60, "게임 화면은 16:9를 유지합니다.\n다른 비율에서는 검은 여백이 생깁니다.", small);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !KarmaInput.Rebinding;
            if (GUI.Button(new Rect(795, 495, 370, 45), "화면 설정 적용", menuButton)) game.ApplyDisplaySettings();
            GUI.enabled = enabled;
            if (Application.isEditor) Label(795, 540, 370, 38, "전체 화면·해상도 전환은 빌드에서 적용됩니다.", small);
        }
        void DisplayConfirmationScreen()
        {
            Label(240, 220, 800, 65, "화면 설정을 유지할까요?", title);
            Label(280, 310, 720, 65,
                Mathf.CeilToInt(game.DisplayConfirmationSeconds) + "초 뒤 이전 설정으로 돌아갑니다.", centered);
            if (GUI.Button(new Rect(300, 430, 300, 55), "유지", menuButton)) game.KeepDisplaySettings();
            if (ShortcutButton(new Rect(680, 430, 300, 55), "되돌리기", KarmaAction.Pause)) game.RevertDisplaySettings();
        }
        void VolumeRow(string label, float y, float value, System.Action<float> set)
        {
            Label(795, y, 290, 30, label, text);
            Label(1085, y, 75, 30, Mathf.RoundToInt(value * 100) + "%", small);
            var bounds = new Rect(800, y + 35, 350, 28);
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
            Color cream = menuButton.normal.textColor;
            Fill(new Rect(bounds.x, bounds.center.y - 1, bounds.width, 2), cream * new Color(1, 1, 1, 0.25f));
            Fill(new Rect(bounds.x, bounds.center.y - 1, bounds.width * value, 2), cream);
            SpriteArt(new Rect(bounds.x + bounds.width * value - 7, bounds.center.y - 7, 14, 14), KarmaArtwork.Sprite("UI", "FilledSlot"), cream);
            return value;
        }
    }
}
