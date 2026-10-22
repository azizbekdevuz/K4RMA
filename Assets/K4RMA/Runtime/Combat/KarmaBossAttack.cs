using UnityEngine;
namespace KarmaPrototype
{
    // Boss-owned attack lifetime. Visual size and swept hit box share a single source.
    public sealed class KarmaBossAttack : MonoBehaviour
    {
        KarmaGame game;
        KarmaEnemy owner;
        Vector2 velocity, size;
        float remaining, damage, stun;
        KarmaAttackContact contact;
        public static KarmaBossAttack Spawn(KarmaGame game, KarmaEnemy owner, Transform root, Vector2 at,
            Vector2 velocity, Vector2 size, float life, float damage, Color color, KarmaAttackContact contact, float stun)
        {
            var sr = KarmaVisuals.Box(root, "유효 보스 공격", at, size, color, 10);
            var attack = sr.gameObject.AddComponent<KarmaBossAttack>();
            attack.game = game; attack.owner = owner; attack.velocity = velocity; attack.size = size;
            attack.remaining = life; attack.damage = damage; attack.contact = contact ?? new KarmaAttackContact(); attack.stun = stun;
            return attack;
        }
        void Update()
        {
            if (game == null || owner == null || !owner.Alive || game.Phase != RunPhase.Combat)
            { gameObject.SetActive(false); Destroy(gameObject); return; }
            if (game.Paused) return;
            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0, remaining));
            Vector2 from = transform.position, to = from + velocity * dt;
            transform.position = to;
            owner.TryAttackHit(from, to, size, damage, contact, stun);
            remaining -= Time.deltaTime;
            if (remaining <= 0 || to.x < 0 || to.x > 40 || to.y < 0 || to.y > 12)
            { gameObject.SetActive(false); Destroy(gameObject); }
        }
    }
}
