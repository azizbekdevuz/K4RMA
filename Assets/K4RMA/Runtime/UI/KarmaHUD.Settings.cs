using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] bindingNames = { "왼쪽 이동", "오른쪽 이동", "위쪽 방향", "아래쪽 방향 / 앉기", "점프", "검 공격", "관통베기 · 검기 고유화", "반격", "검기 발사", "상승베기", "원형 방어" };
        void SettingsScreen()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0.015f, 0.025f, 0.07f, 0.68f));
            Label(80, 35, 1120, 60, "환경설정", title);
            SpriteArt(new Rect(460, 110, 360, 18), KarmaArtwork.Sprite("UI", "LongRule"));
            Label(95, 140, 620, 35, "키 설정", heading);
            GUI.enabled = !KarmaInput.Rebinding;
            for (int i = 0; i < KarmaInput.ConfigurableActions.Length; i++)
            {
                var action = KarmaInput.ConfigurableActions[i]; float y = 185 + i * 34;
                Label(100, y, 360, 31, bindingNames[i], text);
                if (GUI.Button(new Rect(490, y, 210, 31), "〈 " + KarmaInput.BindingLabel(action) + " 〉", keyButton)) KarmaInput.BeginRebinding(action);
            }
            if (GUI.Button(new Rect(110, 595, 570, 40), "키 기본값 복원", menuButton)) KarmaInput.ResetBindings();
            GUI.enabled = true;
            Label(790, 140, 390, 35, "소리와 화면", heading);
            VolumeRow("전체 볼륨", 185, KarmaPreferences.MasterVolume, v => KarmaPreferences.MasterVolume = v);
            VolumeRow("음악 볼륨", 270, KarmaPreferences.MusicVolume, v => KarmaPreferences.MusicVolume = v);
            VolumeRow("효과음 볼륨", 355, KarmaPreferences.EffectsVolume, v => KarmaPreferences.EffectsVolume = v);
            VolumeRow("화면 흔들림", 440, KarmaPreferences.ScreenShake, v => KarmaPreferences.ScreenShake = v);
            if (GUI.Button(new Rect(795, 525, 360, 40), KarmaPreferences.Muted ? "◆ 음소거 켜짐" : "◇ 음소거 꺼짐", menuButton)) KarmaPreferences.Muted = !KarmaPreferences.Muted;
            Label(790, 580, 390, 42, KarmaInput.RebindMessage ?? "변경할 키를 선택한 뒤 새 키를 누르세요.", small);
            if (GUI.Button(new Rect(790, 635, 390, 45), "저장하고 돌아가기 [Esc]", menuButton)) game.CloseSettings();
            Label(100, 650, 620, 28, "Esc : 돌아가기 / 키 변경 취소", small);
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
