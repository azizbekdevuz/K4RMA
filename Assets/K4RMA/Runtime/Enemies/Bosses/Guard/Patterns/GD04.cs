using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class GD04Pattern : GuardBossPattern
        {
            readonly GD02Pattern heavy;
            public GD04Pattern(KarmaEnemy owner, GD02Pattern heavy) : base(owner) { this.heavy = heavy; }
            public override void Interrupt() { owner.animator.SetGroundSlamFrame(-1); }

            public override IEnumerator Execute()
            {
                owner.frontalGuard = true; owner.state = ActionState.Telegraph;
                owner.direction = owner.game.Player.Position.x < owner.Position.x ? -1 : 1;
                int count = Mathf.Clamp(Settings.barrierSteps, 3, 8);
                float width = KarmaSpace.Units(1.65f);
                float room = Mathf.Max(0.1f, (owner.Facing > 0 ? KarmaStage.ArenaRight - owner.Position.x
                    : owner.Position.x - KarmaStage.ArenaLeft) - KarmaSpace.Units(0.15f));
                float gap = Mathf.Min(KarmaSpace.Units(1.2f), room * 0.2f);
                // Fit toward the player instead of flipping the attack behind the boss.
                // Keep the normal footprint where possible, and at least three steps if they fit.
                int fitting = Mathf.FloorToInt((room - gap - width) / (width * 0.92f)) + 1;
                count = Mathf.Min(count, Mathf.Max(3, fitting));
                while (count > 1 && (room - gap) / (1 + (count - 1) * 0.92f) < KarmaSpace.Units(0.75f)) count--;
                width = Mathf.Min(width, (room - gap) / (1 + (count - 1) * 0.92f));
                float stride = width * 0.92f, offset = gap + width / 2;
                owner.art.localScale = new Vector3(owner.Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                int terrainDirection = owner.Facing;
                bool sawPlayerInFront = (owner.game.Player.Position.x - owner.Position.x) * terrainDirection >= 0;
                var sizes = new Vector2[count]; var centers = new Vector2[count];
                var marks = new SpriteRenderer[count];
                for (int i = 0; i < count; i++)
                {
                    float height = KarmaSpace.Units(1.1f + i * 0.6f);
                    sizes[i] = new Vector2(width, height);
                    centers[i] = new Vector2(owner.Position.x + owner.direction * (offset + i * stride), height / 2);
                    marks[i] = owner.Mark("GD04 계단 상승 예고 " + (i + 1), centers[i], sizes[i]);
                }

                float warning = owner.Warning(0.8f);
                float descent = Mathf.Min(0.10f, warning * 0.25f);
                owner.animator.SetGroundSlamFrame(0);
                yield return owner.Wait(warning - descent);
                owner.animator.SetGroundSlamFrame(1);
                yield return owner.Wait(descent);
                owner.state = ActionState.Attack; owner.animator.Attack(Vector2.down);
                owner.animator.SetGroundSlamFrame(2);
                KarmaCombatVFX.Burst(owner.effectsRoot, owner.animator.GroundSlamContact,
                    (KarmaArtwork.Sprite("GuardBasaltDebris", "Burst") ?? KarmaArtwork.Sprite("GuardBossVFX", "Bash")), "GD04 ground impact",
                    KarmaSpace.Units(new Vector2(1.65f, 1.4f)), 0.23f, 0, new Color(1, 0.54f, 0.26f), true);
                float reboundAt = owner.FightSeconds + 0.12f;
                bool finished = false;
                var wave = owner.StartCoroutine(RiseBarrierWave(centers, sizes, marks, terrainDirection, () => finished = true));
                float held = 0;
                while (owner.Active && (held < 0.6f || !finished))
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    if (owner.FightSeconds >= reboundAt)
                    {
                        owner.animator.SetGroundSlamFrame(3);
                        reboundAt = float.PositiveInfinity;
                    }
                    float signedDistance = (owner.game.Player.Position.x - owner.Position.x) * terrainDirection;
                    if (signedDistance >= 0) sawPlayerInFront = true;
                    // One rear counter per GD04, while the terrain stays on its original side.
                    if ((owner.FightSeconds < owner.shieldUntil) && sawPlayerInFront && signedDistance < -KarmaSpace.PlayerHalfWidth
                        && -signedDistance <= Settings.heavyStrikeReach + KarmaSpace.PlayerHalfWidth)
                    {
                        Interrupt();
                        owner.selected = owner.previous = EnemyAttack.GD02;
                        owner.direction = -terrainDirection;
                        owner.art.localScale = new Vector3(owner.Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                        owner.patternStarted = owner.FightSeconds;

                        yield return heavy.ExecuteWithTerrain(wave);
                        yield break;
                    }
                    if (finished) held += Time.deltaTime;
                    yield return null;
                }
                Interrupt();
                yield return owner.Recovery(Mathf.Max(0.3f, Settings.barrierRecovery));
            }

            IEnumerator RiseBarrierWave(Vector2[] centers, Vector2[] sizes, SpriteRenderer[] marks, int direction, System.Action completed)
            {
                int count = centers.Length;
                var surfaces = new BoxCollider2D[count];
                for (int i = 0; i < count; i++)
                {
                    var rise = RiseBarrierStep(centers[i], sizes[i], marks[i], surfaces, i, direction);
                    if (i == count - 1) yield return rise;
                    else
                    {
                        owner.StartCoroutine(rise);
                        yield return owner.Wait(Mathf.Max(0.12f, Settings.barrierStepGap));
                    }
                }
                completed();
            }

            IEnumerator RiseBarrierStep(Vector2 center, Vector2 size, SpriteRenderer mark, BoxCollider2D[] surfaces, int index, int direction)
            {
                int number = index + 1;
                mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                var visual = KarmaVisuals.Box(owner.effectsRoot, "GD04 상승 지면 " + number, center, Vector2.one, Color.white, 9);
                var sprite = KarmaArtwork.Sprite("GuardBasalt", "Step" + Mathf.Min(number, 6));
                bool basalt = sprite != null;
                sprite = sprite ?? KarmaArtwork.Sprite("GuardBossVFX", "Pillar");
                visual.sprite = sprite;
                visual.flipX = direction < 0;
                // The sprite pivot is the hot ledge. Align it with the collider's top.
                Vector3 scale = new Vector3(size.x / sprite.bounds.size.x, size.y / (basalt ? -sprite.bounds.min.y : sprite.bounds.size.y), 1);
                visual.transform.localScale = scale;
                float elapsed = 0;
                while (elapsed < 0.18f && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    elapsed += Time.deltaTime;
                    float t = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / 0.18f), 2);
                    visual.transform.localScale = new Vector3(scale.x, scale.y * t, 1);
                    visual.transform.position = new Vector2(center.x, (basalt ? size.y : -sprite.bounds.min.y * scale.y) * t);
                    yield return null;
                }
                // The boss may already have turned into GD02 while this terrain is still rising.
                var attack = KarmaBossAttack.Spawn(owner.game, owner, owner.effectsRoot, center, Vector2.zero, size,
                    0.25f, owner.Damage, new Color(1, 0.7f, 0.12f), null, 0, "Pillar", visualSheet: "GuardBossVFX", renderArtwork: false);
                yield return owner.Wait(0.25f);
                if (attack != null) { attack.gameObject.SetActive(false); Destroy(attack.gameObject); }
                owner.game.Player.ResolveBarrier(center.x, size.x, size.y);
                var wall = KarmaVisuals.Box(owner.effectsRoot, "완성 방벽 · 계단 " + number,
                    center, size, Color.clear, 7);
                wall.gameObject.AddComponent<KarmaSurface>();
                var surface = wall.gameObject.AddComponent<BoxCollider2D>();
                surface.size = Vector2.one;
                surfaces[index] = surface;
                if (index == 0 && Settings.barrierHeatDamageMultiplier > 0) owner.StartCoroutine(HeatOnSteps(surfaces));
            }

            IEnumerator HeatOnSteps(BoxCollider2D[] surfaces)
            {
                var player = owner.game.Player;
                var sparks = KarmaArtwork.Sprite("GuardBasaltHeat", "Sparks");
                float grace = Mathf.Max(0.05f, Settings.barrierHeatGrace);
                float interval = Mathf.Max(0.05f, Settings.barrierHeatInterval);
                float tickAt = float.PositiveInfinity;
                // All steps share one timer, including when the boss turns into GD02.
                while (owner.Active && surfaces[0] != null && surfaces[0].isActiveAndEnabled)
                {
                    if (!owner.game.Paused)
                    {
                        Vector2 feet = player.Position - Vector2.up * KarmaSpace.PlayerHalfHeight;
                        if (!OnStepTop(surfaces, feet) || player.Airborne) tickAt = float.PositiveInfinity;
                        else if (float.IsPositiveInfinity(tickAt)) tickAt = owner.FightSeconds + grace;
                        else if (owner.FightSeconds >= tickAt)
                        {
                            tickAt = owner.FightSeconds + interval;
                            if (player.TakeTerrainDamage(owner.Damage * Settings.barrierHeatDamageMultiplier) && owner.Active && sparks != null)
                                KarmaCombatVFX.Burst(owner.effectsRoot, feet, sparks, "GD04 hot step sparks",
                                    KarmaSpace.Units(new Vector2(0.85f, 0.65f)), 0.19f, 0, new Color(1, 0.4f, 0.14f), true);
                        }
                    }
                    yield return null;
                }
            }

            static bool OnStepTop(BoxCollider2D[] surfaces, Vector2 feet)
            {
                foreach (var surface in surfaces)
                {
                    if (surface == null || !surface.isActiveAndEnabled) continue;
                    var bounds = surface.bounds;
                    if (feet.y >= bounds.max.y - 0.03f && feet.y <= bounds.max.y + KarmaSpace.Units(0.1f)
                        && Mathf.Abs(feet.x - bounds.center.x) < bounds.extents.x + KarmaSpace.Units(0.29f)) return true;
                }
                return false;
            }
        }
    }
}
