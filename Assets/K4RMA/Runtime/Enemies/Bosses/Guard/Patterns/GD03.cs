using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class GD03Pattern : GuardBossPattern
        {
            public GD03Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                owner.state = ActionState.Telegraph;
                var contact = new KarmaAttackContact();
                Vector2 origin = owner.Position + Vector2.right * owner.direction * KarmaSpace.Scale;
                Vector2 impact = new Vector2(Mathf.Clamp(owner.game.Player.Position.x, KarmaStage.ArenaLeft + 4, KarmaStage.ArenaRight - 4), KarmaSpace.Units(0.65f));
                Vector2 size = KarmaSpace.Units(new Vector2(1.2f, 1.2f));
                var mark = owner.PathMark(origin, impact, size);
                yield return owner.Wait(owner.Warning(0.8f)); Destroy(mark.gameObject);
                owner.state = ActionState.Attack; owner.animator.Attack();
                owner.Attack(origin, (impact - origin) / 0.5f, size, 0.5f, Settings.shieldThrowDamageMultiplier, contact, Settings.fragmentStunSeconds);
                yield return owner.Wait(0.55f);
                int count = Mathf.Clamp(Settings.shieldFragments, 2, 5);
                var starts = new Vector2[count]; var aims = new Vector2[count];
                var paths = new SpriteRenderer[count];
                Vector2 target = owner.game.Player.Position; // One sample for all fragments; no late tracking.
                for (int i = 0; i < count; i++)
                {
                    starts[i] = impact + KarmaSpace.Units(new Vector2((i - (count - 1) / 2f) * 1.1f, 1.3f + i * 0.35f));
                    aims[i] = (target - starts[i]).normalized * KarmaSpace.Units(10);
                    paths[i] = owner.PathMark(starts[i], starts[i] + aims[i] * 0.8f, KarmaSpace.Units(new Vector2(0.5f, 0.5f)));
                }

                yield return owner.Wait(owner.Warning(Settings.fragmentAimWarning));
                for (int i = 0; i < count; i++)
                {
                    Destroy(paths[i].gameObject);
                    owner.Attack(starts[i], aims[i], KarmaSpace.Units(new Vector2(0.5f, 0.5f)), 0.8f, Settings.shieldFragmentDamageMultiplier, contact, Settings.fragmentStunSeconds);
                }
                yield return owner.Wait(0.85f);
                // Return uses separate non-damaging visuals and a fixed deadline.
                var pieces = new SpriteRenderer[count];
                for (int i = 0; i < count; i++)
                {
                    pieces[i] = KarmaVisuals.Box(owner.effectsRoot, "회수 조각 · 피해 없음", starts[i] + aims[i] * 0.8f,
                    Vector2.one, new Color(0.3f, 0.85f, 1, 0.65f), 8);
                    if (!KarmaCombatVFX.ApplySprite(pieces[i], "GuardBossVFX", "Shard", KarmaSpace.Units(new Vector2(0.4f, 0.4f)), true))
                    KarmaCombatVFX.ApplySprite(pieces[i], "GuardianVFX", "Ward", KarmaSpace.Units(new Vector2(0.4f, 0.4f)), true);
                }

                float elapsed = 0;
                while (elapsed < 0.6f && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    elapsed += Time.deltaTime;
                    for (int i = 0; i < count; i++)
                    {
                        pieces[i].transform.position = Vector2.Lerp(starts[i] + aims[i] * 0.8f, owner.Position, Mathf.Clamp01(elapsed / 0.6f));
                        Vector2 heading = owner.Position - (Vector2)pieces[i].transform.position;
                        pieces[i].transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                    }
                    yield return null;
                }
                yield return owner.Recovery(1.2f);
            }

        }
    }
}
