using UnityEngine;

namespace K4RMA
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] Rigidbody body;
        [SerializeField] SphereCollider hitCollider;

        Health owner;
        BossController sourceBoss;
        LayerMask hitLayers;
        int direction = 1;
        float speed = 8f;
        int damage = 1;
        float lifetime = 2f;
        float knockback = 3f;
        float knockbackSeconds = 0.1f;
        float age;
        bool launched;
        bool consumed;
        Color tint;

        void Awake()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (hitCollider == null)
                hitCollider = GetComponent<SphereCollider>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            }
            if (hitCollider != null)
                hitCollider.isTrigger = true;
        }

        public void Launch(
            int travelDirection,
            float travelSpeed,
            int hitDamage,
            float lifeSeconds,
            float hitKnockback,
            float hitKnockbackSeconds,
            LayerMask layers,
            int layer,
            Health ownerHealth,
            BossController bossSource,
            Color color)
        {
            direction = travelDirection >= 0 ? 1 : -1;
            speed = travelSpeed;
            damage = hitDamage;
            lifetime = lifeSeconds;
            knockback = hitKnockback;
            knockbackSeconds = hitKnockbackSeconds;
            hitLayers = layers;
            owner = ownerHealth;
            sourceBoss = bossSource;
            tint = color;
            gameObject.layer = layer;
            launched = true;
            age = 0f;
            GetComponent<BodyTint>()?.SetPersistent(tint);
        }

        void FixedUpdate()
        {
            if (!launched || consumed || body == null)
                return;

            var next = body.position + Vector3.right * (direction * speed * Time.fixedDeltaTime);
            next.z = 0f;
            body.MovePosition(next);
        }

        void Update()
        {
            if (!launched || consumed)
                return;
            age += Time.deltaTime;
            if (age >= lifetime)
                Consume();
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
            if (!launched || consumed || other == null)
                return;
            if ((hitLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            var health = other.GetComponentInParent<Health>();
            if (health == null)
            {
                Consume();
                return;
            }
            if (health == owner)
                return;

            var playerCombat = health.GetComponent<PlayerCombat>();
            if (playerCombat != null && playerCombat.DeflectOpen && sourceBoss != null)
            {
                playerCombat.NotifyDeflected(sourceBoss);
                ImpactFeedback.PlayDeflect(transform.position);
                Consume();
                return;
            }

            health.GetComponentInParent<IKnockback>()?.ApplyKnockback(direction * knockback, knockbackSeconds);
            health.GetComponentInParent<BodyTint>()?.Flash(Color.white, 0.08f);
            health.ApplyDamage(damage);
            Consume();
        }

        void Consume()
        {
            if (consumed)
                return;
            consumed = true;
            Destroy(gameObject);
        }
    }
}
