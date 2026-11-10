using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class UP01Pattern : RisingBossPattern
        {
            public UP01Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                Vector2 start = owner.Position;
                var rising = (RisingBossBehavior)owner.behavior;
                bool empowered = rising.GuardEmpowered;
                if (empowered) rising.BeginBarrier(0f);
                Vector2 landing = new Vector2(Mathf.Clamp(owner.game.Player.Position.x,
                    KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                Vector2 area = KarmaSpace.Units(empowered ? new Vector2(8.5f, 3.2f) : new Vector2(6.5f, 1.25f));
                Vector2 impact = empowered ? landing + Vector2.up * KarmaSpace.Units(1.0f)
                    : new Vector2(landing.x, KarmaSpace.Units(0.62f));
                owner.state = ActionState.Telegraph;
                var drop = RisingPath(start, landing, bodyAttackSize, KarmaRisingCue.Locked);
                var zone = owner.Mark("UP01 착지 범위", impact, area);
                KarmaRisingPolish.LockPop(owner.effectsRoot, impact);
                KarmaRisingAudio.Warn(0.16f);
                yield return owner.Wait(0.16f);
                KarmaRisingAudio.EndWarning();
                if (drop != null) Destroy(drop.gameObject);
                if (zone != null) Destroy(zone.gameObject);
                var contact = new KarmaAttackContact();
                float fallTime = Mathf.Clamp(Mathf.Abs(start.y - landing.y) / KarmaSpace.Units(8f), 0.38f, 0.62f);
                if (owner.Active && start.y > landing.y) KarmaRisingAudio.Descent(fallTime);
                yield return owner.Motion(landing, fallTime, bodyAttackSize, contact, KarmaRisingArt.Get("Slash"));
                // The shards burst from the impact point when the downward strike lands.
                rising.ClearBarrier();
                if (empowered && owner.Active) new UP04Pattern(owner).Burst(landing);
                KarmaRisingPolish.AreaImpact(owner.game, owner.effectsRoot, impact, area * new Vector2(1.15f, 1.8f));
                if (owner.Active && start.y > landing.y && Mathf.Approximately(owner.Position.y, landing.y))
                    KarmaRisingAudio.Landing();
                var blast = owner.Attack(impact, Vector2.zero, area, 0.18f, Settings.landingDamageMultiplier,
                    contact, 0f, visualPose: "GroundRing");
                if (blast != null) blast.Repaint(KarmaRisingArt.Get("Impact"), true);
                yield return owner.Wait(0.18f);
                yield return owner.Recovery(0.8f);
            }
        }
    }
}
