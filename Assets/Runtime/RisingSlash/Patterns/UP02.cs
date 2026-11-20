using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class UP02Pattern : RisingBossPattern
        {
            public UP02Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                var rising = (RisingBossBehavior)owner.behavior;
                if (rising.GuardEmpowered) rising.BeginBarrier(0.26f);
                Vector2 start = owner.Position;
                Vector2 target = start;
                owner.state = ActionState.Telegraph;
                KarmaRisingAudio.Warn(0.18f);
                var charge = BeginRisingCharge(KarmaRisingCue.Tracking);
                SpriteRenderer path = null;
                float elapsed = 0;
                while (elapsed < 0.18f && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    var player = owner.game.Player;
                    var body = player.GetComponent<Rigidbody2D>();
                    target = player.Position + (body != null ? body.linearVelocity * 0.1f : Vector2.zero);
                    target.x = Mathf.Clamp(target.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall);
                    target.y = Mathf.Clamp(target.y + KarmaSpace.Units(0.65f),
                        KarmaSpace.GuardianGroundY + KarmaSpace.Units(1.2f), KarmaSpace.Units(7));
                    if (path == null) path = RisingPath(start, target, bodyAttackSize, KarmaRisingCue.Tracking);
                    else path.GetComponent<KarmaAttackWarning>().SetPath(path, start, target, bodyAttackSize);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (charge != null) charge.Lock();
                yield return owner.Wait(0.08f);
                EndRisingCharge(charge);
                if (path != null) Destroy(path.gameObject);
                float pursuit = DashSeconds(target);
                KarmaRisingAudio.EndWarning();
                if (owner.Active) KarmaRisingAudio.Pursuit();
                owner.animator.StartRisingPursuit();
                yield return owner.Motion(target, pursuit, bodyAttackSize, null, KarmaRisingArt.Get("Slash"));
                owner.animator.EndRisingMotion();
                // The pursuit ends in the air: drop immediately rather than resetting on the floor.
                yield return new UP01Pattern(owner).Execute();
            }
        }
    }
}
