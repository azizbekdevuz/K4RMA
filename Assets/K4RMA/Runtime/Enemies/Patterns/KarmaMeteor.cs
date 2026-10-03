using UnityEngine;

namespace KarmaPrototype
{
    // A locked landing point: warning, visible descent, then one impact.
    public sealed class KarmaMeteor : MonoBehaviour
    {
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer marker, rock;
        float delay, targetX, damage, nextTrail;
        bool falling, warningStarted, trackPlayer;
        float leadIn;
        public static void Spawn(KarmaGame game, KarmaEnemy owner, float x, float delay, float damage, float leadIn = 0, bool trackPlayer = false)
        {
            var root = new GameObject("메테오 예고와 낙하");
            root.transform.SetParent(game.World, false);
            var m = root.AddComponent<KarmaMeteor>();
            m.game = game; m.owner = owner; m.targetX = Mathf.Clamp(x, 0.5f, 39.5f);
            m.trackPlayer = trackPlayer;
            m.delay = Mathf.Max(0.4f, delay); m.damage = damage; m.leadIn = Mathf.Max(0, leadIn);
            m.marker = KarmaVisuals.Box(root.transform, "낙하 범위", new Vector2(m.targetX, 0.12f),
                new Vector2(game.Config.BossTuning.meteorRadius * 2, 0.18f), new Color(1, 0.35f, 0.1f, 0.8f), 9);
            m.rock = KarmaVisuals.Box(root.transform, "운석", new Vector2(m.targetX, 10),
                Vector2.one * 1.2f, game.Config.flameColor, 12);
            m.rock.enabled = false; m.marker.enabled = false;
            if (m.leadIn <= 0) m.BeginWarning();
        }
        void BeginWarning()
        {
            if (warningStarted) return;
            warningStarted = true;
            // Sample once per meteor, after its lead-in. Do not home after the marker appears.
            if (trackPlayer && game.Player != null)
                targetX = Mathf.Clamp(game.Player.Position.x, 0.5f, 39.5f);
            marker.transform.localPosition = new Vector2(targetX, 0.12f);
            rock.transform.localPosition = new Vector2(targetX, 10);
            marker.enabled = true;
        }
        void Update()
        {
            if (game == null || !game.IsCombat || owner == null || !owner.Alive) { Destroy(gameObject); return; }
            if (game.Paused) return;
            if (owner.Stunned || owner.Invulnerable) { Destroy(gameObject); return; }
            if (leadIn > 0)
            {
                leadIn -= Time.deltaTime;
                if (leadIn > 0) return;
                BeginWarning();
                // Start a full warning on this frame, even after a frame hitch.
                return;
            }
            var t = game.Config.BossTuning;
            if (!falling)
            {
                delay -= Time.deltaTime;
                marker.color = new Color(1, 0.3f, 0.05f, 0.45f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 9)));
                if (delay > 0) return;
                falling = true; rock.enabled = true;
            }
            Vector2 from = rock.transform.position;
            Vector2 to = from + Vector2.down * Mathf.Max(1, t.meteorSpeed) * Time.deltaTime;
            to.y = Mathf.Max(0.55f, to.y);
            rock.transform.position = to;
            rock.transform.Rotate(0, 0, 180 * Time.deltaTime);
            if (Time.time >= nextTrail)
            {
                KarmaRemnantVFX.Trail(game, to, Essence.Flame, Vector2.down * Mathf.Max(1, t.meteorSpeed));
                nextTrail = Time.time + 0.05f;
            }
            bool directHit = KarmaProjectile.Intersects(from, to, game.Player.Position, 1);
            if (directHit || to.y <= 0.55f)
            {
                if (directHit)
                {
                    Vector2 segment = to - from;
                    float fraction = segment.sqrMagnitude < 0.000001f ? 0 :
                        Mathf.Clamp01(Vector2.Dot(game.Player.Position - from, segment) / segment.sqrMagnitude);
                    to = from + segment * fraction;
                }
                KarmaRemnantVFX.Impact(game, to, Essence.Flame);
                KarmaVisuals.Flash(game.World, to, Vector2.one * t.meteorRadius * 2, new Color(1, 0.45f, 0.08f, 0.65f), 0.35f);
                if (Vector2.Distance(game.Player.Position, to) <= t.meteorRadius + 0.5f)
                    game.Player.TakeImpact(damage, to, t.meteorKnockback);
                Destroy(gameObject);
            }
        }
        void OnDestroy() { if (owner != null) owner.MeteorFinished(); }
    }
}
