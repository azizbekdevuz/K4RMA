using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Color PatternColor { get { return PrimaryType.HasValue ? game.Config.ColorOf(PrimaryType.Value) : new Color(1, 0.75f, 0.3f); } }
        bool UsesWaveVisuals(EnemyAttack attack) { return PrimaryType == Essence.SwordWave && (attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02
            || attack == EnemyAttack.SW03 || attack == EnemyAttack.SW04 || attack == EnemyAttack.SW05); }
        bool WaveVisuals { get { return UsesWaveVisuals(selected); } }
        SpriteRenderer Mark(string name, Vector2 center, Vector2 size, bool locked = true)
        {
            var color = locked ? new Color(1, 0.55f, 0.2f, 0.3f) : new Color(0.3f, 0.85f, 1, 0.22f);
            var marker = KarmaVisuals.Box(effectsRoot, name, center, size, color, 8);
            marker.gameObject.AddComponent<KarmaAttackWarning>().SetPath(marker, center, center, size, WaveVisuals);
            return marker;
        }
        SpriteRenderer PathMark(Vector2 from, Vector2 to, Vector2 size, bool locked = true)
        {
            if (WaveVisuals)
                to = from + (to - from) * SwordShotLife(from, to - from, 1);
            var marker = Mark(locked ? "고정 공격 경로" : "추적 중인 경로", Vector2.zero, Vector2.one, locked);
            marker.GetComponent<KarmaAttackWarning>().SetPath(marker, from, to, size, false, WaveVisuals,
                WaveVisuals && selected == EnemyAttack.SW05);
            return marker;
        }
        void ClearPatternObjects()
        {
            if (effectsRoot == null) return;
            foreach (Transform child in effectsRoot)
            {
                if (child == swordGuardRoot) continue;
                child.gameObject.SetActive(false); Destroy(child.gameObject);
            }
        }
        KarmaBossAttack Attack(Vector2 at, Vector2 velocity, Vector2 size, float life,
            float multiplier = 1, KarmaAttackContact contact = null, float stun = 0,
            EnemyAttack? pattern = null, Transform parent = null)
        {
            var attack = pattern ?? selected;
            bool wave = UsesWaveVisuals(attack);
            string pose = null;
            if (velocity.sqrMagnitude > 0.01f)
            {
                if (attack == EnemyAttack.SW02) pose = "AimedWave";
                else if (attack == EnemyAttack.SW05 || attack == EnemyAttack.GD03) pose = "Blade";
            }
            else if (attack == EnemyAttack.UP03 || attack == EnemyAttack.GD04) pose = "Pillar";
            return KarmaBossAttack.Spawn(game, this, parent != null ? parent : effectsRoot, at, velocity, size, life,
                Damage * multiplier, PrimaryType == Essence.Guard ? new Color(1, 0.7f, 0.12f) : new Color(1, 0.38f, 0.12f), contact, stun, pose,
                wave, wave && (attack == EnemyAttack.SWBasic || attack == EnemyAttack.SW01 || attack == EnemyAttack.SW02),
                wave ? KarmaWaveArtwork.Pose(attack, velocity.sqrMagnitude <= 0.01f) : null);
        }
        IEnumerator Motion(Vector2 target, float duration, Vector2 hitSize, KarmaAttackContact contact = null)
        {
            Vector2 from = Position; float elapsed = 0;
            var ledger = contact ?? new KarmaAttackContact();
            Vector2 heading = target - from;
            if (Mathf.Abs(heading.x) > 0.01f) { direction = Mathf.Sign(heading.x); art.localScale = new Vector3(Facing * Mathf.Abs(art.localScale.x), art.localScale.y, art.localScale.z); }
            state = ActionState.Attack; animator.Attack(heading.normalized);
            SpriteRenderer sweep = null;
            if (hitSize != Vector2.zero)
            {
                sweep = KarmaCombatVFX.Burst(effectsRoot, from, KarmaArtwork.Sprite("GuardianVFX", "AirSlash"),
                    "Guardian motion slash", hitSize * (WaveVisuals ? 0.9f : 1.4f), duration, 0, new Color(1, 0.45f, 0.15f), WaveVisuals);
                // AirSlash is authored upright: mirror horizontally without turning rising art upside down.
                sweep.transform.parent.localScale = new Vector3(Facing, 1, 1);
            }
            while (elapsed < duration && Active)
            {
                if (game.Paused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                Vector2 before = Position;
                Vector2 to = Vector2.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                transform.position = to;
                if (sweep != null) sweep.transform.parent.position = to;
                if (hitSize != Vector2.zero) TryAttackHit(before, to, hitSize, Damage, ledger, 0);
                yield return null;
            }
            if (sweep != null)
            {
                Destroy(sweep.transform.parent.gameObject);
                if (heading.y < -0.1f && target.y <= KarmaSpace.GuardianGroundY + 0.1f)
                    KarmaCombatVFX.Burst(effectsRoot, target, KarmaArtwork.Sprite("GuardianVFX", "Landing"),
                        "Guardian landing impact", hitSize * new Vector2(2.4f, 1.2f), 0.35f, 0, new Color(1, 0.55f, 0.1f), WaveVisuals);
            }
        }
        public void TryAttackHit(Vector2 from, Vector2 to, Vector2 size, float amount, KarmaAttackContact contact, float stun, Vector2? attackAxis = null)
        {
            if (!Active || game.Paused || contact.Consumed) return;
            var player = game.Player; var p = player.Position;
            var body = WaveVisuals ? SwordPlayerBounds : new Bounds(p, KarmaSpace.PlayerSize);
            p = body.center;
            bool hit = attackAxis.HasValue
                ? KarmaBossGeometry.SweptOrientedBox(from.x, from.y, to.x, to.y, p.x, p.y, attackAxis.Value.x, attackAxis.Value.y,
                    size.x, size.y, body.extents.x, body.extents.y)
                : KarmaBossGeometry.SweptBox(from.x, from.y, to.x, to.y, p.x, p.y, size.x / 2 + body.extents.x, size.y / 2 + body.extents.y);
            if (!hit) return;
            if (!contact.TryConsume()) return;
            int parries = player.ParryCount;
            float before = player.Health;
            player.TakeDamage(amount);
            if (player.ParryCount != parries)
            {
                // Projectile parries remain local; GD02 is the shield-breaking parry.
                if (selected == EnemyAttack.GD02) pendingParry = true;
                return;
            }
            if (player.Health < before)
            { trial.Hit(); if (stun > 0 && Active) player.Stun(stun); }
        }
    }
}
