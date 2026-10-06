using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        readonly KarmaAirSlashMotion airSlashMotion = new KarmaAirSlashMotion();
        readonly HashSet<KarmaEnemy> airSlashHits = new HashSet<KarmaEnemy>();
        bool airSlashRunning, airSlashConnected;
        Vector2 airSlashDirection, airSlashPrevious;
        float airSlashBufferedUntil, airRecoveryLeft, airLaunchX;
        bool CanSword() { return game.IsCombat && Health > 0 && Time.time >= nextSword && !airSlashRunning && !KnockedBack && !Shielded && !techniques.CounterReady(Time.time); }
        Vector2 AttackDirection()
        {
            float vertical = KarmaInput.Vertical;
            Vector2 direction = new Vector2(KarmaInput.Horizontal, vertical);
            return direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right * Facing;
        }
        void ReadUniqueDirectionTap()
        {
            if (!Essences.HasRemnant(Essence.Flame) || !CanSword()) { techniques.ClearDirectionTap(); return; }
            int direction = 0, count = 0;
            if (KarmaInput.DirectionPressed(1)) { direction = 1; count++; }
            if (KarmaInput.DirectionPressed(2)) { direction = 2; count++; }
            if (KarmaInput.DirectionPressed(3)) { direction = 3; count++; }
            if (KarmaInput.DirectionPressed(4)) { direction = 4; count++; }
            if (count > 1) { techniques.ClearDirectionTap(); return; }
            if (count == 0 || !techniques.DirectionTap(direction, Time.time, game.Config.Techniques.doubleTapWindow)) return;
            Vector2 aim = direction == 1 ? Vector2.left : direction == 2 ? Vector2.right : direction == 3 ? Vector2.up : Vector2.down;
            BeginDirectionalSlash(aim, game.Config.Techniques.pierceDistance, Essence.Flame);
        }
        void Attack()
        {
            if (!CanSword()) return;
            // Preserve a simultaneous jump + attack press through the takeoff frame.
            if (Essences.HasRemnant(Essence.Dash) && airSlashBufferedUntil > Time.time &&
                jumpBufferedUntil > Time.time && !Airborne) return;
            if (Essences.HasRemnant(Essence.Dash) && Airborne && airSlashBufferedUntil > Time.time && techniques.TryAirSlash())
            {
                BeginAirSlash(AttackDirection());
                return;
            }
            animator.Attack();
            nextSword = Time.time + game.Config.swordCooldown;
            var swordTuning = game.Config.Techniques;
            KarmaRemnantVFX.SwordSlash(game.World, Position, Vector2.right * Facing,
                swordTuning.longSlashReach, swordTuning.longSlashHeight, swordTuning.slashLife);
            bool hit = false;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive) continue;
                var delta = enemy.Position - Position;
                if (Mathf.Abs(delta.y) <= swordTuning.longSlashHeight * 0.5f && delta.x * Facing >= -0.35f && Mathf.Abs(delta.x) <= swordTuning.longSlashReach)
                { SwordHit(enemy, Vector2.right * Facing); hit = true; }
            }
            if (hit && game.IsCombat) Essences.OnSwordConnected();
        }
        void BeginDirectionalSlash(Vector2 direction, float distance, Essence essence)
        {
            animator.Attack(direction); nextSword = Time.time + game.Config.swordCooldown;
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            Vector2 from = Position;
            Vector2 to = MoveSlash(direction, distance);
            Color color = game.Config.ColorOf(essence);
            var flash = KarmaVisuals.Box(game.World, "Directional slash", (from + to) * 0.5f,
                new Vector2(Mathf.Max(0.2f, Vector2.Distance(from, to)), 0.3f), color, 10);
            flash.gameObject.AddComponent<KarmaEffect>().Initialize(0.2f);
            flash.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            KarmaRemnantVFX.SwordSlash(game.World, to, direction, 1.6f, 2.1f, game.Config.Techniques.slashLife);
            Essences.NotifyUnique(essence);
            bool hit = false;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive) continue;
                if (KarmaProjectile.Intersects(from, to, enemy.Position, 1.3f))
                { SwordHit(enemy, direction); hit = true; }
            }
            if (hit && game.IsCombat) Essences.OnSwordConnected();
        }
        void BeginAirSlash(Vector2 direction)
        {
            var tuning = game.Config.Techniques;
            airSlashDirection = direction;
            airSlashPrevious = Position;
            airSlashMotion.Begin(tuning.airSlashDuration);
            airSlashRunning = true; airSlashConnected = false;
            airSlashHits.Clear(); airSlashBufferedUntil = 0; airRecoveryLeft = 0;
            jumpBufferedUntil = 0; lastGrounded = -10; dashUntil = 0;
            nextSword = Time.time + Mathf.Max(game.Config.swordCooldown, tuning.airSlashDuration);
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            animator.Attack(direction);
            Essences.NotifyUnique(Essence.Dash);
            KarmaRemnantVFX.SwordSlash(transform, Vector2.zero, direction, 1.6f, 1.8f, tuning.airSlashDuration + 0.04f);
        }
        void DamageAirSlash()
        {
            Vector2 from = airSlashPrevious + airSlashDirection * 0.35f;
            Vector2 to = Position + airSlashDirection * 0.85f;
            airSlashPrevious = Position;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive || airSlashHits.Contains(enemy)) continue;
                if (!KarmaProjectile.Intersects(from, to, enemy.Position, 0.75f)) continue;
                airSlashHits.Add(enemy);
                SwordHit(enemy, airSlashDirection);
                if (!airSlashConnected && game.IsCombat)
                { Essences.OnSwordConnected(); airSlashConnected = true; }
            }
        }
        void SwordHit(KarmaEnemy enemy, Vector2 direction)
        {
            float before = enemy.Health;
            Vector2 point = enemy.Position;
            enemy.TakeDamage(game.Config.swordDamage);
            if (enemy.Health >= before) return;
            KarmaRemnantVFX.SwordImpact(game, point, direction);
            game.SwordHitFeedback();
        }
        void CancelAirSlash()
        {
            airSlashRunning = false; airSlashMotion.Reset(); airSlashHits.Clear();
            airSlashBufferedUntil = airRecoveryLeft = 0;
        }
        Vector2 MoveSlash(Vector2 direction, float distance)
        {
            Vector2 from = Position;
            Vector2 to = from + direction * Mathf.Max(0, distance);
            to.x = Mathf.Clamp(to.x, 0.2f, 39.8f);
            float travel = Vector2.Distance(from, to);
            direction = travel > 0 ? (to - from).normalized : direction;
            var hits = Physics2D.BoxCastAll(from, new Vector2(0.7f, 1.55f), 0, direction, travel);
            foreach (var h in hits)
            {
                if (h.collider.isTrigger || h.collider.GetComponent<KarmaSurface>() == null) continue;
                // One-way platforms block only downward crossings from above.
                if (h.collider.GetComponent<PlatformEffector2D>() != null &&
                    (direction.y >= 0 || from.y - 0.775f < h.collider.bounds.max.y - 0.05f)) continue;
                // Ignore contacts we are moving away from (e.g. jumping off the floor).
                if (Vector2.Dot(h.normal, direction) >= -0.001f) continue;
                travel = Mathf.Min(travel, Mathf.Max(0, h.distance - 0.05f));
            }
            to = from + direction * travel;
            body.position = to; transform.position = to;
            return to;
        }
        public void BeginRisingSlash()
        {
            CancelAirSlash(); animator.Attack(Vector2.up); body.linearVelocity = new Vector2(body.linearVelocity.x, game.Config.Techniques.risingSpeed);
            KarmaVisuals.Flash(game.World, Position + Vector2.up, new Vector2(1.1f, 3), game.Config.dashColor, 0.25f);
            foreach (var enemy in game.Enemies.ToArray())
                if (enemy != null && Mathf.Abs(enemy.Position.x - Position.x) < 2.1f && enemy.Position.y >= Position.y - 1 && enemy.Position.y <= Position.y + 3)
                    enemy.TakeDamage(game.Config.Techniques.risingDamage);
        }
        public void BeginCounter() { techniques.ArmCounter(Time.time, game.Config.Techniques.counterWindow); }
        public void BeginWard(float seconds) { wardUntil = Time.time + seconds; }
        public void TakeDamage(float damage)
        {
            if (!game.IsCombat || Health <= 0 || damage <= 0 || Time.time < invulnerableUntil) return;
            if (techniques.ConsumeCounter(Time.time))
            {
                invulnerableUntil = Time.time + 0.12f;
                Essences.NotifyUnique(Essence.Ward);
                foreach (var enemy in game.Enemies.ToArray())
                {
                    if (enemy == null) continue;
                    if (Vector2.Distance(enemy.Position, Position) < 3.5f) enemy.TakeDamage(game.Config.Techniques.counterDamage);
                    else
                    {
                        Vector2 aim = (enemy.Position - Position).normalized;
                        KarmaProjectile.Spawn(game, Position + aim * 0.7f, aim * 14,
                            game.Config.Techniques.counterDamage, true, game.Config.wardColor, 0.25f);
                    }
                }
                return;
            }
            if (Shielded) { KarmaVisuals.Flash(game.World, Position, new Vector2(1.6f, 2.1f), game.Config.wardColor); return; }
            float actual = Mathf.Min(Health, damage);
            CancelAirSlash();
            animator.Hurt();
            Health -= actual; game.DamageTaken += actual;
            invulnerableUntil = Time.time + game.Config.hitInvulnerability;
            KarmaVisuals.Flash(game.World, Position, Vector2.one, new Color(1, 0.2f, 0.3f));
            if (Health <= 0) game.PlayerDied();
        }
        public void TakeImpact(float damage, Vector2 source, float speed, float duration = 0.3f)
        {
            float before = Health; TakeDamage(damage);
            if (Health >= before || Health <= 0 || !game.IsCombat) return;
            knockbackDirection = Position.x < source.x ? -1 : Position.x > source.x ? 1 : -Facing;
            knockbackSpeed = speed; knockbackUntil = Time.time + duration;
            body.linearVelocity = new Vector2(knockbackDirection * speed, 4);
        }
        public void Heal(float amount) { Health = Mathf.Min(game.Config.playerHealth, Health + amount); }
    }
}
