using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void UpdateAutoShield()
        {
            if (PrimaryType != Essence.Ward || autoShield || trial.Active || Stunned || state == ActionState.Recover || frontalGuard) return;
            if (shieldWarningUntil > 0)
            {
                if (FightSeconds >= shieldWarningUntil) RefreshAutoShield();
                return;
            }
            // Re-form between attacks after GD02 parry or GD05 success has broken GD01.
            if (!running && FightSeconds >= nextShield)
            { shieldWarningUntil = FightSeconds + Mathf.Max(0.3f, Tuning.autoShieldWarning); }
            if (shieldWarningUntil > 0)
                KarmaVisuals.Flash(game.World, Position, KarmaSpace.Units(new Vector2(1.8f, 2.6f)), new Color(1, 0.85f, 0.2f, 0.3f), 0.25f);
        }
        void RefreshAutoShield()
        {
            autoShield = true;
            shieldWarningUntil = 0;
        }
        void BreakAutoShield()
        {
            autoShield = false; shieldWarningUntil = 0;
            nextShield = FightSeconds + Mathf.Max(2, Tuning.autoShieldPeriod);
        }
        IEnumerator HeavyStrike(Coroutine terrainWave = null)
        {
            Vector2 center = new Vector2(Position.x + direction * KarmaSpace.Units(KarmaPatternCatalog.GuardHeavyReach / 2), KarmaSpace.Units(2.2f));
            Vector2 size = KarmaSpace.Units(new Vector2(KarmaPatternCatalog.GuardHeavyReach, 4.4f));
            var mark = Mark("GD02 강공격 · 패링 가능", center, size);
            state = ActionState.Telegraph; animator.PrepareHeavyStrike();
            float cueSeconds = Mathf.Max(0.1f, Tuning.heavyParryCueSeconds);
            yield return Wait(Mathf.Max(0, Warning(0.9f) - cueSeconds));
            heavyParryQueued = false; heavyParryUntil = FightSeconds + cueSeconds;
            Intent = "GD02 붉은 빛 · 지금 K로 패링";
            var cue = KarmaVisuals.Box(effectsRoot, "GD02 parry cue", animator.HeavyHandPosition,
                Vector2.one, Color.white, 50);
            cue.sprite = Resources.Load<Sprite>("K4RMAArtwork/Sheets/GD02/ParryRed");
            cue.sharedMaterial = KarmaVisuals.DefaultMaterial;
            cue.transform.localScale = Vector3.one * KarmaSpace.Units(0.65f) / cue.sprite.bounds.size.x;
            // An opaque core remains legible at normal Game-view size; the outer red glow adds light.
            var glowObject = new GameObject("GD02 parry glow");
            glowObject.transform.SetParent(cue.transform, false);
            glowObject.transform.localScale = Vector3.one * 1.2f;
            var glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = cue.sprite; glow.sharedMaterial = KarmaCombatVFX.GlowMaterial;
            glow.sortingOrder = 49;
            // The light and acceptance window start/end together, including the final visible frame.
            while (FightSeconds < heavyParryUntil && Active)
            {
                cue.transform.position = animator.HeavyHandPosition;
                glow.color = new Color(3, 0.8f, 0.45f, 0.9f + 0.1f * Mathf.Sin(FightSeconds * 24));
                yield return null;
            }
            cue.gameObject.SetActive(false); Destroy(cue.gameObject); heavyParryUntil = 0;
            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
            animator.SwingHeavyStrike(); state = ActionState.Attack;
            Intent = "GD02 강공격 · 내려베기";
            int parries = game.Player.ParryCount;
            var contact = new KarmaAttackContact(breaksShieldOnParry: true);
            Attack(center, Vector2.zero, size, 0.18f, 1.5f, contact, Tuning.heavyHitStunSeconds);
            TryAttackHit(center, center, size, Damage * 1.5f, contact, Tuning.heavyHitStunSeconds);
            if (game.Player.ParryCount != parries) yield break;
            // Include contacts made after the first frame of the active hit box.
            yield return HeavySlashTrail();
            heavyParryQueued = false;
            if (!Active || pendingParry) yield break;
            // Only the inherited sword-wave form gains this follow-up, at 50% HP or below.
            if (game.Player.Essences.HasRemnant(Essence.Flame) && Health <= MaxHealth * 0.5f)
            {
                Vector2 waveAt = new Vector2(Position.x, KarmaSpace.Units(0.65f));
                Vector2 waveSize = KarmaSpace.Units(new Vector2(1.2f, 0.65f));
                var warning = PathMark(waveAt, waveAt + Vector2.right * direction * KarmaSpace.Units(10), waveSize);
                Intent = "강공격 후 지면 충격파 · 점프로";
                yield return Wait(Warning(0.6f)); Destroy(warning.gameObject);
                Attack(waveAt, Vector2.right * direction * KarmaSpace.Units(14), waveSize, 0.7f, 0.75f);
                yield return Wait(0.75f);
            }
            // Do not remove steps while their child coroutines are still building colliders.
            if (terrainWave != null) yield return terrainWave;
            yield return Recovery(1.4f);
        }
        IEnumerator HeavySlashTrail()
        {
            var trailObject = new GameObject("GD02 overhead sword sweep");
            trailObject.transform.SetParent(effectsRoot, false);
            var trail = trailObject.AddComponent<LineRenderer>();
            trail.sharedMaterial = KarmaVisuals.DefaultMaterial;
            trail.sortingOrder = 9; trail.positionCount = 12;
            trail.startWidth = KarmaSpace.Units(0.18f); trail.endWidth = KarmaSpace.Units(0.03f);
            float elapsed = 0;
            while (elapsed < 0.2f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / 0.2f);
                for (int i = 0; i < trail.positionCount; i++)
                {
                    float t = Mathf.Clamp01(progress - (1 - i / 11f) * 0.4f);
                    float angle = Mathf.Lerp(115, -75, t) * Mathf.Deg2Rad;
                    trail.SetPosition(i, new Vector3(Position.x + direction * Mathf.Cos(angle) * KarmaSpace.Units(3),
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
