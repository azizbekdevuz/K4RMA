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
                float face = gap < 0f ? -1f : 1f;
                Face(face);
                if (Settings.basicVariant == RisingBasicVariant.DashTwoCut) yield return DashCombo(face);
                else yield return AdvanceCombo(face);
            }
            IEnumerator AdvanceCombo(float face)
            {
                float stop = KarmaSpace.Units(1.75f);
                float gap = Mathf.Abs(owner.game.Player.Position.x - owner.Position.x);
                if (gap > stop + KarmaSpace.Units(0.35f))
                {
                    float step = Mathf.Min(gap - stop, KarmaSpace.Units(5.5f));
                    float duration = Mathf.Clamp(step / KarmaSpace.Units(18f), 0.12f, 0.26f);
                    Vector2 close = new Vector2(Mathf.Clamp(owner.Position.x + face * step, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                    owner.state = ActionState.Approach;
                    ShowPursuit(face);
                    yield return Glide(close, duration);
                }
                owner.state = ActionState.Telegraph;
                Vector2 tellSize = KarmaSpace.Units(new Vector2(3.4f, 2.2f));
                Vector2 tellCenter = new Vector2(owner.Position.x + face * KarmaSpace.Units(1.7f), KarmaSpace.GuardianGroundY + KarmaSpace.Units(0.85f));
                var mark = owner.Mark("기본 삼연베기", tellCenter, tellSize);
                KarmaRisingPolish.MeleeTell(owner.effectsRoot, tellCenter, face);
                yield return owner.Wait(owner.Warning(0.24f));
                if (mark != null) Destroy(mark.gameObject);
                int count = RisingSlashRules.ComboCount(RisingBasicVariant.AdvancingThreeHit);
                for (int i = 0; i < count && owner.Active; i++)
                {
                    string body = i == count - 1 ? "Rising" : "Slash";
                    float gapAfter = i < count - 1 ? RisingSlashRules.BasicGapSeconds : 0f;
                    yield return Cut(face, RisingSlashRules.ComboStrike(RisingBasicVariant.AdvancingThreeHit, i), RisingSlashRules.ComboAdvance(RisingBasicVariant.AdvancingThreeHit, i), body, gapAfter);
                }
                yield return owner.Recovery(RisingSlashRules.BasicRecoverySeconds);
            }
            IEnumerator DashCombo(float face)
            {
                owner.state = ActionState.Telegraph;
                owner.animator.HoldRisingPose("Windup", 0.34f);
                Vector2 tellSize = KarmaSpace.Units(new Vector2(3.2f, 2.4f));
                Vector2 tellCenter = new Vector2(owner.Position.x + face * KarmaSpace.Units(1.9f), KarmaSpace.GuardianGroundY + KarmaSpace.Units(0.9f));
                var mark = owner.Mark("기본 이단베기", tellCenter, tellSize);
                KarmaRisingPolish.MeleeTell(owner.effectsRoot, tellCenter, face);
                yield return owner.Wait(owner.Warning(0.30f));
                if (mark != null) Destroy(mark.gameObject);
                float stop = KarmaSpace.Units(1.15f);
                float gap = Mathf.Abs(owner.game.Player.Position.x - owner.Position.x);
                float step = Mathf.Max(KarmaSpace.Units(0.8f), Mathf.Min(Mathf.Max(0f, gap - stop), KarmaSpace.Units(6.5f)));
                Vector2 close = new Vector2(Mathf.Clamp(owner.Position.x + face * step, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                owner.state = ActionState.Approach;
                ShowPursuit(face);
                float duration = Mathf.Clamp(step / KarmaSpace.Units(22f), 0.10f, 0.20f);
                yield return Glide(close, duration);
                yield return Cut(face, RisingSlashRules.ComboStrike(RisingBasicVariant.DashTwoCut, 0), RisingSlashRules.ComboAdvance(RisingBasicVariant.DashTwoCut, 0), "Slash", RisingSlashRules.DashTwoCutGapSeconds);
                yield return Cut(face, RisingSlashRules.ComboStrike(RisingBasicVariant.DashTwoCut, 1), RisingSlashRules.ComboAdvance(RisingBasicVariant.DashTwoCut, 1), "Rising", 0f);
                yield return owner.Recovery(RisingSlashRules.BasicRecoverySeconds);
            }
            IEnumerator Cut(float face, RisingBasicStrike strike, float advance, string bodyPose, float gapAfter)
            {
                Face(face);
                owner.state = ActionState.Attack;
                owner.animator.HoldRisingPose(bodyPose, strike.ActiveSeconds + 0.08f);
                Vector2 stepTo = new Vector2(Mathf.Clamp(owner.Position.x + face * KarmaSpace.Units(advance), KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                yield return Glide(stepTo, 0.06f);
                float centerX, centerY, sizeX, sizeY;
                RisingSlashRules.BasicStrikeBox(owner.Position.x, KarmaSpace.GuardianGroundY, owner.direction, KarmaSpace.Scale, strike, out centerX, out centerY, out sizeX, out sizeY);
                Vector2 size = new Vector2(sizeX, sizeY);
                Vector2 center = new Vector2(centerX, centerY);
                KarmaAttackContact contact = RisingSlashRules.NewStrikeContact();
                float share = strike.DamageShare;
                if (Settings.basicVariant == RisingBasicVariant.DashTwoCut && strike.Index == 1 && Settings.guardBasicAccent
                    && owner.game.Player.Essences.HasRemnant(Essence.Guard))
                    share *= RisingSlashRules.GuardSecondCutAccent;
                KarmaRisingPolish.SuppressStockAttackVfx = true;
                KarmaBossAttack slash;
                try
                {
                    slash = owner.Attack(center, Vector2.zero, size, strike.ActiveSeconds, Settings.meleeDamageMultiplier * share, contact, 0f, visualPose: "Cleave");
                }
                finally { KarmaRisingPolish.SuppressStockAttackVfx = false; }
                if (slash != null && slash.Visual != null) slash.Visual.gameObject.SetActive(false);
                KarmaRisingPolish.BasicArc(owner.effectsRoot, center, size, owner.direction, strike.SpritePose);
                yield return owner.Wait(strike.ActiveSeconds);
                if (gapAfter > 0f) yield return owner.Wait(gapAfter);
            }
            void Face(float face)
            {
                owner.direction = face;
                owner.art.localScale = new Vector3(face * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            }
        }
    }
}
