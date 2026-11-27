using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // IMGUI keeps setup dependency-free. Replace this module with Canvas/TMP later.
    public sealed partial class KarmaHUD : MonoBehaviour
    {
        KarmaGame game;
        GUIStyle title, heading, text, small, menuButton, centered;
        Font runtimeFont;
        Texture2D hudShade;
        int hudView = -1;
        float hudViewAt;
        Rect hoverBounds;
        float hoverAt;
        static readonly Color HudInk = new Color32(15, 22, 32, 245);
        static readonly Color HudBrass = new Color32(226, 196, 138, 255);
        static readonly Color HudMuted = new Color32(203, 196, 183, 255);
        static readonly Color HudRule = new Color32(185, 150, 96, 120);
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
            heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            menuButton = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            menuButton.normal.textColor = new Color(0.92f, 0.9f, 0.82f);
            menuButton.hover.textColor = Color.white;
            menuButton.active.textColor = Color.white;
            menuButton.focused.textColor = menuButton.normal.textColor;
            foreach (var s in new[] { title, heading, text, small }) s.normal.textColor = HudIvory;
            centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            foreach (var s in new[] { title, heading, text, small, menuButton, centered }) if (font != null) s.font = font;
        }
        void OnGUI()
        {
            if (game == null) return;
            Styles();
            int view = game.SettingsOpen ? game.DisplayConfirmationPending ? 101 : 100
                : game.Paused ? 102 : game.EndingCompleted ? 103 : (int)game.Phase;
            if (hudView != view)
            {
                hudView = view;
                hudViewAt = Time.unscaledTime;
                hoverBounds = default;
                if (view == (int)RunPhase.Choosing) choicePreviewRoute = 0;
            }
            Matrix4x4 saved = GUI.matrix;
            DrawViewportBorders();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            if (game.Phase == RunPhase.Title)
                Art(new Rect(0, 0, 1280, 720), "MenuBackground");
            if (game.SettingsOpen)
            {
                SettingsScreen(); GUI.matrix = saved; return;
            }
            if (game.Phase == RunPhase.Ending || game.EndingCompleted)
            { if (game.Phase == RunPhase.Ending) TopBar(); EndingScreen(); CinematicSkipHint(); GUI.matrix = saved; return; }
            if (game.Phase == RunPhase.Title) TitleScreen();
            else
            {
                if (game.Tutorial != null) TutorialScreen();
                else if (game.Phase != RunPhase.Choosing && game.Phase != RunPhase.Defeat && game.Phase != RunPhase.Victory)
                    TopBar();
                if (game.Paused) DrawPauseOverlay();
                else switch (game.Phase)
                {
                    case RunPhase.GuardianFallen:
                        if (game.StageIndex < 3)
                        {
                            Panel(new Rect(245, 589, 790, 55));
                            DrawSpeakerNameplate(263, 562, "수호자");
                            Label(265, 592, 750, 48, game.GuardianLearningResult, centered);
                        }
                        break;
                    case RunPhase.Choosing: ChoiceScreen(); break;
                    case RunPhase.Transition:
                        StageTransitionScreen();
                        if (!game.GuardianInheriting && game.Player != null)
                        {
                            Panel(new Rect(390, 515, 500, 67));
                            Label(405, 519, 470, 55, "자동 고유화 · " + MasteryNames[(int)game.LastPersonalizedEssence]
                                + "  " + game.Player.Essences.Mastery(game.LastPersonalizedEssence), centered);
                        }
                        break;
                    case RunPhase.PlayerFallen: PlayerDefeatScreen(); break;
                    case RunPhase.Defeat: EndScreen(false); break;
                    case RunPhase.Victory: EndScreen(true); break;
                    case RunPhase.Gate:
                        Panel(new Rect(300, 525, 680, 110));
                        Label(325, 539, 640, 35, "시험 통과", heading);
                        KeyPrompt(new Rect(325, 578, 640, 44), 18, false,
                            "오른쪽 수련문 앞에서  ", KarmaAction.Interact, "  키를 누르면 고유화할 기술을 선택할 수 있습니다.");
                        break;
                }
            }
            if (!game.Paused && !string.IsNullOrEmpty(game.OpeningArrivalSpeech) && game.OpeningArrivalFade < 0.45f)
            {
                Panel(new Rect(230, 550, 820, 140));
                DrawSpeakerNameplate(255, 523, "제자");
                Label(255, 560, 770, 75, game.OpeningArrivalSpeech, text);
                KeyPrompt(new Rect(255, 655, 770, 25), 14, false, KarmaAction.DialogueConfirm, "  계속");
            }
            if (game.OpeningArrivalFade > 0) Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, game.OpeningArrivalFade));
            CinematicSkipHint();
            GUI.matrix = saved;
        }

        void OnDestroy()
        {
            ((IDisposable)keycapText).Dispose();
            if (runtimeFont != null) Destroy(runtimeFont);
            if (hudShade != null) Destroy(hudShade);
            if (tutorialRouteArrow != null) Destroy(tutorialRouteArrow);
        }

        static void Art(Rect bounds, string name, Rect? source = null)
        {
            var texture = KarmaArtwork.Texture(name);
            if (texture == null)
            {
                Fill(bounds, new Color(0.22f, 0.035f, 0.035f));
                GUI.Label(bounds, "이미지 파일이 누락되었습니다. " + name + "\nAssets 전체를 적용해야 합니다.");
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
        void Label(float x, float y, float w, float h, string value, GUIStyle style) { GUI.Label(new Rect(x, y, w, h), value, style); }
        static string Clock(float seconds) { return ((int)seconds / 60) + ":" + ((int)seconds % 60).ToString("00"); }
        GUIStyle speakerNameplate;

        void DrawSpeakerNameplate(float x, float y, string speaker, float opacity = 1f)
        {
            if (speakerNameplate == null)
                speakerNameplate = new GUIStyle(centered) { fontSize = 16, fontStyle = FontStyle.Bold };

            var accent = (Color32)HudBrass;
            speakerNameplate.normal.textColor = accent;
            var tab = new Rect(x, y, 88, 28);
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(opacity) * 255f);
            Fill(tab, new Color32(31, 27, 27, alpha));
            accent.a = alpha;
            Fill(new Rect(tab.x, tab.y, tab.width, 2), accent);
            Color saved = GUI.color;
            GUI.color = new Color(saved.r, saved.g, saved.b, saved.a * opacity);
            GUI.Label(tab, speaker, speakerNameplate);
            GUI.color = saved;
        }

        const float DialoguePanelOpacity = 0.7f;
        static void Panel(Rect rect, float opacity = 1)
        {
            Fill(rect, WithOpacity(HudInk, 0.93f * opacity));
            Fill(new Rect(rect.x, rect.y, rect.width, 1), WithOpacity(HudRule, opacity));
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), WithOpacity(HudRule, opacity));
        }

        static Color WithOpacity(Color color, float opacity)
        {
            color.a *= opacity;
            return color;
        }

        float HudReveal => Ease((Time.unscaledTime - hudViewAt) / 0.22f);

        void DrawHudShade(float density)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (hudShade == null)
            {
                hudShade = new Texture2D(1, 64, TextureFormat.RGBA32, false)
                    { name = "K4RMA HUD shade", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[64];
                for (int y = 0; y < pixels.Length; y++)
                    pixels[y] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Ease(1 - y / 63f)));
                hudShade.SetPixels32(pixels);
                hudShade.Apply(false, true);
            }
            float appear = Mathf.Lerp(0.68f, 1, HudReveal);
            Fill(new Rect(0, 0, 1280, 720), WithOpacity(HudInk, density * appear));
            var color = GUI.color;
            GUI.color = WithOpacity(HudInk, 0.83f * appear);
            GUI.DrawTexture(new Rect(0, 0, 1280, 720), hudShade);
            GUI.color = color;
        }

        void DrawHudButtonPlate(Rect rect, bool primary, bool selected, bool pressed)
        {
            if (Event.current.type != EventType.Repaint) return;
            bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
            if (hovered && hoverBounds != rect)
            {
                hoverBounds = rect;
                hoverAt = Time.unscaledTime;
            }
            else if (!hovered && hoverBounds == rect) hoverBounds = default;
            float hover = hovered ? Ease((Time.unscaledTime - hoverAt) / 0.12f) : 0;
            float emphasis = GUI.enabled ? (pressed && (primary || selected || hovered) ? 1 : hover) : 0;
            var surface = primary ? new Color32(75, 53, 34, 255)
                : selected ? new Color32(49, 41, 37, 255) : HudInk;
            Fill(rect, WithOpacity(Color.Lerp(surface, new Color32(100, 70, 43, 255), emphasis * 0.5f),
                GUI.enabled ? 1 : 0.6f));
            var rule = primary || selected ? HudBrass : Color.Lerp(HudRule, HudBrass, hover);
            Fill(new Rect(rect.x, rect.y, rect.width, 1), rule);
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), rule);
            Fill(new Rect(rect.x, rect.y, selected ? 3 : 1, rect.height), rule);
            Fill(new Rect(rect.xMax - 1, rect.y, 1, rect.height), rule);
        }
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
