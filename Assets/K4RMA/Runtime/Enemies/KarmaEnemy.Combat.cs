using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        SpriteRenderer[] damageRenderers;
        internal bool PresentationActive { get { return Active && isActiveAndEnabled; } }
        // Only the guardian artwork contributes: attack VFX and shield halos are separate objects.
        public Bounds DamageBounds
        {
            get
            {
                if (damageRenderers == null || damageRenderers.Length == 0)
                {
                    damageRenderers = art.GetComponentsInChildren<SpriteRenderer>();
                }
                Bounds bounds = default;
                bool found = false;
                foreach (var renderer in damageRenderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null) continue;
                    var body = behavior != null ? behavior.DamageBounds(renderer) : renderer.bounds;
                    if (!found) { bounds = body; found = true; }
                    else bounds.Encapsulate(body);
                }
                return found ? bounds : new Bounds(Position, KarmaSpace.Units(new Vector2(1.4f, 2.3f)));
            }
        }
        public bool OverlapsDamageArea(Vector2 center, Vector2 size)
        {
            var bounds = DamageBounds;
            return Mathf.Abs(center.x - bounds.center.x) <= size.x / 2 + bounds.extents.x
                && Mathf.Abs(center.y - bounds.center.y) <= size.y / 2 + bounds.extents.y;
        }
        public bool IntersectsDamagePath(Vector2 from, Vector2 to, float radius)
        {
            var bounds = DamageBounds;
            return KarmaBossGeometry.SweptBox(from.x, from.y, to.x, to.y, bounds.center.x, bounds.center.y,
                bounds.extents.x + radius, bounds.extents.y + radius);
        }
        public Vector2 ClosestDamagePoint(Vector2 source) { return DamageBounds.ClosestPoint(source); }
        public void TakeDamage(float amount) { TakeDamage(amount, game.Player.Position); }
        public void TakeDamage(float amount, Vector2 source)
        {
            if (!Alive || !game.IsCombat || amount <= 0 || stopped) return;
            engaged = true;
            if (fullShield) return;
            bool reduced = behavior.ReduceDamage(source);
            behavior.OnDamaged();
            if (reduced)
            {
                amount *= Mathf.Clamp01(behavior.Balance.shieldDamageMultiplier);
                behavior.ReducedDamageFeedback();
            }
            ReceiveDamage(amount, source);
        }
        void ReceiveDamage(float amount, Vector2 source)
        {
            if (amount <= 0) return;
            behavior.DamageFeedback(ClosestDamagePoint(source));
            Health = Mathf.Max(0, Health - amount);
            if (Health <= 0)
            {
                CancelCombat(); art.SetParent(game.World, true); animator.Die();
                game.EnemyDefeated(this, art); Destroy(gameObject);
            }
        }
    }
}
