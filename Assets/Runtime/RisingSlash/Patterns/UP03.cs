using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class UP03Pattern : RisingBossPattern
        {
            public UP03Pattern(KarmaEnemy owner) : base(owner) { }
            public override IEnumerator Execute()
            {
                Vector2 start = new Vector2(owner.Position.x, KarmaSpace.GuardianGroundY);
                yield return owner.Motion(start, DashSeconds(start), Vector2.zero);
                float room = owner.direction > 0 ? KarmaStage.ArenaRight - 1.2f - start.x : start.x - (KarmaStage.ArenaLeft + 1.2f);
                float height = Mathf.Clamp(Settings.groundBladeHeight, KarmaSpace.Units(4.4f), KarmaSpace.Units(6.4f));
                float gap = KarmaSpace.Units(0.45f);
                float reach = Mathf.Max(KarmaSpace.Units(8f), room * 0.78f);
                Vector2 size = new Vector2(Mathf.Max(KarmaSpace.Units(4f), reach - gap), height);
                Vector2 center = new Vector2(start.x + owner.direction * (gap + size.x * 0.5f), height * 0.5f);
                var mark = owner.Mark("전방 대검기 · 등 뒤로", center, size);
                owner.state = ActionState.Telegraph;
                float warning = owner.Warning(0.72f);
                owner.animator.PrepareRisingWave(warning);
                KarmaRisingAudio.Warn(warning);

                yield return owner.Wait(warning);
                if (mark != null) { mark.gameObject.SetActive(false); Destroy(mark.gameObject); }
                KarmaRisingAudio.EndWarning();
                const float activeTime = 0.42f;
                const float finishTime = 0.48f;
                var wave = KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, center, Vector2.zero,
                    size, activeTime, owner.Damage * Settings.waveDamageMultiplier, Color.white, null, 0f,
                    visualPose: "Wave", renderArtwork: false, sourcePattern: owner.selected);
                SpriteRenderer visual = null;
                if (wave != null)
                {
                    if (owner.Active)
                    {
                        owner.animator.StartRisingWave();
                        KarmaRisingAudio.Wave();
                    }
                    visual = KarmaRisingPolish.ShowContact(wave, KarmaRisingArt.Get("Wave"));
                    if (visual != null) visual.flipX = owner.direction < 0;
                }
                Vector3 fullScale = visual != null ? visual.transform.localScale : Vector3.one;
                float release = 0;
                while (release < finishTime && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    float sample = Mathf.Floor(release / KarmaBootstrap.Step) * KarmaBootstrap.Step;
                    if (visual != null)
                    {
                        float spread = Mathf.Lerp(0.90f, 1f, Mathf.Clamp01(sample / (2 * KarmaBootstrap.Step)));
                        var scale = fullScale;
                        scale.x *= spread;
                        visual.transform.localScale = scale;
                        visual.transform.localPosition = Vector3.left * owner.direction * size.x * (1f - spread) * 0.5f;
                        float dissolve = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(KarmaBootstrap.Step, activeTime, sample));
                        visual.color = new Color(1, 1, 1, Mathf.Lerp(1f, 0.24f, dissolve));
                    }
                    release += Time.deltaTime;
                    yield return null;
                }
                owner.animator.EndRisingMotion();
                yield return owner.Recovery(1.15f);
            }

        }
    }
}
