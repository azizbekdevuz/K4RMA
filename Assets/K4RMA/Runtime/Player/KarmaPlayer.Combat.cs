using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        readonly KarmaAirSlashMotion airSlashMotion = new KarmaAirSlashMotion();
        readonly HashSet<KarmaEnemy> airSlashHits = new HashSet<KarmaEnemy>();
        bool airSlashRunning;
        Vector2 airSlashDirection, airSlashPrevious;
        float airSlashBufferedUntil, airRecoveryLeft, airLaunchX;
        bool CanSword() { return game.IsCombat && Health > 0 && Time.time >= nextSword && !airSlashRunning && !KnockedBack && !Stunned && !Shielded && !techniques.CounterReady(Time.time); }
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
            KarmaArtworkVFX.Player(game,Position,Airborne?11:0,Vector2.right*Facing);
            nextSword = Time.time + game.Config.swordCooldown;
            var swordTuning = game.Config.Techniques;
            KarmaRemnantVFX.SwordSlash(game.World, Position, Vector2.right * Facing,
                swordTuning.longSlashReach, swordTuning.longSlashHeight, swordTuning.slashLife);
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive) continue;
                float width = swordTuning.longSlashReach + KarmaSpace.PlayerHalfWidth;
                Vector2 center = Position + Vector2.right * Facing * (swordTuning.longSlashReach - KarmaSpace.PlayerHalfWidth) * 0.5f;
                if (enemy.OverlapsDamageArea(center, new Vector2(width, swordTuning.longSlashHeight)))
                    SwordHit(enemy, Vector2.right * Facing);
            }
        }
        void BeginDirectionalSlash(Vector2 direction, float distance, Essence essence)
        {
            animator.Attack(direction); nextSword = Time.time + game.Config.swordCooldown;
            animator.PlayPose("Pierce", 0.22f);
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            animator.SpecialPose(direction.y>0?19:direction.y<0?6:17,0.18f);
            Vector2 from = Position;
            Vector2 to = MoveSlash(direction, distance);
            Color color = game.Config.ColorOf(essence);
            var flash = KarmaVisuals.Box(game.World, "Directional slash", (from + to) * 0.5f,
                new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Vector2.Distance(from, to)), KarmaSpace.Units(0.3f)), color, 10);
            KarmaSheetEffects.Apply(flash, "PlayerVFX", "PierceTrail",
                new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Vector2.Distance(from, to)), KarmaSpace.Units(0.3f)));
            flash.gameObject.AddComponent<KarmaEffect>().Initialize(0.2f);
            flash.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            KarmaRemnantVFX.SwordSlash(game.World, to, direction, KarmaSpace.Units(1.6f), KarmaSpace.Units(2.1f), game.Config.Techniques.slashLife);
            KarmaArtworkVFX.Player(game,to,direction.y>0?3:direction.y<0?4:10,direction);
            Essences.NotifyUnique(essence);
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive) continue;
                if (enemy.IntersectsDamagePath(from, to, KarmaSpace.Units(1.3f)))
                SwordHit(enemy, direction);
            }
        }
        void BeginAirSlash(Vector2 direction)
        {
            var tuning = game.Config.Techniques;
            airSlashDirection = direction;
            airSlashPrevious = Position;
            airSlashMotion.Begin(tuning.airSlashDuration);
            airSlashRunning = true;
            airSlashHits.Clear(); airSlashBufferedUntil = 0; airRecoveryLeft = 0;
            jumpBufferedUntil = 0; lastGrounded = -10;
            nextSword = Time.time + Mathf.Max(game.Config.swordCooldown, tuning.airSlashDuration);
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            animator.Attack(direction);
<<<<<<< HEAD
            animator.PlayPose("AirSlash", tuning.airSlashDuration + 0.04f);
=======
            animator.SpecialPose(direction.y<0?16:direction.y>0?19:18,tuning.airSlashDuration);
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c
            Essences.NotifyUnique(Essence.Dash);
            KarmaArtworkVFX.Player(game,Position,direction.y<0?4:3,direction);
            KarmaRemnantVFX.SwordSlash(transform, Vector2.zero, direction, KarmaSpace.Units(1.6f), KarmaSpace.Units(1.8f), tuning.airSlashDuration + 0.04f);
        }
        void DamageAirSlash()
        {
            Vector2 from = airSlashPrevious + airSlashDirection * KarmaSpace.Units(0.35f);
            Vector2 to = Position + airSlashDirection * KarmaSpace.Units(0.85f);
            airSlashPrevious = Position;
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive || airSlashHits.Contains(enemy)) continue;
                if (!enemy.IntersectsDamagePath(from, to, KarmaSpace.Units(0.75f))) continue;
                airSlashHits.Add(enemy);
                SwordHit(enemy, airSlashDirection);
            }
        }
        void SwordHit(KarmaEnemy enemy, Vector2 direction)
        {
            float before = enemy.Health;
            Vector2 point = enemy.ClosestDamagePoint(Position);
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
            to.x = Mathf.Clamp(to.x, KarmaStage.ArenaLeft + KarmaSpace.PlayerHalfWidth, KarmaStage.ArenaRight - KarmaSpace.PlayerHalfWidth);
            float travel = Vector2.Distance(from, to);
            direction = travel > 0 ? (to - from).normalized : direction;
            var hits = Physics2D.BoxCastAll(from, KarmaSpace.PlayerSize, 0, direction, travel);
            foreach (var h in hits)
            {
                if (h.collider.isTrigger || h.collider.GetComponent<KarmaSurface>() == null) continue;
                // One-way platforms block only downward crossings from above.
                if (h.collider.GetComponent<PlatformEffector2D>() != null &&
                    (direction.y >= 0 || from.y - KarmaSpace.PlayerHalfHeight < h.collider.bounds.max.y - KarmaSpace.Units(0.05f))) continue;
                // Ignore contacts we are moving away from (e.g. jumping off the floor).
                if (Vector2.Dot(h.normal, direction) >= -0.001f) continue;
                travel = Mathf.Min(travel, Mathf.Max(0, h.distance - KarmaSpace.Units(0.05f)));
            }
            to = from + direction * travel;
            body.position = to; transform.position = to;
            return to;
        }
        public void BeginRisingSlash()
        {
            KarmaArtworkVFX.Player(game,Position,3,Vector2.up);
            CancelAirSlash(); animator.Attack(Vector2.up); body.linearVelocity = new Vector2(body.linearVelocity.x, game.Config.Techniques.risingSpeed);
            KarmaSheetEffects.Spawn(game.World, Position + Vector2.up * KarmaSpace.Scale, "PlayerVFX", "Rising",
                KarmaSpace.Units(new Vector2(1.8f, 3)), 0.25f);
            foreach (var enemy in game.Enemies.ToArray())
                if (enemy != null && enemy.Alive && enemy.OverlapsDamageArea(Position + Vector2.up * KarmaSpace.Scale, KarmaSpace.Units(new Vector2(4.2f, 4))))
                    enemy.TakeDamage(game.Config.Techniques.risingDamage);
        }
