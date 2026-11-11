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
        internal void BeginBossEntrance(KarmaEnemy boss)
        {
            if (boss == null || !boss.PrimaryType.HasValue || entranceBoss == boss) return;
            entranceBoss = boss;
            entranceDuration = introducedBosses.Add(boss.PrimaryType.Value) ? 2.8f : 0.9f;
            entranceAt = Time.time + 0.2f;
            entranceName = KarmaPatternCatalog.TypeName(game.Player.Essences.PersonalizationOrder);
            entranceAccent = boss.PrimaryType == Essence.RisingSlash ? new Color(0.35f, 0.79f, 0.78f)
                : boss.PrimaryType == Essence.SwordWave ? new Color(0.76f, 0.27f, 0.29f)
                : new Color(0.78f, 0.61f, 0.35f);
        }

        bool DrawBossEntrance()
        {
            if (!game.GuardianInheriting || entranceBoss == null || !entranceBoss.Alive
                || game.Enemies.Count == 0 || game.Enemies[0] != entranceBoss) return false;
            // Suppress the old caption throughout this transition, including the clear exit.
            float elapsed = Time.time - entranceAt;
            if (elapsed < 0 || elapsed >= entranceDuration) return true;
            float t = elapsed * (2.8f / entranceDuration);
            float frame = Ease(t / 0.4f) * (1 - Ease((t - 2.2f) / 0.6f));
            float reveal = Ease((t - 0.4f) / 0.6f);
            float alpha = reveal * (1 - Ease((t - 2.2f) / 0.6f));
            if (entranceTitle == null)
            {
                entranceTitle = new GUIStyle(title) { fontSize = 48, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                entranceLabel = new GUIStyle(small) { fontSize = 11, alignment = TextAnchor.MiddleLeft, wordWrap = false };
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
            GUI.Label(new Rect(148, 513, 900, 24), "B O S S   /   E N C O U N T E R", entranceLabel);
            entranceTitle.normal.textColor = new Color(0.94f, 0.93f, 0.87f);
            // A clipped reveal and small settling movement leave the canonical name intact.
            GUI.BeginGroup(new Rect(148, 542, 970 * reveal, 76));
            GUI.Label(new Rect(18 * (1 - reveal), -12 * (1 - reveal), 950, 72), entranceName, entranceTitle);
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
