using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void FireRing(Color primary, Color secondary, float overrideDamage = -1)
        {
            int count = Mathf.Max(4, game.Config.fusionRingProjectiles) + (CombatPhase - 1) * 2;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                KarmaProjectile.Spawn(game, Position + outward * 0.8f,
                    outward * game.Config.fusionProjectileSpeed, overrideDamage >= 0 ? overrideDamage : Damage,
                    false, i % 2 == 0 ? primary : secondary, 0.25f);
            }
            KarmaVisuals.Flash(game.World, Position, Vector2.one * 2.5f, primary, 0.25f);
        }
        void FireFan(int count, Color color, float speed)
        {
            Vector2 target = (game.Player.Position - Position).normalized;
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * 16;
                Vector2 velocity = (Quaternion.Euler(0, 0, angle) * (Vector3)target) * speed;
                KarmaProjectile.Spawn(game, Position + target * 0.75f, velocity, Damage,
                    false, color, 0.22f);
            }
        }
        public void TakeDamage(float amount)
        {
            if (!Alive || !game.IsCombat || amount <= 0) return;
            engaged = true;
            if (Invulnerable) return;
            if (FlameWardActive) game.Player.ApplyBurn(Position);
            if (!game.IsCombat) return;
            if (Armored)
            {
                float received = amount * Mathf.Clamp01(game.Config.enemyWardDamageMultiplier);
                if (reflectedDamage <= 0) reflectAt = Time.time + Tuning.reflectDelay;
                reflectedDamage += (amount - received) * Tuning.reflectMultiplier;
                amount = received;
                Intent = "반사 충격파 충전: " + Mathf.CeilToInt(reflectedDamage) + " 피해 / 공격을 멈추고 회피!";
                KarmaRemnantVFX.Impact(game, Position, Essence.Ward);
            }
            ReceiveDamage(amount);
        }
        void ReceiveDamage(float amount)
        {
            if (Stunned) amount *= Tuning.stunDamageMultiplier;
            Health = Mathf.Max(0, Health - Mathf.Max(0, amount));
            KarmaVisuals.Flash(game.World, Position, Vector2.one * 0.7f, Color.white, 0.1f);
            if (Health <= 0)
            {
                overlay.Cancel(); burn.Clear(); reflectedDamage = 0; ClearIndicator();
                game.EnemyDefeated(this);
                Destroy(gameObject);
            }
        }
        void ReleaseReflection()
        {
            if (reflectedDamage <= 0 || !game.IsCombat) return;
            float amount = reflectedDamage; reflectedDamage = 0;
            FireRing(game.Config.wardColor, Color.white, amount);
        }
    }
}
