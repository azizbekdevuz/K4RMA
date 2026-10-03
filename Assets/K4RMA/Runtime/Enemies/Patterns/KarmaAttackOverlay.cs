using UnityEngine;

namespace KarmaPrototype
{
    // Explicitly driven by the boss. Never runs its own independent attack loop.
    public sealed class KarmaAttackOverlay : MonoBehaviour
    {
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer warning;
        int remaining;
        float wait;
        Vector2 aim;
        bool aiming;
        public void Initialize(KarmaGame director, KarmaEnemy boss) { game = director; owner = boss; }
        public void Begin(int phase)
        {
            Cancel();
            if (!game.Config.BossTuning.enableAttackOverlap || phase < 2) return;
            remaining = phase == 2 ? 1 : 2;
            wait = Mathf.Max(0.1f, game.Config.BossTuning.overlayStartDelay);
        }
        public void Tick(bool allowed)
        {
            if (!allowed || owner == null || !owner.Alive || !game.IsCombat) { Cancel(); return; }
            if (game.Paused || remaining <= 0) return;
            wait -= Time.deltaTime;
            if (wait > 0) return;
            var t = game.Config.BossTuning;
            if (!aiming)
            {
                // Lock aim at warning time so the player can move out before release.
                aim = (game.Player.Position - owner.Position).normalized;
                if (aim.sqrMagnitude < 0.01f) aim = Vector2.right;
                warning = KarmaVisuals.Box(owner.transform, "추가 마력탄 조준 예고", aim * 1.5f,
                    new Vector2(1.4f, 0.12f), new Color(1, 0.85f, 0.35f), 15);
                warning.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                wait = Mathf.Max(0.3f, t.overlayAimWarning); aiming = true;
            }
            else
            {
                ClearWarning();
                KarmaProjectile.Spawn(game, owner.Position + aim * 0.9f, aim * Mathf.Max(1, t.overlayBoltSpeed),
                    game.Config.enemyDamage * t.damageMultiplier * t.overlayDamageMultiplier,
                    false, new Color(1, 0.85f, 0.35f), 0.22f);
                remaining--; aiming = false;
                wait = Mathf.Max(0.2f, t.overlayShotGap);
            }
        }
        public void Cancel() { remaining = 0; aiming = false; ClearWarning(); }
        void ClearWarning() { if (warning != null) Destroy(warning.gameObject); warning = null; }
        void OnDestroy() { ClearWarning(); }
    }
}
