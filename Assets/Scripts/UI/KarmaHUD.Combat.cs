using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly Color HudIvory = new Color(0.97f, 0.93f, 0.81f);

        void TopBar()
        {
            if (game.Player == null) return;
            var healthDiamond = KarmaArtwork.Sprite("UI", "Health");
            var healthOutline = KarmaArtwork.Sprite("UI", "HealthOutline");
            float health = Mathf.Clamp01(game.Player.Health / Mathf.Max(1, game.Config.playerHealth)) * 5;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(47 + i * 23, 43, 20, 20);
                SpriteArt(r, healthOutline, new Color(HudIvory.r, HudIvory.g, HudIvory.b, 0.45f));
                float fraction = Mathf.Clamp01(health - i);
                if (fraction <= 0) continue;
                // Clip the last diamond so small amounts of damage remain visible.
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * fraction, r.height));
                SpriteArt(new Rect(0, 0, r.width, r.height), healthDiamond, Color.white);
                GUI.EndGroup();
            }

            // Only confirmed personalizations appear, packed in acquisition order.
            var owned = game.Player.Essences.PersonalizationOrder;
            for (int i = 0; i < owned.Count; i++)
            {
                var essence = owned[i];
                var r = new Rect(43 + i * 45, 76, 32, 32);
                bool cooling = game.Player.Essences.Cooldown(essence) > 0;
                Color color = HudIvory;
                color.a = cooling ? 0.35f : 1;
                if (game.Player.Essences.JustTriggered(essence)) color = Color.white;
                SpriteArt(r, KarmaArtwork.SkillIcon(essence), color);

            }

            if (game.Enemies.Count == 0 || game.Enemies[0] == null) return;
            var enemy = game.Enemies[0];
            if (!enemy.Alive) return;
            float ratio = Mathf.Clamp01(enemy.Health / Mathf.Max(1, enemy.MaxHealth));
            // Keep the boss readable above the action, clear of the player's corner HUD.
            const float left = 330, width = 620, y = 56;
            Panel(new Rect(left - 16, 16, width + 32, 62));
            Label(left, 20, width, 27, enemy.DisplayName, centered);
            Color track = new Color(HudIvory.r, HudIvory.g, HudIvory.b, DialoguePanelOpacity);
            Fill(new Rect(left, y - 2, width, 0.8f), track);
            Fill(new Rect(left, y + 9.2f, width, 0.8f), track);
            Fill(new Rect(left, y, width * ratio, 8), HudIvory);
            Fill(new Rect(left - 3, y - 5, 1.2f, 20), track);
            Fill(new Rect(left + width + 2, y - 5, 1.2f, 20), track);
            if (game.IsCombat && Event.current.type == EventType.Repaint)
                DrawInheritanceCue(enemy);
        }

        void DrawInheritanceCue(KarmaEnemy enemy)
        {
            if (!enemy.TryGetInheritanceCue(out int mask, out Vector3 shoulder, out float opacity)) return;
            Vector3 viewport = game.GameCamera.WorldToViewportPoint(shoulder);
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return;

            var order = game.Player.Essences.PersonalizationOrder;
            int count = 0;
            foreach (var essence in order)
                if ((mask & (1 << (int)essence)) != 0) count++;

            const float iconSize = 42, spacing = 5;
            float width = count * (iconSize + spacing) - spacing;
            float x = Mathf.Clamp(viewport.x * 1280 - width * 0.5f, 12, 1268 - width);
            float y = Mathf.Max(84, (1 - viewport.y) * 720 - iconSize - 6);
            foreach (var essence in order)
            {
                if ((mask & (1 << (int)essence)) == 0) continue;
                var icon = KarmaArtwork.SkillIcon(essence);
                SpriteArt(new Rect(x + 1, y + 2, iconSize, iconSize), icon, new Color(0, 0, 0, 0.7f * opacity));
                Color tint = KarmaPersonalizationVFX.TechniqueColor(game, essence);
                tint.a = opacity;
                SpriteArt(new Rect(x, y, iconSize, iconSize), icon, tint);
                x += iconSize + spacing;
            }
        }

    }
}
