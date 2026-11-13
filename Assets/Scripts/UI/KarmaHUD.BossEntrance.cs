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
            var order = game.Player.Essences.PersonalizationOrder;
            entranceName = KarmaPatternCatalog.TypeName(order);
            entranceAccent = EntranceColor(boss.PrimaryType.Value);
            if (order.Count > 1)
            {
                var inherited = new List<string>();
                for (int i = 1; i < order.Count; i++)
                {
                    string name = KarmaPatternCatalog.Name(order[i]);
                    inherited.Add(name);
                }
                entranceName += " : " + string.Join("·", inherited) + " 계승";
            }
        }

        static Color EntranceColor(Essence type) => type == Essence.SwordWave
            ? new Color32(114, 191, 255, 255) : type == Essence.RisingSlash
            ? new Color32(255, 119, 113, 255) : new Color32(242, 207, 99, 255);

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
                entranceTitle = new GUIStyle(displayText) { fontSize = 48, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                entranceLabel = new GUIStyle(mapLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = false };
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
            GUI.Label(new Rect(148, 513, 900, 24), "수호자 등장", entranceLabel);
            entranceTitle.normal.textColor = Ivory;
            // A clipped reveal and small settling movement leave the canonical name intact.
            GUI.BeginGroup(new Rect(148, 542, 970 * reveal, 76));
            GUI.Label(new Rect(18 * (1 - reveal), -12 * (1 - reveal), 950, 72), entranceName, entranceTitle);
            GUI.EndGroup();
            GUI.color = old;
            Color accent = entranceAccent; accent.a = alpha;
            BrushRule(148, 629, 680 * reveal, Alpha(accent, 0.35f));
            BrushRule(148, 628, 112 * reveal, accent);
            float flourish = Ease((t - 0.4f) / 0.45f);
            Vector2 center = new Vector2(105, 580);
            if (entranceBoss.PrimaryType is Essence type)
                TechniqueIcon(new Rect(center.x - 36, center.y - 27, 72, 54), type, alpha * flourish);
            float glint = Mathf.Clamp01((t - 0.9f) / 0.35f);
            Fill(new Rect(148 + 680 * glint, 628, 12, 2), new Color(1, 0.96f, 0.83f, Mathf.Sin(glint * Mathf.PI) * alpha));
            return true;
        }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
