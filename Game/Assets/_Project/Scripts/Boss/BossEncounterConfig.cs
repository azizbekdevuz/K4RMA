using UnityEngine;

namespace K4RMA
{
    [CreateAssetMenu(menuName = "K4RMA/Prototype/Boss Encounter", fileName = "BossEncounter")]
    public class BossEncounterConfig : ScriptableObject
    {
        public string stageLabel = "Stage 1";
        public int maxHealth = 60;
        public float approachSpeed = 3.3f;
        public float meleeRange = 1.75f;
        public Vector3 meleeHitboxSize = new Vector3(1f, 1.1f, 0.8f);
        public float meleeTelegraphSeconds = 0.8f;
        public float meleeStrikeSeconds = 0.18f;
        public float meleeRecoverSeconds = 0.55f;
        public int meleeDamage = 14;
        public float meleeKnockback = 6.5f;
        public float meleeKnockbackSeconds = 0.14f;
        public float lungeSpeed = 5.2f;
        public float hurtStunSeconds = 0.35f;
        public Color deadColor = new Color(0.25f, 0.22f, 0.22f);
        public Color meleeTelegraphColor = new Color(1f, 0.42f, 0.12f);

        [Header("Inherited prototype ability")]
        [Tooltip("Empty for stage 1. proto.projectile enables the telegraphed shot.")]
        public string inheritedAbilityId;
        public float projectileTelegraphSeconds = 0.95f;
        public float projectileSpeed = 6.5f;
        public int projectileDamage = 12;
        public float projectileCooldownSeconds = 2.6f;
        public float projectileLifetimeSeconds = 2.4f;
        public float projectileReleaseSeconds = 0.2f;
        public float projectileKnockback = 4.5f;
        public float projectileKnockbackSeconds = 0.12f;
        public Color projectileTelegraphColor = new Color(0.4f, 0.78f, 1f);
        public Color projectileColor = new Color(1f, 0.5f, 0.16f);
    }
}
