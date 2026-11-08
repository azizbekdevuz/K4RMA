using UnityEngine;

namespace KarmaPrototype
{
    // Cosmetic, confirmed-damage-only feedback. World ownership lets the short
    // flash finish if the attack enters recovery in the same frame.
    public static class KarmaSwordHitFeedback
    {
        public static void Show(Transform root, Vector2 at, bool boss)
        {
            var sprite = KarmaWaveArtwork.Sprite("Sword_HitBurst_v2");
            if (sprite == null) return;
            var flash = KarmaCombatVFX.Burst(root, at, sprite,
                boss ? "Sword boss confirmed hit" : "Sword player confirmed hit",
                Vector2.one * (boss ? 2.3f : 2.7f), boss ? 0.14f : 0.17f,
                0, KarmaSwordAttackPolish.Blue, true);
            foreach (var renderer in flash.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                { renderer.sortingOrder += 18; KarmaSwordSkillSurface.Apply(renderer, 4); }
        }
    }

    // Sword-only overlay follows the rendered pose after secondary motion.
    // It never changes the source sprite, animator state, root or damage bounds.
    [DefaultExecutionOrder(450)]
    public sealed class KarmaSwordDamageFlash : MonoBehaviour
    {
        KarmaEnemy owner;
        SpriteRenderer source, overlay;
        float queuedHealth, age = 1, nextPulse;
        bool pending;
        public void Queue(KarmaEnemy enemy)
        {
            owner = enemy;
            queuedHealth = pending ? Mathf.Max(queuedHealth, enemy.Health) : enemy.Health;
            pending = true;
        }
        void LateUpdate()
        {
            if (owner == null || !owner.PresentationActive || owner.Stunned
                || owner.CurrentPattern == (int)EnemyAttack.SW05)
            { if (overlay != null) overlay.enabled = false; pending = false; age = 1; return; }
            if (pending)
            {
                if (owner.Health < queuedHealth && Time.time >= nextPulse)
                { age = 0; nextPulse = Time.time + 0.12f; }
                pending = false;
            }
            age += Time.deltaTime;
            if (age >= 0.18f) { if (overlay != null) overlay.enabled = false; return; }
            if (source == null)
            {
                var picture = transform.Find("Guardian sprite");
                if (picture == null) return;
                source = picture.GetComponent<SpriteRenderer>();
            }
            if (overlay == null)
            {
                var node = new GameObject("Sword confirmed damage silhouette");
                node.transform.SetParent(source.transform.parent, false);
                overlay = node.AddComponent<SpriteRenderer>();
                overlay.sharedMaterial = Resources.Load<Material>("KarmaSwordRetreatEcho");
            }
            overlay.enabled = source.enabled;
            overlay.sprite = source.sprite; overlay.flipX = source.flipX; overlay.flipY = source.flipY;
            overlay.sortingLayerID = source.sortingLayerID; overlay.sortingOrder = source.sortingOrder + 2;
            overlay.transform.localPosition = source.transform.localPosition;
            overlay.transform.localRotation = source.transform.localRotation;
            overlay.transform.localScale = source.transform.localScale;
            float alpha = age < 0.045f ? 0.88f : 0.88f * (1 - Mathf.Clamp01((age - 0.045f) / 0.135f));
            overlay.color = new Color(1, 0.75f, 0.65f, alpha);
        }
        void OnDisable() { if (overlay != null) overlay.enabled = false; }
        void OnDestroy() { if (overlay != null) Destroy(overlay.gameObject); }
    }

    // Persistent status feedback follows the real stun state and combat clock.
    // Keep its geometry outside Artwork so it never enlarges damage bounds.
    [DefaultExecutionOrder(475)]
    public sealed class KarmaSwordStunFeedback : MonoBehaviour
    {
        KarmaEnemy owner;
        KarmaGame game;
        SpriteRenderer picture;
        Transform orbit;
        readonly LineRenderer[] stars = new LineRenderer[3];
        public void Initialize(KarmaEnemy enemy, KarmaGame director, Transform artwork)
        {
            owner = enemy; game = director;
            picture = artwork.Find("Guardian sprite").GetComponent<SpriteRenderer>();
            orbit = new GameObject("Sword stunned orbit").transform;
            orbit.SetParent(transform, false);
            var ring = Line("Stun orbit", 48, 0.045f);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 1.45f, Mathf.Sin(a) * 0.38f, 0));
            }
            ring.startColor = ring.endColor = new Color(0.35f, 0.8f, 1, 0.75f);
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = Line("Stun star " + i, 8, 0.065f);
                for (int j = 0; j < 8; j++)
                {
                    float a = j * Mathf.PI / 4;
                    float radius = j % 2 == 0 ? 0.28f : 0.09f;
                    stars[i].SetPosition(j, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
                }
                stars[i].startColor = stars[i].endColor = new Color(0.82f, 0.96f, 1);
            }
            orbit.gameObject.SetActive(false);
        }
        LineRenderer Line(string name, int points, float width)
        {
            var node = new GameObject(name); node.transform.SetParent(orbit, false);
            var line = node.AddComponent<LineRenderer>();
            line.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            line.useWorldSpace = false; line.loop = true; line.positionCount = points;
            line.widthMultiplier = width; line.numCornerVertices = 2; line.sortingOrder = 45;
            return line;
        }
        void LateUpdate()
        {
            if (orbit == null) return;
            bool visible = owner != null && owner.PresentationActive && owner.Stunned;
            orbit.gameObject.SetActive(visible);
            if (!visible || picture == null || game.Paused) return;
            // WaveMotion resets the authored transform first. Lean at the feet;
            // the boss root and its original incoming-damage bounds stay fixed.
            float angle = 9 + Mathf.Sin(owner.FightSeconds * 5) * 1.5f;
            var rotation = Quaternion.Euler(0, 0, angle);
            var foot = new Vector3(0, picture.sprite.bounds.min.y * picture.transform.localScale.y, 0);
            picture.transform.localPosition += foot - rotation * foot;
            picture.transform.localRotation *= rotation;
            orbit.position = new Vector3(picture.bounds.center.x, picture.bounds.max.y + 0.8f, 0);
            for (int i = 0; i < stars.Length; i++)
            {
                float a = owner.FightSeconds * 4 + i * Mathf.PI * 2 / stars.Length;
                stars[i].transform.localPosition = new Vector3(Mathf.Cos(a) * 1.45f, Mathf.Sin(a) * 0.38f, 0);
                stars[i].transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            }
        }
        void OnDisable() { if (orbit != null) orbit.gameObject.SetActive(false); }
    }
}
