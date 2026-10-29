using UnityEngine;
namespace KarmaPrototype
{
    // Boss-owned lifetime. Directional artwork is independent of the unrotated swept hit box.
    public sealed class KarmaBossAttack : MonoBehaviour
    {
        KarmaGame game;
        KarmaEnemy owner;
        Vector2 velocity, size;
        float remaining, damage, stun;
        bool parryStun;
        KarmaAttackContact contact;
        public static KarmaBossAttack Spawn(KarmaGame game, KarmaEnemy owner, Transform root, Vector2 at,
            Vector2 velocity, Vector2 size, float life, float damage, Color color, KarmaAttackContact contact, float stun, string visualPose = null, bool parryStun = false)
        {
            var go = new GameObject("유효 보스 공격"); go.transform.SetParent(root, false); go.transform.localPosition = at;
            var attack = go.AddComponent<KarmaBossAttack>();
            var picture = new GameObject("Directional attack artwork"); picture.transform.SetParent(go.transform, false);
            var sr = picture.AddComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(sr); sr.sortingOrder = 26;
            string pose = visualPose ?? VisualPose(velocity, size);
            ConfigureVisual(sr, velocity, size, owner.Facing, pose);
            sr.color = Color.Lerp(Color.white, color, 0.18f);
            attack.Visual = sr;
            KarmaCombatVFX.AttachAttack(sr, velocity, color, root);
            float angle = sr.transform.localEulerAngles.z;
            var burst = KarmaCombatVFX.Burst(root, at, sr.sprite, "Guardian launch/" + pose,
                new Vector2(sr.sprite.bounds.size.x * sr.transform.localScale.x, sr.sprite.bounds.size.y * sr.transform.localScale.y) * 1.15f, 0.24f, angle, color);
            burst.transform.parent.localScale = new Vector3(sr.flipX ? -1 : 1, 1, 1);
            attack.game = game; attack.owner = owner; attack.velocity = velocity; attack.size = size;
            attack.remaining = life; attack.damage = damage; attack.contact = contact ?? new KarmaAttackContact(); attack.stun = stun; attack.parryStun = parryStun;
            return attack;
        }
        public SpriteRenderer Visual { get; private set; }
        public void Repaint(Sprite sprite, bool fillFootprint)
        {
            if (sprite == null || Visual == null) return;
            Visual.sprite = sprite;
            var glowObject = Visual.transform.Find("Attack radiance");
            if (glowObject != null)
            {
                var glow = glowObject.GetComponent<SpriteRenderer>();
                if (glow != null) glow.sprite = sprite;
            }
            var bounds = sprite.bounds.size;
            if (fillFootprint)
            {
                Visual.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, bounds.x), size.y / Mathf.Max(0.01f, bounds.y), 1f);
                return;
            }
            bool moving = velocity.sqrMagnitude > 0.01f;
            if (!moving)
            {
                float fit = Mathf.Min(size.x / Mathf.Max(0.01f, bounds.x), size.y / Mathf.Max(0.01f, bounds.y));
                Visual.transform.localScale = new Vector3(fit, fit, 1f);
                return;
            }
            Vector2 axis = velocity.normalized;
            float width = Mathf.Abs(axis.x) * bounds.x + Mathf.Abs(axis.y) * bounds.y;
            float height = Mathf.Abs(axis.y) * bounds.x + Mathf.Abs(axis.x) * bounds.y;
            float fitted = Mathf.Min(size.x / Mathf.Max(0.01f, width), size.y / Mathf.Max(0.01f, height)) * 1.15f;
            Visual.transform.localScale = new Vector3(fitted, fitted, 1f);
        }
        public static string VisualPose(Vector2 velocity, Vector2 size)
        {
            if (velocity.sqrMagnitude > 0.01f) return size.magnitude < KarmaSpace.Units(1) ? "Blade" : "Wave";
            return size.x > size.y * 2 ? "GroundRing" : size.y > size.x * 1.5f ? "Pillar" : "Cleave";
        }
        // All moving art points along local +X. Upright pillars only mirror; hit boxes never rotate.
        public static void ConfigureVisual(SpriteRenderer sr, Vector2 velocity, Vector2 size, int facing, string pose)
        {
            bool moving = velocity.sqrMagnitude > 0.01f;
            KarmaCombatVFX.ApplySprite(sr, "GuardianVFX", pose, size * 1.15f, pose != "Pillar");
            if (moving)
            {
                // Fit the rotated sprite to the world-space footprint, including diagonal shots.
                Vector2 axis = velocity.normalized;
                var artSize = sr.sprite.bounds.size;
                float width = Mathf.Abs(axis.x) * artSize.x + Mathf.Abs(axis.y) * artSize.y;
                float height = Mathf.Abs(axis.y) * artSize.x + Mathf.Abs(axis.x) * artSize.y;
                float fit = Mathf.Min(size.x / width, size.y / height) * 1.15f;
                sr.transform.localScale = new Vector3(fit, fit, 1);
            }
            sr.flipX = !moving && facing < 0;
            sr.transform.localRotation = Quaternion.Euler(0, 0,
                moving ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0);
        }
        void Update()
        {
            if (game == null || owner == null || !owner.Alive || game.Phase != RunPhase.Combat)
            { gameObject.SetActive(false); Destroy(gameObject); return; }
            if (game.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0, remaining));
            Vector2 from = transform.position, to = from + velocity * dt;
            transform.position = to;
            owner.TryAttackHit(from, to, size, damage, contact, stun, parryStun);
            remaining -= Time.deltaTime;
            if (remaining <= 0 || to.x < KarmaStage.ArenaLeft || to.x > KarmaStage.ArenaRight || to.y < 0 || to.y > KarmaSpace.Units(12))
            { gameObject.SetActive(false); Destroy(gameObject); }
        }
    }
}
