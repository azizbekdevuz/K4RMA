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
                bool groundedStart = start.y <= landing.y + 0.02f;
                Vector2 apex = groundedStart
                    ? new Vector2(Mathf.Lerp(start.x, landing.x, 0.4f), landing.y + KarmaSpace.Units(3f))
                    : start;
                Vector2 area = KarmaSpace.Units(empowered ? new Vector2(8.5f, 3.2f) : new Vector2(6.5f, 1.25f));
                Vector2 impact = empowered ? landing + Vector2.up * KarmaSpace.Units(1.0f)
                    : new Vector2(landing.x, KarmaSpace.Units(0.62f));
                owner.state = ActionState.Telegraph;
                if (!groundedStart) owner.animator.PrepareRisingDescent();
                else rising.FaceCrossing(landing);
                var drop = RisingPath(apex, landing, bodyAttackSize, KarmaRisingCue.Locked);
                var zone = owner.Mark("UP01 착지 범위", impact, area);
                KarmaRisingAudio.Warn(0.16f);
                yield return owner.Wait(0.16f);
                KarmaRisingAudio.EndWarning();
                if (groundedStart && owner.Active) yield return Ascend(start, apex);
                if (!owner.Active) yield break;
                if (drop != null) { drop.gameObject.SetActive(false); Destroy(drop.gameObject); }
                var contact = new KarmaAttackContact();
                float fallTime = Mathf.Clamp((apex.y - landing.y) / KarmaSpace.Units(8f), 0.38f, 0.62f);
                if (apex.y > landing.y)
                {
                    owner.animator.StartRisingDescent(fallTime);
                    KarmaRisingAudio.Descent(fallTime);
                }
                yield return rising.Cross(landing, fallTime, bodyAttackSize, contact, acceleratingFall: groundedStart);
                // Hold the landing tell through the descent, until the impact goes live.
                if (zone != null) { zone.gameObject.SetActive(false); Destroy(zone.gameObject); }
                rising.ClearBarrier();
                owner.game.PresentationShake(0.14f, 0.75f);
                if (owner.Active && apex.y > landing.y && Mathf.Approximately(owner.Position.y, landing.y))
                {
                    owner.animator.LandRisingDescent();
                    KarmaRisingAudio.Landing();
                }
                var blast = KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, impact, Vector2.zero,
                    area, 0.18f, owner.Damage * Settings.landingDamageMultiplier, Color.white, contact, 0f,
                    visualPose: "GroundRing", renderArtwork: false, sourcePattern: owner.selected);
                var landingFlash = KarmaRisingPolish.ShowContact(blast, KarmaRisingArt.Get("Impact"), empowered ? 0.62f : 0.92f);
                if (landingFlash != null) landingFlash.transform.position = landing;
                const float impactTime = 0.18f;
                if (empowered && owner.Active)
                {
                    // Spend the first impact cel on the shell. Interrupting the attack also cancels the release.
                    var shell = KarmaRisingPolish.ShieldBreakBeat(owner.effectsRoot,
                        landing + Vector2.up * KarmaSpace.Units(0.75f), KarmaSpace.Units(5.3f));
                    KarmaRisingAudio.ShieldBurst();
                    float impactEnd = Time.time + impactTime;
                    yield return owner.Wait(KarmaBootstrap.Step);
                    if (shell != null) { shell.gameObject.SetActive(false); Destroy(shell.gameObject); }
                    if (!owner.Active) yield break;
                    new UP04Pattern(owner).Burst(landing);
                    yield return owner.Wait(impactEnd - Time.time);
                }
                else yield return owner.Wait(impactTime);
                yield return owner.Recovery(0.8f);
            }

            IEnumerator Ascend(Vector2 floor, Vector2 apex)
            {
                const float duration = 5 * KarmaBootstrap.Step;
                owner.state = ActionState.Attack;
                owner.animator.StartRisingAscent();
                float elapsed = 0;
                while (elapsed < duration && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float height = progress * (2f - progress);
                    owner.transform.position = new Vector2(Mathf.Lerp(floor.x, apex.x, progress),
                        Mathf.Lerp(floor.y, apex.y, height));
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                if (owner.Active) owner.transform.position = apex;
            }
        }
    }
}
