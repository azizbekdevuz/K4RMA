using System.Collections.Generic;
using UnityEngine;

namespace K4RMA
{
    public interface IKnockback
    {
        void ApplyKnockback(float signedSpeed, float duration);
    }

    public class MeleeHitbox : MonoBehaviour
    {
        [SerializeField] BoxCollider hitCollider;
        [SerializeField] LayerMask targetLayers;

        readonly HashSet<Health> alreadyHit = new HashSet<Health>();
        int damage;
        float knockback;
        float knockbackSeconds;
        bool active;

        void Awake()
        {
            if (hitCollider == null)
                hitCollider = GetComponent<BoxCollider>();
            if (hitCollider != null)
            {
                hitCollider.isTrigger = true;
                hitCollider.enabled = false;
            }
        }

        public LayerMask TargetLayers => targetLayers;

        public void SetTargetLayers(LayerMask layers)
        {
            targetLayers = layers;
        }

        public void SetSize(Vector3 size)
        {
            if (hitCollider == null)
                hitCollider = GetComponent<BoxCollider>();
            if (hitCollider != null)
                hitCollider.size = size;
        }

        public void Begin(int damageAmount, float knockbackAmount, float knockbackDuration)
        {
            damage = damageAmount;
            knockback = knockbackAmount;
            knockbackSeconds = knockbackDuration;
            alreadyHit.Clear();
            active = true;
            if (hitCollider != null)
            {
                hitCollider.enabled = true;
                // Collider.bounds stays empty until the physics step after a collider
                // is enabled. RisingSlash leaves that volume before the step runs.
                SampleOverlaps();
            }
        }

        void SampleOverlaps()
        {
            if (hitCollider == null)
                return;

            Vector3 scale = hitCollider.transform.lossyScale;
            Vector3 halfExtents = new Vector3(
                Mathf.Abs(hitCollider.size.x * scale.x) * 0.5f,
                Mathf.Abs(hitCollider.size.y * scale.y) * 0.5f,
                Mathf.Abs(hitCollider.size.z * scale.z) * 0.5f);
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
                return;

            Vector3 center = hitCollider.transform.TransformPoint(hitCollider.center);
            var overlaps = Physics.OverlapBox(
                center,
                halfExtents,
                hitCollider.transform.rotation,
                targetLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
                TryHit(overlaps[i]);
        }

        public void End()
        {
            active = false;
            alreadyHit.Clear();
            if (hitCollider != null)
                hitCollider.enabled = false;
        }

        void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        void TryHit(Collider other)
        {
            if (!active || other == null)
                return;
            if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            var health = other.GetComponentInParent<Health>();
            if (health == null || !alreadyHit.Add(health))
                return;
            if (health.transform.root == transform.root)
                return;

            if (!health.ApplyDamage(damage, transform.root.gameObject))
                return;

            float sign = Mathf.Sign(health.transform.position.x - transform.root.position.x);
            if (Mathf.Abs(sign) < 0.01f)
                sign = 1f;

            health.GetComponentInParent<IKnockback>()?.ApplyKnockback(sign * knockback, knockbackSeconds);
            health.GetComponentInParent<BodyTint>()?.Flash(Color.white, 0.08f);
            ImpactFeedback.PlayHit(health.transform.position + Vector3.up * 0.4f, health.GetComponent<PlayerController>() != null);
        }
    }
}
