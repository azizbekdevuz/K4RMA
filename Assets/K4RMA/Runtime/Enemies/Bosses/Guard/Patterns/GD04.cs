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
                int attackDirection = owner.Facing;
                owner.art.localScale = new Vector3(attackDirection * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                bool sawPlayerInFront = (owner.game.Player.Position.x - owner.Position.x) * attackDirection >= 0;
                float room = attackDirection > 0 ? KarmaStage.ArenaRight - owner.Position.x : owner.Position.x - KarmaStage.ArenaLeft;
                float reach = Mathf.Min(Mathf.Max(0.1f, Settings.slamRange), Mathf.Max(0.1f, room));
                float near = Mathf.Min(KarmaSpace.Units(0.6f), reach * 0.25f);
                const int count = 3;
                var sizes = new Vector2[count];
                var centers = new Vector2[count];
                var marks = new SpriteRenderer[count];
                float cursor = near;
                float distanceScale = Mathf.Clamp01((reach - near) / Mathf.Max(0.1f, Settings.slamRange - near));
                for (int i = 0; i < count; i++)
                {
                    float weight = i == 0 ? 1 : i == 1 ? 1.8f : 3;
                    float width = (reach - near) * weight / 5.8f;
                    float growth = (i == 0 ? 0 : i == 1 ? 0.25f : 1) * distanceScale;
                    float height = Mathf.Lerp(Settings.slamHeight, Mathf.Max(Settings.slamHeight, Settings.slamFinalHeight), growth);
                    sizes[i] = new Vector2(width, Mathf.Max(0.1f, height));
                    centers[i] = new Vector2(owner.Position.x + attackDirection * (cursor + width / 2), height / 2);
                    marks[i] = owner.Mark(i == count - 1 ? "GD04 큰 지면 파열 · 패링 불가" : "GD04 지면 상승 · 패링 불가", centers[i], sizes[i]);
                    cursor += width;
                }
                float warning = owner.Warning(0.8f);
                const float descent = 0.1f;
                owner.animator.SetGroundSlamFrame(0);
                yield return owner.Wait(Mathf.Max(0, warning - descent));
                owner.animator.SetGroundSlamFrame(1);
                yield return owner.Wait(descent);
                if (!owner.Active) yield break;
                foreach (var mark in marks) { mark.gameObject.SetActive(false); Destroy(mark.gameObject); }
                owner.state = ActionState.Attack; owner.animator.Attack(Vector2.down);
                owner.animator.SetGroundSlamFrame(2);
                if (owner.Active) KarmaAudio.Play(KarmaSfxId.GdHeavySlam);
                Vector2 impact = owner.animator.GroundSlamContact;
                impact.y = 0.08f;
                KarmaCombatVFX.Burst(owner.effectsRoot, impact, KarmaArtwork.Sprite("GuardBasaltDebris", "Burst"),
                    "GD04 ground impact", KarmaSpace.Units(new Vector2(3.2f, 2.4f)), 0.32f, 0, new Color(1, 0.54f, 0.26f), true);
                owner.game.PresentationShake(0.22f, 1.2f);
                var eruptionRoot = new GameObject("GD04 전방 충격파");
                eruptionRoot.transform.SetParent(owner.effectsRoot, false);
                var slabs = new SpriteRenderer[count];
                var homes = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    var section = new GameObject("GD04 고정 지면 " + i).transform;
                    section.SetParent(eruptionRoot.transform, false);
                    section.position = new Vector2(centers[i].x, 0);
                    section.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 4;
                    // Clip only below the floor and at arena walls, preserving the original wide artwork.
                    var aperture = KarmaVisuals.Box(section, "지면 위 표시 영역",
                        new Vector2(KarmaStage.ArenaCenter - centers[i].x, 8), new Vector2(KarmaStage.ArenaWidth, 16), Color.white);
                    var mask = aperture.gameObject.AddComponent<SpriteMask>();
                    mask.sprite = aperture.sprite;
                    mask.isCustomRangeActive = true; mask.backSortingOrder = -1; mask.frontSortingOrder = 1;
                    aperture.enabled = false;
                    var slab = KarmaVisuals.Box(section, "GD04 지면 상승 " + i, Vector2.zero, Vector2.one, Color.white);
                    if (!KarmaCombatVFX.ApplySprite(slab, "GuardSlamFan", "Impact", Vector2.one, true))
                        throw new System.InvalidOperationException("GD04 지면 상승 이미지가 없습니다.");
                    slab.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    // Preserve the authored pivot and aspect; center the rupture inside its fixed damage footprint.
                    float pixelsPerUnit = slab.sprite.pixelsPerUnit;
                    float uniformScale = (sizes[i].y + KarmaSpace.Units(0.2f)) / (slab.sprite.rect.height * 0.8f / pixelsPerUnit);
                    var scale = new Vector3(attackDirection * uniformScale, uniformScale, 1);
                    slab.transform.localScale = scale;
                    float floorLocalY = (slab.sprite.rect.height * 0.15f - slab.sprite.pivot.y) / pixelsPerUnit;
                    float centerLocalX = (slab.sprite.rect.width * 0.6f - slab.sprite.pivot.x) / pixelsPerUnit;
                    homes[i] = new Vector3(-centerLocalX * scale.x, 0.05f - floorLocalY * scale.y, 0);
                    slab.transform.localPosition = homes[i] - Vector3.up * slab.sprite.bounds.size.y * scale.y;
                    slab.enabled = false; slabs[i] = slab;
                }
                float started = owner.FightSeconds, previousElapsed = -0.001f;
                bool resolved = false;
                // Speed up each vertical emergence to two 24 FPS visual frames.
                // Preserve the near-to-far spread, damage windows and complete sequence/chain timing.
                const float travelSeconds = 0.55f, riseSeconds = 2f / 24f, hitStart = 0.12f, hitEnd = 0.20f;
                const float lowerAt = travelSeconds + hitStart + 0.16f, lowerSeconds = 0.24f;
                while (owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    float elapsed = owner.FightSeconds - started;
                    if (elapsed >= 0.14f) owner.animator.SetGroundSlamFrame(3);
                    float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - lowerAt) / lowerSeconds));
                    for (int i = 0; i < count; i++)
                    {
                        float beginsAt = travelSeconds * i / (count - 1);
                        float age = elapsed - beginsAt;
                        var slab = slabs[i];
                        slab.enabled = age >= 0 && fall < 1;
                        if (slab.enabled)
                        {
                            float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / riseSeconds));
                            float height = slab.sprite.bounds.size.y * slab.transform.localScale.y;
                            slab.transform.localPosition = homes[i] - Vector3.up * height * (1 - rise + fall);
                            slab.color = new Color(1, 1, 1, 1 - fall);
                        }
                        // Damage follows each upward eruption; the tiles never translate horizontally.
                        if (!resolved && age >= hitStart && previousElapsed - beginsAt <= hitEnd)
                            resolved = TryGroundImpact(centers[i], sizes[i]);
                    }
                    if (!owner.Active) yield break;
                    float signedDistance = (owner.game.Player.Position.x - owner.Position.x) * attackDirection;
                    if (signedDistance >= 0) sawPlayerInFront = true;
                    if (elapsed >= lowerAt + lowerSeconds) break;
                    previousElapsed = elapsed;
                    yield return null;
                }
                Interrupt();
                eruptionRoot.SetActive(false);
                // Finish the complete ground sequence before checking the existing shielded rear counter.
                float rearDistance = (owner.game.Player.Position.x - owner.Position.x) * attackDirection;
                if (owner.Active && owner.AutoShieldActive && sawPlayerInFront && rearDistance < -KarmaSpace.PlayerHalfWidth
                    && -rearDistance <= Settings.heavyStrikeReach + KarmaSpace.PlayerHalfWidth)
                {
                    owner.selected = owner.previous = EnemyAttack.GD02;
                    owner.direction = -attackDirection;
                    owner.art.localScale = new Vector3(owner.Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    owner.patternStarted = owner.FightSeconds;
                    yield return heavy.Execute();
                }
            }

            bool TryGroundImpact(Vector2 center, Vector2 size)
            {
                var player = owner.game.Player;
                var collider = player.GetComponent<Collider2D>();
                var bounds = collider != null && collider.enabled ? collider.bounds : new Bounds(player.Position, KarmaSpace.PlayerSize);
                if (Mathf.Abs(bounds.center.x - center.x) > bounds.extents.x + size.x / 2
                    || Mathf.Abs(bounds.center.y - center.y) > bounds.extents.y + size.y / 2) return false;
                if (player.Shielded)
                    KarmaCombatVFX.SpawnSprite(owner.effectsRoot, player.Position, "PlayerVFX", "Barrier", KarmaSpace.Units(new Vector2(1.6f, 2.1f)), 0.2f);
                else if (player.TakeTerrainDamage(owner.Damage * Settings.slamDamageMultiplier) && owner.Active)
                {
                    // Ground damage bypasses counter consumption; Stun cancels player attacks before the physical launch.
                    player.Stun(Mathf.Max(0.1f, Settings.slamStunSeconds));
                    player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up * Mathf.Max(0.1f, Settings.slamKnockUpSpeed);
                    KarmaCombatVFX.SpawnSprite(owner.effectsRoot, player.Position, "HitVFX", "Burst", KarmaSpace.Units(Vector2.one), 0.18f);
                }
                return true;
            }
        }
    }
}
