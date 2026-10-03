using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // IMGUI keeps setup dependency-free. Replace this module with Canvas/TMP later.
    public sealed partial class KarmaHUD : MonoBehaviour
    {
        KarmaGame game;
        GUIStyle title, heading, text, small, button;
        Font runtimeFont;
        readonly string[] names = { "화염", "돌진", "보호막" };
        readonly string[] keys = { "Q", "SHIFT", "E" };
        public void Initialize(KarmaGame director) { game = director; }
        void Styles()
        {
            if (title != null) return;
            Font font = game.Config.koreanFont;
            if (font == null)
            {
                runtimeFont = Font.CreateDynamicFontFromOSFont(new[] {
                    "Apple SD Gothic Neo", "Malgun Gothic", "맑은 고딕",
                    "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial Unicode MS"
                }, 18);
                font = runtimeFont;
            }
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true, padding = new RectOffset(15, 15, 10, 10) };
            foreach (var s in new[] { title, heading, text, small }) s.normal.textColor = new Color(0.9f, 0.91f, 0.94f);
            foreach (var s in new[] { title, heading, text, small, button }) if (font != null) s.font = font;
        }
        void OnGUI()
        {
            if (game == null) return;
            Styles();
            Matrix4x4 saved = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            if (game.Phase == RunPhase.Title) TitleScreen();
            else
            {
                TopBar();
                if (game.Paused) PauseScreen();
                else switch (game.Phase)
                {
                    case RunPhase.Choosing: ChoiceScreen(); break;
                    case RunPhase.Transition: TransitionScreen(); break;
                    case RunPhase.Defeat: EndScreen(false); break;
                    case RunPhase.Victory: EndScreen(true); break;
                    case RunPhase.Gate:
                        Panel(new Rect(300, 525, 680, 110));
                        Label(325, 539, 640, 35, "시험 통과", heading);
                        Label(325, 578, 640, 44, "오른쪽 수련문으로 이동한 뒤 F를 눌러 무의식화할 기술을 고르세요.", text);
                        break;
                }
                Label(30, 675, 1220, 30, "A/D·방향키 이동    Space 점프    J 검    Q 화염    Shift 돌진    E 보호막    Esc 일시정지", small);
            }
            GUI.matrix = saved;
        }

        void OnDestroy() { if (runtimeFont != null) Destroy(runtimeFont); }

        void Label(float x, float y, float w, float h, string value, GUIStyle style) { GUI.Label(new Rect(x, y, w, h), value, style); }
        static string Clock(float seconds) { return ((int)seconds / 60) + ":" + ((int)seconds % 60).ToString("00"); }
        static void Panel(Rect rect) { Fill(rect, new Color(0.045f, 0.052f, 0.09f, 0.95f)); }
        static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
        static void Bar(Rect r, float fraction, Color c)
        {
            Fill(r, new Color(0.19f, 0.2f, 0.25f));
            r.width *= Mathf.Clamp01(fraction); Fill(r, c);
        }
    }
}
