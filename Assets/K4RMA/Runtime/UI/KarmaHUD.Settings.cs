using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] bindingNames = { "왼쪽 이동", "오른쪽 이동", "위쪽 방향", "아래쪽 방향 / 앉기", "점프 / 더블점프", "검 공격 / 공중 베기", "관통베기 · 검기 고유화", "반격", "검기 발사", "상승베기", "원형 방어" };
        void SettingsScreen()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0.015f, 0.025f, 0.07f, 0.94f));
            Label(80, 35, 1120, 60, "환경설정", title);
            Label(85, 105, 680, 35, "키 설정", heading);
            GUI.enabled = !KarmaInput.Rebinding;
            for (int i = 0; i < KarmaInput.ConfigurableActions.Length; i++)
            {
                var action = KarmaInput.ConfigurableActions[i]; float y = 150 + i * 37;
                Label(90, y, 360, 31, bindingNames[i], text);
                if (GUI.Button(new Rect(455, y, 270, 31), KarmaInput.BindingLabel(action), keyButton)) KarmaInput.BeginRebinding(action);
            }
            if (GUI.Button(new Rect(90, 585, 300, 40), "키 기본값 복원", button)) KarmaInput.ResetBindings();
            GUI.enabled = true;
            Label(790, 105, 390, 35, "소리와 화면", heading);
            VolumeRow("전체 볼륨", 160, KarmaPreferences.MasterVolume, v => KarmaPreferences.MasterVolume = v);
            VolumeRow("음악 볼륨", 245, KarmaPreferences.MusicVolume, v => KarmaPreferences.MusicVolume = v);
            VolumeRow("효과음 볼륨", 330, KarmaPreferences.EffectsVolume, v => KarmaPreferences.EffectsVolume = v);
            VolumeRow("화면 흔들림", 415, KarmaPreferences.ScreenShake, v => KarmaPreferences.ScreenShake = v);
            if (GUI.Button(new Rect(795, 505, 360, 40), KarmaPreferences.Muted ? "음소거 : 켜짐" : "음소거 : 꺼짐", button)) KarmaPreferences.Muted = !KarmaPreferences.Muted;
            Label(790, 560, 390, 42, KarmaInput.RebindMessage ?? "변경할 키를 누른 뒤 새 키를 입력하세요.", small);
            if (GUI.Button(new Rect(790, 625, 390, 45), "저장하고 돌아가기 [Esc]", button)) game.CloseSettings();
            Label(90, 640, 630, 38, "Esc : 돌아가기 / 키 변경 취소     O : 환경설정", small);
        }
        void VolumeRow(string label, float y, float value, System.Action<float> set)
        {
            Label(795, y, 290, 30, label, text);
            Label(1085, y, 75, 30, Mathf.RoundToInt(value * 100) + "%", small);
            float next = GUI.HorizontalSlider(new Rect(800, y + 43, 350, 24), value, 0, 1);
            if (!Mathf.Approximately(next, value)) set(next);
        }
    }
}
