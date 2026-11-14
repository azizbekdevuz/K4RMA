using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        // Cropped Xelu dark blanks (CC0), recolored to match the HUD.
        // Source https://thoseawesomeguys.com/prompts/
        const string KeycapResource = "UI/KeyboardKeycaps";
        static readonly Rect[] keycapRegions = {
            new(2, 2, 76, 72), new(102, 2, 92, 62), new(202, 2, 96, 48),
            new(302, 2, 92, 85), new(402, 2, 54, 94)
        };
        static readonly Color keycapInk = new Color(0.97f, 0.93f, 0.85f);
        static readonly Color keycapIdle = new Color(0.72f, 0.83f, 1f);
        static readonly Color keycapPressed = new Color(1.03f, 0.94f, 0.79f);
        readonly GUIContent promptContent = new GUIContent();
        Texture2D keycapAtlas;
        GUIStyle keycapLegend, promptText;
        bool keycapAtlasLoaded;

        enum KeycapShape { Square, Wide, Spacebar, Enter, Tall }

        readonly struct Keycap
        {
            public readonly string Legend;
            public readonly KeycapShape Shape;

            public Keycap(string label)
            {
                Legend = label switch {
                    "Left Arrow" => "←", "Right Arrow" => "→", "Up Arrow" => "↑", "Down Arrow" => "↓",
                    "Left Shift" => "L Shift", "Right Shift" => "R Shift",
                    "Left Ctrl" => "L Ctrl", "Right Ctrl" => "R Ctrl",
                    "Left Alt" => "L Alt", "Right Alt" => "R Alt",
                    "Numpad Enter" => "↵", _ => label
                };
                Shape = label switch {
                    "Space" => KeycapShape.Spacebar,
                    "Enter" => KeycapShape.Enter,
                    "Numpad Enter" => KeycapShape.Tall,
                    _ => Legend.Length <= 3 ? KeycapShape.Square : KeycapShape.Wide
                };
            }
        }

        readonly struct PromptPart
        {
            public readonly string Text;
            public readonly KarmaAction Action;
            public bool IsKey => Text == null;

            PromptPart(string text, KarmaAction action) { Text = text; Action = action; }
            public static implicit operator PromptPart(string text) => new(text, default);
            public static implicit operator PromptPart(KarmaAction action) => new(null, action);
        }

        void KeycapStyles()
        {
            if (keycapLegend != null) return;
            keycapLegend = new GUIStyle(small) {
                alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip,
                padding = new RectOffset(), fontStyle = FontStyle.Bold
            };
            keycapLegend.normal.textColor = keycapInk;
            promptText = new GUIStyle(text) {
                alignment = TextAnchor.MiddleLeft, wordWrap = false, padding = new RectOffset()
            };
        }

        Vector2 KeycapSize(Keycap cap, float height)
        {
            var region = keycapRegions[(int)cap.Shape];
            keycapLegend.fontSize = Mathf.RoundToInt(height * (cap.Shape == KeycapShape.Enter ? 0.34f : 0.43f));
            promptContent.text = cap.Legend;
            float width = region.width / region.height * height;
            if (cap.Shape == KeycapShape.Wide)
                width = Mathf.Max(width, keycapLegend.CalcSize(promptContent).x + height * 0.56f);
            return new Vector2(width, height);
        }

        void DrawKeycap(Rect rect, Keycap cap, bool pressed)
        {
            if (!keycapAtlasLoaded)
            {
                keycapAtlasLoaded = true;
                keycapAtlas = Resources.Load<Texture2D>(KeycapResource);
            }
            Color color = GUI.color;
            if (!GUI.enabled) GUI.color = new Color(color.r, color.g, color.b, color.a * 0.42f);
            if (pressed && GUI.enabled)
            {
                rect.y += 1.5f;
            }
            Color legendTint = GUI.color;
            GUI.color *= pressed && GUI.enabled ? keycapPressed : keycapIdle;
            if (keycapAtlas != null)
            {
                var region = keycapRegions[(int)cap.Shape];
                if (cap.Shape == KeycapShape.Wide && rect.width > rect.height * region.width / region.height + 1f)
                {
                    // Widen the flat center and preserve the shape of the two beveled edges.
                    const float edge = 18;
                    float corner = edge * rect.height / region.height;
                    DrawKeycapSlice(new Rect(rect.x, rect.y, corner, rect.height),
                        new Rect(region.x, region.y, edge, region.height));
                    DrawKeycapSlice(new Rect(rect.x + corner, rect.y, rect.width - 2 * corner, rect.height),
                        new Rect(region.x + edge, region.y, region.width - 2 * edge, region.height));
                    DrawKeycapSlice(new Rect(rect.xMax - corner, rect.y, corner, rect.height),
                        new Rect(region.xMax - edge, region.y, edge, region.height));
                }
                else DrawKeycapSlice(rect, region);
            }
            else GUI.Box(rect, GUIContent.none);

            if (cap.Shape == KeycapShape.Enter) rect.y += rect.height * 0.11f;
            else rect.y -= rect.height * 0.045f;
            promptContent.text = cap.Legend;
            GUI.color = legendTint;
            GUI.Label(rect, promptContent, keycapLegend);
            GUI.color = color;
        }

        void DrawKeycapSlice(Rect bounds, Rect topLeft)
        {
            GUI.DrawTextureWithTexCoords(bounds, keycapAtlas, KarmaArtwork.UV(keycapAtlas, topLeft), true);
        }

        bool ShortcutButton(Rect bounds, string label, KarmaAction action)
        {
            bool clicked = GUI.Button(bounds, label, menuButton);
            if (Event.current.type == EventType.Repaint)
            {
                KeycapStyles();
                var cap = new Keycap(KarmaInput.BindingLabel(action));
                var size = KeycapSize(cap, Mathf.Min(34, bounds.height - 8));
                DrawKeycap(new Rect(bounds.xMax - 15 - size.x, bounds.center.y - size.y * 0.5f, size.x, size.y),
                    cap, KarmaInput.Held(action));
            }
            return clicked;
        }

        bool KeyBindingButton(Rect bounds, KarmaAction action)
        {
            bool clicked = GUI.Button(bounds, GUIContent.none, menuButton);
            if (Event.current.type == EventType.Repaint)
            {
                KeycapStyles();
                var cap = new Keycap(KarmaInput.BindingLabel(action));
                var size = KeycapSize(cap, bounds.height - 3);
                bool hover = GUI.enabled && bounds.Contains(Event.current.mousePosition);
                DrawKeycap(new Rect(bounds.center - size * 0.5f, size), cap, hover || KarmaInput.Held(action));
            }
            return clicked;
        }

        void KeyPrompt(Rect bounds, int fontSize, bool center, params PromptPart[] parts)
        {
            if (Event.current.type != EventType.Repaint) return;
            KeycapStyles();
            promptText.fontSize = fontSize;
            float height = Mathf.Min(bounds.height - 2, fontSize + 11);
            float x = bounds.x;
            if (center)
            {
                float width = 0;
                foreach (var part in parts)
                    width += part.IsKey ? KeycapSize(new Keycap(KarmaInput.BindingLabel(part.Action)), height).x : PromptTextWidth(part.Text);
                x = Mathf.Max(x, bounds.center.x - width * 0.5f);
            }

            foreach (var part in parts)
            {
                if (part.IsKey)
                {
                    var cap = new Keycap(KarmaInput.BindingLabel(part.Action));
                    var size = KeycapSize(cap, height);
                    DrawKeycap(new Rect(x, bounds.center.y - height * 0.5f, size.x, height), cap, KarmaInput.Held(part.Action));
                    x += size.x;
                }
                else
                {
                    float width = PromptTextWidth(part.Text);
                    GUI.Label(new Rect(x, bounds.y, width + 1, bounds.height), promptContent, promptText);
                    x += width;
                }
            }
        }

        float PromptTextWidth(string value)
        {
            promptContent.text = value;
            return promptText.CalcSize(promptContent).x;
        }
    }
}
