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
                yield return owner.Wait(owner.Warning(0.24f));
                if (mark != null) Destroy(mark.gameObject);
                yield return owner.Motion(target, DashSeconds(target),
                    bodyAttackSize, new KarmaAttackContact(), KarmaRisingArt.Get("Slash"));
                Vector2 size = KarmaSpace.Units(new Vector2(3.05f, 1.55f));
                for (int i = 0; i < 2 && owner.Active; i++)
                {
                    var center = owner.Position + new Vector2(owner.direction * KarmaSpace.Units(1.62f), KarmaSpace.Units(0.72f));
                    var player = owner.game.Player.Position;
                    if (Mathf.Abs(player.x - center.x) > size.x / 2 + KarmaSpace.PlayerHalfWidth ||
                        Mathf.Abs(player.y - center.y) > size.y / 2 + KarmaSpace.PlayerHalfHeight) break;
                    owner.state = ActionState.Attack;
                    owner.animator.Attack(Vector2.right * owner.direction);
                    var slash = owner.Attack(center, Vector2.zero, size, 0.09f, Settings.meleeDamageMultiplier, null, 0f, visualPose: "Cleave");
                    if (slash != null) slash.Repaint(KarmaRisingArt.Get("Slash"), true);
                    yield return owner.Wait(0.12f);
                }
                yield return owner.Recovery(0.4f);
            }
        }
    }
}
