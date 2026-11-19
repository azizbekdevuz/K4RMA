using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        public void BeginCounter() => BeginCounterAt(Time.time);
        internal void BeginCounterAt(float pressedAt)
        {
            FaceAction();
            techniques.ArmCounter(pressedAt, game.Config.Techniques.counterWindow);
            animator.PlayPlayerAction("Parry", Mathf.Max(0, pressedAt + game.Config.Techniques.counterWindow - Time.time));
        }
        public void BeginGuard(float seconds)
        {
            FaceAction();
            aura.flipX = Facing < 0;
            guardUntil = Time.time + seconds; animator.PlayPlayerAction("Guard", seconds);
            KarmaAudio.Play(KarmaSfxId.GdShieldActivate);
        }
        public void TakeDamage(float damage)
        { TakeDamage(damage, true); }
        internal void TakeDamage(float damage, bool counterDamage) => TakeDamageAt(damage, counterDamage, Time.time);
        internal void TakeDamageAt(float damage, bool counterDamage, float hitAt)
        {
            if (!game.AllowsPlayerActions || Health <= 0 || damage <= 0) return;
            bool invulnerable = Time.time < invulnerableUntil;
            if ((!invulnerable || !counterDamage) && techniques.ConsumeCounter(hitAt))
            {
                ParryCount++;
                KarmaAudio.Play(KarmaSfxId.CombatPerfectParry);
                animator.PlayPlayerAction("Riposte", 0.3f);
                invulnerableUntil = Time.time + 0.12f;
                Essences.NotifyUnique(Essence.Guard);
                if (!counterDamage) return;
                using var snapshot = game.GetEnemySnapshot(out var targets);
                foreach (var enemy in targets)
                {
                    if (enemy == null || !enemy.Alive) continue;
                    if (Vector2.Distance(enemy.ClosestDamagePoint(Position), Position) < KarmaSpace.Units(3.5f)) enemy.TakeDamage(game.Config.Techniques.counterDamage);
                    else
                    {
                        Vector2 aim = ((Vector2)enemy.DamageBounds.center - Position).normalized;
                        KarmaProjectile.Spawn(game, Position + aim * KarmaSpace.Units(0.7f), aim * KarmaSpace.Units(14),
                            game.Config.Techniques.counterDamage, game.Config.guardColor, KarmaSpace.Units(0.25f));
                    }
                }
                return;
            }
            if (invulnerable) return;
            if (Shielded)
            {
                KarmaAudio.Play(KarmaSfxId.CombatGuardBlock);
                game.Tutorial?.GuardBlocked();
                var blockVisual = KarmaCombatVFX.SpawnSprite(game.World, Position, "PlayerVFX", "Barrier", KarmaSpace.Units(new Vector2(1.6f, 2.1f)), 0.2f);
                if (blockVisual != null) blockVisual.transform.parent.localScale = new Vector3(Facing, 1, 1);
                return;
            }
            if (game.Tutorial != null || TestInvincible) return;
            float actual = Mathf.Min(Health, damage);
            CancelAirSlash();
            FaceAction(); animator.Hurt();
            Health -= actual; game.DamageTaken += actual;
            KarmaAudio.Play(KarmaSfxId.PlDamage);
            invulnerableUntil = Time.time + game.Config.hitInvulnerability;
            animator.StartDamageFlash(game.Config.hitInvulnerability);
            if (Health <= 0) game.PlayerDied();
        }
        internal bool TakeTerrainDamage(float damage)
        {
            if (!game.IsCombat || game.Paused || Health <= 0 || damage <= 0 || TestInvincible
                || Time.time < invulnerableUntil) return false;
            float actual = Mathf.Min(Health, damage);
            Health -= actual;
            game.DamageTaken += actual;
            animator.StartDamageFlash(0.18f);
            KarmaAudio.Play(KarmaSfxId.PlDamage);
            if (Health <= 0) game.PlayerDied();
            return true;
        }
        public void Heal(float amount) { Health = Mathf.Min(game.Config.playerHealth, Health + amount); }
    }
}
