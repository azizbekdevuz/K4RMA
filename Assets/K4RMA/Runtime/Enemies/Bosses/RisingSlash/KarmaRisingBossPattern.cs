using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        abstract class RisingBossPattern : BossPattern
        {
            protected KarmaRisingBossSettings Settings { get { return owner.Tuning.rising; } }
            protected RisingBossPattern(KarmaEnemy owner) : base(owner) { }
            readonly protected Vector2 bodyAttackSize = KarmaSpace.Units(new Vector2(1.4f, 1.8f));
            protected SpriteRenderer RisingPath(Vector2 from, Vector2 to, Vector2 size, KarmaRisingCue cue)
            {
                var color = cue == KarmaRisingCue.Locked
                ? new Color(1f, 0.28f, 0.02f, 0.66f)
                : cue == KarmaRisingCue.Tracking
                ? new Color(0.15f, 0.95f, 1f, 0.5f)
                : new Color(1f, 0.78f, 0.28f, 0.58f);
                string label = cue == KarmaRisingCue.Locked ? "상승베기 고정" : cue == KarmaRisingCue.Tracking ? "상승베기 추적" : "상승베기 준비";
                var marker = KarmaVisuals.Box(owner.effectsRoot, label, Vector2.zero, Vector2.one, color, 9);
                var warning = marker.gameObject.AddComponent<KarmaAttackWarning>();
                warning.SetPath(marker, from, to, size);
                warning.Emphasize();
                return marker;
            }
            protected KarmaRisingCharge BeginRisingCharge(KarmaRisingCue cue)
            {
                return KarmaRisingCharge.Begin(owner.effectsRoot, owner.transform, cue);
            }
            protected static void EndRisingCharge(KarmaRisingCharge charge)
            {
                if (charge != null) charge.End();
            }
            // Movement that keeps the caller's pose. Shared Motion always forces the attack state.
            protected IEnumerator Glide(Vector2 target, float duration)
            {
                Vector2 from = owner.Position;
                float elapsed = 0f;
                duration = Mathf.Max(0.01f, duration);
                while (elapsed < duration && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float shaped = t * t * (3f - 2f * t);
                    float x = Mathf.Clamp(Mathf.Lerp(from.x, target.x, shaped), KarmaStage.BossLeftWall, KarmaStage.BossRightWall);
                    owner.transform.position = new Vector2(x, Mathf.Lerp(from.y, target.y, shaped));
                    yield return null;
                }
                if (owner.Active)
                    owner.transform.position = new Vector2(Mathf.Clamp(target.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), target.y);
            }
            protected void ShowPursuit(float direction)
            {
                KarmaRisingPolish.Pursuit(owner.effectsRoot, owner.Position, direction, Settings.trailStyle, owner.art.GetComponent<SpriteRenderer>());
            }
        }
    }
}
