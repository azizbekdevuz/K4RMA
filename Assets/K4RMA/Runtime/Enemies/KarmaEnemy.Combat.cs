using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        public void TakeDamage(float amount)
        {
            if (!Alive || !game.IsCombat || amount <= 0) return;
            engaged = true;
            if (Armored)
            {
                defenseHit = true; amount *= Mathf.Clamp01(game.Config.enemyWardDamageMultiplier);
                Intent = "방어 성공 · 짧은 반격 예고를 읽으세요";
                KarmaRemnantVFX.Impact(game, Position, Essence.Ward);
            }
            ReceiveDamage(amount);
        }
        void ReceiveDamage(float amount)
        {
            art.GetComponent<KarmaCharacterAnimator>().Hurt();
            Health = Mathf.Max(0, Health - Mathf.Max(0, amount));
            KarmaVisuals.Flash(game.World, Position, Vector2.one * 0.7f, Color.white, 0.1f);
            if (Health <= 0)
            {
                burn.Clear(); ClearIndicator();
                art.SetParent(game.World, true); art.GetComponent<KarmaCharacterAnimator>().Die();
                Destroy(art.gameObject, 0.45f); game.EnemyDefeated(this); Destroy(gameObject);
            }
        }
    }
}
