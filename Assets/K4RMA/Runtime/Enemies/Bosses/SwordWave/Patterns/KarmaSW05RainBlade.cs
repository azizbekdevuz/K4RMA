using UnityEngine;

namespace KarmaPrototype
{
    // Flight shares the planner's equation. Planted swords stay harmless until
    // the final volley explicitly detonates their pre-warned volumes.
    public sealed class KarmaSW05RainBlade : MonoBehaviour
    {
        KarmaGame game;
        KarmaEnemy owner;
        KarmaAerialSwordPlanner.Shot shot;
        SpriteRenderer warning;
        readonly KarmaAttackContact contact = new KarmaAttackContact();
        float age, damage;
        KarmaSW05VFX planted;
        Vector2 landing;
        float explosionHeight;
        bool explosionArmed, detonating;
        float explosionAge, explosionWarning;
        KarmaAttackContact explosionContact;
        public const float ExplosionActiveSeconds = 0.12f;
        public bool Landed { get; private set; }
        // A grounded player's collider bottom rises by v²/2g at most (the
        // fixed-step trajectory is slightly lower). Put the ceiling halfway
        // into the real upward air-slash travel above that apex. A normal jump
        // remains inside; an upward slash near the apex clears it with its
        // actual collider, without consulting any player immunity flag.
        public static float CalculateExplosionHeight(KarmaConfig config)
        {
            float gravity = Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y * config.gravityScale));
            return config.jumpSpeed * config.jumpSpeed / (2 * gravity) + config.Techniques.airSlashDistance * 0.5f;
        }
        public Vector2 ExplosionSize => new Vector2(Mathf.Max(1.6f, shot.size.y * 1.8f), explosionHeight);
        public Vector2 ExplosionCenter => new Vector2(landing.x, ExplosionSize.y / 2);
        public void ArmExplosion(float seconds = 0.75f)
        {
            explosionArmed = true; explosionWarning = seconds;
            if (!Landed) return;
            if (warning != null) return;
            warning = KarmaSW05VFX.Warning(transform.parent, game, new Vector2(landing.x, 0.12f),
                new Vector2(ExplosionSize.x, 0.32f));
            warning.name = "SW05 planted explosion warning / locked footprint";
            KarmaSW05VFX.EmphasizeExplosion(warning);
            if (planted != null) planted.ChargePlanted(seconds);
        }
        public void Explode(KarmaAttackContact sharedContact)
        {
            if (!Landed || detonating) return;
            detonating = true; explosionContact = sharedContact;
            if (warning != null) { warning.gameObject.SetActive(false); Destroy(warning.gameObject); }
            var size = ExplosionSize;
            var at = ExplosionCenter;
            // One shared ledger prevents overlapping planted swords multiplying damage.
            owner.TryAttackHit(at, at, size, damage / game.Config.BossTuning.sword.aerialSwordDamageMultiplier,
                sharedContact, 0, sourcePattern: EnemyAttack.SW05);
            KarmaSW05VFX.Explosion(transform.parent, game, owner, at, size);
            if (planted != null) planted.DissolvePlanted();
        }
        public SpriteRenderer Visual { get; private set; }
        public static KarmaSW05RainBlade Spawn(KarmaGame game, KarmaEnemy owner, Transform root,
            KarmaAerialSwordPlanner.Shot shot, float damage, SpriteRenderer warning)
        {
            var go = new GameObject("SW05 descending judgement / active damage");
            go.transform.SetParent(root, false); go.transform.position = shot.origin;
            var blade = go.AddComponent<KarmaSW05RainBlade>();
            blade.game = game; blade.owner = owner; blade.shot = shot;
            // Lock the height along with the landing footprint, so a setting
            // change cannot separate the warning from the subsequent damage.
            blade.explosionHeight = CalculateExplosionHeight(game.Config);
            blade.damage = damage; blade.warning = warning;
            blade.Visual = KarmaVisuals.Box(go.transform, "SW05 azure sword", Vector2.zero, Vector2.one, Color.white, 26);
            KarmaSW05VFX.ApplySword(blade.Visual, shot.size, shot.velocity);
            KarmaSW05VFX.ApplyDepth(blade.Visual, shot.depth);
            KarmaSW05VFX.Falling(root, game, blade.transform, blade.Visual, shot.size, shot.delay + shot.life, shot.velocity.normalized);
            return blade;
        }
        void Update()
        {
            if (game == null || owner == null || !owner.Alive || game.Phase != RunPhase.Combat)
            { gameObject.SetActive(false); Destroy(gameObject); return; }
            if (game.Paused) return;
            if (detonating)
            {
                explosionAge += Time.deltaTime;
                if (explosionAge >= ExplosionActiveSeconds)
                { gameObject.SetActive(false); Destroy(gameObject); return; }
                // The same ledger spans every zone and every active frame.
                // Recheck entry during the flash without multiplying damage.
                var at = ExplosionCenter;
                owner.TryAttackHit(at, at, ExplosionSize, damage / game.Config.BossTuning.sword.aerialSwordDamageMultiplier,
                    explosionContact, 0, sourcePattern: EnemyAttack.SW05);
                return;
            }
            float previous = age; age += Time.deltaTime;
            if (Landed || age < shot.delay) return;
            float fromTime = Mathf.Clamp(previous - shot.delay, 0, shot.life);
            float toTime = Mathf.Clamp(age - shot.delay, 0, shot.life);
            Vector2 from = shot.PositionAt(fromTime), to = shot.PositionAt(toTime);
            transform.position = to;
            owner.TryAttackHit(from, to, shot.size, damage, contact, 0, shot.velocity.normalized);
            if (toTime < shot.life) return;
            if (warning != null) { warning.gameObject.SetActive(false); Destroy(warning.gameObject); warning = null; }
            landing = to + shot.velocity.normalized * (shot.size.x / 2);
            planted = KarmaSW05VFX.Strike(transform.parent, game, to, shot.size, shot.velocity.normalized, shot.depth);
            Landed = true;
            if (game.IsCombat && owner.Alive) KarmaAudio.Play(KarmaSfxId.SwPerSwordThunder, lifetimeOwner: this);
            Visual.gameObject.SetActive(false);
            if (explosionArmed) ArmExplosion(explosionWarning);
        }
    }
}
