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
            return KarmaVisuals.Box(effectsRoot, name, center, size, color, 8);
        }
        SpriteRenderer PathMark(Vector2 from, Vector2 to, Vector2 size, bool locked = true)
        {
            var marker = Mark(locked ? "고정 공격 경로" : "추적 중인 경로", Vector2.zero, Vector2.one, locked);
            marker.gameObject.AddComponent<KarmaAttackWarning>().SetPath(marker, from, to, size);
            return marker;
        }
        void ClearPatternObjects()
        {
            if (effectsRoot == null) return;
            foreach (Transform child in effectsRoot)
            { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        KarmaBossAttack Attack(Vector2 at, Vector2 velocity, Vector2 size, float life,
            float multiplier = 1, KarmaAttackContact contact = null, float stun = 0)
        {
            return KarmaBossAttack.Spawn(game, this, effectsRoot, at, velocity, size, life,
                Damage * multiplier, PatternColor, contact, stun);
        }
        IEnumerator Motion(Vector2 target, float duration, Vector2 hitSize, KarmaAttackContact contact = null)
        {
            Vector2 from = Position; float elapsed = 0;
            var ledger = contact ?? new KarmaAttackContact();
            state = ActionState.Attack; animator.Attack((target - from).normalized);
            while (elapsed < duration && Active)
            {
                if (game.Paused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                Vector2 before = Position;
                Vector2 to = Vector2.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                transform.position = to;
                if (hitSize != Vector2.zero) TryAttackHit(before, to, hitSize, Damage, ledger, 0);
                yield return null;
            }
        }
        public void TryAttackHit(Vector2 from, Vector2 to, Vector2 size, float amount, KarmaAttackContact contact, float stun)
        {
            if (!Active || game.Paused || contact.Consumed) return;
            var player = game.Player; var p = player.Position;
            if (!KarmaBossGeometry.SweptBox(from.x, from.y, to.x, to.y, p.x, p.y, size.x / 2 + 0.35f, size.y / 2 + 0.775f)) return;
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
