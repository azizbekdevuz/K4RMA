using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        internal bool SwordAreaOverlaps(KarmaEnemy enemy)
        {
            var tuning = game.Config.Techniques;
            float width = tuning.longSlashReach + KarmaSpace.PlayerHalfWidth;
            Vector2 center = Position + Vector2.right * Facing * (tuning.longSlashReach - KarmaSpace.PlayerHalfWidth) * 0.5f;
            return enemy.OverlapsDamageArea(center, new Vector2(width, tuning.longSlashHeight));
        }

        int trainingExecution;
        float normalSwordPressureUntil;
        internal bool SwordPressureActive { get { return !airSlashRunning && Time.time < normalSwordPressureUntil && Time.time < nextSword; } }
        bool CanSword() { return game.AllowsPlayerActions && Health > 0 && Time.time >= nextSword && !airSlashRunning && !ControlLocked && !Shielded && !techniques.CounterReady(Time.time); }
        Vector2 AttackDirection()
        {
            float vertical = KarmaInput.Vertical;
            Vector2 direction = new Vector2(KarmaInput.Horizontal, vertical);
            return direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right * Facing;
        }
        void ReadUniqueSwordInput()
        {
            if (Essences.HasRemnant(Essence.SwordWave) && CanSword() && KarmaInput.Pressed(KarmaAction.Pierce))
                BeginDirectionalSlash(AttackDirection(), game.Config.Techniques.pierceDistance, Essence.SwordWave);
        }
        void Attack()
        {
            if (!CanSword()) return;
            // Preserve a simultaneous jump + attack press through the takeoff frame.
            if (Essences.HasRemnant(Essence.RisingSlash) && airSlashBufferedUntil > Time.time &&
                jumpBufferedUntil > Time.time && !Airborne) return;
            if (Essences.HasRemnant(Essence.RisingSlash) && Airborne && airSlashBufferedUntil > Time.time && techniques.TryAirSlash())
            {
                BeginAirSlash(AttackDirection());
                return;
            }
            FaceAction(); animator.Attack();
            KarmaAudio.Play(KarmaSfxId.PlSwordSwing);
            int trainingAttack = ++trainingExecution;
            nextSword = Time.time + game.Config.swordCooldown;
            var swordTuning = game.Config.Techniques;
            normalSwordPressureUntil = Time.time + swordTuning.slashLife;
            game.Tutorial?.HitArea(KarmaTrainingAttack.Sword, trainingAttack, Position + Vector2.right * Facing * (swordTuning.longSlashReach - KarmaSpace.PlayerHalfWidth) * 0.5f, new Vector2(swordTuning.longSlashReach + KarmaSpace.PlayerHalfWidth, swordTuning.longSlashHeight));
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
            string piercePose = direction.y > 0.75f ? "PierceUp" : direction.y < -0.25f ? "PierceDown"
                : direction.y > 0.25f ? "PierceDiagonal" : "Pierce";
            animator.PlayPlayerAction(piercePose, "Pierce", 0.22f);
            if (direction.x != 0) Facing = direction.x > 0 ? 1 : -1;
            FaceAction();
            Vector2 from = Position;
            Vector2 to = MoveSlash(direction, distance);
            // MoveSlash teleports the collision body: attacks along the skipped path miss,
            // while the destination remains vulnerable throughout the existing slash visuals.
            if (essence == Essence.SwordWave && (to - from).sqrMagnitude > 0.0001f)
            {
                KarmaAudio.Play(KarmaSfxId.PlDash);
            }
            Color color = game.Config.ColorOf(essence);
            var flash = KarmaVisuals.Box(game.World, "Directional slash", (from + to) * 0.5f,
                new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Vector2.Distance(from, to)), KarmaSpace.Units(0.3f)), color, 10);
            KarmaCombatVFX.ApplySprite(flash, "PlayerVFX", "PierceTrail",
                new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Vector2.Distance(from, to)), KarmaSpace.Units(0.3f)));
            flash.gameObject.AddComponent<KarmaEffect>().Initialize(0.2f);
            flash.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            KarmaRemnantVFX.SwordSlash(game.World, to, direction, KarmaSpace.Units(1.6f), KarmaSpace.Units(2.1f), game.Config.Techniques.slashLife);
            Essences.NotifyUnique(essence);
            foreach (var enemy in game.Enemies.ToArray())
            {
                if (enemy == null || !enemy.Alive) continue;
                if (enemy.IntersectsDamagePath(from, to, KarmaSpace.Units(1.3f)))
                SwordHit(enemy, direction);
            }
        }
        bool SwordHit(KarmaEnemy enemy, Vector2 direction)
        {
            float before = enemy.Health;
            Vector2 point = enemy.ClosestDamagePoint(Position);
            enemy.TakeDamage(game.Config.swordDamage, out bool reduced);
            if (enemy.Health >= before) return false;
            if (!reduced) KarmaAudio.Play(KarmaSfxId.CombatBladeHit);
            KarmaRemnantVFX.SwordImpact(game, point, direction);
            game.SwordHitFeedback();
            return true;
        }
        Vector2 MoveSlash(Vector2 direction, float distance)
        {
            Vector2 from = Position;
            Vector2 to = from + direction * Mathf.Max(0, distance);
            to.x = Mathf.Clamp(to.x, KarmaStage.ArenaLeft + KarmaSpace.PlayerHalfWidth, KarmaStage.ArenaRight - KarmaSpace.PlayerHalfWidth);
            float travel = Vector2.Distance(from, to);
            direction = travel > 0 ? (to - from).normalized : direction;
            KarmaPhysicsQueries.BoxCast(from, KarmaSpace.PlayerSize, direction, travel, movementHits);
            foreach (var h in movementHits)
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
            if (to != from) SuppressLandingAudioIfGrounded();
            return to;
        }
        public void BeginRisingSlash()
        {
            game.Tutorial?.HitArea(KarmaTrainingAttack.Rising, ++trainingExecution, Position + Vector2.up * KarmaSpace.Scale, KarmaSpace.Units(new Vector2(4.2f, 4)));
            CancelAirSlash(); FaceAction(); animator.Attack(Vector2.up); body.linearVelocity = new Vector2(body.linearVelocity.x, game.Config.Techniques.risingSpeed);
            KarmaAudio.Play(KarmaSfxId.PlRisingSlash);
            var risingVisual = KarmaCombatVFX.SpawnSprite(game.World, Position + Vector2.up * KarmaSpace.Scale, "PlayerVFX", "Rising",
                KarmaSpace.Units(new Vector2(1.8f, 3)), 0.25f);
            if (risingVisual != null) risingVisual.transform.parent.localScale = new Vector3(Facing, 1, 1);
            foreach (var enemy in game.Enemies.ToArray())
                if (enemy != null && enemy.Alive && enemy.OverlapsDamageArea(Position + Vector2.up * KarmaSpace.Scale, KarmaSpace.Units(new Vector2(4.2f, 4))))
                {
                    float before = enemy.Health;
                    enemy.TakeDamage(game.Config.Techniques.risingDamage, out bool reduced);
                    if (enemy.Health < before && !reduced) KarmaAudio.Play(KarmaSfxId.CombatBladeHit);
                }
        }
        public void BeginSwordWave() { FaceAction(); animator.PlayPlayerAction("SwordWave", "SwordWave", 0.25f); }
    }
}
