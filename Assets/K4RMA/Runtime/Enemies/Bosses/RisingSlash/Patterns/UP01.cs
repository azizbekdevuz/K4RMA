using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UP01Pattern : RisingBossPattern
        {
            public UP01Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                Vector2 start = owner.Position;
                float rise = RisingSlashRules.PursuitRiseAboveGround(Settings.riseHeight, KarmaSpace.Units(2.4f), KarmaSpace.Units(5.2f));
                float apexY = KarmaSpace.GuardianGroundY + rise;
                Vector2 apex = new Vector2(Mathf.Clamp(start.x + owner.direction * KarmaSpace.Units(1.4f), KarmaStage.ArenaLeft + 3f, KarmaStage.ArenaRight - 3f), apexY);
                owner.state = ActionState.Telegraph;
                var risePath = RisingPath(start, apex, bodyAttackSize, KarmaRisingCue.Charging);
                var charge = BeginRisingCharge(KarmaRisingCue.Charging);

                yield return owner.Wait(owner.Warning(RisingSlashRules.Up01WindupSeconds));
                EndRisingCharge(charge);
                if (risePath != null) Destroy(risePath.gameObject);
                charge = BeginRisingCharge(KarmaRisingCue.Tracking);
                SpriteRenderer path = null;
                float elapsed = 0f;
                Vector2 tracked = SampleLanding(apex.x);
                owner.state = ActionState.Attack;
                owner.animator.Attack(Vector2.up);
                while (elapsed < RisingSlashRules.Up01TrackingSeconds && owner.Active)
                {
                    bool paused = owner.game.Paused;
                    if (!paused)
                    {
                        float along = RisingSlashRules.AscentBlend(elapsed, RisingSlashRules.Up01AscentSeconds);
                        Vector2 at = Vector2.Lerp(start, apex, along);
                        owner.transform.position = at;
                        tracked = SampleLanding(apex.x);
                        if (path == null) path = RisingPath(at, tracked, bodyAttackSize, KarmaRisingCue.Tracking);
                        else path.GetComponent<KarmaAttackWarning>().SetPath(path, at, tracked, bodyAttackSize);
                    }
                    elapsed = RisingSlashRules.AdvanceUnpaused(elapsed, Time.deltaTime, paused);
                    yield return null;
                }
                if (!owner.Active) yield break;
                owner.transform.position = apex;
                // Freeze the last tracked landing. Later player movement must not enter this value.
                RisingSlashRules.RisingLandingPlan locked = RisingSlashRules.LockLanding(tracked.x, apex.x, KarmaSpace.GuardianGroundY, KarmaSpace.Units(0.62f),
                    KarmaSpace.Units(6.5f), KarmaSpace.Units(1.25f), KarmaStage.ArenaLeft, KarmaStage.ArenaRight, RisingSlashRules.Up01EdgeMargin, KarmaSpace.Units(4.5f));
                Vector2 landing = new Vector2(locked.LandingX, locked.LandingY);
                Vector2 impact = new Vector2(locked.ImpactX, locked.ImpactY);
                Vector2 area = new Vector2(locked.AreaWidth, locked.AreaHeight);
                if (path != null) Destroy(path.gameObject);
                if (charge != null) charge.Lock();
                var drop = RisingPath(apex, landing, bodyAttackSize, KarmaRisingCue.Locked);
                var zone = owner.Mark("UP01 착지 범위", impact, area);
                KarmaRisingPolish.LockPop(owner.effectsRoot, impact);

                yield return owner.Wait(owner.Warning(RisingSlashRules.Up01LockWarningSeconds));
                EndRisingCharge(charge);
                if (drop != null) Destroy(drop.gameObject);
                if (zone != null) Destroy(zone.gameObject);
                var contact = new KarmaAttackContact();
                yield return owner.Motion(landing, RisingSlashRules.Up01DropSeconds, bodyAttackSize, contact, KarmaRisingArt.Get("Slash"));
                KarmaRisingPolish.AreaImpact(owner.game, owner.effectsRoot, impact, area * new Vector2(1.15f, 1.8f));
                var blast = owner.Attack(impact, Vector2.zero, area, 0.22f, Settings.landingDamageMultiplier, contact, 0f, visualPose: "GroundRing");
                if (blast != null) blast.Repaint(KarmaRisingArt.Get("Impact"), true);
                if (owner.Enraged)
                {
                    yield return owner.Wait(0.28f);
                    Vector2 echoSize = new Vector2(area.x * 0.72f, area.y);
                    var echo = owner.Mark("착지 여파", impact, echoSize);

                    yield return owner.Wait(owner.Warning(0.42f));
                    if (echo != null) Destroy(echo.gameObject);
                    var second = owner.Attack(impact, Vector2.zero, echoSize, 0.18f, 0.7f, null, 0f, visualPose: "GroundRing");
                    if (second != null) second.Repaint(KarmaRisingArt.Get("Impact"), true);
                    KarmaRisingPolish.AreaImpact(owner.game, owner.effectsRoot, impact, echoSize);
                    yield return owner.Wait(0.22f);
                }
                else yield return owner.Wait(0.34f);
                yield return owner.Recovery(1.05f);
            }
            Vector2 SampleLanding(float apexX)
            {
                float x = RisingSlashRules.ClampLandingX(owner.game.Player.Position.x, apexX, KarmaStage.ArenaLeft, KarmaStage.ArenaRight,
                    RisingSlashRules.Up01EdgeMargin, KarmaSpace.Units(4.5f));
                return new Vector2(x, KarmaSpace.GuardianGroundY);
            }

        }
    }
}
