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
        [SerializeField] ArenaBounds arenaBounds;

        Rigidbody body;
        float jumpBuffer;
        float coyoteTimer;
        float knockbackTimer;
        float knockbackSpeed;

        PlayerGuard guard;
        PlayerPiercingSlash piercingSlash;

        public int Facing { get; private set; } = 1;
        public bool IsGrounded { get; private set; }
        public bool InKnockback => knockbackTimer > 0f;
        public float BodyRadius => bodyCollider != null ? bodyCollider.radius : 0.45f;
        public ArenaBounds Arena => arenaBounds;
        public PlayerTuning Tuning => tuning;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (bodyCollider == null)
                bodyCollider = GetComponent<CapsuleCollider>();
            if (arenaBounds == null)
                arenaBounds = FindAnyObjectByType<ArenaBounds>();
        }

        void Start()
        {
            if (health != null && tuning != null)
                health.ConfigureMax(tuning.maxHealth);
            ResolveGuard();
            IsGrounded = CheckGrounded();
        }

        PlayerGuard ResolveGuard()
        {
            if (guard == null)
                guard = GetComponent<PlayerGuard>();
            return guard;
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

            if (piercingSlash == null)
                piercingSlash = GetComponent<PlayerPiercingSlash>();
            bool lockFacing = piercingSlash != null && piercingSlash.IsActive;
            if (!lockFacing && Mathf.Abs(input.MoveX) > 0.01f)
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

            IsGrounded = CheckGrounded();
            if (IsGrounded)
                coyoteTimer = tuning.coyoteTimeSeconds;
            else
                coyoteTimer -= Time.fixedDeltaTime;

            var velocity = body.linearVelocity;
            bool alive = health == null || health.IsAlive;
            float speed = tuning.moveSpeed;
            var activeGuard = ResolveGuard();
            if (activeGuard != null && activeGuard.IsGuarding)
                speed *= Mathf.Clamp01(tuning.guardMoveMultiplier);
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                velocity.x = knockbackSpeed;
            }
            else if (alive)
            {
                velocity.x = (input != null ? input.MoveX : 0f) * speed;
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
            if (arenaBounds != null)
            {
                float padding = bodyCollider != null ? bodyCollider.radius : 0.45f;
                float clamped = arenaBounds.ClampX(position.x, padding);
                if (!Mathf.Approximately(clamped, position.x))
                    velocity.x = 0f;
                position.x = clamped;
            }

            body.linearVelocity = velocity;
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
