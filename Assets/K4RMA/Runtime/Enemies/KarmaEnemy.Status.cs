using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        public void ApplyBurn()
        {
            if (!Alive || !game.IsCombat || Invulnerable) return;
            burn.Apply(Time.time, Tuning.enemyBurnDuration, Tuning.enemyBurnInterval, 0);
        }
        void UpdateBurn()
        {
            float unused;
            if (burn.ConsumeTick(Time.time, out unused) && !Invulnerable)
            {
                // Damage over time is not a new attack: never reflect it or retaliate against it.
                KarmaRemnantVFX.Impact(game, Position, Essence.Flame);
                ReceiveDamage(Tuning.enemyBurnDamage * (Armored ? game.Config.enemyWardDamageMultiplier : 1));
            }
        }
        void TickFireAura()
        {
            if (Time.time < nextAura) return;
            nextAura = Time.time + 0.45f;
            KarmaRemnantVFX.Impact(game, Position, Essence.Flame);
            if (Vector2.Distance(game.Player.Position, Position) < 2.2f)
                game.Player.TakeDamage(Damage * 0.65f);
        }
        void CrashIntoWall()
        {
            // Wall impact is the intended counter: boss recoil, no contact damage during stun.
            overlay.Cancel();
            KarmaProjectile.ClearHostile(game);
            ClearIndicator(); reflectedDamage = 0;
            state = ActionState.Stunned; timer = Tuning.wallStunSeconds;
            recoilRemaining = Tuning.bossRecoilDistance;
            indicator = KarmaVisuals.Box(transform, "기절 표시", Vector2.up * 2.2f,
                new Vector2(0.7f, 0.3f), Color.yellow, 14);
            Intent = "벽 유도 성공! 방어막 파괴 · 수호자 넉백·기절 / 지금 공격하세요!";
            KarmaVisuals.Flash(game.World, Position, Vector2.one * 5, new Color(1, 0.4f, 0.1f, 0.75f), 0.4f);
            KarmaRemnantVFX.Impact(game, Position, Essence.Ward);
            if (Vector2.Distance(game.Player.Position, Position) < 2.5f)
                game.Player.TakeImpact(Damage * 1.5f, Position, 10);
        }
    }
}
