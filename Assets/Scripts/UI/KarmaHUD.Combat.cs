using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaHUD
    {
        KarmaEnemy hudEnemy;
        float shownEnemyHealth = 1f, previousEnemyHealth = 1f, bossDamageAt = -1f;
        KarmaPlayer hudPlayer;
        float previousPlayerHealth, playerDamageAt = -1f;

        void TopBar()
        {
            if (game.Player == null) return;
            var healthDiamond = KarmaArtwork.Sprite("UI", "Health");
            var healthOutline = KarmaArtwork.Sprite("UI", "HealthOutline");
            float health = Mathf.Clamp01(game.Player.Health / Mathf.Max(1, game.Config.playerHealth)) * 5;
            if (hudPlayer != game.Player) playerDamageAt = -1f;
            else if (health < previousPlayerHealth) playerDamageAt = Time.unscaledTime;
            hudPlayer = game.Player;
            previousPlayerHealth = health;
            float hit = playerDamageAt < 0 ? 0 : 1 - Ease((Time.unscaledTime - playerDamageAt) / 0.34f);

            var strip = new Rect(21, 12, 178, 36);
            Ornament(strip, UiOrnament.HealthBacking, Color.white);
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(40 + i * 30, 20, 22, 22);
                SpriteArt(r, healthOutline, Alpha(Ivory, 0.72f));
                float fraction = Mathf.Clamp01(health - i);
                if (fraction <= 0) continue;
                GUI.BeginGroup(new Rect(r.x, r.y, r.width * fraction, r.height));
                SpriteArt(new Rect(0, 0, r.width, r.height), healthDiamond, Color.Lerp(Ivory, TechniqueColors[0], hit));
                GUI.EndGroup();
            }

            var owned = game.Player.Essences.PersonalizationOrder;
            for (int i = 0; i < owned.Count; i++)
            {
                var essence = owned[i];
                var r = new Rect(25 + i * 92, 53, 79, 49);
                bool cooling = game.Player.Essences.Cooldown(essence) > 0;
                bool triggered = game.Player.Essences.JustTriggered(essence);
                if (i > 0) BrushDivider(new Rect(r.x - 4, 58, 3, 35), Alpha(Ivory, 0.75f));
                if (triggered) Frame(r, UiFrame.SelectedCard, Alpha(Color.white, 0.72f));
                TechniqueIcon(r, essence, triggered || !cooling ? 1 : 0.36f);
            }

            var enemy = game.Enemies.Count > 0 ? game.Enemies[0] : null;
            if (enemy == null || !enemy.Alive)
            {
                hudEnemy = null;
                return;
            }

            float ratio = Mathf.Clamp01(enemy.Health / Mathf.Max(1, enemy.MaxHealth));
            if (hudEnemy != enemy || ratio > previousEnemyHealth)
            {
                shownEnemyHealth = ratio;
                bossDamageAt = -1f;
            }
            else if (ratio < previousEnemyHealth)
            {
                shownEnemyHealth = Mathf.Max(shownEnemyHealth, previousEnemyHealth);
                bossDamageAt = Time.unscaledTime;
            }
            hudEnemy = enemy;
            previousEnemyHealth = ratio;
            if (Event.current.type == EventType.Repaint && !game.Paused)
                shownEnemyHealth = Mathf.MoveTowards(shownEnemyHealth, ratio, Time.unscaledDeltaTime * 0.85f);

            TintTexture(new Rect(336, 9, 608, 64), titleShade, Alpha(DeepInk, 0.83f));
            ThemeLabel(new Rect(336, 10, 608, 32), enemy.DisplayName, bossName, Ivory);
            var track = new Rect(350, 56, 580, 9);
            float flash = bossDamageAt < 0 ? 0 : 1 - Ease((Time.unscaledTime - bossDamageAt) / 0.27f);
            ProgressTrack(track, ratio, shownEnemyHealth, flash);
        }
    }
}
