using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UP04Pattern : RisingBossPattern
        {
            public UP04Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                Vector2 start = owner.Position;
                owner.fullShield = true;
                owner.state = ActionState.Approach;
                KarmaRisingPolish.ShieldPlant(owner.effectsRoot, start, KarmaSpace.Units(new Vector2(2.2f, 2.6f)));
                KarmaRisingPolish.AegisBreak(owner.effectsRoot, start, RisingSlashRules.Up04AscentSeconds);
                float rise = RisingSlashRules.PursuitRiseAboveGround(Settings.riseHeight, KarmaSpace.Units(2.4f), KarmaSpace.Units(5.2f));
                float apexY = KarmaSpace.GuardianGroundY + rise;
                Vector2 apex = new Vector2(Mathf.Clamp(start.x, KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), apexY);
                owner.animator.HoldRisingPose("Rising", RisingSlashRules.Up04AscentSeconds);
                yield return owner.Motion(apex, RisingSlashRules.Up04AscentSeconds, Vector2.zero);
                Vector2 landing = new Vector2(Mathf.Clamp(start.x + owner.direction * KarmaSpace.Units(1.2f), KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.GuardianGroundY);
                owner.animator.HoldRisingPose("Fall", RisingSlashRules.Up04DescentSeconds);
                yield return owner.Motion(landing, RisingSlashRules.Up04DescentSeconds, bodyAttackSize);
                owner.animator.HoldRisingPose("Slash", 0.4f);
                Vector2 origin = new Vector2(landing.x, KarmaSpace.Units(2.35f));
                float life = Settings.fragmentLifetime;
                int count = RisingSlashRules.FragmentDegrees.Length;
                var warnings = new SpriteRenderer[count];
                var ends = new Vector2[count];
                var velocities = new Vector2[count];
                var launches = new Vector2[count];
                Vector2 shard = KarmaSpace.Units(new Vector2(1.15f, 0.78f));
                for (int i = 0; i < count; i++)
                {
                    float rad = RisingSlashRules.FragmentDegrees[i] * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    launches[i] = origin + dir * KarmaSpace.Units(0.9f);
                    var aim = RisingSlashRules.AimFragment(launches[i].x, launches[i].y, RisingSlashRules.FragmentDegrees[i], life,
                        KarmaStage.ArenaLeft, KarmaStage.ArenaRight, 0.8f, KarmaSpace.Units(14f), KarmaSpace.Units(7.4f), KarmaSpace.Units(0.45f), KarmaSpace.Units(3.5f), 0.35f);
                    ends[i] = new Vector2(aim.EndX, aim.EndY);
                    velocities[i] = new Vector2(aim.VelocityX, aim.VelocityY);
                    warnings[i] = owner.PathMark(launches[i], ends[i], shard);
                }
                owner.state = ActionState.Telegraph;

                yield return owner.Wait(owner.Warning(0.48f));
                for (int i = 0; i < warnings.Length; i++) if (warnings[i] != null) Destroy(warnings[i].gameObject);
                owner.fullShield = false;
                KarmaRisingPolish.ShieldPlant(owner.effectsRoot, landing, KarmaSpace.Units(new Vector2(2.4f, 2.4f)));
                KarmaRisingPolish.AegisBreak(owner.effectsRoot, landing, 0.55f);
                for (int i = 0; i < count && owner.Active; i++)
                {
                    var shot = owner.Attack(launches[i], velocities[i], shard, life, Settings.fragmentDamageMultiplier, new KarmaAttackContact(), 0f, visualPose: "Blade", parryStun: RisingSlashRules.FragmentParryStun);
                    if (shot == null) continue;
                    shot.name = "방패 파편";
                    shot.Repaint(KarmaRisingArt.Get("Fragment" + (i % 8)), false);
                    if (shot.Visual != null) shot.Visual.color = Color.white;
                }
                yield return owner.Wait(life);
                yield return owner.Recovery(1.15f);
            }
        }
    }
}
