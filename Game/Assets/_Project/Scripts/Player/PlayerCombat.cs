using UnityEngine;

namespace K4RMA
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] PlayerTuning tuning;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerController motor;
        [SerializeField] PlayerAbilityState abilities;
        [SerializeField] Health health;
        [SerializeField] MeleeHitbox melee;
        [SerializeField] Transform weapon;
        [SerializeField] Projectile projectilePrefab;
        [SerializeField] BodyTint tint;
        [SerializeField] LayerMask projectileHitLayers;
        [SerializeField] int projectileLayer;

        float meleeTimer;
        float meleeCooldown;
        float abilityCooldown;
        float deflectTimer;
        Quaternion weaponRestRotation;
        PlayerGuard guard;
        PlayerRisingSlash risingSlash;

        public bool DeflectOpen => deflectTimer > 0f;
        public float Swing01 { get; private set; }
        public bool IsSwinging => meleeTimer > 0f;

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerController>();
            if (weapon != null)
                weaponRestRotation = weapon.localRotation;
            if (melee != null && tuning != null)
                melee.SetSize(tuning.meleeHitboxSize);
            EnsureOriginalTechniques();
        }

        void EnsureOriginalTechniques()
        {
            guard = GetComponent<PlayerGuard>();
            if (guard == null)
                guard = gameObject.AddComponent<PlayerGuard>();
            risingSlash = GetComponent<PlayerRisingSlash>();
            if (risingSlash == null)
                risingSlash = gameObject.AddComponent<PlayerRisingSlash>();
            risingSlash.Bind(melee);
        }

        void Update()
        {
            if (tuning == null || input == null)
                return;
            if (health != null && !health.IsAlive)
            {
                melee?.End();
                return;
            }

            TickTimers();
            PlaceMelee();
            SwingWeapon();
            UpdateDeflectRing();

            var choice = OriginalAttackPriority.Choose(
                input.GuardHeld,
                guard != null && guard.IsGuarding,
                risingSlash != null && risingSlash.IsActive,
                meleeTimer > 0f,
                input.RisingSlashPressed,
                input.AttackPressed,
                input.AbilityPressed);
            if (choice == OriginalAttackStart.RisingSlash)
                risingSlash?.TryActivate();
            else if (choice == OriginalAttackStart.Melee && meleeCooldown <= 0f)
                StartMelee();
            else if (choice == OriginalAttackStart.Ability && abilityCooldown <= 0f)
                UseAbility();
        }

        public void NotifyDeflected(BossController boss)
        {
            deflectTimer = 0f;
            if (boss != null && tuning != null)
                boss.ApplyDeflectPunish(tuning.deflectPunishDamage, tuning.deflectStunSeconds);
        }

        void TickTimers()
        {
            if (meleeTimer > 0f)
            {
                meleeTimer -= Time.deltaTime;
                if (meleeTimer <= 0f)
                    melee?.End();
            }

            meleeCooldown -= Time.deltaTime;
            abilityCooldown -= Time.deltaTime;
            if (deflectTimer > 0f)
                deflectTimer -= Time.deltaTime;
        }

        void StartMelee()
        {
            meleeTimer = tuning.meleeActiveSeconds;
            meleeCooldown = tuning.meleeCooldownSeconds;
            melee?.Begin(tuning.meleeDamage, tuning.meleeKnockback, tuning.meleeKnockbackSeconds);
            AudioFeedback.Play(AudioCue.Swing);
        }

        void UseAbility()
        {
            if (abilities != null && abilities.HasDeflect)
            {
                // Legacy slice only. This is not PiercingSlash.
                deflectTimer = tuning.deflectDurationSeconds;
                abilityCooldown = tuning.deflectCooldownSeconds;
                tint?.Flash(tuning.deflectColor, tuning.deflectDurationSeconds);
                AudioFeedback.Play(AudioCue.DeflectReady);
                return;
            }

            if (abilities != null && abilities.HasActiveProjectile)
                FireSwordWave();
        }

        void FireSwordWave()
        {
            if (projectilePrefab == null || motor == null || tuning == null)
                return;

            int facing = motor.Facing;
            Vector3 origin = transform.position + Vector3.right * (facing * (tuning.meleeRange + 0.55f));
            origin.y = transform.position.y + 0.15f;
            origin.z = 0f;
            var shot = Instantiate(projectilePrefab, origin, Quaternion.identity);
            AudioFeedback.Play(AudioCue.Projectile);
            shot.Launch(
                facing,
                tuning.projectileSpeed,
                tuning.projectileDamage,
                tuning.projectileLifetimeSeconds,
                tuning.projectileKnockback,
                tuning.projectileKnockbackSeconds,
                projectileHitLayers,
                projectileLayer,
                health,
                null,
                tuning.projectileColor);
            abilityCooldown = tuning.projectileCooldownSeconds;
        }

        void PlaceMelee()
        {
            if (melee == null || motor == null)
                return;
            melee.transform.localPosition = new Vector3(motor.Facing * tuning.meleeRange, 0.15f, 0f);
        }

        void SwingWeapon()
        {
            if (weapon == null)
                return;
            float duration = Mathf.Max(0.05f, tuning.meleeActiveSeconds);
            Swing01 = meleeTimer > 0f ? 1f - Mathf.Clamp01(meleeTimer / duration) : 0f;
            float angle = 0f;
            if (meleeTimer > 0f)
                angle = Mathf.Lerp(-80f, 55f, Swing01);
            else if (risingSlash != null && risingSlash.IsActive)
                angle = 85f;
            weapon.localRotation = weaponRestRotation * Quaternion.Euler(0f, 0f, angle);
        }

        void UpdateDeflectRing()
        {
            var ring = transform.Find("DeflectRing");
            if (ring == null)
                return;
            bool show = DeflectOpen;
            if (ring.gameObject.activeSelf != show)
                ring.gameObject.SetActive(show);
            if (!show || tuning == null)
                return;
            float pulse = 1f + Mathf.Sin(Time.time * 28f) * 0.06f;
            ring.localScale = new Vector3(pulse, pulse, 0.15f);
        }
    }
}
