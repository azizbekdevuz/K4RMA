using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Color PatternColor { get { return PrimaryType.HasValue ? game.Config.ColorOf(PrimaryType.Value) : new Color(1, 0.75f, 0.3f); } }
        SpriteRenderer Mark(string name, Vector2 center, Vector2 size, bool locked = true)
        {
            var color = locked ? new Color(1, 0.55f, 0.2f, 0.3f) : new Color(0.3f, 0.85f, 1, 0.22f);
            var marker = KarmaVisuals.Box(effectsRoot, name, center, size, color, 8);
            marker.gameObject.AddComponent<KarmaAttackWarning>().SetPath(marker, center, center, size);
            return marker;
        }
        SpriteRenderer PathMark(Vector2 from, Vector2 to, Vector2 size, bool locked = true)
        {
            var marker = Mark(locked ? "고정 공격 경로" : "추적 중인 경로", Vector2.zero, Vector2.one, locked);
            marker.GetComponent<KarmaAttackWarning>().SetPath(marker, from, to, size);
            return marker;
        }
        void ClearPatternObjects()
        {
            if (effectsRoot == null) return;
            foreach (Transform child in effectsRoot)
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        KarmaBossAttack Attack(Vector2 at, Vector2 velocity, Vector2 size, float life,
            float multiplier = 1, KarmaAttackContact contact = null, float stun = 0, bool renderArtwork = true)
        {
            string pose = null;
            if (velocity.sqrMagnitude > 0.01f)
            {
                if (selected == EnemyAttack.SW02) pose = "AimedWave";
                else if (selected == EnemyAttack.SW05) pose = "Blade";
                else if (selected == EnemyAttack.GD03) pose = "Ward";
            }
            else if (selected == EnemyAttack.UP03 || selected == EnemyAttack.GD04) pose = "Pillar";
            string sheet = "GuardianVFX";
            if (PrimaryType == Essence.Ward)
            {
                if (selected == EnemyAttack.GD03) sheet = "GuardShard";
                else if (selected == EnemyAttack.GD04) sheet = "GuardPillar";
                else if (selected == EnemyAttack.GD02 && velocity.sqrMagnitude <= 0.01f) sheet = "GuardBash";
                else if ((selected == EnemyAttack.GD02 || selected == EnemyAttack.GD05) && velocity.sqrMagnitude > 0.01f) sheet = "GuardWave";
            }
            return KarmaBossAttack.Spawn(game, this, effectsRoot, at, velocity, size, life,
                Damage * multiplier, PrimaryType == Essence.Ward ? new Color(1, 0.7f, 0.12f) : new Color(1, 0.38f, 0.12f), contact, stun, pose, sheet,
                renderArtwork && (selected != EnemyAttack.GD02 || velocity.sqrMagnitude > 0.01f));
        }
        // Decorations are separate from warning geometry, colliders and damage objects.
        void GuardDecoration(string sheet, string pose, Vector2 center, Vector2 size, Color color, int order = 7)
        {
            if (KarmaArtwork.Sprite(sheet, pose) == null) return;
            var sr = KarmaVisuals.Box(effectsRoot, "청동 결계 장식 · 피해 없음", center, size, color, order);
            KarmaCombatVFX.ApplySprite(sr, sheet, pose, size);
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
                    "Guardian motion slash", hitSize * 1.4f, duration, 0, new Color(1, 0.45f, 0.15f));
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
                        "Guardian landing impact", hitSize * new Vector2(2.4f, 1.2f), 0.35f, 0, new Color(1, 0.55f, 0.1f));
            }
        }
        public void TryAttackHit(Vector2 from, Vector2 to, Vector2 size, float amount, KarmaAttackContact contact, float stun)
        {
            if (!Active || game.Paused || contact.Consumed) return;
            var player = game.Player; var p = player.Position;
            if (!KarmaBossGeometry.SweptBox(from.x, from.y, to.x, to.y, p.x, p.y, size.x / 2 + KarmaSpace.PlayerHalfWidth, size.y / 2 + KarmaSpace.PlayerHalfHeight)) return;
            if (!contact.TryConsume()) return;
            if (selected == EnemyAttack.GD02 && contact.BreaksShieldOnParry && state == ActionState.Attack && heavyParryQueued)
            {
                // The tell's first-frame input must survive the ordinary 0.18s counter window.
                heavyParryQueued = false;
                player.BeginCounter();
            }
            int parries = player.ParryCount;
            float before = player.Health;
            player.TakeDamage(amount);
            if (player.ParryCount != parries)
            {
                // GD02's follow-up wave can be countered without breaking GD01 or stunning the boss.
                if (selected == EnemyAttack.GD02 && contact.BreaksShieldOnParry) pendingParry = true;
                return;
            }
            if (player.Health < before)
            { contact.RecordDamage(); trial.Hit(); if (stun > 0 && Active) player.Stun(stun); }
        }
    }
}
