using UnityEngine;

namespace K4RMA
{
    public enum BossState
    {
        Approach,
        MeleeTelegraph,
        MeleeStrike,
        ProjectileTelegraph,
        ProjectileAttack,
        Recover,
        Hurt,
        Dead
    }

    public class BossController : MonoBehaviour, IKnockback
    {
        [SerializeField] BossEncounterConfig config;
        [SerializeField] Health health;
        [SerializeField] Transform player;
        [SerializeField] MeleeHitbox melee;
        [SerializeField] Projectile projectilePrefab;
        [SerializeField] Transform aimPoint;
        [SerializeField] BodyTint tint;
        [SerializeField] Transform visualRoot;
        [SerializeField] LayerMask projectileHitLayers;
        [SerializeField] int projectileLayer;

        Rigidbody body;
        ArenaBounds arena;
        float stateTimer;
        float projectileCooldown;
        float knockbackTimer;
        float knockbackSpeed;
        float hurtLimit;
        float desiredSpeed;
        bool strikeStarted;
        bool shotStarted;
        int facing = -1;

        public BossState State { get; private set; } = BossState.Approach;
        public BossEncounterConfig Config => config;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            arena = FindAnyObjectByType<ArenaBounds>();
        }

        void Start()
        {
            if (health != null && config != null)
                health.ConfigureMax(config.maxHealth);
            if (melee != null && config != null)
                melee.SetSize(config.meleeHitboxSize);
            if (health != null)
                health.Died += HandleDied;
            projectileCooldown = 0.8f;
            Enter(BossState.Approach);
        }

        void OnDestroy()
        {
            if (health != null)
                health.Died -= HandleDied;
        }

        void Update()
        {
            if (config == null || health == null || State == BossState.Dead)
            {
                desiredSpeed = 0f;
                return;
            }

            stateTimer += Time.deltaTime;
            projectileCooldown -= Time.deltaTime;
            FacePlayer();
            PlaceMelee();

            switch (State)
            {
                case BossState.Approach:
                    TickApproach();
                    break;
                case BossState.MeleeTelegraph:
                    desiredSpeed = 0f;
                    if (stateTimer >= config.meleeTelegraphSeconds)
                        Enter(BossState.MeleeStrike);
                    break;
                case BossState.MeleeStrike:
                    TickMeleeStrike();
                    break;
                case BossState.ProjectileTelegraph:
                    desiredSpeed = 0f;
                    if (stateTimer >= config.projectileTelegraphSeconds)
                        Enter(BossState.ProjectileAttack);
                    break;
                case BossState.ProjectileAttack:
                    TickProjectileAttack();
                    break;
                case BossState.Recover:
                    desiredSpeed = 0f;
                    if (stateTimer >= config.meleeRecoverSeconds)
                        Enter(BossState.Approach);
                    break;
                case BossState.Hurt:
                    desiredSpeed = 0f;
                    if (stateTimer >= hurtLimit)
                        Enter(BossState.Approach);
                    break;
            }
        }

        void FixedUpdate()
        {
            if (body == null)
                return;

            float speed = desiredSpeed;
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                speed = knockbackSpeed;
            }

            var velocity = body.linearVelocity;
            velocity.x = speed;
            velocity.z = 0f;
            body.linearVelocity = velocity;

            var position = body.position;
            position.z = 0f;
            if (arena != null)
                position.x = arena.ClampX(position.x, 0.7f);
            body.position = position;
        }

        public void ApplyKnockback(float signedSpeed, float duration)
        {
            if (State == BossState.Dead)
                return;
            knockbackSpeed = signedSpeed;
            knockbackTimer = duration;
        }

        public void ApplyDeflectPunish(int damage, float stunSeconds)
        {
            if (health == null || !health.IsAlive)
                return;

            hurtLimit = stunSeconds;
            melee?.End();
            health.ApplyDamage(damage);
            if (!health.IsAlive)
                return;
            tint?.Flash(Color.white, 0.12f);
            Enter(BossState.Hurt);
        }

        bool HasProjectile =>
            config != null && config.inheritedAbilityId == PrototypeIds.Projectile;

        void TickApproach()
        {
            if (player == null)
            {
                desiredSpeed = 0f;
                return;
            }

            float delta = player.position.x - transform.position.x;
            float distance = Mathf.Abs(delta);
            if (distance <= config.meleeRange)
            {
                desiredSpeed = 0f;
                Enter(BossState.MeleeTelegraph);
                return;
            }

            if (HasProjectile && projectileCooldown <= 0f)
            {
                desiredSpeed = 0f;
                Enter(BossState.ProjectileTelegraph);
                return;
            }

            desiredSpeed = Mathf.Sign(delta) * config.approachSpeed;
        }

        void TickMeleeStrike()
        {
            if (!strikeStarted)
            {
                strikeStarted = true;
                melee?.Begin(config.meleeDamage, config.meleeKnockback, config.meleeKnockbackSeconds);
            }

            desiredSpeed = facing * config.lungeSpeed;
            if (stateTimer >= config.meleeStrikeSeconds)
            {
                melee?.End();
                Enter(BossState.Recover);
            }
        }

        void TickProjectileAttack()
        {
            desiredSpeed = 0f;
            if (!shotStarted)
            {
                shotStarted = true;
                FireProjectile();
                projectileCooldown = config.projectileCooldownSeconds;
            }

            if (stateTimer >= config.projectileReleaseSeconds)
                Enter(BossState.Recover);
        }

        void FireProjectile()
        {
            if (projectilePrefab == null)
                return;

            Vector3 origin = aimPoint != null ? aimPoint.position : transform.position;
            origin += Vector3.right * facing * 0.9f;
            origin.z = 0f;
            AudioFeedback.Play(AudioCue.Projectile);
            var shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
            shot.Launch(
                facing,
                config.projectileSpeed,
                config.projectileDamage,
                config.projectileLifetimeSeconds,
                config.projectileKnockback,
                config.projectileKnockbackSeconds,
                projectileHitLayers,
                projectileLayer,
                health,
                this,
                config.projectileColor);
        }

        void PlaceMelee()
        {
            if (melee == null || config == null)
                return;
            melee.transform.localPosition = new Vector3(facing * config.meleeRange, 0.15f, 0f);
        }

        void FacePlayer()
        {
            if (player == null)
                return;
            float delta = player.position.x - transform.position.x;
            if (Mathf.Abs(delta) < 0.05f)
                return;
            facing = delta >= 0f ? 1 : -1;
            if (visualRoot == null)
                return;
            var scale = visualRoot.localScale;
            scale.x = facing * Mathf.Abs(scale.x == 0f ? 1f : scale.x);
            visualRoot.localScale = scale;
        }

        void Enter(BossState next)
        {
            State = next;
            stateTimer = 0f;
            strikeStarted = false;
            shotStarted = false;

            if (next == BossState.MeleeTelegraph)
                tint?.SetPersistent(config.meleeTelegraphColor);
            else if (next == BossState.ProjectileTelegraph)
                tint?.SetPersistent(config.projectileTelegraphColor);
            else if (next == BossState.Hurt || next == BossState.Dead || next == BossState.Approach || next == BossState.Recover)
                tint?.ClearPersistent();
        }

        void HandleDied(Health _)
        {
            melee?.End();
            desiredSpeed = 0f;
            tint?.SetPersistent(config != null ? config.deadColor : Color.gray);
            State = BossState.Dead;
        }
    }
}
