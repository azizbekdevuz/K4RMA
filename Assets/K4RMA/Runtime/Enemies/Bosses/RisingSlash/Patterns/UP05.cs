using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UP05Pattern : RisingBossPattern
        {
            public UP05Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                // Recovery and pattern interruption clear this through the existing host lifecycle.
                owner.fullShield = true;
                owner.state = ActionState.Telegraph;
                var charge = BeginRisingCharge(KarmaRisingCue.Charging);
                float warning = owner.Warning(0.62f);
                KarmaRisingAudio.Charge(warning);
                yield return owner.Wait(warning);
                KarmaRisingAudio.EndWarning();
                EndRisingCharge(charge);
                float left = KarmaStage.ArenaLeft + 4f, right = KarmaStage.ArenaRight - 4f;
                float ground = KarmaSpace.GuardianGroundY, mid = KarmaSpace.Units(3.15f), high = KarmaSpace.Units(5.35f);
                Vector2[] points =
                {
                    new Vector2(left, ground), new Vector2(right, mid),
                    new Vector2(left, high), new Vector2(right, ground),
                    new Vector2(left, mid), new Vector2(right, high),
                    new Vector2(left, ground), new Vector2(right, high),
                    new Vector2(left, mid)
                };
                yield return owner.Motion(points[0], DashSeconds(points[0], ultimate: true), Vector2.zero);
                Vector2 slash = KarmaSpace.Units(new Vector2(2.2f, 1.8f));
                var trail = KarmaRisingArt.Get("Slash");
                for (int i = 0; i < points.Length - 1 && owner.Active; i++)
                {
                    Vector2 start = owner.Position;
                    Vector2 endpoint = points[i + 1];
                    Vector2 target = endpoint;
                    var mark = RisingPath(start, target, slash, KarmaRisingCue.Tracking);
                    float tracking = owner.Warning(0.12f);
                    float elapsed = 0;
                    while (elapsed < tracking && owner.Active)
                    {
                        if (owner.game.Paused) { yield return null; continue; }
                        Vector2 player = owner.game.Player.Position;
                        // Preserve the original left-right endpoint route while bending
                        // each crossing toward the player's current height.
                        float trackingY = Mathf.Clamp(player.y, ground, KarmaSpace.Units(5.8f));
                        target = new Vector2(endpoint.x, Mathf.Lerp(endpoint.y, trackingY, 0.9f));
                        if (mark != null) mark.GetComponent<KarmaAttackWarning>().SetPath(mark, start, target, slash);
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

                    yield return owner.Wait(0.08f);
                    if (mark != null) Destroy(mark.gameObject);
                    float duration = DashSeconds(target, ultimate: true);
                    if (owner.Active) KarmaRisingAudio.Crossing();
                    if (i == points.Length - 2)
                        yield return ((RisingBossBehavior)owner.behavior).MotionWithImpact(target, duration, slash, trail, KarmaSfxId.RsUp05Final);
                    else
                        yield return owner.Motion(target, duration, slash, null, trail);
                }
                var returnPoint = new Vector2(Mathf.Clamp(owner.Position.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), ground);
                yield return owner.Motion(returnPoint, DashSeconds(returnPoint, ultimate: true), Vector2.zero);
                yield return owner.Recovery(2.5f);
            }

        }
    }
}
