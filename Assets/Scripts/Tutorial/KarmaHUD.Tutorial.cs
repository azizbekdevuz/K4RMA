using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly string[] tutorialLabels = { "기본 공격", "검기 명중", "상승베기 명중", "방어 성공" };
        static readonly KarmaAction[] tutorialKeys = { KarmaAction.Attack, KarmaAction.SwordWave, KarmaAction.RisingSlash, KarmaAction.Guard };
        Texture2D tutorialRouteArrow;
        GUIStyle trainingTargetLabel;

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
                DrawSpeakerNameplate(275, 513, t.SpeechTitle);
                Label(275, 550, 730, 50, "공격이 닿기 전, 보호의 힘을 펼쳐라.", centered);
                float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 5) * 0.2f;
                var old = GUI.color; GUI.color = new Color(1, 0.9f, 0.6f, pulse);
                string guardKey = KarmaInput.BindingLabel(KarmaAction.Guard);
                string instruction = guardKey == "K" ? "를 누르면 공격을 막을 수 있습니다." : " 키를 누르면 공격을 막을 수 있습니다.";
                KeyPrompt(new Rect(275, 615, 730, 55), 23, true, KarmaAction.Guard, instruction); GUI.color = old;
                return;
            }
            if (!string.IsNullOrEmpty(t.Speech))
            {
                Panel(new Rect(210, 550, 860, 140));
                if (t.Step == KarmaTutorialStep.Complete)
                    Label(235, 560, 810, 25, tutorialLabels[t.Lesson] + " 3/3 · 수련 완료", small);
                else
                    DrawSpeakerNameplate(235, 523, t.SpeechTitle);
                float dialogueTop = t.Step == KarmaTutorialStep.Complete ? 588 : 560;
                Label(235, dialogueTop, 810, 65, t.Speech, text);
                KeyPrompt(new Rect(235, 655, 810, 25), 14, false, KarmaAction.DialogueConfirm, "  계속");
                return;
            }
            if (t.Step == KarmaTutorialStep.Practice && t.Lesson >= 0 && t.Lesson < 3)
            {
                Bounds target = t.ActiveTargetBounds;
                bool approach = t.Lesson != 1
                    && Mathf.Abs(game.Player.Position.x - target.center.x) > target.extents.x + KarmaSpace.Units(2);
                DrawTrainingTarget(target, approach);
                if (approach) DrawTutorialRouteArrow(target.center.x);
            }

            bool outsideGate = t.Step == KarmaTutorialStep.Gate && Mathf.Abs(game.Player.Position.x - KarmaTutorialStage.FinalGate) > 5;
            if (t.Step == KarmaTutorialStep.Travel)
                DrawTutorialRouteArrow(KarmaTutorial.Stations[t.Lesson + 1]);
            else if (outsideGate) DrawTutorialRouteArrow(KarmaTutorialStage.FinalGate);

            string blockedMessage = t.BlockedMessage;
            if (!string.IsNullOrEmpty(blockedMessage))
            {
                Panel(new Rect(210, 120, 860, 70));
                DrawSpeakerNameplate(235, 93, t.SpeechTitle);
                Label(230, 130, 820, 50, blockedMessage, centered);
            }
            Panel(new Rect(210, 615, 860, 65));
            var objective = new Rect(230, 630, 820, 42);
            if (outsideGate)
                GUI.Label(objective, "모든 훈련을 마쳤습니다. 관문으로 이동하면 다음 시련을 시작할 수 있습니다.", centered);
            else if (t.Step == KarmaTutorialStep.Gate)
                KeyPrompt(objective, 18, true, "관문 앞에서  ", KarmaAction.Interact, "  키를 누르면 다음 시련으로 이동합니다.");
            else if (t.Step == KarmaTutorialStep.Travel && t.Lesson < 0)
                KeyPrompt(objective, 18, true, KarmaAction.Left, " / ", KarmaAction.Right, "  이동   ·   ",
                    KarmaAction.Down, "  앉기   ·   ", KarmaAction.Jump, "  점프");
            else if (t.Step == KarmaTutorialStep.Travel)
                GUI.Label(objective, "다음 훈련 장소에서 수련을 이어갈 수 있습니다.", centered);
            else
            {
                string detail = t.Lesson switch
                {
                    1 => "  ·  표적에서 거리를 두고 발사",
                    2 => "  ·  허수아비 가까이에서 올려베기",
                    3 => "  ·  공격이 가까워지면 방어",
                    _ => ""
                };
                if (t.Lesson == 1 || t.Lesson == 2)
                {
                    float cooldown = game.Player.Essences.Cooldown((Essence)(t.Lesson - 1));
                    if (cooldown > 0) detail += "  ·  재사용 대기 " + cooldown.ToString("0.0") + "초";
                }
                KeyPrompt(objective, 18, true, tutorialLabels[t.Lesson] + " " + t.Successes + "/3  ·  ", tutorialKeys[t.Lesson], detail);
            }
        }

        void DrawTrainingTarget(Bounds bounds, bool approach)
        {
            if (Event.current.type != EventType.Repaint) return;
            var world = new Vector3(bounds.center.x, bounds.max.y + 0.35f, bounds.center.z);
            Vector3 point = game.GameCamera.WorldToViewportPoint(world);
            if (point.z <= 0) return;
            if (trainingTargetLabel == null)
            {
                trainingTargetLabel = new GUIStyle(centered) { fontSize = 15, fontStyle = FontStyle.Bold };
                trainingTargetLabel.normal.textColor = trainingTargetLabel.hover.textColor = HudBrass;
            }
            const float halfWidth = 94;
            float x = Mathf.Clamp(point.x * 1280, halfWidth + 10, 1270 - halfWidth);
            float y = Mathf.Clamp((1 - point.y) * 720, 105, 552);
            var label = new Rect(x - halfWidth, y - 32, halfWidth * 2, 30);
            Panel(label);
            string caption = approach ? "가까이 다가가세요" : "훈련 표적";
            if (point.x > 1) caption += "  ›";
            else if (point.x < 0) caption = "‹  " + caption;
            GUI.Label(label, caption, trainingTargetLabel);
            if (point.x >= 0 && point.x <= 1)
                SpriteArt(new Rect(x - 5, y - 3, 10, 10), KarmaArtwork.Sprite("UI", "FilledSlot"), HudBrass);
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
