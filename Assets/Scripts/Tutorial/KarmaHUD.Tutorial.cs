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
                Panel(new Rect(250, 540, 780, 150));
                Label(275, 550, 730, 50, "공격이 닿기 전, 보호의 힘을 펼쳐라.", centered);
                float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 5) * 0.2f;
                var old = GUI.color; GUI.color = new Color(1, 0.9f, 0.6f, pulse);
                KeyPrompt(new Rect(275, 615, 730, 55), 23, true, KarmaAction.Guard, "  새로 눌러 막으세요"); GUI.color = old;
                return;
            }
            if (!string.IsNullOrEmpty(t.Speech))
            {
                Panel(new Rect(210, 550, 860, 140));
                Label(235, 560, 810, 25, t.Step == KarmaTutorialStep.Complete ? tutorialLabels[t.Lesson] + " 3/3 · 수련 완료" : t.SpeechTitle, small);
                Label(235, 588, 810, 65, t.Speech, text);
                if (t.Step != KarmaTutorialStep.Complete)
                    KeyPrompt(new Rect(235, 655, 810, 25), 14, false, KarmaAction.DialogueConfirm, "  계속");
                return;
            }
            bool outsideGate = t.Step == KarmaTutorialStep.Gate && Mathf.Abs(game.Player.Position.x - KarmaTutorialStage.FinalGate) > 5;
            if (t.Step == KarmaTutorialStep.Travel)
                DrawTutorialRouteArrow(KarmaTutorial.Stations[t.Lesson + 1]);
            else if (outsideGate) DrawTutorialRouteArrow(KarmaTutorialStage.FinalGate);

            string blockedMessage = t.BlockedMessage;
            if (!string.IsNullOrEmpty(blockedMessage))
            {
                Panel(new Rect(210, 120, 860, 70));
                Label(230, 130, 820, 50, blockedMessage, centered);
            }
            Panel(new Rect(210, 615, 860, 65));
            var objective = new Rect(230, 630, 820, 42);
            if (outsideGate)
                GUI.Label(objective, "모든 훈련을 마쳤다. 관문으로 이동하세요.", centered);
            else if (t.Step == KarmaTutorialStep.Gate)
                KeyPrompt(objective, 18, true, "관문 앞에서  ", KarmaAction.Interact, "  다음 보스 스테이지로 이동");
            else if (t.Step == KarmaTutorialStep.Travel && t.Lesson < 0)
                KeyPrompt(objective, 18, true, KarmaAction.Left, " / ", KarmaAction.Right, "  이동   ·   ",
                    KarmaAction.Down, "  앉기   ·   ", KarmaAction.Jump, "  점프");
            else if (t.Step == KarmaTutorialStep.Complete && t.Lesson < 3)
                GUI.Label(objective, tutorialTrainingNames[t.Lesson] + " 훈련을 마쳤다. 다음 훈련을 시작하자.", centered);
            else if (t.Step == KarmaTutorialStep.Travel)
                GUI.Label(objective, tutorialTrainingNames[t.Lesson] + " 훈련을 마쳤다.", centered);
            else
            {
                string detail = t.Lesson == 1 ? "  ·  표적에서 거리를 두고 발사"
                    : t.Lesson == 2 ? "  ·  허수아비 가까이에서 올려베기" : "";
                if (t.Lesson > 0)
                {
                    float cooldown = game.Player.Essences.Cooldown((Essence)(t.Lesson - 1));
                    if (cooldown > 0) detail += "  ·  재사용 대기 " + cooldown.ToString("0.0") + "초";
                }
                KeyPrompt(objective, 18, true, tutorialLabels[t.Lesson] + " " + t.Successes + "/3  ·  ", tutorialKeys[t.Lesson], detail);
            }
        }

        void DrawTutorialRouteArrow(float destination)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (tutorialRouteArrow == null) tutorialRouteArrow = CreateTutorialRouteArrow();

            var player = game.Player.Position;
            bool pointsLeft = destination < player.x;
            float wave = Mathf.Sin(Time.time * 4f);
            float nudge = wave * (pointsLeft ? -3f : 3f);
            var anchor = game.GameCamera.WorldToViewportPoint(player + Vector2.up * (KarmaSpace.PlayerHalfHeight + 0.8f));
            const float width = 72, height = 31.5f;
            var rect = new Rect(Mathf.Clamp(anchor.x * 1280 + nudge - width * 0.5f, 8, 1272 - width),
                Mathf.Clamp((1 - anchor.y) * 720 - height - 3 * wave, 10, 590 - height), width, height);
            var uv = pointsLeft ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            GUI.DrawTextureWithTexCoords(rect, tutorialRouteArrow, uv, true);
        }

        static Texture2D CreateTutorialRouteArrow()
        {
            const int width = 128, height = 56;
            // A dark ink outline keeps the aged brass legible against the lantern-lit courtyard.
            Vector2[] outline = {
                new(9, 21), new(25, 19), new(75, 19), new(72, 8), new(120, 28),
                new(72, 48), new(75, 37), new(25, 37), new(9, 35), new(17, 28)
            };
            Color ink = new Color32(33, 26, 17, 255);
            Color brassShadow = new Color32(100, 72, 27, 255);
            Color brassFace = new Color32(162, 123, 48, 255);
            Color bronze = new Color32(146, 105, 36, 255);
            Color goldEdge = new Color32(211, 171, 81, 255);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float bevel = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(12, 45, y));
                Color face = Color.Lerp(brassShadow, brassFace, bevel);
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = ArrowOutlineDistance(point, outline);
                    Color color = Color.Lerp(ink, Color.Lerp(bronze, goldEdge, bevel), Mathf.Clamp01(distance - 1.5f));
                    color = Color.Lerp(color, face, Mathf.Clamp01(distance - 4f));
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
