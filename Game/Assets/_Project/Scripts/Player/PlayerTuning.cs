using UnityEngine;

namespace K4RMA
{
    [CreateAssetMenu(menuName = "K4RMA/Prototype/Player Tuning", fileName = "PlayerTuning")]
    public class PlayerTuning : ScriptableObject
    {
        [Header("Prototype mechanic preview")]
        [Tooltip("Prototype only. Not progression. Leave off for original SwordWave and legacy Deflect. On: K is Piercing Slash until the old altar transfer gives Deflect.")]
        public bool previewPiercingSlash;
        [Tooltip("Prototype only. Not progression. Leave off for original Rising Slash. On: L no longer launches. Attacking while airborne briefly hovers.")]
        public bool previewAirHover;
        [Tooltip("Prototype only. Not progression. Leave off for original Guard. On: a tap of I opens a short Counter window. This does not replace legacy Deflect.")]
        public bool previewCounter;

        [Header("Movement")]
        public float moveSpeed = 7f;
        [Tooltip("Upward speed applied when a jump starts.")]
        public float jumpForce = 8f;
        public float jumpBufferSeconds = 0.12f;
        public float coyoteTimeSeconds = 0.08f;
        public float groundCheckDistance = 0.2f;

        [Header("Health")]
        public int maxHealth = 100;

        [Header("Melee")]
        public int meleeDamage = 12;
        public float meleeRange = 1.15f;
        public Vector3 meleeHitboxSize = new Vector3(0.85f, 0.95f, 0.7f);
        public float meleeActiveSeconds = 0.14f;
        public float meleeCooldownSeconds = 0.42f;
        public float meleeKnockback = 4f;
        public float meleeKnockbackSeconds = 0.12f;

        [Header("Sword wave")]
        [Tooltip("SwordWave cooldown. Field name is kept so the existing tuning asset stays valid.")]
        public float projectileCooldownSeconds = 0.65f;
        public float projectileSpeed = 12f;
        public int projectileDamage = 8;
        public float projectileLifetimeSeconds = 1.6f;
        public float projectileKnockback = 3.5f;
        public float projectileKnockbackSeconds = 0.1f;
        public Color projectileColor = new Color(0.35f, 0.86f, 1f);

        [Header("Rising slash")]
        public float risingSlashLaunchSpeed = 11f;
        public int risingSlashDamage = 14;
        public float risingSlashActiveSeconds = 0.22f;
        public float risingSlashCooldownSeconds = 0.85f;
        public float risingSlashForwardOffset = 1f;
        public float risingSlashHeight = 0.65f;
        public Vector3 risingSlashHitboxSize = new Vector3(0.9f, 1.6f, 0.7f);
        public float risingSlashKnockback = 3.5f;
        public float risingSlashKnockbackSeconds = 0.1f;
        public Color risingSlashColor = new Color(1f, 0.82f, 0.45f);

        [Header("Guard")]
        public float guardActiveSeconds = 0.8f;
        public float guardCooldownSeconds = 0.55f;
        public float guardMoveMultiplier = 0.45f;
        public Color guardColor = new Color(0.95f, 0.78f, 0.28f);

        [Header("Piercing slash")]
        public int piercingSlashDamage = 14;
        public float piercingSlashSeekSeconds = 0.16f;
        public float piercingSlashCrossSeconds = 0.14f;
        public float piercingSlashMissDistance = 1.35f;
        public float piercingSlashCooldownSeconds = 0.7f;
        public float piercingSlashClearance = 0.2f;
        public float piercingSlashKnockback = 3f;
        public float piercingSlashKnockbackSeconds = 0.1f;
        public Vector3 piercingSlashProbeSize = new Vector3(1.1f, 1.2f, 0.7f);
        public float piercingSlashProbeForward = 0.85f;
        public Color piercingSlashColor = new Color(0.45f, 0.85f, 1f);

        [Header("Air hover")]
        public float airHoverDurationSeconds = 0.28f;
        public float airHoverCooldownSeconds = 0.5f;
        [Tooltip("Vertical speed during the suspend. Values above zero are clamped to zero so this cannot launch upward.")]
        public float airHoverVerticalSpeed;
        public Color airHoverColor = new Color(0.75f, 0.9f, 1f);

        [Header("Counter")]
        public float counterWindowSeconds = 0.24f;
        public float counterWhiffCooldownSeconds = 0.48f;
        public float counterSuccessCooldownSeconds = 0.62f;
        public int counterDamage = 10;
        public float counterKnockback = 3.5f;
        public float counterKnockbackSeconds = 0.12f;
        public float counterStaggerSeconds = 0.45f;
        public Color counterColor = new Color(0.85f, 0.95f, 1f);

        [Header("Prototype deflect")]
        public float deflectDurationSeconds = 0.4f;
        public float deflectCooldownSeconds = 0.35f;
        public int deflectPunishDamage = 8;
        public float deflectStunSeconds = 0.55f;
        public Color deflectColor = new Color(0.7f, 0.95f, 1f);
    }
}
