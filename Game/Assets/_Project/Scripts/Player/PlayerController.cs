using UnityEngine;

namespace K4RMA
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour, IKnockback
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] PlayerInputReader input;
        [SerializeField] Health health;
        [SerializeField] CapsuleCollider bodyCollider;
        [SerializeField] LayerMask groundLayers;
        [SerializeField] Transform visualRoot;

        Rigidbody body;
        float jumpBuffer;
        float coyoteTimer;
        float knockbackTimer;
        float knockbackSpeed;

        public int Facing { get; private set; } = 1;
        public PlayerTuning Tuning => tuning;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (bodyCollider == null)
                bodyCollider = GetComponent<CapsuleCollider>();
        }

        void Start()
        {
            if (health != null && tuning != null)
                health.ConfigureMax(tuning.maxHealth);
        }

        void Update()
        {
            if (tuning == null || input == null)
                return;
            if (health != null && !health.IsAlive)
            {
                jumpBuffer = 0f;
                return;
            }

            if (Mathf.Abs(input.MoveX) > 0.01f)
            {
                Facing = input.MoveX > 0f ? 1 : -1;
                if (visualRoot != null)
                {
                    var scale = visualRoot.localScale;
                    scale.x = Facing * Mathf.Abs(scale.x == 0f ? 1f : scale.x);
                    visualRoot.localScale = scale;
                }
            }

            if (input.JumpPressed)
                jumpBuffer = tuning.jumpBufferSeconds;
            else
                jumpBuffer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            if (tuning == null || body == null)
                return;

            bool grounded = CheckGrounded();
            if (grounded)
                coyoteTimer = tuning.coyoteTimeSeconds;
            else
                coyoteTimer -= Time.fixedDeltaTime;

            var velocity = body.linearVelocity;
            bool alive = health == null || health.IsAlive;
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                velocity.x = knockbackSpeed;
            }
            else if (alive)
            {
                velocity.x = (input != null ? input.MoveX : 0f) * tuning.moveSpeed;
            }
            else
            {
                velocity.x = 0f;
            }

            if (alive && jumpBuffer > 0f && coyoteTimer > 0f)
            {
                velocity.y = tuning.jumpForce;
                jumpBuffer = 0f;
                coyoteTimer = 0f;
            }

            velocity.z = 0f;
            body.linearVelocity = velocity;

            var position = body.position;
            position.z = 0f;
            body.position = position;
        }

        public void ApplyKnockback(float signedSpeed, float duration)
        {
            knockbackSpeed = signedSpeed;
            knockbackTimer = duration;
        }

        bool CheckGrounded()
        {
            float halfHeight = bodyCollider != null ? bodyCollider.height * 0.5f : 1f;
            Vector3 feet = transform.position + Vector3.down * halfHeight + Vector3.up * 0.05f;
            return Physics.Raycast(
                feet,
                Vector3.down,
                tuning.groundCheckDistance + 0.05f,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
