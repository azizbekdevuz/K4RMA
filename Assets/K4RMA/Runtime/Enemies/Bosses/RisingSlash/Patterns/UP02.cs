using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UP02Pattern : RisingBossPattern
        {
            public UP02Pattern(KarmaEnemy owner) : base(owner) { }
            IEnumerator AirPursuit(bool recover)
            {
                Vector2 origin = owner.Position, target = origin;
                SpriteRenderer path = null;
                owner.state = ActionState.Telegraph;
                owner.animator.HoldRisingPose("Windup", 0.2f);
                Vector2 perch = new Vector2(Mathf.Clamp(origin.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), Mathf.Min(KarmaSpace.Units(4.4f), origin.y + KarmaSpace.Units(1.7f)));
                yield return Glide(perch, 0.16f);
                origin = owner.Position;
                owner.animator.HoldRisingPose("Rising", 0.72f);
                var charge = BeginRisingCharge(KarmaRisingCue.Tracking);
                float elapsed = 0;
                while (elapsed < 0.36f && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    var body = owner.game.Player.GetComponent<Rigidbody2D>();
                    float lead = RisingSlashRules.InterceptLead(body != null ? body.linearVelocity.x * 0.18f : 0f, KarmaSpace.Units(1.6f));
                    target = owner.game.Player.Position;
                    target.x += lead;
                    target.x = Mathf.Clamp(target.x, Mathf.Max(KarmaStage.ArenaLeft + 2, origin.x - KarmaSpace.Units(7)), Mathf.Min(KarmaStage.ArenaRight - 2, origin.x + KarmaSpace.Units(7)));
                    target.y = Mathf.Clamp(target.y, KarmaSpace.Units(2.5f), KarmaSpace.Units(7));
                    if (path == null) path = RisingPath(origin, target, bodyAttackSize, KarmaRisingCue.Tracking);
                    else path.GetComponent<KarmaAttackWarning>().SetPath(path, origin, target, bodyAttackSize);
                    elapsed += Time.deltaTime; yield return null;
                }
                if (path != null) Destroy(path.gameObject);
                if (charge != null) charge.Lock();
                KarmaRisingPolish.LockPop(owner.effectsRoot, target);
                path = RisingPath(origin, target, bodyAttackSize, KarmaRisingCue.Locked);

                yield return owner.Wait(owner.Warning(0.36f));
                EndRisingCharge(charge);
                if (path != null) Destroy(path.gameObject);
                owner.animator.HoldRisingPose("AirSlash", 0.28f);
                yield return owner.Motion(target, 0.28f, bodyAttackSize, null, KarmaRisingArt.Get("Slash"));
                KarmaRisingPolish.BasicArc(owner.effectsRoot, target, KarmaSpace.Units(new Vector2(2.4f, 1.5f)), target.x < origin.x ? -1f : 1f, "SlashHigh");
                owner.animator.HoldRisingPose("Fall", 0.32f);
                yield return owner.Motion(new Vector2(target.x, KarmaSpace.GuardianGroundY), 0.32f, Vector2.zero);
                if (recover && owner.Enraged)
                {
                    yield return owner.Wait(Mathf.Max(0.45f, Settings.crossingReset * owner.Tempo));
                    yield return AirPursuit(false);
                }
                if (recover) yield return owner.Recovery(0.9f);
            }
            public override IEnumerator Execute() { return AirPursuit(true); }

        }
    }
}
