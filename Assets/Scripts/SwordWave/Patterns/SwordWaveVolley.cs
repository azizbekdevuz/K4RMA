using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        static class SwordWaveVolley
        {
            static void AimSW01(KarmaEnemy owner, bool high, SpriteRenderer mark,
                out Vector2 origin, out Vector2 velocity, out float life, Vector2 size)
            {
                float dx = owner.game.Player.Position.x - owner.Position.x;
                owner.direction = Mathf.Abs(dx) > 0.0001f ? Mathf.Sign(dx) : owner.Facing;
                var scale = owner.art.localScale;
                scale.x = Mathf.Abs(scale.x) * owner.direction; owner.art.localScale = scale;
                origin = new Vector2(owner.Position.x + owner.direction * KarmaSpace.Scale,
                    KarmaSpace.Units(high ? 3.3f : 1.05f));
                velocity = Vector2.right * owner.direction * Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed * 1.3f);
                life = KarmaBossProjectileGeometry.SwordShotLife(origin, velocity, float.MaxValue);
                if (mark != null) mark.GetComponent<KarmaAttackWarning>().SetPath(mark, origin, origin + velocity * life, size, false, true);
            }
            static IEnumerator TrackSW01(KarmaEnemy owner, bool high, SpriteRenderer mark, Vector2 size, float seconds)
            {
                // Match Wait's yield/increment order exactly. Turning only flips
                // the existing pose; it never starts another animation or shot.
                float elapsed = 0;
                while (elapsed < seconds && owner.Active)
                {
                    if (!owner.game.Paused) AimSW01(owner, high, mark, out _, out _, out _, size);
                    yield return null;
                    if (!owner.game.Paused) elapsed += Time.deltaTime;
                }
            }
            public static IEnumerator Fire(KarmaEnemy owner, SwordBossBehavior sword, bool scars)
            {
                owner.state = ActionState.Telegraph;
                // Pick once per cast; the previous cast never influences this order.
                Vector2 first = new Vector2(owner.Position.x + owner.direction * KarmaSpace.Scale, KarmaSpace.Units(1.05f));
                Vector2 second = new Vector2(owner.Position.x + owner.direction * KarmaSpace.Scale, KarmaSpace.Units(3.3f));
                Vector2 velocity = Vector2.right * owner.direction * Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed * (scars ? 1 : 1.3f));
                Vector2 waveSize = sword.SwordSize(scars ? EnemyAttack.SW03 : EnemyAttack.SW01);
                // SW01 ends after two lanes; SW03 keeps its existing volley before the scars.
                int shots = scars ? (owner.Enraged ? 4 : 3) : 2;
                bool highFirst = !scars && Random.value < 0.5f;
                for (int i = 0; i < shots; i++)
                {
                    bool high = (i % 2 != 0) ^ highFirst;
                    Vector2 origin = high ? second : first;
                    float life = scars ? sword.WaveLife
                        : KarmaBossProjectileGeometry.SwordShotLife(origin, velocity, float.MaxValue);
                    if (!scars) AimSW01(owner, high, null, out origin, out velocity, out life, waveSize);
                    var mark = owner.PathMark(origin, origin + velocity * life, waveSize);
                    if (!scars) KarmaSwordAttackPolish.Warning(mark);

                    float warning = i == 0 ? owner.Warning(0.65f) : Mathf.Max(0.42f, 0.54f * owner.Tempo);
                    if (!scars) KarmaSwordWaveAnimation.Charge(owner.game, owner, owner.art, mark.transform, warning, false);
                    if (!scars)
                    {
                        owner.state = ActionState.Telegraph;
                        owner.animator.PrepareWaveAttack(warning, false);
                    }
                    else owner.animator.PlayWavePhase("Windup", warning);
                    if (scars) yield return owner.Wait(warning);
                    else yield return TrackSW01(owner, high, mark, waveSize, warning);
                    if (!scars && !owner.Active) yield break;
                    // Sample again on the release event itself, after the final
                    // wait resumes. Each released shot owns this immutable vector.
                    if (!scars) AimSW01(owner, high, mark, out origin, out velocity, out life, waveSize);
                    owner.state = ActionState.Attack; owner.animator.Attack(); Destroy(mark.gameObject);
                    if (!scars)
                    {
                        sword.ReleaseSW01(origin, velocity, waveSize, life, high);
                        sword.WaveLaunchVFX(origin, velocity, waveSize, false, high);
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
                Vector2 velocity = Vector2.right * facing * Mathf.Max(KarmaSpace.Scale, owner.Tuning.sword.swordSpeed * (attack == EnemyAttack.SW01 ? 1.3f : 1));
                float life = attack == EnemyAttack.SW01 ? float.MaxValue : sword.WaveLife;
                return sword.SwordShotCanReach(new Vector2(origin.x, KarmaSpace.Units(1.05f)), velocity, sword.SwordSize(attack), life, target)
                    || sword.SwordShotCanReach(new Vector2(origin.x, KarmaSpace.Units(3.3f)), velocity, sword.SwordSize(attack), life, target);
            }
        }
    }
}
