using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        static class SwordWaveVolley
        {
            public static IEnumerator Fire(KarmaEnemy owner, SwordBossBehavior sword, bool scars)
            {
                owner.state = ActionState.Telegraph;
                // Alternating lanes force repeated jump/landing decisions.
                Vector2 first = new Vector2(owner.Position.x + owner.direction * KarmaSpace.Scale, KarmaSpace.Units(1.05f));
                Vector2 second = new Vector2(owner.Position.x + owner.direction * KarmaSpace.Scale, KarmaSpace.Units(3.3f));
                Vector2 velocity = Vector2.right * owner.direction * Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed);
                Vector2 waveSize = sword.SwordSize(scars ? EnemyAttack.SW03 : EnemyAttack.SW01);
                // SW01 ends after two lanes; SW03 keeps its existing volley before the scars.
                int shots = scars ? (owner.Enraged ? 4 : 3) : 2;
                for (int i = 0; i < shots; i++)
                {
                    Vector2 origin = i % 2 == 0 ? first : second;
                    float life = scars ? sword.WaveLife
                        : KarmaBossProjectileGeometry.SwordShotLife(origin, velocity, float.MaxValue);
                    var mark = owner.PathMark(origin, origin + velocity * life, waveSize);
                    if (!scars) KarmaSwordAttackPolish.Warning(mark);

                    float warning = i == 0 ? owner.Warning(0.65f) : Mathf.Max(0.42f, 0.54f * owner.Tempo);
                    if (!scars) KarmaSwordAttackPolish.Charge(owner.game, owner, owner.art, origin, warning, false);
                    if (!scars)
                    {
                        owner.state = ActionState.Telegraph;
                        owner.animator.PrepareWaveAttack(warning, false);
                    }
                    else owner.animator.PlayWavePhase("Windup", warning);
                    yield return owner.Wait(warning);
                    owner.state = ActionState.Attack; owner.animator.Attack(); Destroy(mark.gameObject);
                    if (!scars)
                    {
                        sword.ReleaseSW01(origin, velocity, waveSize, life, i % 2 != 0);
                        sword.WaveLaunchVFX(origin, velocity, waveSize, false, i % 2 != 0);
                    }
                    else { owner.Attack(origin, velocity, waveSize, sword.WaveLife); owner.animator.ReleaseWavePhase(); owner.animator.PlayWavePhase("Guard"); }
                }
                // Released SW01 lanes own their boundary lifetime independently.
                yield return owner.Wait(scars ? sword.WaveLife : 0.12f);
            }
            public static bool CanReach(KarmaEnemy owner, SwordBossBehavior sword, EnemyAttack attack, Vector2 from, Vector2 target)
            {
                float facing = target.x < from.x ? -1 : 1;
                Vector2 origin = from + Vector2.right * facing * KarmaSpace.Scale;
                Vector2 velocity = Vector2.right * facing * Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed);
                float life = attack == EnemyAttack.SW01 ? float.MaxValue : sword.WaveLife;
                return sword.SwordShotCanReach(new Vector2(origin.x, KarmaSpace.Units(1.05f)), velocity, sword.SwordSize(attack), life, target)
                    || sword.SwordShotCanReach(new Vector2(origin.x, KarmaSpace.Units(3.3f)), velocity, sword.SwordSize(attack), life, target);
            }
        }
    }
}
