using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginRisingPattern()
        {
            risingHitPlayer = false; risingElapsed = 0; risingStartX = Position.x; state = ActionState.Rising; timer = 0.95f;
            Intent = selected == EnemyAttack.AirWave ? "공중 검기 · 도약 정점에서 발사" : "상승베기 · 착지 후 빈틈";
        }
        void TickRising(float dt)
        {
            risingElapsed += dt; float t = Mathf.Clamp01(risingElapsed / 0.95f); Vector2 before = Position;
            transform.position = new Vector3(Mathf.Clamp(risingStartX + direction * 4 * t, 1.5f, 38.5f), 1.15f + Mathf.Sin(t * Mathf.PI) * 3.8f, 0);
            if (!risingHitPlayer && KarmaProjectile.Intersects(before, Position, game.Player.Position, 1.15f))
            { risingHitPlayer = true; game.Player.TakeImpact(Damage, Position, 5); }
            if (!Alive || !game.IsCombat) return;
            if (selected == EnemyAttack.AirWave && risingElapsed - dt < 0.475f && risingElapsed >= 0.475f) FireSwordWave();
            if (t >= 1) { transform.position = new Vector3(Position.x, 1.15f, 0); Recover(1.0f); }
        }
    }
}
