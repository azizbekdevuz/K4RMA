using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UPBasicPattern : RisingBossPattern
        {
            public UPBasicPattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                float gap = owner.game.Player.Position.x - owner.Position.x;
                owner.direction = gap < 0 ? -1 : 1;
                owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                float stop = KarmaSpace.Units(1.75f);
                if (Mathf.Abs(gap) > stop + KarmaSpace.Units(0.35f))
                {
                    float step = Mathf.Min(Mathf.Abs(gap) - stop, KarmaSpace.Units(5.5f));
                    float duration = Mathf.Clamp(step / KarmaSpace.Units(18f), 0.12f, 0.26f);
                    Vector2 close = new Vector2(Mathf.Clamp(owner.Position.x + owner.direction * step, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                    owner.state = ActionState.Approach;
                    KarmaRisingPolish.PursuitStreak(owner.effectsRoot, owner.Position, owner.direction);
                    yield return owner.Motion(close, duration, Vector2.zero);
                }
                owner.state = ActionState.Telegraph;
                Vector2 tellSize = KarmaSpace.Units(new Vector2(3.4f, 2.2f));
                Vector2 tellCenter = new Vector2(owner.Position.x + owner.direction * KarmaSpace.Units(1.7f), KarmaSpace.GuardianGroundY + KarmaSpace.Units(0.85f));
                var mark = owner.Mark("기본 삼연베기", tellCenter, tellSize);
                KarmaRisingPolish.MeleeTell(owner.effectsRoot, tellCenter, owner.direction);

                yield return owner.Wait(owner.Warning(0.24f));
                if (mark != null) Destroy(mark.gameObject);
                for (int i = 0; i < RisingSlashRules.BasicStrikeCount && owner.Active; i++)
                {
                    float dx = owner.game.Player.Position.x - owner.Position.x;
                    owner.direction = dx < 0f ? -1f : 1f;
                    owner.art.localScale = new Vector3(owner.direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    RisingBasicStrike strike = RisingSlashRules.BasicStrike(i);
                    float centerX, centerY, sizeX, sizeY;
                    RisingSlashRules.BasicStrikeBox(owner.Position.x, KarmaSpace.GuardianGroundY, owner.direction, KarmaSpace.Scale, strike, out centerX, out centerY, out sizeX, out sizeY);
                    Vector2 size = new Vector2(sizeX, sizeY);
                    Vector2 center = new Vector2(centerX, centerY);
                    owner.state = ActionState.Attack;
                    KarmaAttackContact contact = RisingSlashRules.NewStrikeContact();
                    var slash = owner.Attack(center, Vector2.zero, size, strike.ActiveSeconds, Settings.meleeDamageMultiplier * strike.DamageShare, contact, 0f, visualPose: "Cleave");
                    if (slash != null) slash.Repaint(KarmaRisingArt.Get(strike.SpritePose), true);
                    Vector2 arc = i == RisingSlashRules.BasicStrikeCount - 1 ? size * new Vector2(1.18f, 1.22f) : size;
                    KarmaRisingPolish.BasicArc(owner.effectsRoot, center, arc, owner.direction, strike.SpritePose);
                    yield return owner.Wait(strike.ActiveSeconds);
                    if (i < RisingSlashRules.BasicStrikeCount - 1) yield return owner.Wait(RisingSlashRules.BasicGapSeconds);
                }
                yield return owner.Recovery(RisingSlashRules.BasicRecoverySeconds);
            }

        }
    }
}
