using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // IMGUI keeps setup dependency-free. Replace this module with Canvas/TMP later.
    public sealed partial class KarmaHUD : MonoBehaviour
    {
        KarmaGame game;
        GUIStyle title, text, centered;
        Font runtimeFont;
        readonly string[] names = { "검기", "상승베기", "방어" };
        public void Initialize(KarmaGame director) { game = director; }
        Font ResolveFont()
        {
            if (game.Config.koreanFont != null) return game.Config.koreanFont;

            var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
            bool mac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
            var candidates = new List<string>(mac
                ? new[] { "Apple SD Gothic Neo", "Malgun Gothic", "맑은 고딕" }
                : new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo" });
            candidates.AddRange(new[] { "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Arial Unicode MS" });
            foreach (string candidate in candidates)
            {
                if (!installed.Contains(candidate)) continue;
                // Do not pass unavailable OS faces to Unity's font fallback chain.
                var font = Font.CreateDynamicFontFromOSFont(candidate, 18);
                if (font != null && font.HasCharacter('가'))
                {
                    runtimeFont = font;
                    return font;
                }
                if (font != null) Destroy(font);
            }
            Debug.LogWarning("K4RMA: 사용 가능한 한글 UI 폰트를 찾지 못했습니다. KarmaConfig의 한글 UI 폰트를 연결하세요.", this);
            return null;
        }
        void Styles()
        {
            if (title != null) return;
            Font font = ResolveFont();
            title = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            foreach (var s in new[] { title, text }) s.normal.textColor = new Color(0.9f, 0.91f, 0.94f);
            foreach (var s in new[] { title, text, centered }) if (font != null) s.font = font;
            ThemeStyles(font);
        }
        void OnGUI()
        {
            if (game == null) return;
            Styles();
            Matrix4x4 saved = GUI.matrix;
            DrawViewportBorders();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            UpdateHudView();
            if (game.Phase == RunPhase.Title)
                Art(FullView, "MenuBackground");
            if (hudView == HudView.Settings)
            {
                SettingsScreen(); GUI.matrix = saved; return;
            }
            if (hudView == HudView.Results)
            {
                EndScreen(game.Phase == RunPhase.Victory || game.EndingCompleted);
                FadeIntoMenu();
                GUI.matrix = saved; return;
            }
            if (game.Phase == RunPhase.Ending)
            {
                EndingScreen(); CinematicSkipHint(); GUI.matrix = saved; return;
            }
            if (hudView == HudView.Title) TitleScreen();
            else if (hudView == HudView.Choice) ChoiceScreen();
            else
            {
                if (game.Tutorial != null) TutorialScreen(); else TopBar();
                if (hudView == HudView.Pause) PauseScreen();
                else switch (game.Phase)
                {
                    case RunPhase.Transition: StageTransitionScreen(); break;
                    case RunPhase.Gate:
                        ThemePanel(new Rect(300, 525, 680, 110));
                        ThemeLabel(new Rect(325, 534, 640, 37), "시험 통과", rowText, Gold);
                        KeyPrompt(new Rect(325, 573, 640, 44), text, Ivory, false,
                            "오른쪽 수련문 앞에서  ", KarmaAction.Interact, "  고유화 선택");
                        break;
                }
            }
            if (hudView == HudView.Title || hudView == HudView.Choice) FadeIntoMenu();
            if (!string.IsNullOrEmpty(game.OpeningArrivalSpeech) && game.OpeningArrivalFade < 0.45f)
            {
                ThemePanel(new Rect(230, 550, 820, 140));
                BrushRule(252, 561, 775, Alpha(Gold, 0.62f));
                ThemeLabel(new Rect(255, 582, 770, 61), game.OpeningArrivalSpeech, text, Ivory);
                KeyPrompt(new Rect(255, 650, 770, 26), mapLabel, Gold, false, KarmaAction.DialogueConfirm, "  계속");
            }
            if (game.OpeningArrivalFade > 0) Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, game.OpeningArrivalFade));
            CinematicSkipHint();
            GUI.matrix = saved;
        }

        void OnDestroy()
        {
            if (runtimeFont != null) Destroy(runtimeFont);
            if (tutorialRouteArrow != null) Destroy(tutorialRouteArrow);
            ReleaseTheme();
        }

        static void Art(Rect bounds, string name, Rect? source = null)
        {
            var texture = KarmaArtwork.Texture(name);
            if (texture == null)
            {
                Fill(bounds, new Color(0.22f, 0.035f, 0.035f));
                GUI.Label(bounds, "이미지 파일 누락: " + name + "\nAssets 전체를 적용하세요.");
                return;
            }
            Rect crop = source ?? new Rect(0, 0, texture.width, texture.height);
            float ratio = crop.width / crop.height;
            float width = Mathf.Min(bounds.width, bounds.height * ratio);
            float height = width / ratio;
            var destination = new Rect(bounds.x + (bounds.width - width) * 0.5f,
                bounds.y + (bounds.height - height) * 0.5f, width, height);
            Color old = GUI.color; GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(destination, texture, KarmaArtwork.UV(texture, crop), true);
            GUI.color = old;
        }
        static void SpriteArt(Rect bounds, Sprite sprite, Color? tint = null)
        {
            if (sprite == null) return;
            float ratio = sprite.rect.width / sprite.rect.height;
            float width = Mathf.Min(bounds.width, bounds.height * ratio), height = width / ratio;
            var rect = new Rect(bounds.center.x - width / 2, bounds.center.y - height / 2, width, height);
            Color old = GUI.color; GUI.color = tint ?? Color.white;
            var source = sprite.rect;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, new Rect(source.x / sprite.texture.width,
                source.y / sprite.texture.height, source.width / sprite.texture.width, source.height / sprite.texture.height), true);
            GUI.color = old;
        }
        static string Clock(float seconds) { return ((int)seconds / 60) + ":" + ((int)seconds % 60).ToString("00"); }
        static void DrawViewportBorders()
        {
            Matrix4x4 saved = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                Rect view = KarmaGame.ScreenViewport(Screen.width, Screen.height);
                Fill(new Rect(0, 0, Screen.width, view.yMin), Color.black);
                Fill(new Rect(0, view.yMax, Screen.width, Screen.height - view.yMax), Color.black);
                Fill(new Rect(0, view.yMin, view.xMin, view.height), Color.black);
                Fill(new Rect(view.xMax, view.yMin, Screen.width - view.xMax, view.height), Color.black);
            }
            finally
            {
                GUI.matrix = saved;
            }
        }
        static void FillScreen(Color color)
        {
            Matrix4x4 saved = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                Fill(new Rect(0, 0, Screen.width, Screen.height), color);
            }
            finally
            {
                GUI.matrix = saved;
            }
        }
        static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
        }
    }
}
