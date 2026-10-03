using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginOverdrive()
        {
            overlay.Cancel();
            KarmaProjectile.ClearHostile(game);
            state = ActionState.OverdriveWindup; timer = Tuning.overdriveWindup; nextAura = 0;
            Intent = "삼중 합성 충전 · 무적 / 벽 방향으로 유도! 돌진 직전 방향 고정";
            ShowWard(game.Config.flameColor);
            indicator.transform.localScale = new Vector3(3.2f, 3.6f, 1);
        }
    }
}