<<<<<<< HEAD
        public void BeginSwordWave() { animator.PlayPose("SwordWave", 0.25f); }
        public void BeginCounter()
        {
            techniques.ArmCounter(Time.time, game.Config.Techniques.counterWindow);
            animator.PlayPose("Parry", game.Config.Techniques.counterWindow);
        }
        public void BeginWard(float seconds)
        {
            wardUntil = Time.time + seconds; animator.PlayPose("Guard", seconds);
        }
=======
        public void BeginCounter() { KarmaArtworkVFX.Player(game,Position,6,Vector2.right*Facing); techniques.ArmCounter(Time.time, game.Config.Techniques.counterWindow); }
        public void BeginWard(float seconds) { KarmaArtworkVFX.Player(game,Position,7,Vector2.right*Facing,seconds); wardUntil = Time.time + seconds; }
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c
        public void TakeDamage(float damage)
        {
            if (!game.IsCombat || Health <= 0 || damage <= 0 || Time.time < invulnerableUntil) return;
            if (techniques.ConsumeCounter(Time.time))
            {
                KarmaArtworkVFX.Player(game,Position,8,Vector2.right*Facing);
                ParryCount++;
                animator.PlayPose("Riposte", 0.3f);
                invulnerableUntil = Time.time + 0.12f;
                Essences.NotifyUnique(Essence.Ward);
                foreach (var enemy in game.Enemies.ToArray())
                {
                    if (enemy == null || !enemy.Alive) continue;
                    if (Vector2.Distance(enemy.ClosestDamagePoint(Position), Position) < KarmaSpace.Units(3.5f)) enemy.TakeDamage(game.Config.Techniques.counterDamage);
                    else
                    {
                        Vector2 aim = ((Vector2)enemy.DamageBounds.center - Position).normalized;
                        KarmaProjectile.Spawn(game, Position + aim * KarmaSpace.Units(0.7f), aim * KarmaSpace.Units(14),
                            game.Config.Techniques.counterDamage, game.Config.wardColor, KarmaSpace.Units(0.25f));
                    }
                }
                return;
            }
<<<<<<< HEAD
            if (Shielded)
            {
                KarmaSheetEffects.Spawn(game.World, Position, "PlayerVFX", "Barrier", KarmaSpace.Units(new Vector2(1.6f, 2.1f)), 0.2f);
                return;
            }
=======
            if (Shielded) { animator.SpecialPose(5,0.14f); KarmaArtworkVFX.Player(game,Position,5,Vector2.right*Facing); KarmaVisuals.Flash(game.World, Position, KarmaSpace.Units(new Vector2(1.6f, 2.1f)), game.Config.wardColor); return; }
>>>>>>> e8d524ec86dec83deb348c14c41617b54d84da4c
            float actual = Mathf.Min(Health, damage);
            CancelAirSlash();
            animator.Hurt();
            if(KarmaArtwork.Settings.useCompleteArtwork) KarmaArtworkVFX.Spawn(game.World,Position,KarmaArtAtlas.Effects(3)[4+UnityEngine.Random.Range(0,4)],Vector2.one*KarmaSpace.Units(1.5f),0.2f);
            Health -= actual; game.DamageTaken += actual;
            invulnerableUntil = Time.time + game.Config.hitInvulnerability;
            KarmaVisuals.Flash(game.World, Position, Vector2.one * KarmaSpace.Scale, new Color(1, 0.2f, 0.3f));
            if (Health <= 0) game.PlayerDied();
        }
        public void TakeImpact(float damage, Vector2 source, float speed, float duration = 0.3f)
        {
            float before = Health; TakeDamage(damage);
            if (Health >= before || Health <= 0 || !game.IsCombat) return;
            knockbackDirection = Position.x < source.x ? -1 : Position.x > source.x ? 1 : -Facing;
            knockbackSpeed = KarmaSpace.Units(speed); knockbackUntil = Time.time + duration;
            body.linearVelocity = new Vector2(knockbackDirection * knockbackSpeed, KarmaSpace.Units(4));
        }
        public void Heal(float amount) { Health = Mathf.Min(game.Config.playerHealth, Health + amount); }
    }
}
