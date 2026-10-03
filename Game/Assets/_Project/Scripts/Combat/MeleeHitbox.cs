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
                var overlaps = Physics.OverlapBox(
                    hitCollider.bounds.center,
                    hitCollider.bounds.extents,
                    hitCollider.transform.rotation,
                    targetLayers,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < overlaps.Length; i++)
                    TryHit(overlaps[i]);
            }
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

            float sign = Mathf.Sign(health.transform.position.x - transform.root.position.x);
            if (Mathf.Abs(sign) < 0.01f)
                sign = 1f;

            health.GetComponentInParent<IKnockback>()?.ApplyKnockback(sign * knockback, knockbackSeconds);
            health.GetComponentInParent<BodyTint>()?.Flash(Color.white, 0.08f);
            health.ApplyDamage(damage);
            ImpactFeedback.PlayHit(health.transform.position + Vector3.up * 0.4f, health.GetComponent<PlayerController>() != null);
        }
    }
}
