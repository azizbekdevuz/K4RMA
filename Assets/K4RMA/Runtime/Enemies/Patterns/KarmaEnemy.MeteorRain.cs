using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void BeginMeteorRain()
        {
            activeMeteors = Tuning.MeteorCountFor(CombatPhase);
            state = ActionState.MeteorRain;
            for (int i = 0; i < activeMeteors; i++)
                KarmaMeteor.Spawn(game, this, KarmaMeteorLayout.LandingX(lockedX, i),
                    Tuning.meteorWarning, Damage * Tuning.meteorDamageMultiplier,
                    i * Mathf.Max(0.2f, Tuning.meteorSequenceGap), Tuning.meteorTracksPlayer);
            overlay.Begin(CombatPhase);
            Intent = "메테오 연속 낙하 / 예고된 위치에서 계속 이동하세요" + (CombatPhase >= 2 && Tuning.enableAttackOverlap ? " · 추가 마력탄 조준 주의" : "");
        }
    }
}
