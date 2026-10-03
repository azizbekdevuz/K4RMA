using System;
using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEssences
    {
        public void OnSwordConnected()
        {
            swordHits++;
            var c = game.Config;
            Vector2 origin = player.Position + Vector2.right * player.Facing;
            if (HasRemnant(Essence.Flame))
            {
                ShowProc(Essence.Flame);
                foreach (float offset in new[] { -0.22f, 0.22f })
                    KarmaProjectile.Spawn(game, origin, new Vector2(player.Facing * 11, offset * 11),
                        c.emberDamage, true, c.flameColor, 0.13f, Essence.Flame);
            }
            if (HasRemnant(Essence.Dash) && swordHits % Mathf.Max(1, c.waveEveryHits) == 0)
            {
                ShowProc(Essence.Dash);
                KarmaProjectile.Spawn(game, origin, Vector2.right * player.Facing * 16,
                    c.waveDamage, true, c.dashColor, 0.42f, Essence.Dash);
            }
            if (HasRemnant(Essence.Ward) && swordHits % Mathf.Max(1, c.wardBurstEveryHits) == 0)
            {
                ShowProc(Essence.Ward);
                // Snapshot: defeating the last enemy changes the phase during this loop.
                var enemies = game.Enemies.ToArray();
                foreach (var enemy in enemies)
                    if (enemy != null && Vector2.Distance(enemy.Position, player.Position) < c.wardBurstRadius)
                        enemy.TakeDamage(c.wardBurstDamage);
            }
        }
        void ShowProc(Essence e)
        {
            procCounts[(int)e]++;
            lastProc[(int)e] = Time.time;
            KarmaRemnantVFX.Proc(game, player.Position, player.Facing, e);
        }
    }
}
