using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class GD02Pattern : GuardBossPattern
        {
            public GD02Pattern(KarmaEnemy owner) : base(owner) { }
            float heavyParryUntil;
            bool heavyParryQueued;
            public bool ParryWindowOpen { get { return owner.Active && !owner.game.Paused && owner.running
                && (owner.selected == EnemyAttack.GD02 || owner.selected == EnemyAttack.GD05)
                && owner.state == ActionState.Telegraph && heavyParryUntil > 0; } }
            public bool TryQueueParry() { if (!ParryWindowOpen) return false; heavyParryQueued = true; return true; }
            public bool ConsumeQueuedParry() { bool queued = heavyParryQueued; heavyParryQueued = false; return queued; }
            public override void Interrupt() { heavyParryUntil = 0; heavyParryQueued = false; owner.animator.EndHeavyStrike(); }
            public override IEnumerator Execute() { return ExecuteWithTerrain(null); }
            public IEnumerator ExecuteWithTerrain(Coroutine terrainWave, bool rearCounter = false)
            {
                yield return Strike(false, rearCounter: rearCounter || terrainWave != null);
                if (!owner.Active || owner.pendingParry) yield break;
                // Only the inherited sword-wave form gains this follow-up, at 50% HP or below.
                if (owner.game.Player.Essences.HasRemnant(Essence.SwordWave) && owner.Health <= owner.MaxHealth * 0.5f)
                {
                    Vector2 waveAt = new Vector2(owner.Position.x, KarmaSpace.Units(0.65f));
                    Vector2 waveSize = KarmaSpace.Units(new Vector2(1.2f, 0.65f));
                    var warning = owner.PathMark(waveAt, waveAt + Vector2.right * owner.direction * KarmaSpace.Units(10), waveSize);

                    yield return owner.Wait(owner.Warning(0.6f)); Destroy(warning.gameObject);
                    owner.Attack(waveAt, Vector2.right * owner.direction * KarmaSpace.Units(14), waveSize, 0.7f, 0.75f);
                    yield return owner.Wait(0.75f);
                }
                // Do not remove steps while their child coroutines are still building colliders.
                if (terrainWave != null) yield return terrainWave;
                yield return owner.Recovery(1.4f);
            }
            float CueSeconds { get { return Mathf.Max(0.1f, Settings.heavyParryCueSeconds); } }
            public float StrikeDuration { get { return Mathf.Max(CueSeconds, owner.Warning(0.9f)) + 0.24f; } }
            public IEnumerator Strike(bool inDome, bool rearCounter = false)
            {
                Vector2 center = new Vector2(owner.Position.x + owner.direction * (Settings.heavyStrikeReach / 2), KarmaSpace.Units(2.2f));
                Vector2 size = new Vector2(Settings.heavyStrikeReach, KarmaSpace.Units(4.4f));
                var mark = owner.Mark("GD02 강공격 · 패링 가능", center, size);
                owner.state = ActionState.Telegraph; owner.animator.PrepareHeavyStrike();
                float cueSeconds = CueSeconds;
                yield return owner.Wait(Mathf.Max(0, owner.Warning(0.9f) - cueSeconds));
                heavyParryQueued = false; heavyParryUntil = owner.FightSeconds + cueSeconds;
                owner.animator.HeatHeavyStrike();

                while (owner.FightSeconds < heavyParryUntil && owner.Active)
                    yield return null;
                heavyParryUntil = 0;
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                owner.animator.SwingHeavyStrike(); owner.state = ActionState.Attack;
                if (owner.Active) KarmaAudio.Play(rearCounter ? KarmaSfxId.GdCounter : KarmaSfxId.GdHeavySwing);

                int parries = owner.game.Player.ParryCount;
                var contact = new KarmaAttackContact(breaksShieldOnParry: !inDome, parryWithoutRiposte: inDome);
                var hitbox = inDome
                    ? KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, center, Vector2.zero, size, 0.18f,
                        owner.Damage * Settings.heavyStrikeDamageMultiplier, new Color(1, 0.7f, 0.12f),
                        contact, Settings.heavyHitStunSeconds, "Bash", visualSheet: "GuardBossVFX",
                        renderArtwork: false, sourcePattern: EnemyAttack.GD02)
                    : owner.Attack(center, Vector2.zero, size, 0.18f, Settings.heavyStrikeDamageMultiplier,
                        contact, Settings.heavyHitStunSeconds, pattern: EnemyAttack.GD02);
                owner.TryAttackHit(center, center, size, owner.Damage * Settings.heavyStrikeDamageMultiplier, contact, Settings.heavyHitStunSeconds, sourcePattern: EnemyAttack.GD02);
                if (owner.game.Player.ParryCount != parries)
                {
                    if (inDome && hitbox != null) { hitbox.gameObject.SetActive(false); Destroy(hitbox.gameObject); }
                    yield break;
                }
                // Include contacts made after the first frame of the active hit box.
                yield return HeavySlashTrail();
                heavyParryQueued = false;
            }
            IEnumerator HeavySlashTrail()
            {
                var trailObject = new GameObject("GD02 overhead sword sweep");
                trailObject.transform.SetParent(owner.effectsRoot, false);
                var trail = trailObject.AddComponent<LineRenderer>();
                trail.sharedMaterial = KarmaVisuals.DefaultMaterial;
                trail.sortingOrder = 9; trail.positionCount = 12;
                trail.startWidth = KarmaSpace.Units(0.18f); trail.endWidth = KarmaSpace.Units(0.03f);
                float elapsed = 0;
                while (elapsed < 0.2f && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / 0.2f);
                    for (int i = 0; i < trail.positionCount; i++)
                    {
                        float t = Mathf.Clamp01(progress - (1 - i / 11f) * 0.4f);
                        float angle = Mathf.Lerp(115, -75, t) * Mathf.Deg2Rad;
                        trail.SetPosition(i, new Vector3(owner.Position.x + owner.direction * Mathf.Cos(angle) * KarmaSpace.Units(3),
                            KarmaSpace.Units(2.6f) + Mathf.Sin(angle) * KarmaSpace.Units(2.6f), 0));
                    }
                    trail.startColor = new Color(1, 0.72f, 0.3f, 1 - progress * 0.7f);
                    trail.endColor = new Color(1, 0.95f, 0.75f, 0);
                    yield return null;
                }
                trailObject.SetActive(false); Destroy(trailObject);
            }
        }
    }
}
