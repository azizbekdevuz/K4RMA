using UnityEngine;
using UnityEngine.InputSystem;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        enum HudView { Play, Title, Choice, Pause, Settings, Ending, Results }

        static readonly Rect FullView = new Rect(0, 0, 1280, 720);
        static readonly Color Ink = new Color32(14, 24, 40, 255);
        static readonly Color DeepInk = new Color32(7, 14, 26, 255);
        static readonly Color Ivory = new Color32(248, 232, 201, 255);
        static readonly Color SoftIvory = new Color32(196, 188, 177, 255);
        static readonly Color Gold = new Color32(249, 197, 122, 255);
        static readonly Color[] TechniqueColors =
        {
            new Color32(240, 116, 103, 255), new Color32(129, 220, 246, 255), new Color32(249, 202, 110, 255)
        };
        static readonly KarmaAction[] TechniqueKeys =
        {
            KarmaAction.ChooseSwordWave, KarmaAction.ChooseRisingSlash, KarmaAction.ChooseGuard
        };
        static readonly string[] TechniquePreviews =
        {
            "적을 가로지르며 벱니다.",
            "공중에서 베고 한 번 더 도약합니다.",
            "받아낸 공격의 힘을 되돌려줍니다."
        };
        static readonly Vector2[] TrialMarkers =
        {
            new Vector2(149, 580), new Vector2(365, 425), new Vector2(271, 296), new Vector2(470, 178)
        };
        static readonly Vector2[] TrailControlA =
        {
            new Vector2(275, 595), new Vector2(338, 363), new Vector2(277, 248)
        };
        static readonly Vector2[] TrailControlB =
        {
            new Vector2(210, 455), new Vector2(205, 354), new Vector2(499, 285)
        };
        static readonly string[] TrialNumbers = { "1", "2", "3", "4" };

        enum UiFrame { Parchment, Card, SelectedCard, Control, PauseInk, Panel }
        enum UiOrnament { Pointer, Brush, VerticalBrush, TrialSeal, SummitSeal, Waypoint, HealthBacking, Track, GoldStroke }

        // Bounds are in the source textures, measured from the top-left.
        static readonly Rect[] FrameRegions =
        {
            new Rect(24, 136, 591, 258), new Rect(638, 129, 594, 268),
            new Rect(22, 508, 594, 254), new Rect(640, 521, 588, 228),
            new Rect(24, 855, 586, 305), new Rect(639, 857, 593, 302)
        };
        static readonly Rect[] OrnamentRegions =
        {
            new Rect(172, 163, 91, 127), new Rect(437, 213, 387, 25), new Rect(1024, 65, 41, 318),
            new Rect(61, 473, 304, 310), new Rect(473, 476, 308, 299), new Rect(908, 486, 277, 281),
            new Rect(31, 985, 375, 89), new Rect(436, 1001, 381, 57), new Rect(855, 1015, 367, 28)
        };

        GUIStyle displayText, logoText, mapLogo, rowText, actionText, mapLabel, mapNumber, modalTitle, resultsTitle, rightNumber, largeAction, bossName, titleSubtitle;
        Font logoFont, themeFont;
        Texture2D mapArt, techniqueAtlas, uiFrames, uiOrnaments, titleShade;
        bool atlasLoaded;
        bool mapLoaded;
        HudView hudView;
        RunPhase lastHudPhase;
        bool hudViewKnown;
        float hudViewAt, selectionAt, routeStartedAt = -1f, hoverAt;
        int lastChoiceStage, lastChoiceCount;
        Rect? hoveredButton;
        Essence? shownChoice;
        int choiceStage = -1;
        float ScreenReveal => Ease((Time.unscaledTime - hudViewAt) / 0.23f);

        void ThemeStyles(Font koreanFont)
        {
            displayText = new GUIStyle(title) { fontSize = 55, alignment = TextAnchor.MiddleLeft };
            rowText = new GUIStyle(centered) { fontSize = 24, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            actionText = new GUIStyle(centered) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = false };
            mapLabel = new GUIStyle(rowText) { fontSize = 17, fontStyle = FontStyle.Bold };
            mapNumber = new GUIStyle(actionText) { fontSize = 17 };
            logoText = new GUIStyle(displayText) { fontSize = 74, fontStyle = FontStyle.Italic };
            var installed = Font.GetOSInstalledFontNames();
            foreach (string name in new[] { "Georgia", "Baskerville", "Times New Roman" })
            {
                if (System.Array.IndexOf(installed, name) < 0) continue;
                logoFont = Font.CreateDynamicFontFromOSFont(name, 86);
                if (logoFont != null) break;
            }
            logoText.font = logoFont != null ? logoFont : koreanFont;
            mapLogo = new GUIStyle(logoText) { fontSize = 36 };
            themeFont = InstalledFont(installed, 24, '가', "Nanum Myeongjo", "NanumMyeongjo", "Batang",
                "바탕", "AppleMyungjo", "Noto Serif CJK KR", "Noto Serif KR");
            if (themeFont != null)
                foreach (var style in new[] { displayText, rowText, actionText, mapLabel, mapNumber }) style.font = themeFont;
            modalTitle = new GUIStyle(displayText) { fontSize = 43, alignment = TextAnchor.MiddleCenter };
            resultsTitle = new GUIStyle(modalTitle) { fontSize = 54 };
            largeAction = new GUIStyle(actionText) { fontSize = 30 };
            bossName = new GUIStyle(actionText) { fontSize = 25 };
            titleSubtitle = new GUIStyle(rowText) { fontSize = 32 };
            rightNumber = new GUIStyle(actionText) { fontSize = 28, alignment = TextAnchor.MiddleRight };
            foreach (var style in new[] { displayText, logoText, mapLogo, rowText, actionText, mapLabel, mapNumber, modalTitle, resultsTitle, rightNumber, largeAction, bossName, titleSubtitle })
            {
                style.normal.textColor = Color.white;
                style.padding = new RectOffset();
            }

            uiFrames = Resources.Load<Texture2D>("UI/UIFrames");
            uiOrnaments = Resources.Load<Texture2D>("UI/UIOrnaments");
            titleShade = CreateUiTexture("K4RMA title shade", 128, 32, (x, y) =>
                new Color(1, 1, 1, (1 - Ease((x - 0.27f) / 0.73f)) * Ease(y * 7) * Ease((1 - y) * 6)));
        }

        static Texture2D CreateUiTexture(string name, int width, int height, System.Func<float, float, Color> sample)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name, hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = sample(x / (width - 1f), 1 - y / (height - 1f));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        Font InstalledFont(string[] installed, int size, char required, params string[] names)
        {
            foreach (string name in names)
            {
                if (System.Array.IndexOf(installed, name) < 0) continue;
                var candidate = Font.CreateDynamicFontFromOSFont(name, size);
                if (candidate != null && candidate.HasCharacter(required)) return candidate;
                if (candidate != null) Destroy(candidate);
            }
            return null;
        }

        void Frame(Rect bounds, UiFrame frame, Color tint)
        {
            if (Event.current.type != EventType.Repaint || bounds.width <= 0 || bounds.height <= 0 || tint.a <= 0) return;
            if (uiFrames == null)
            {
                Fill(bounds, Alpha(frame == UiFrame.Parchment ? Ivory : Ink, tint.a));
                return;
            }
            Rect source = FrameRegions[(int)frame];
            if (frame == UiFrame.PauseInk)
            {
                DrawRegion(bounds, uiFrames, source, tint);
                return;
            }

            // Keep the painted corners and edge strokes proportional on both cards and compact controls.
            const float sourceInset = 34;
            float inset = Mathf.Min(16, Mathf.Min(bounds.width, bounds.height) * 0.34f);
            float middleWidth = bounds.width - 2 * inset;
            float middleHeight = bounds.height - 2 * inset;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    float sx = col == 2 ? source.xMax - sourceInset : source.x + col * sourceInset;
                    float sy = row == 2 ? source.yMax - sourceInset : source.y + row * sourceInset;
                    float x = col == 2 ? bounds.xMax - inset : bounds.x + col * inset;
                    float y = row == 2 ? bounds.yMax - inset : bounds.y + row * inset;
                    DrawRegion(new Rect(x, y, col == 1 ? middleWidth : inset, row == 1 ? middleHeight : inset),
                        uiFrames, new Rect(sx, sy, col == 1 ? source.width - 2 * sourceInset : sourceInset,
                            row == 1 ? source.height - 2 * sourceInset : sourceInset), tint);
                }
        }

        void Ornament(Rect bounds, UiOrnament ornament, Color tint, float portion = 1)
        {
            if (Event.current.type != EventType.Repaint || bounds.width <= 0 || bounds.height <= 0 || tint.a <= 0 || portion <= 0) return;
            if (uiOrnaments == null)
            {
                if (ornament == UiOrnament.Brush || ornament == UiOrnament.GoldStroke)
                    Fill(new Rect(bounds.x, bounds.y, bounds.width * portion, bounds.height), tint);
                return;
            }
            Rect source = OrnamentRegions[(int)ornament];
            bounds.width *= portion;
            source.width *= portion;
            DrawRegion(bounds, uiOrnaments, source, tint);
        }

        static void DrawRegion(Rect bounds, Texture2D atlas, Rect source, Color tint)
        {
            Color saved = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(bounds, atlas, KarmaArtwork.UV(atlas, source), true);
            GUI.color = saved;
        }

        void ThemePanel(Rect bounds, float opacity = 0.96f)
            => Frame(bounds, UiFrame.Panel, Alpha(Color.white, opacity));

        void BrushRule(float x, float y, float width, Color color)
            => Ornament(new Rect(x, y, width, 4), UiOrnament.Brush, color);

        void BrushDivider(Rect bounds, Color color)
            => Ornament(bounds, UiOrnament.VerticalBrush, color);

        void SelectionPointer(Vector2 tip, Color color)
            => Ornament(new Rect(tip.x - 17, tip.y - 12, 17, 24), UiOrnament.Pointer, color);

        void ProgressTrack(Rect bounds, float fraction, float trailing = 0, float flash = 0)
        {
            Ornament(new Rect(bounds.x - 7, bounds.y - 7, bounds.width + 14, bounds.height + 14), UiOrnament.Track, Color.white);
            if (trailing > fraction) Ornament(bounds, UiOrnament.GoldStroke, Alpha(Color.white, 0.32f), trailing);
            Ornament(bounds, UiOrnament.GoldStroke, Color.Lerp(Color.white, Ivory, flash), fraction);
        }

        Texture2D TechniqueAtlas()
        {
            if (!atlasLoaded)
            {
                techniqueAtlas = Resources.Load<Texture2D>("UI/TechniqueIcons");
                atlasLoaded = true;
            }
            return techniqueAtlas;
        }

        static Rect TechniqueIconUv(Essence essence) => new Rect((int)essence / 3f, 0.13f, 1f / 3, 0.74f);

        void TechniqueIcon(Rect bounds, Essence essence, float opacity = 1)
        {
            var atlas = TechniqueAtlas();
            if (atlas == null)
            {
                SpriteArt(bounds, KarmaArtwork.SkillIcon(essence), Alpha(TechniqueColors[(int)essence], opacity));
                return;
            }
            var saved = GUI.color;
            GUI.color = Alpha(Color.white, opacity);
            GUI.DrawTextureWithTexCoords(bounds, atlas, TechniqueIconUv(essence), true);
            GUI.color = saved;
        }

        // The ending owns these runtime sprites. The HUD draws the same atlas regions directly.
        internal Sprite CreateEndingTechniqueIcon(Essence essence)
        {
            var atlas = TechniqueAtlas();
            if (atlas == null) return null;
            var region = TechniqueIconUv(essence);
            int left = Mathf.RoundToInt(region.xMin * atlas.width);
            int right = Mathf.RoundToInt(region.xMax * atlas.width);
            int bottom = Mathf.RoundToInt(region.yMin * atlas.height);
            int top = Mathf.RoundToInt(region.yMax * atlas.height);
            return Sprite.Create(atlas, new Rect(left, bottom, right - left, top - bottom),
                Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, Vector4.zero, false);
        }

        void ReleaseTheme()
        {
            if (logoFont != null) Destroy(logoFont);
            if (themeFont != null) Destroy(themeFont);


            if (titleShade != null) Destroy(titleShade);

        }

        void FadeIntoMenu()
        {
            float fade = 1 - ScreenReveal;
            if (fade > 0) Fill(FullView, Alpha(DeepInk, fade * 0.78f));
        }

        void UpdateHudView()
        {
            HudView next = game.SettingsOpen ? HudView.Settings
                : game.EndingCompleted ? HudView.Results
                : game.Paused ? HudView.Pause
                : game.Phase == RunPhase.Ending ? HudView.Ending
                : game.Phase == RunPhase.Title ? HudView.Title
                : game.Phase == RunPhase.Choosing ? HudView.Choice
                : game.Phase == RunPhase.Victory || game.Phase == RunPhase.Defeat ? HudView.Results
                : HudView.Play;

            if (!hudViewKnown || next != hudView)
            {
                if (hudViewKnown && game.Phase == lastHudPhase)
                {
                    if (next == HudView.Pause && (hudView == HudView.Play || hudView == HudView.Ending))
                        KarmaAudio.Play(KarmaSfxId.UiCancel);
                    else if (hudView == HudView.Pause && (next == HudView.Play || next == HudView.Ending))
                        KarmaAudio.Play(KarmaSfxId.UiConfirm);
                }
                hudView = next;
                hudViewKnown = true;
                hudViewAt = Time.unscaledTime;
                hoveredButton = null;
            }

            if (lastHudPhase == RunPhase.Choosing && game.Phase == RunPhase.Transition
                && game.Player != null && game.Player.Essences.PersonalizationOrder.Count > lastChoiceCount)
                routeStartedAt = Time.unscaledTime;
            else if (game.Phase != RunPhase.Transition)
                routeStartedAt = -1f;
            lastHudPhase = game.Phase;

            if (next == HudView.Choice && game.Player != null)
            {
                lastChoiceStage = game.StageIndex;
                lastChoiceCount = game.Player.Essences.PersonalizationOrder.Count;
                if (choiceStage != game.StageIndex || shownChoice != game.PendingChoice)
                {
                    choiceStage = game.StageIndex;
                    shownChoice = game.PendingChoice;
                    selectionAt = Time.unscaledTime;
                }
            }
            else if (next != HudView.Settings) choiceStage = -1;

            if (hoveredButton.HasValue && !hoveredButton.Value.Contains(Event.current.mousePosition))
                hoveredButton = null;
        }

        static Color Alpha(Color color, float opacity)
        {
            color.a *= opacity;
            return color;
        }

        static void TintTexture(Rect bounds, Texture texture, Color tint, ScaleMode scale = ScaleMode.StretchToFill)
        {
            Color saved = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(bounds, texture, scale, true);
            GUI.color = saved;
        }

        static void ThemeLabel(Rect bounds, string value, GUIStyle style, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.Label(bounds, value, style);
            GUI.color = saved;
        }

        void InkPanel(float left)
            => Frame(new Rect(left - 160, -4, 1280 - left + 169, 728), UiFrame.PauseInk, Color.white);

        static string Keyed(string label, KarmaAction action)
            => label + " [" + KarmaInput.BindingLabel(action) + "]";

        float HoverAmount(Rect bounds, bool enabled = true)
        {
            if (!enabled || !bounds.Contains(Event.current.mousePosition)) return 0;
            if (!hoveredButton.HasValue || hoveredButton.Value != bounds)
            {
                hoveredButton = bounds;
                hoverAt = Time.unscaledTime;
            }
            return Mathf.Lerp(0.26f, 1, Ease((Time.unscaledTime - hoverAt) / 0.13f));
        }

        static bool PointerDown(float hover)
            => hover > 0 && GUIUtility.hotControl != 0 && Mouse.current != null && Mouse.current.leftButton.isPressed;

        bool ThemeButton(Rect bounds, string label, bool primary = false, bool enabled = true, bool compact = false, bool textOnly = false, bool large = false)
        {
            float hover = HoverAmount(bounds, enabled);
            bool pressed = PointerDown(hover);
            Rect face = bounds;
            if (pressed) face = new Rect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4);
            if (primary && enabled)
                Frame(face, UiFrame.Parchment, Color.Lerp(new Color(0.93f, 0.91f, 0.88f), Color.white, hover));
            else if (!textOnly || hover > 0)
                Frame(face, UiFrame.Control, Alpha(Color.Lerp(SoftIvory, Color.white, hover), enabled ? 0.83f + 0.17f * hover : 0.52f));
            GUIStyle buttonText = compact ? mapNumber : large ? largeAction : actionText;
            ThemeLabel(face, label, buttonText, primary && enabled ? DeepInk : Alpha(Ivory, enabled ? 1 : 0.45f));
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && enabled;
            bool clicked = GUI.Button(bounds, GUIContent.none, GUIStyle.none);
            GUI.enabled = wasEnabled;
            return clicked;
        }

        void DrawMountainMap(int cleared, bool showNext, float advance = 0, bool failed = false, float opacity = 1)
        {
            if (!mapLoaded)
            {
                mapArt = Resources.Load<Texture2D>("UI/GuardianTrialMap");
                mapLoaded = true;
            }
            var backdrop = mapArt != null ? mapArt : KarmaArtwork.Texture("MenuBackground");
            Fill(FullView, Alpha(DeepInk, opacity));
            if (backdrop != null) TintTexture(FullView, backdrop, Alpha(Color.white, opacity), ScaleMode.ScaleAndCrop);
            int next = cleared;
            for (int i = 0; i < TrialMarkers.Length - 1; i++)
            {
                DrawTrail(i, 1, false, opacity);
                if (i + 1 < cleared) DrawTrail(i, 1, true, opacity);
                else if (i + 1 == next && showNext && advance > 0) DrawTrail(i, advance, true, opacity);
            }

            for (int i = 0; i < TrialMarkers.Length; i++)
            {
                Vector2 p = TrialMarkers[i];
                bool complete = i < cleared;
                bool upcoming = i == next && showNext;
                if (upcoming)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.8f);
                    Ornament(new Rect(p.x - 20 - 2 * pulse, p.y - 20 - 2 * pulse, 40 + 4 * pulse, 40 + 4 * pulse),
                        UiOrnament.Waypoint, Alpha(Gold, (0.30f + pulse * 0.36f) * opacity));
                }
                Color markerTint = complete ? Color.white : upcoming ? Gold : new Color32(181, 190, 200, 255);
                Ornament(new Rect(p.x - 17, p.y - 17, 34, 34), UiOrnament.Waypoint, Alpha(markerTint, opacity));
                ThemeLabel(new Rect(p.x - 17, p.y - 17, 34, 34), TrialNumbers[i], mapNumber, Alpha(Ivory, opacity));

                bool seal = complete && i == cleared - 1;
                if (seal)
                {
                    DrawTrialSeal(p, cleared == 4, opacity);
                    continue;
                }
                string label = complete ? null : upcoming
                    ? (failed ? "도전 중단" : "다음 시련")
                    : i == 3 ? "마지막 시련" : i == 2 ? "세 번째 시련" : null;
                if (label == null) continue;
                Rect tag = new Rect(p.x + 19, p.y - 14, i == 3 ? 128 : 124, 32);
                Frame(tag, UiFrame.Parchment, Alpha(upcoming ? Color.white : SoftIvory, opacity * 0.94f));
                ThemeLabel(tag, label, mapNumber, Alpha(DeepInk, opacity));
            }
        }

        void DrawTrialSeal(Vector2 marker, bool final, float opacity)
        {
            if (final)
            {
                var square = new Rect(marker.x + 33, marker.y - 25, 80, 56);
                Ornament(square, UiOrnament.SummitSeal, Alpha(Color.white, opacity));
                ThemeLabel(square, "완료", actionText, Alpha(Ivory, opacity));
                return;
            }
            var ring = new Rect(marker.x + 8, marker.y - 12, 77, 77);
            Ornament(ring, UiOrnament.TrialSeal, Alpha(Color.white, opacity));
            ThemeLabel(new Rect(ring.x, ring.y + 9, 77, 29), "시련", actionText, Alpha(Ivory, opacity));
            ThemeLabel(new Rect(ring.x, ring.y + 37, 77, 29), "완료", actionText, Alpha(Ivory, opacity));
        }

        void DrawTrail(int index, float progress, bool solid, float opacity)
        {
            const int samples = 30;
            Vector2 start = TrialMarkers[index], end = TrialMarkers[index + 1];
            Vector2 a = TrailControlA[index], b = TrailControlB[index];
            Vector2 previous = start;
            int count = Mathf.CeilToInt(samples * progress);
            for (int step = 1; step <= count; step++)
            {
                float t = Mathf.Min(step / (float)samples, progress);
                Vector2 first = Vector2.Lerp(Vector2.Lerp(start, a, t), Vector2.Lerp(a, b, t), t);
                Vector2 second = Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, end, t), t);
                Vector2 point = Vector2.Lerp(first, second, t);
                if (solid)
                {
                    TrailStroke(previous, point, 8, Alpha(Gold, 0.25f * opacity));
                    TrailStroke(previous, point, 4.8f, Alpha(Color.white, opacity));
                }
                else if (step % 3 == 0)
                    TrailStroke(previous, Vector2.Lerp(previous, point, 0.63f), 2.4f, Alpha(Ivory, 0.57f * opacity));
                previous = point;
            }
        }

        void TrailStroke(Vector2 start, Vector2 end, float width, Color tint)
        {
            Matrix4x4 matrix = GUI.matrix;
            Vector2 delta = end - start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            Ornament(new Rect(start.x - 0.5f, start.y - width * 0.5f, delta.magnitude + 1, width), UiOrnament.GoldStroke, tint);
            GUI.matrix = matrix;
        }

        void DrawChoiceExit()
        {
            float elapsed = Time.unscaledTime - routeStartedAt;
            if (routeStartedAt < 0 || elapsed >= 0.57f || game.GuardianInheriting) return;
            // Keep the existing personalization shot visible behind a shrinking map.
            float fade = 1 - Ease((elapsed - 0.24f) / 0.33f);
            float scale = Mathf.Lerp(0.31f, 0.39f, fade);
            Matrix4x4 matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(-18 * (1 - fade), 720 * (1 - scale), 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.BeginGroup(new Rect(0, 0, 720, 720));
            DrawMountainMap(lastChoiceStage + 1, true, Ease(elapsed / 0.36f), opacity: fade);
            GUI.EndGroup();

            GUI.matrix = matrix;
        }
    }
}
