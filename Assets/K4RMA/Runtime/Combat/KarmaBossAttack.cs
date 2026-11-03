using UnityEngine;
namespace KarmaPrototype
{
    // Boss-owned lifetime. Directional artwork is independent of the unrotated swept hit box.
    public class KarmaBossAttack : MonoBehaviour
    {
        protected KarmaGame game;
        KarmaEnemy owner;
        protected Vector2 velocity, size;
        float remaining, damage, stun;
        KarmaAttackContact contact;
        EnemyAttack sourcePattern;
        bool parryStun, shieldBreak;
        protected virtual bool Oriented { get { return false; } }
        protected virtual bool AttachTrail { get { return true; } }
        protected virtual bool AttachLaunch { get { return true; } }
        protected virtual bool ApplyPatternVisual(SpriteRenderer renderer, string extraPose, Vector2 size, Vector2 velocity) { return false; }
        protected virtual Color AttackColor(Color color, bool restrained)
        { return restrained ? new Color(1, 0.94f, 0.8f, 0.86f) : Color.Lerp(Color.white, color, 0.18f); }
        protected virtual float FlightStep(Vector2 from, float dt) { return dt; }
        protected virtual bool FinishFlight(Vector2 to) { return false; }
        public static KarmaBossAttack Spawn(KarmaGame game, KarmaEnemy owner, Transform root, Vector2 at,
            Vector2 velocity, Vector2 size, float life, float damage, Color color, KarmaAttackContact contact, float stun, string visualPose = null,
            bool restrainedVisual = false, bool separateLaunch = false, string extraWavePose = null, string visualSheet = "GuardianVFX", bool parryStun = false, bool shieldBreak = false, bool renderArtwork = true, EnemyAttack? sourcePattern = null)
        {
            var go = new GameObject("유효 보스 공격"); go.transform.SetParent(root, false); go.transform.localPosition = at;
            var attack = owner.CreateAttackComponent(go);
            var picture = new GameObject("Directional attack artwork"); picture.transform.SetParent(go.transform, false);
            var sr = picture.AddComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(sr); sr.sortingOrder = 26;
            string pose = visualPose ?? VisualPose(velocity, size);
            attack.parryStun = parryStun; attack.shieldBreak = shieldBreak;
            if (!attack.ApplyPatternVisual(sr, extraWavePose, size, velocity))
                ConfigureVisual(sr, velocity, size, owner.Facing, pose, restrainedVisual, visualSheet);
            sr.color = attack.AttackColor(color, restrainedVisual);
            attack.Visual = sr;
            picture.SetActive(renderArtwork);
            if (renderArtwork && attack.AttachTrail) KarmaCombatVFX.AttachAttack(sr, velocity, color, root, restrainedVisual);
            float angle = sr.transform.localEulerAngles.z;
            if (renderArtwork && !separateLaunch && attack.AttachLaunch)
            {
                var burst = KarmaCombatVFX.Burst(root, at, sr.sprite, "Guardian launch/" + pose,
                    new Vector2(sr.sprite.bounds.size.x * sr.transform.localScale.x, sr.sprite.bounds.size.y * sr.transform.localScale.y)
                        * (restrainedVisual ? 0.8f : 1.15f), 0.24f, angle, color, restrainedVisual);
                burst.transform.parent.localScale = new Vector3(sr.flipX ? -1 : 1, 1, 1);
            }
            attack.game = game; attack.owner = owner; attack.velocity = velocity; attack.size = size;
            attack.remaining = life; attack.damage = damage; attack.contact = contact ?? new KarmaAttackContact(); attack.stun = stun;
            attack.sourcePattern = sourcePattern ?? (EnemyAttack)owner.CurrentPattern;
            return attack;
        }
        public SpriteRenderer Visual { get; private set; }
        public static string VisualPose(Vector2 velocity, Vector2 size)
        {
            if (velocity.sqrMagnitude > 0.01f) return size.magnitude < KarmaSpace.Units(1) ? "Blade" : "Wave";
            return size.x > size.y * 2 ? "GroundRing" : size.y > size.x * 1.5f ? "Pillar" : "Cleave";
        }
        // Shared attacks use axis-aligned hit boxes; SwordBossVFX uses its own oriented path.
        public static void ConfigureVisual(SpriteRenderer sr, Vector2 velocity, Vector2 size, int facing, string pose, bool restrainedVisual = false, string visualSheet = "GuardianVFX")
        {
            bool moving = velocity.sqrMagnitude > 0.01f;
            float visualScale = restrainedVisual ? (moving ? 1 : 0.9f) : 1.15f;
            KarmaCombatVFX.ApplySprite(sr, visualSheet, pose, size * visualScale, pose != "Pillar");
            if (moving)
            {
                // Fit the rotated sprite to the world-space footprint, including diagonal shots.
                Vector2 axis = velocity.normalized;
                var artSize = sr.sprite.bounds.size;
                float width = Mathf.Abs(axis.x) * artSize.x + Mathf.Abs(axis.y) * artSize.y;
                float height = Mathf.Abs(axis.y) * artSize.x + Mathf.Abs(axis.x) * artSize.y;
                float fit = Mathf.Min(size.x / width, size.y / height) * visualScale;
                sr.transform.localScale = new Vector3(fit, fit, 1);
            }
            sr.flipX = !moving && facing < 0;
            sr.transform.localRotation = Quaternion.Euler(0, 0,
                moving ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0);
        }
        // Presentation replacement never changes the shared hit shape or contact ledger.
        public void Repaint(Sprite sprite, bool upright)
        {
            if (sprite == null || Visual == null) return;
            Visual.sprite = sprite;
            Visual.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            if (upright) Visual.transform.localRotation = Quaternion.identity;
        }
        protected void Update()
        {
            if (game == null || owner == null || !owner.Alive || game.Phase != RunPhase.Combat)
            { gameObject.SetActive(false); Destroy(gameObject); return; }
            if (game.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0, remaining));
            Vector2 from = transform.position, to = from + velocity * dt;
            dt = FlightStep(from, dt);
            to = from + velocity * dt;
            transform.position = to;
            owner.TryAttackHit(from, to, size, damage, contact, stun, Oriented ? (velocity.sqrMagnitude > 0.000001f ? velocity.normalized : Vector2.right) : (Vector2?)null, parryStun, shieldBreak, sourcePattern);
            remaining -= Time.deltaTime;
            if (FinishFlight(to))
            { gameObject.SetActive(false); Destroy(gameObject); return; }
            if (remaining <= 0 || Oriented && dt < Time.deltaTime - 0.000001f || to.x < KarmaStage.ArenaLeft || to.x > KarmaStage.ArenaRight || to.y < 0 || to.y > KarmaSpace.Units(12))
            { gameObject.SetActive(false); Destroy(gameObject); }
        }
    }
}
