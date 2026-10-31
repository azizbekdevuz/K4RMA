using System.Collections;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        IEnumerator RisingBarrier()
        {
            frontalGuard = true; state = ActionState.Telegraph;
            direction = game.Player.Position.x < Position.x ? -1 : 1;
            int count = Mathf.Clamp(Tuning.barrierSteps, 3, 8);
            float width = KarmaSpace.Units(1.65f);
            float room = Mathf.Max(0.1f, (Facing > 0 ? KarmaStage.ArenaRight - Position.x
                : Position.x - KarmaStage.ArenaLeft) - KarmaSpace.Units(0.15f));
            float gap = Mathf.Min(KarmaSpace.Units(1.2f), room * 0.2f);
            // Fit toward the player instead of flipping the attack behind the boss.
            // Keep the normal footprint where possible, and at least three steps if they fit.
            int fitting = Mathf.FloorToInt((room - gap - width) / (width * 0.92f)) + 1;
            count = Mathf.Min(count, Mathf.Max(3, fitting));
            while (count > 1 && (room - gap) / (1 + (count - 1) * 0.92f) < KarmaSpace.Units(0.75f)) count--;
            width = Mathf.Min(width, (room - gap) / (1 + (count - 1) * 0.92f));
            float stride = width * 0.92f, offset = gap + width / 2;
            art.localScale = new Vector3(Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            int terrainDirection = Facing;
            bool sawPlayerInFront = (game.Player.Position.x - Position.x) * terrainDirection >= 0;
            var sizes = new Vector2[count]; var centers = new Vector2[count];
            var marks = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                float height = KarmaSpace.Units(1.1f + i * 0.6f);
                sizes[i] = new Vector2(width, height);
                centers[i] = new Vector2(Position.x + direction * (offset + i * stride), height / 2);
                marks[i] = Mark("GD04 계단 상승 예고 " + (i + 1), centers[i], sizes[i]);
            }
            Intent = "GD04 연속 지면 상승 · 낮은 지면부터 한 방향으로";
            yield return Wait(Warning(0.8f));
            state = ActionState.Attack; animator.Attack(Vector2.down);
            bool finished = false;
            var wave = StartCoroutine(RiseBarrierWave(centers, sizes, marks, () => finished = true));
            float held = 0;
            while (Active && (held < 0.6f || !finished))
            {
                if (game.Paused) { yield return null; continue; }
                float signedDistance = (game.Player.Position.x - Position.x) * terrainDirection;
                if (signedDistance >= 0) sawPlayerInFront = true;
                // One rear counter per GD04, while the terrain stays on its original side.
                if (autoShield && sawPlayerInFront && signedDistance < -KarmaSpace.PlayerHalfWidth
                    && -signedDistance <= KarmaSpace.Units(KarmaPatternCatalog.GuardHeavyReach) + KarmaSpace.PlayerHalfWidth)
                {
                    selected = previous = EnemyAttack.GD02;
                    direction = -terrainDirection;
                    art.localScale = new Vector3(Facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
                    patternStarted = FightSeconds;
                    Intent = "GD04 후방 회피 대응 · GD02 강공격 / 붉은 빛에 K";
                    yield return HeavyStrike(wave);
                    yield break;
                }
                if (finished)
                {
                    Intent = "GD04 계단 지형 유지 · 완성된 지면 위로 이동 가능";
                    held += Time.deltaTime;
                }
                yield return null;
            }
            yield return Recovery(Mathf.Max(0.3f, Tuning.barrierRecovery));
        }

        IEnumerator RiseBarrierWave(Vector2[] centers, Vector2[] sizes, SpriteRenderer[] marks, System.Action completed)
        {
            int count = centers.Length;
            for (int i = 0; i < count; i++)
            {
                if (i == count - 1) yield return RiseBarrierStep(centers[i], sizes[i], marks[i], i + 1);
                else
                {
                    StartCoroutine(RiseBarrierStep(centers[i], sizes[i], marks[i], i + 1));
                    yield return Wait(Mathf.Max(0.12f, Tuning.barrierStepGap));
                }
            }
            completed();
        }

        IEnumerator RiseBarrierStep(Vector2 center, Vector2 size, SpriteRenderer mark, int number)
        {
            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
            var visual = KarmaVisuals.Box(effectsRoot, "GD04 상승 지면 " + number, center, Vector2.one, Color.white, 9);
            KarmaCombatVFX.ApplySprite(visual, "GuardPillar", "Pillar", size);
            Vector3 scale = visual.transform.localScale;
            float elapsed = 0;
            while (elapsed < 0.18f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.18f);
                visual.transform.localScale = new Vector3(scale.x, scale.y * t, 1);
                visual.transform.position = new Vector2(center.x, -visual.sprite.bounds.min.y * scale.y * t);
                yield return null;
            }
            // The boss may already have turned into GD02 while this terrain is still rising.
            var attack = KarmaBossAttack.Spawn(game, this, effectsRoot, center, Vector2.zero, size,
                0.25f, Damage, new Color(1, 0.7f, 0.12f), null, 0, "Pillar", "GuardPillar", renderArtwork: false);
            yield return Wait(0.25f);
            if (attack != null) { attack.gameObject.SetActive(false); Destroy(attack.gameObject); }
            game.Player.ResolveBarrier(center.x, size.x, size.y);
            var wall = KarmaVisuals.Box(effectsRoot, "완성 방벽 · 계단 " + number,
                center, size, new Color(0.3f, 0.85f, 1, 0.12f), 7);
            wall.gameObject.AddComponent<KarmaSurface>();
            wall.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
        }
    }
}
