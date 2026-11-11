using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        public void BeginCounter()
        {
            FaceAction();
            techniques.ArmCounter(Time.time, game.Config.Techniques.counterWindow);
            animator.PlayPlayerAction("Parry", "Parry", game.Config.Techniques.counterWindow);
        }
        public void BeginGuard(float seconds)
        {
            FaceAction();
            aura.flipX = Facing < 0;
            guardUntil = Time.time + seconds; animator.PlayPlayerAction("Guard", "Guard", seconds);
            KarmaAudio.Play(KarmaSfxId.GdShieldActivate);
        }
        public void TakeDamage(float damage)
        { TakeDamage(damage, false); }
        // Attack owners may supply their confirmed-contact sprite separately.
        // Preserve the public damage API and all other sources' feedback.
        internal void TakeDamage(float damage, bool customHitVisual, bool counterDamage = true)
        {
            if (!game.AllowsPlayerActions || Health <= 0 || damage <= 0) return;
            bool invulnerable = Time.time < invulnerableUntil;
            if ((!invulnerable || !counterDamage) && techniques.ConsumeCounter(Time.time))
            {
                ParryCount++;
                KarmaAudio.Play(KarmaSfxId.CombatPerfectParry);
                animator.PlayPlayerAction("Riposte", "Riposte", 0.3f);
                invulnerableUntil = Time.time + 0.12f;
                Essences.NotifyUnique(Essence.Guard);
                if (!counterDamage) return;
                foreach (var enemy in game.Enemies.ToArray())
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
            if (!customHitVisual)
                KarmaVisuals.Flash(game.World, Position, Vector2.one * KarmaSpace.Scale, new Color(1, 0.2f, 0.3f));
            if (Health <= 0) game.PlayerDied();
        }
        internal bool TakeTerrainDamage(float damage)
        {
            if (!game.IsCombat || game.Paused || Health <= 0 || damage <= 0 || TestInvincible
                || Time.time < invulnerableUntil) return false;
            float actual = Mathf.Min(Health, damage);
            Health -= actual;
            game.DamageTaken += actual;
            KarmaAudio.Play(KarmaSfxId.PlDamage);
            if (Health <= 0) game.PlayerDied();
            return true;
        }
        public void Heal(float amount) { Health = Mathf.Min(game.Config.playerHealth, Health + amount); }
    }
}
