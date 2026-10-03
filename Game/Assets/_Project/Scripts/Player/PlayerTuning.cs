using UnityEngine;

namespace K4RMA
{
    [CreateAssetMenu(menuName = "K4RMA/Prototype/Player Tuning", fileName = "PlayerTuning")]
    public class PlayerTuning : ScriptableObject
    {
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

        [Header("Prototype projectile")]
        public float projectileCooldownSeconds = 0.65f;
        public float projectileSpeed = 12f;
        public int projectileDamage = 8;
        public float projectileLifetimeSeconds = 1.6f;
        public float projectileKnockback = 3.5f;
        public float projectileKnockbackSeconds = 0.1f;
        public Color projectileColor = new Color(0.35f, 0.86f, 1f);

        [Header("Prototype deflect")]
        public float deflectDurationSeconds = 0.4f;
        public float deflectCooldownSeconds = 0.35f;
        public int deflectPunishDamage = 8;
        public float deflectStunSeconds = 0.55f;
        public Color deflectColor = new Color(0.7f, 0.95f, 1f);
    }
}
