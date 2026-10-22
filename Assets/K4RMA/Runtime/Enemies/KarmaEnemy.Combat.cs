using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        public void TakeDamage(float amount) { TakeDamage(amount, game.Player.Position); }
        public void TakeDamage(float amount, Vector2 source)
        {
            if (!Alive || !game.IsCombat || amount <= 0 || stopped) return;
            engaged = true;
            if (fullShield) return;
            bool front = frontalGuard && (source.x - Position.x) * direction >= 0;
            bool reduced = front || swordWard || FightSeconds < shieldUntil;
            if (swordWard && FightSeconds < swordWardUntil && FightSeconds >= nextWardResponse && !wardResponsePending)
            {
                wardResponsePending = true;
                nextWardResponse = FightSeconds + Mathf.Max(0.5f, Tuning.swordWardResponseCooldown);
            }
            if (reduced)
            {
                amount *= Mathf.Clamp01(Tuning.shieldDamageMultiplier);
                KarmaRemnantVFX.Impact(game, Position, Essence.Ward);
            }
            ReceiveDamage(amount);
        }
        void ReceiveDamage(float amount)
        {
            if (amount <= 0) return;
            animator.Hurt(); Health = Mathf.Max(0, Health - amount);
            KarmaVisuals.Flash(game.World, Position, Vector2.one * KarmaSpace.Units(0.7f), Color.white, 0.1f);
            if (Health <= 0)
            {
                CancelCombat(); art.SetParent(game.World, true); animator.Die();
                Destroy(art.gameObject, 0.45f); game.EnemyDefeated(this); Destroy(gameObject);
            }
        }
    }
}
