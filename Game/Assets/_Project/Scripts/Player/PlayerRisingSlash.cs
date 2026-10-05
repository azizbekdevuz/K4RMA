using UnityEngine;

namespace K4RMA
{
    public class PlayerRisingSlash : MonoBehaviour
    {
        PlayerTuning tuning;
        PlayerController motor;
        Health health;
        BodyTint tint;
        Rigidbody body;
        MeleeHitbox hitbox;
        float active;
        float cooldown;

        public bool IsActive => active > 0f;
        public float CooldownRemaining => cooldown;

        public void Bind(MeleeHitbox basicMelee)
        {
            if (motor == null)
                motor = GetComponent<PlayerController>();
            if (tuning == null && motor != null)
                tuning = motor.Tuning;
            if (health == null)
                health = GetComponent<Health>();
            if (tint == null)
                tint = GetComponent<BodyTint>();
            if (body == null)
                body = GetComponent<Rigidbody>();
            EnsureHitbox(basicMelee);
        }

        void OnDisable()
        {
            Cancel();
        }

        void Update()
        {
            if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;
                if (cooldown < 0f)
                    cooldown = 0f;
            }

            if (active <= 0f)
                return;
            if (health != null && !health.IsAlive)
            {
                Cancel();
                return;
            }

            active -= Time.deltaTime;
            PlaceHitbox();
            if (active <= 0f)
            {
                active = 0f;
                hitbox?.End();
            }
        }

        public bool TryActivate()
        {
            bool alive = health == null || health.IsAlive;
            bool grounded = motor != null && motor.IsGrounded;
            if (!RisingSlashRules.CanActivate(alive, grounded, active > 0f, cooldown))
                return false;
            if (tuning == null || body == null || hitbox == null || motor == null)
                return false;

            active = tuning.risingSlashActiveSeconds;
            cooldown = tuning.risingSlashCooldownSeconds;
            var velocity = body.linearVelocity;
            velocity.y = tuning.risingSlashLaunchSpeed;
            body.linearVelocity = velocity;
            hitbox.SetSize(tuning.risingSlashHitboxSize);
            PlaceHitbox();
            hitbox.Begin(tuning.risingSlashDamage, tuning.risingSlashKnockback, tuning.risingSlashKnockbackSeconds);
            tint?.Flash(tuning.risingSlashColor, tuning.risingSlashActiveSeconds);
            SparkBurst.Play(transform.position + Vector3.up * tuning.risingSlashHeight, tuning.risingSlashColor);
            AudioFeedback.Play(AudioCue.RisingSlash);
            return true;
        }

        void Cancel()
        {
            active = 0f;
            hitbox?.End();
        }

        void EnsureHitbox(MeleeHitbox basicMelee)
        {
            if (hitbox != null || basicMelee == null)
                return;

            var hitObject = new GameObject("RisingSlashHitbox");
            hitObject.layer = basicMelee.gameObject.layer;
            hitObject.transform.SetParent(transform, false);
            var collider = hitObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            hitbox = hitObject.AddComponent<MeleeHitbox>();
            hitbox.SetTargetLayers(basicMelee.TargetLayers);
            if (tuning != null)
                hitbox.SetSize(tuning.risingSlashHitboxSize);
        }

        void PlaceHitbox()
        {
            if (hitbox == null || motor == null || tuning == null)
                return;
            hitbox.transform.localPosition = new Vector3(
                motor.Facing * tuning.risingSlashForwardOffset,
                tuning.risingSlashHeight,
                0f);
        }
    }
}
