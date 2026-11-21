using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class UPBasicPattern : RisingBossPattern
        {
            public UPBasicPattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                float gap = owner.game.Player.Position.x - owner.Position.x;
                owner.direction = gap < 0 ? -1 : 1;
                owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                var target = new Vector2(Mathf.Clamp(owner.game.Player.Position.x - owner.direction * KarmaSpace.Units(1.35f),
                    KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                owner.state = ActionState.Telegraph;
                // A ground dash sweeps a rectangle; no generated polygon is needed.
                var mark = KarmaVisuals.Box(owner.effectsRoot, "기본 돌진 경로", (owner.Position + target) * 0.5f,
                    bodyAttackSize + Vector2.right * Mathf.Abs(target.x - owner.Position.x), new Color(1f, 0.48f, 0.15f, 0.4f), 9);
                Vector2 size = KarmaSpace.Units(new Vector2(3.05f, 1.55f));
                float dashX = target.x - owner.Position.x;
                float slashFacing = Mathf.Abs(dashX) > 0.01f ? Mathf.Sign(dashX) : owner.direction;
                var slashCenter = FollowupCenter(target, slashFacing);
                var slashMark = RisingPath(slashCenter, slashCenter, size, KarmaRisingCue.Charging);
                float warning = owner.Warning(0.24f);
                KarmaRisingAudio.Warn(warning);
                yield return owner.Wait(warning);
                if (mark != null) Destroy(mark.gameObject);
                var rising = (RisingBossBehavior)owner.behavior;
                bool gauntlet = owner.animator.MotionForm == "RiseWard";
                KarmaRisingAudio.EndWarning();
                if (owner.Active) KarmaRisingAudio.Dash();
                yield return rising.MotionWithImpact(target, DashSeconds(target),
                    bodyAttackSize, gauntlet ? null : KarmaRisingArt.Get("Slash"), KarmaSfxId.RsDashImpact);
                if (slashMark != null)
                {
                    slashMark.gameObject.SetActive(false);
                    Destroy(slashMark.gameObject);
                }
                for (int i = 0; i < 2 && owner.Active; i++)
                {
                    var center = FollowupCenter(owner.Position, owner.direction);
                    var player = owner.game.Player.Position;
                    if (Mathf.Abs(player.x - center.x) > size.x / 2 + KarmaSpace.PlayerHalfWidth ||
                        Mathf.Abs(player.y - center.y) > size.y / 2 + KarmaSpace.PlayerHalfHeight) break;
                    owner.state = ActionState.Attack;
                    owner.animator.Attack(Vector2.right * owner.direction);
                    if (i == 0) owner.animator.StartRisingBasicStrike();
                    else owner.animator.StartRisingBasicFollowup();
                    KarmaRisingAudio.Slash(i);
                    var slash = KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, center, Vector2.zero,
                        size, 0.09f, owner.Damage * Settings.meleeDamageMultiplier, Color.white, null, 0f,
                        visualPose: "Cleave", renderArtwork: false, sourcePattern: owner.selected);
                    var impact = gauntlet ? KarmaArtwork.Sprite("HitVFX", "Burst") : KarmaRisingArt.Get("Slash");
                    var strike = KarmaRisingPolish.ShowContact(slash, impact);
                    if (strike != null)
                    {
                        strike.flipX = owner.direction < 0;
                        if (gauntlet) strike.transform.localScale *= 0.72f;
                        else if (i == 1 && owner.animator.MotionForm == "Rise")
                            strike.transform.localRotation = Quaternion.Euler(0, 0, -28f * owner.direction);
                    }
                    yield return owner.Wait(0.12f);
                }
                yield return owner.Recovery(0.4f);
            }

            static Vector2 FollowupCenter(Vector2 position, float facing) =>
                position + new Vector2(facing * KarmaSpace.Units(1.62f), KarmaSpace.Units(0.72f));
        }
    }
}
