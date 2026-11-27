using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        static readonly Color HudIvory = new Color(0.97f, 0.93f, 0.81f);

        float combatHudFadeAt;
        string fallenBossName;

        internal void BeginCombatHudFade(string defeatedName)
        {
            combatHudFadeAt = Time.time;
            fallenBossName = defeatedName;
        }

        internal float CombatHudOpacity
        {
            get
            {
                switch (game.Phase)
                {
                    case RunPhase.GuardianFallen:
                    case RunPhase.PlayerFallen:
                    case RunPhase.Ending: return 1 - Ease((Time.time - combatHudFadeAt) / 0.55f);
                    case RunPhase.Choosing: return 0;
                    case RunPhase.Transition: return game.GuardianInheriting && entranceBoss != null
                        ? NameReveal(Time.time - entranceAt) : 0;
                    default: return 1;
                }
            }
        }

        void TopBar()
        {
            float opacity = CombatHudOpacity;
            if (game.Player == null || opacity <= 0) return;
            var healthDiamond = KarmaArtwork.Sprite("UI", "Health");
            var healthOutline = KarmaArtwork.Sprite("UI", "HealthOutline");
            float health = Mathf.Clamp01(game.Player.Health / Mathf.Max(1, game.Config.playerHealth)) * 5;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(47 + i * 23, 43, 20, 20);
                SpriteArt(r, healthOutline, new Color(HudIvory.r, HudIvory.g, HudIvory.b, 0.45f * opacity));
                float fraction = Mathf.Clamp01(health - i);
                if (fraction <= 0) continue;
                // Clip the last diamond so small amounts of damage remain visible.
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * fraction, r.height));
                SpriteArt(new Rect(0, 0, r.width, r.height), healthDiamond, WithOpacity(Color.white, opacity));
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
                if (game.Player.Essences.JustTriggered(essence)) color = HudBrass;
                SpriteArt(r, KarmaArtwork.SkillIcon(essence), WithOpacity(color, opacity));

            }

            var enemy = game.Enemies.Count > 0 ? game.Enemies[0] : null;
            string name = enemy != null && enemy.Alive ? enemy.DisplayName :
                game.Phase == RunPhase.GuardianFallen || game.Phase == RunPhase.Ending ? fallenBossName : null;
            if (name == null) return;
            if (game.IsCombat && enemy != null && enemy.Alive && enemy.CombatImitationLine is string line)
            {
                Panel(new Rect(290, 84, 700, 44), opacity);
                Label(300, 87, 680, 38, line, centered);
            }
            float ratio = enemy != null ? Mathf.Clamp01(enemy.Health / Mathf.Max(1, enemy.MaxHealth)) : 0;
            // Keep the boss readable above the action, clear of the player's corner HUD.
            const float left = 330, width = 620, y = 56;
            Panel(new Rect(left - 16, 16, width + 32, 62), opacity);
            Color saved = GUI.color;
            GUI.color = WithOpacity(saved, opacity);
            Label(left, 20, width, 27, name, centered);
            GUI.color = saved;
            Color track = WithOpacity(HudRule, opacity);
            Fill(new Rect(left, y - 2, width, 0.8f), track);
            Fill(new Rect(left, y + 9.2f, width, 0.8f), track);
            Fill(new Rect(left, y, width * ratio, 8), WithOpacity(HudBrass, opacity));
            Fill(new Rect(left - 3, y - 5, 1.2f, 20), track);
            Fill(new Rect(left + width + 2, y - 5, 1.2f, 20), track);
        }

    }
}
