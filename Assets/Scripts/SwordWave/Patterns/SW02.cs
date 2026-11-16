using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class SW02Pattern : SwordBossPattern
        {
            public SW02Pattern(KarmaEnemy owner, SwordBossBehavior sword) : base(owner, sword) { }
            public const float Floor = 0.05f;
            const float TrackingSeconds = 0.34f, ReleaseSeconds = 0.10f;
            float AimedSlashSpeed { get { return Mathf.Max(0.1f, Settings.swordSpeed * 1.25f * 1.3f); } }
            Vector2 AimedSlashOrigin(Vector2 from, float facing)
            {
                var size = sword.SwordSize(EnemyAttack.SW02);
                float corridor = sword.SwordPlayerBounds.size.x + 0.2f;
                // An outguard wall cast still needs visible travel and an Update for
                // damage. A zero lifetime would be cleared synchronously by recovery.
                float launchRoom = size.x / 2;
                return new Vector2(Mathf.Clamp(from.x + facing * size.x / 2,
                KarmaStage.ArenaLeft + size.x / 2 + (facing > 0 ? corridor : launchRoom),
                KarmaStage.ArenaRight - size.x / 2 - (facing < 0 ? corridor : launchRoom)),
                Floor + size.y / 2);
            }
            float AimedSlashLife(Vector2 origin, Vector2 velocity)
            { return KarmaBossProjectileGeometry.AimedSlashBoundaryLife(origin, velocity, sword.SwordSize(EnemyAttack.SW02), float.MaxValue); }
            float AimedSlashWarningTime(Vector2 origin, float facing, Vector2 size)
            {
                // At an outguard-facing wall the clamped rear edge can extend behind
                // the boss. Advertise enough time to clear that actual edge on foot.
                var player = sword.SwordPlayerBounds;
                float safeX = origin.x - facing * (size.x / 2 + player.extents.x + 0.15f);
                float travel = Mathf.Max(0, facing * (player.center.x - safeX));
                // Reserve ground-friction margin as well as reaction and fixed-step latency.
                float walkingSpeed = Mathf.Max(0.01f, owner.game.Config.moveSpeed * 0.9f);
                return Mathf.Max(0.8f, travel / walkingSpeed + 0.30f + 2 * Time.fixedDeltaTime);
            }
            void RefreshAim(SpriteRenderer marker, out Vector2 origin, out Vector2 velocity, out float facing)
            {
                float dx = owner.game.Player.Position.x - owner.Position.x;
                facing = Mathf.Abs(dx) > 0.0001f ? Mathf.Sign(dx) : owner.Facing;
                owner.direction = facing;
                var scale = owner.art.localScale; scale.x = Mathf.Abs(scale.x) * facing; owner.art.localScale = scale;
                origin = AimedSlashOrigin(owner.Position, facing);
                velocity = Vector2.right * facing * AimedSlashSpeed;
                marker.GetComponent<KarmaAttackWarning>().SetPath(marker, origin,
                    origin + velocity * AimedSlashLife(origin, velocity), sword.SwordSize(EnemyAttack.SW02), false, true);
            }
            IEnumerator TrackWarning(SpriteRenderer marker, float seconds)
            {
                float elapsed = 0;
                while (elapsed < seconds && owner.Active)
                {
                    if (!owner.game.Paused) RefreshAim(marker, out _, out _, out _);
                    yield return null;
                    if (!owner.game.Paused) elapsed += Time.deltaTime;
                }
            }
            public override IEnumerator Execute()
            {
                owner.state = ActionState.Telegraph;
                Vector2 size = sword.SwordSize(EnemyAttack.SW02);
                float facing = owner.game.Player.Position.x < owner.Position.x ? -1 : 1;
                Vector2 origin = AimedSlashOrigin(owner.Position, facing);
                Vector2 velocity = Vector2.right * facing * AimedSlashSpeed;
                var marker = owner.PathMark(origin, origin + velocity * AimedSlashLife(origin, velocity), size, false);
                KarmaSwordAttackPolish.Warning(marker);
                float tracking = TrackingSeconds * owner.Tempo, elapsed = 0;

                owner.animator.PlayPose("Windup", tracking + 0.18f);
                owner.animator.BeginSW02Presentation(tracking);
                KarmaSwordWaveAnimation.Charge(owner.game, owner, owner.art, marker.transform, tracking, true, true);
                while (elapsed < tracking && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    RefreshAim(marker, out origin, out velocity, out facing);
                    elapsed += Time.deltaTime; yield return null;
                }
                if (!owner.Active) yield break;
                marker.gameObject.SetActive(false); Destroy(marker.gameObject);
                float life = AimedSlashLife(origin, velocity);
                marker = owner.PathMark(origin, origin + velocity * life, size, false);
                KarmaSwordAttackPolish.Warning(marker);
                marker.GetComponent<KarmaAttackWarning>().Emphasize();

                // Retain the same preparation budget. The presentation's Locked
                // phase is only its pose clock; targeting continues until firing.
                float warning = AimedSlashWarningTime(origin, facing, size);
                KarmaSwordWaveAnimation.Charge(owner.game, owner, owner.art, marker.transform, warning, true);
                owner.animator.PrepareWaveAttack(warning, true);
                owner.animator.LockSW02Presentation(warning);
                yield return TrackWarning(marker, warning);
                if (!owner.Active) yield break;
                RefreshAim(marker, out origin, out velocity, out facing);
                life = AimedSlashLife(origin, velocity);
                marker.gameObject.SetActive(false); Destroy(marker.gameObject);
                owner.state = ActionState.Attack; owner.animator.Attack(Vector2.right * facing);
                owner.animator.ReleaseSW02Presentation();
                var shot = owner.Attack(origin, velocity, size, life, Settings.aimedDamageMultiplier,
                    parent: sword.PersistentRoot);
                KarmaSwordAttackPolish.Projectile(owner.game, owner, shot, velocity, size);
                sword.WaveLaunchVFX(origin, velocity, size, true);
                yield return owner.Wait(ReleaseSeconds);
                owner.animator.RecoverSW02Presentation();
                yield return owner.Recovery(1.1f);
            }
            public override void Interrupt() { owner.animator.CancelSW02Presentation(); }
            public override bool CanReach(Vector2 from, Vector2 target)
            {
                float facing = target.x < from.x ? -1 : 1;
                Vector2 origin = AimedSlashOrigin(from, facing), velocity = Vector2.right * facing * AimedSlashSpeed;
                float life = AimedSlashLife(origin, velocity);
                float duration = TrackingSeconds * owner.Tempo + AimedSlashWarningTime(origin, facing, sword.SwordSize(EnemyAttack.SW02)) + ReleaseSeconds
                    + Mathf.Max(0.38f, 1.1f * Mathf.Clamp(owner.Tuning.recoveryMultiplier, 0.4f, 2) * owner.Tempo * sword.Balance.recoveryScale);
                return duration < Mathf.Max(5, owner.Tuning.patternTimeout)
                    && sword.SwordShotCanReach(origin, velocity, sword.SwordSize(EnemyAttack.SW02), life, target);
            }

        }
    }
}
