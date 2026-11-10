using System;
using UnityEngine;

namespace KarmaPrototype
{
    // Standard rising attacks keep the shared path. Only configured UP04 shards can reflect.
    public sealed class KarmaRisingFragmentAttack : KarmaBossAttack
    {
        KarmaEnemy target;
        KarmaAttackContact fragmentContact;
        Action<Vector2> onReturnHit;
        float age, outboundLife;
        public bool Reflected { get; private set; }
        public void ConfigureFragment(KarmaEnemy owner, KarmaAttackContact contact,
            float life, Action<Vector2> onHit)
        {
            target = owner; fragmentContact = contact; outboundLife = life; onReturnHit = onHit;
        }
        public bool Matches(KarmaAttackContact contact) => fragmentContact != null && fragmentContact == contact;
        public void Reflect()
        {
            if (Reflected || target == null || !target.PresentationActive) return;
            Reflected = true; age = 0;
            Vector2 direction = (Vector2)target.DamageBounds.center - (Vector2)transform.position;
            velocity = (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up)
                * Mathf.Max(KarmaSpace.Units(16), velocity.magnitude * 1.2f);
            if (Visual != null)
            {
                Visual.transform.Rotate(0, 0, 180);
                Visual.GetComponent<KarmaRisingFragmentVisual>()?.Restart(3f);
            }
        }
        protected override float FlightStep(Vector2 from, float dt)
        {
            return fragmentContact != null && !Reflected
                ? Mathf.Min(dt, Mathf.Max(0, outboundLife - age + Time.deltaTime)) : base.FlightStep(from, dt);
        }
        protected new void Update()
        {
            if (fragmentContact == null) { base.Update(); return; }
            if (game == null || target == null || !target.PresentationActive)
            { End(); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (!Reflected)
            {
                // Spawn reserves three extra seconds for a return; outbound lifetime is unchanged.
                base.Update();
                if (!Reflected && age >= outboundLife) End();
                return;
            }
            if (age >= 3f) { End(); return; }
            Vector2 from = transform.position, to = from + velocity * Time.deltaTime;
            transform.position = to;
            var bounds = target.DamageBounds;
            if (KarmaBossGeometry.SweptBox(from.x, from.y, to.x, to.y, bounds.center.x, bounds.center.y,
                bounds.extents.x + size.x / 2, bounds.extents.y + size.y / 2))
            {
                if (!target.Invulnerable)
                {
                    Vector2 point = target.ClosestDamagePoint(from);
                    target.TakeDamage(game.Config.Techniques.counterDamage, from);
                    if (target.Alive && target.PresentationActive) onReturnHit?.Invoke(point);
                }
                End(); return;
            }
            if (to.x < KarmaStage.ArenaLeft || to.x > KarmaStage.ArenaRight
                || to.y < 0 || to.y > KarmaSpace.Units(12)) End();
        }
        void End() { gameObject.SetActive(false); Destroy(gameObject); }
    }
}
