using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        readonly HashSet<Essence> introducedBosses = new HashSet<Essence>();
        KarmaEnemy entranceBoss;
        float entranceAt, entranceDuration;
        string entranceName;
        Color entranceAccent;
        GUIStyle entranceTitle, entranceLabel;

        // Called only by the existing safe inheritance transition, never LoadRoom.
        // Practice rooms therefore bypass this presentation without an editor dependency.
        internal void BeginBossEntrance(KarmaEnemy boss, float delay = 0.2f)
        {
            if (boss == null || !boss.PrimaryType.HasValue || entranceBoss == boss) return;
            entranceBoss = boss;
            entranceDuration = introducedBosses.Add(boss.PrimaryType.Value) ? 2.8f : 0.9f;
            entranceAt = Time.time + delay;
            entranceName = GuardianEncounterTitle(game.Player.Essences.PersonalizationOrder);
            entranceAccent = EntranceColor(boss.PrimaryType.Value);
        }

        static string GuardianEncounterTitle(IReadOnlyList<Essence> order)
        {
            string title = KarmaPatternCatalog.TypeName(order);
            if (order.Count <= 1) return title;

            var inherited = new string[order.Count - 1];
            for (int i = 1; i < order.Count; i++)
                inherited[i - 1] = KarmaPatternCatalog.Name(order[i]);
            return title + " : " + string.Join("·", inherited) + " 계승";
        }

        static Color EntranceColor(Essence type) => type == Essence.SwordWave
            ? new Color32(114, 191, 255, 255) : type == Essence.RisingSlash
            ? new Color32(255, 119, 113, 255) : new Color32(242, 207, 99, 255);

        float NameReveal(float elapsed) => Ease((elapsed * (2.8f / entranceDuration) - 0.4f) / 0.6f);

        bool DrawBossEntrance()
        {
            if (!game.GuardianInheriting || entranceBoss == null || !entranceBoss.Alive
                || game.Enemies.Count == 0 || game.Enemies[0] != entranceBoss) return false;
            float elapsed = Time.time - entranceAt;
            if (elapsed < 0)
            {
                var growth = game.GuardianGrowth;
                if (growth != null && growth.CaptionAlpha > 0 && !string.IsNullOrEmpty(growth.Caption))
                {
                    float opacity = growth.CaptionAlpha;
                    // A quiet subtitle below the action, with no dialog window,
                    // extra input, or large explanation obscuring the gesture.
                    for (int i = 0; i < 8; i++)
                        Fill(new Rect(0, 616 + i * 13, 1280, 13), new Color(0.008f, 0.012f, 0.018f, i / 7f * 0.45f * opacity));
                    Color before = GUI.color;
                    GUI.color = new Color(0.94f, 0.93f, 0.87f, opacity);
                    Label(new Rect(160, 658, 960, 36), growth.Caption, centered);
                    GUI.color = before;
                }
                return true;
            }
            float t = Mathf.Min(2.2f, elapsed * (2.8f / entranceDuration));
            float frame = Ease(t / 0.4f);
            float reveal = NameReveal(elapsed);
            float alpha = reveal;
            if (entranceTitle == null)
            {
                entranceTitle = new GUIStyle(title) { fontSize = 48, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                entranceLabel = new GUIStyle(small) { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            }
            Fill(new Rect(0, 0, 1280, 720), new Color(0.008f, 0.012f, 0.018f, 0.10f * frame));
            Fill(new Rect(0, 0, 1280, 12 * frame), new Color(0.008f, 0.01f, 0.014f, 0.96f));
            Fill(new Rect(0, 720 - 32 * frame, 1280, 32 * frame), new Color(0.008f, 0.01f, 0.014f, 0.96f));
            // Soft lower-edge shade; no panel covers the guardian's body or revival.
            for (int i = 0; i < 16; i++)
                Fill(new Rect(0, 480 + i * 13, 1280, 13), new Color(0.008f, 0.012f, 0.018f, i / 15f * 0.52f * frame));
            Color old = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            entranceLabel.normal.textColor = entranceAccent;
            entranceLabel.hover.textColor = entranceAccent;
            entranceLabel.active.textColor = entranceAccent;
            entranceLabel.focused.textColor = entranceAccent;

            Label(new Rect(148, 513, 900, 24), "다음 수호자", entranceLabel);
            entranceTitle.normal.textColor = new Color(0.94f, 0.93f, 0.87f);
            // A clipped reveal and small settling movement leave the canonical name intact.
            GUI.BeginGroup(new Rect(148, 542, 970 * reveal, 76));
            Label(new Rect(18 * (1 - reveal), -12 * (1 - reveal), 950, 72), entranceName, entranceTitle);
            GUI.EndGroup();
            GUI.color = old;
            Color accent = entranceAccent; accent.a = alpha;
            Fill(new Rect(148, 629, 680 * reveal, 1), new Color(accent.r, accent.g, accent.b, alpha * 0.35f));
            Fill(new Rect(148, 628, 112 * reveal, 2), accent);
            float flourish = Ease((t - 0.4f) / 0.45f);
            Vector2 center = new Vector2(105, 580);
            if (entranceBoss.PrimaryType == Essence.RisingSlash)
            {
                EntranceLine(center + new Vector2(-18, 30), center + new Vector2(-18, 30) + new Vector2(36, -64) * flourish, 2, accent);
                EntranceLine(center + new Vector2(-8, 30), center + new Vector2(-8, 30) + new Vector2(24, -42) * flourish, 1, accent * 0.6f);
            }
            else if (entranceBoss.PrimaryType == Essence.SwordWave)
            {
                EntranceLine(center + new Vector2(-36, 8), center + new Vector2(-36, 8) + new Vector2(72, -16) * flourish, 2, accent);
                EntranceLine(center + new Vector2(-24, 14), center + new Vector2(-24, 14) + new Vector2(48, -10) * flourish, 1, accent * 0.6f);
            }
            else
            {
                float gap = 14 * (1 - flourish);
                EntranceLine(center + new Vector2(-24-gap, -25), center + new Vector2(-24-gap, 12), 2, accent);
                EntranceLine(center + new Vector2(24+gap, -25), center + new Vector2(24+gap, 12), 2, accent);
                EntranceLine(center + new Vector2(-24-gap, 12), center + new Vector2(0, 28), 1, accent);
                EntranceLine(center + new Vector2(24+gap, 12), center + new Vector2(0, 28), 1, accent);
            }
            float glint = Mathf.Clamp01((t - 0.9f) / 0.35f);
            Fill(new Rect(148 + 680 * glint, 628, 12, 2), new Color(1, 0.96f, 0.83f, Mathf.Sin(glint * Mathf.PI) * alpha));
            if (game.AwaitingBossNameConfirmation)
                KeyPrompt(new Rect(148, 645, 278, 30), 16, false, KarmaAction.DialogueConfirm, "  전투 시작");
            return true;
        }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        static void EntranceLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y-start.y, end.x-start.x) * Mathf.Rad2Deg, start);
            Fill(new Rect(start.x, start.y-width/2, Vector2.Distance(start, end), width), color);
            GUI.matrix = matrix;
        }
    }
}
