using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] tutorialLabels = { "기본 공격", "검기 명중", "상승베기 명중", "방어 성공" };
        static readonly string[] tutorialTrainingNames = { "기본 공격", "검기", "상승베기", "방어" };
        static readonly KarmaAction[] tutorialKeys = { KarmaAction.Attack, KarmaAction.SwordWave, KarmaAction.RisingSlash, KarmaAction.Guard };
        Texture2D tutorialRouteArrow;

        void TutorialScreen()
        {
            var t = game.Tutorial;
            if (game.Paused) return;
            // Skip enters Fade at alpha zero, before a lesson may have been selected.
            if (t.Step == KarmaTutorialStep.Fade) { Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, t.Fade)); return; }
            if (t.WaitingForGuard)
            {
                Fill(new Rect(0, 0, 1280, 720), new Color(0.01f, 0.02f, 0.04f, 0.4f));
                ThemePanel(new Rect(250, 540, 780, 150));
                ThemeLabel(new Rect(275, 554, 730, 45), "공격이 닿기 전, 보호의 힘을 펼쳐라.", centered, Ivory);
                float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 5) * 0.2f;
                BrushRule(438, 603, 404, Alpha(Gold, 0.52f));
                KeyPrompt(new Rect(275, 610, 730, 57), actionText, Alpha(Gold, pulse), true,
                    KarmaAction.Guard, "  새로 눌러 막기");
                return;
            }
            if (!string.IsNullOrEmpty(t.Speech))
            {
                ThemePanel(new Rect(210, 550, 860, 140));
                ThemeLabel(new Rect(235, 556, 810, 32), t.Step == KarmaTutorialStep.Complete ? tutorialLabels[t.Lesson] + " 3/3 · 수련 완료" : t.SpeechTitle, mapLabel, Gold);
                BrushRule(235, 590, 810, Alpha(Gold, 0.34f));
                ThemeLabel(new Rect(235, 599, 810, 53), t.Speech, text, Ivory);
                if (t.Step != KarmaTutorialStep.Complete)
                    KeyPrompt(new Rect(235, 652, 810, 27), mapLabel, Gold, false, KarmaAction.DialogueConfirm, "  계속");
                return;
            }
            bool outsideGate = t.Step == KarmaTutorialStep.Gate && Mathf.Abs(game.Player.Position.x - KarmaTutorialStage.FinalGate) > 5;
            if (t.Step == KarmaTutorialStep.Travel)
                DrawTutorialRouteArrow(KarmaTutorial.Stations[t.Lesson + 1]);
            else if (outsideGate) DrawTutorialRouteArrow(KarmaTutorialStage.FinalGate);

            string blockedMessage = t.BlockedMessage;
            if (!string.IsNullOrEmpty(blockedMessage))
            {
                ThemePanel(new Rect(210, 120, 860, 70));
                ThemeLabel(new Rect(230, 130, 820, 50), blockedMessage, centered, Gold);
            }
            ThemePanel(new Rect(210, 615, 860, 65));
            var objective = new Rect(230, 625, 820, 44);
            if (outsideGate)
                ThemeLabel(objective, "모든 훈련을 마쳤다. 관문으로 이동하세요.", centered, Ivory);
            else if (t.Step == KarmaTutorialStep.Gate)
                KeyPrompt(objective, text, Ivory, true, "관문 앞에서  ", KarmaAction.Interact, "  다음 보스 스테이지로 이동");
            else if (t.Step == KarmaTutorialStep.Travel && t.Lesson < 0)
                KeyPrompt(objective, text, Ivory, true, KarmaAction.Left, " / ", KarmaAction.Right, "  이동   ·   ",
                    KarmaAction.Down, "  앉기   ·   ", KarmaAction.Jump, "  점프");
            else if (t.Step == KarmaTutorialStep.Complete && t.Lesson < 3)
                ThemeLabel(objective, tutorialTrainingNames[t.Lesson] + " 훈련을 마쳤다. 다음 훈련을 시작하자.", centered, Ivory);
            else if (t.Step == KarmaTutorialStep.Travel)
                ThemeLabel(objective, tutorialTrainingNames[t.Lesson] + " 훈련을 마쳤다.", centered, Ivory);
            else
            {
                string detail = t.Lesson == 1 ? "  ·  표적에서 거리를 두고 발사"
                    : t.Lesson == 2 ? "  ·  허수아비 가까이에서 올려베기" : "";
                if (t.Lesson > 0)
                {
                    float cooldown = game.Player.Essences.Cooldown((Essence)(t.Lesson - 1));
                    if (cooldown > 0) detail += "  ·  재사용 대기 " + cooldown.ToString("0.0") + "초";
                }
                KeyPrompt(objective, text, Ivory, true, tutorialLabels[t.Lesson] + " " + t.Successes + "/3  ·  ", tutorialKeys[t.Lesson], detail);
            }
        }

        void DrawTutorialRouteArrow(float destination)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (tutorialRouteArrow == null) tutorialRouteArrow = CreateTutorialRouteArrow();

            var player = game.Player.Position;
            bool pointsLeft = destination < player.x;
            float wave = Mathf.Sin(Time.time * 5f);
            float nudge = wave * (pointsLeft ? -4f : 4f);
            var anchor = game.GameCamera.WorldToViewportPoint(player + Vector2.up * (KarmaSpace.PlayerHalfHeight + 0.8f));
            const float width = 76, height = 33.25f;
            var rect = new Rect(Mathf.Clamp(anchor.x * 1280 + nudge - width * 0.5f, 8, 1272 - width),
                Mathf.Clamp((1 - anchor.y) * 720 - height - 3 * wave, 10, 590 - height), width, height);
            var uv = pointsLeft ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            GUI.DrawTextureWithTexCoords(rect, tutorialRouteArrow, uv, true);
        }

        static Texture2D CreateTutorialRouteArrow()
        {
            const int width = 128, height = 56;
            // A single beveled silhouette in the HUD's gold and ink palette.
            Vector2[] outline = {
                new(10, 22), new(20, 18), new(76, 18), new(76, 7), new(119, 29),
                new(76, 50), new(76, 39), new(20, 39), new(10, 35)
            };
            Color ink = new Color32(37, 26, 22, 255);
            Color bronze = new Color32(177, 115, 53, 255);
            Color gold = new Color32(244, 196, 103, 255);
            Color ivory = new Color32(255, 231, 165, 255);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float bevel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 45, y));
                Color face = Color.Lerp(bronze, gold, bevel);
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = ArrowOutlineDistance(point, outline);
                    Color color = Color.Lerp(ink, Color.Lerp(gold, ivory, bevel), Mathf.Clamp01(distance - 1));
                    color = Color.Lerp(color, face, Mathf.Clamp01(distance - 3.3f));
                    color.a = Mathf.Clamp01(distance + 1.5f);
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Tutorial Route Arrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static float ArrowOutlineDistance(Vector2 point, Vector2[] outline)
        {
            bool inside = false;
            float nearest = float.PositiveInfinity;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                var a = outline[j];
                var edge = outline[i] - a;
                var offset = point - a;
                float t = Mathf.Clamp01(Vector2.Dot(offset, edge) / edge.sqrMagnitude);
                nearest = Mathf.Min(nearest, (offset - edge * t).sqrMagnitude);
                if ((a.y > point.y) != (outline[i].y > point.y) && offset.x < edge.x * offset.y / edge.y)
                    inside = !inside;
            }
            return Mathf.Sqrt(nearest) * (inside ? 1 : -1);
        }
    }
}
