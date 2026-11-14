using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] BindingNames =
        {
            "왼쪽 이동", "오른쪽 이동", "위쪽 방향", "아래쪽 방향 / 앉기", "점프", "검 공격",
            "관통베기 · 검기 고유화", "반격", "검기 발사", "상승베기", "원형 방어"
        };
        bool displaySettingsTab;

        void SettingsScreen()
        {
            Fill(FullView, Alpha(DeepInk, 0.73f));
            if (game.DisplayConfirmationPending)
            {
                DisplayConfirmationScreen();
                return;
            }

            ThemeLabel(new Rect(76, 32, 650, 66), "환경설정", displayText, Ivory);
            BrushRule(78, 107, 1124 * ScreenReveal, Gold);
            ThemePanel(new Rect(76, 131, 610, 522), 0.92f);
            ThemePanel(new Rect(711, 131, 491, 522), 0.92f);
            ThemeLabel(new Rect(102, 145, 500, 44), "키 설정", rowText, Ivory);

            bool canRebind = !KarmaInput.Rebinding;
            for (int i = 0; i < KarmaInput.ConfigurableActions.Length; i++)
            {
                var action = KarmaInput.ConfigurableActions[i];
                float y = 198 + i * 35;
                if ((i & 1) == 0) BrushRule(109, y + 32, 542, Alpha(Ivory, 0.19f));
                ThemeLabel(new Rect(113, y, 374, 31), BindingNames[i], mapLabel, Ivory);
                if (ThemeButton(new Rect(475, y + 2, 172, 27), "", action, enabled: canRebind, compact: true))
                    KarmaInput.BeginRebinding(action);
            }
            if (ThemeButton(new Rect(109, 593, 239, 40), "키 기본값 복원", enabled: canRebind, compact: true))
                KarmaInput.ResetBindings();

            if (ThemeButton(new Rect(736, 151, 210, 43), "소리", !displaySettingsTab)) SelectSettingsTab(false);
            if (ThemeButton(new Rect(968, 151, 210, 43), "화면", displaySettingsTab)) SelectSettingsTab(true);
            BrushRule(737, 207, 439, Alpha(Ivory, 0.39f));
            if (displaySettingsTab) DisplaySettingsPanel();
            else
            {
                float master = KarmaPreferences.MasterVolume;
                float music = KarmaPreferences.MusicVolume;
                float effects = KarmaPreferences.EffectsVolume;
                float shake = KarmaPreferences.ScreenShake;
                float nextMaster = VolumeRow("전체 볼륨", 222, master);
                float nextMusic = VolumeRow("음악 볼륨", 295, music);
                float nextEffects = VolumeRow("효과음 볼륨", 368, effects);
                float nextShake = VolumeRow("화면 흔들림", 441, shake);
                if (!Mathf.Approximately(nextMaster, master)) KarmaPreferences.MasterVolume = nextMaster;
                if (!Mathf.Approximately(nextMusic, music)) KarmaPreferences.MusicVolume = nextMusic;
                if (!Mathf.Approximately(nextEffects, effects)) KarmaPreferences.EffectsVolume = nextEffects;
                if (!Mathf.Approximately(nextShake, shake)) KarmaPreferences.ScreenShake = nextShake;
                if (ThemeButton(new Rect(746, 514, 417, 40),
                    KarmaPreferences.Muted ? "음소거 켜짐" : "음소거 꺼짐", KarmaPreferences.Muted, compact: true))
                {
                    KarmaPreferences.Muted = !KarmaPreferences.Muted;
                    KarmaAudio.Play(KarmaSfxId.UiMove);
                }
            }

            if (KarmaInput.Rebinding)
                KeyPrompt(new Rect(111, 668, 647, 34), mapLabel, Gold, false,
                    "새 키를 누르세요.  ", KarmaAction.Pause, "  취소");
            else
                ThemeLabel(new Rect(111, 668, 647, 34),
                    KarmaInput.RebindMessage ?? "변경할 키를 선택한 뒤 새 키를 누르세요.", mapLabel, SoftIvory);
            if (ThemeButton(new Rect(737, 591, 441, 45), "저장하고 돌아가기", KarmaAction.Pause, true))
                game.CloseSettings();
            FadeIntoMenu();
        }

        void SelectSettingsTab(bool display)
        {
            if (displaySettingsTab == display) return;
            displaySettingsTab = display;
            GUIUtility.keyboardControl = 0;
            KarmaAudio.Play(KarmaSfxId.UiMove);
        }

        static string DisplayModeName(FullScreenMode mode)
        {
            if (mode == FullScreenMode.ExclusiveFullScreen) return "전체 화면";
            return mode == FullScreenMode.Windowed ? "창 모드" : "테두리 없는 전체 화면";
        }

        void DisplaySettingsPanel()
        {
            ThemeLabel(new Rect(746, 222, 419, 40), "화면 모드", rowText, Ivory);
            if (ThemeButton(new Rect(746, 269, 45, 42), "〈")) game.CycleDisplayMode(-1);
            if (ThemeButton(new Rect(1124, 269, 45, 42), "〉")) game.CycleDisplayMode(1);
            ThemeLabel(new Rect(792, 269, 331, 42), DisplayModeName(game.DisplayModes[game.SelectedDisplayMode]), actionText, Ivory);

            ThemeLabel(new Rect(746, 329, 419, 40), "해상도", rowText, Ivory);
            if (ThemeButton(new Rect(746, 375, 45, 42), "〈")) game.CycleDisplayResolution(-1);
            if (ThemeButton(new Rect(1124, 375, 45, 42), "〉")) game.CycleDisplayResolution(1);
            var size = game.DisplayResolutions[game.SelectedDisplayResolution];
            ThemeLabel(new Rect(792, 375, 331, 42), size.x + " × " + size.y, actionText, Ivory);
            ThemeLabel(new Rect(746, 425, 419, 53), "게임 화면은 16:9를 유지합니다.\n다른 비율에서는 검은 여백이 생깁니다.", text, SoftIvory);
            if (ThemeButton(new Rect(746, 484, 423, 43), "화면 설정 적용", true, !KarmaInput.Rebinding))
                game.ApplyDisplaySettings();
            if (Application.isEditor)
                ThemeLabel(new Rect(746, 540, 425, 31), "전체 화면·해상도 전환은 빌드에서 적용됩니다.", mapLabel, SoftIvory);
        }

        void DisplayConfirmationScreen()
        {
            ThemePanel(new Rect(302, 192, 676, 336));
            ThemeLabel(new Rect(333, 225, 614, 65), "화면 설정을 유지할까요?", modalTitle, Ivory);
            float seconds = game.DisplayConfirmationSeconds;
            ThemeLabel(new Rect(333, 300, 614, 45), Mathf.CeilToInt(seconds) + "초 뒤 이전 설정으로 돌아갑니다.",
                actionText, Ivory);
            ProgressTrack(new Rect(388, 365, 504, 3), Mathf.Clamp01(seconds / 15f));

            if (ThemeButton(new Rect(359, 415, 254, 58), "유지", true)) game.KeepDisplaySettings();
            if (ThemeButton(new Rect(667, 415, 254, 58), "되돌리기", KarmaAction.Pause)) game.RevertDisplaySettings();
        }

        float VolumeRow(string label, float y, float value)
        {
            ThemeLabel(new Rect(747, y, 318, 28), label, mapLabel, Ivory);
            ThemeLabel(new Rect(1090, y, 76, 28), Mathf.RoundToInt(value * 100) + "%", mapNumber, Gold);
            return SettingsSlider(new Rect(755, y + 38, 405, 24), value);
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

            float hover = HoverAmount(bounds);
            bool focused = GUIUtility.keyboardControl == id;
            var track = new Rect(bounds.x, bounds.center.y - 2, bounds.width, 4);
            ProgressTrack(track, value);

            float knob = focused || hover > 0 ? 17 : 14;
            SpriteArt(new Rect(bounds.x + bounds.width * value - knob / 2, bounds.center.y - knob / 2, knob, knob),
                KarmaArtwork.Sprite("UI", "Health"), Ivory);
            return value;
        }
    }
}
