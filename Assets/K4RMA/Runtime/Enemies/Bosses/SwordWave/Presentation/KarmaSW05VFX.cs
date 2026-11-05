using UnityEngine;

namespace KarmaPrototype
{
    // SW05 presentation only. Damage, timing, escape reservations and cancellation
    // remain owned by SW05Pattern, its rain blades and the existing planner.
    public sealed class KarmaSW05VFX : MonoBehaviour
    {
        static readonly Color Radiance = new Color(0.85f, 0.13f, 0.075f);
        static readonly Color Ember = new Color(1, 0.36f, 0.2f);
        static Material celestial, crimsonBlade, woodMaterial, contactMaterial;
        static Sprite judgementSword;
        static readonly Sprite[] impactFrames = new Sprite[4];
        static readonly int Progress = Shader.PropertyToID("_Progress");
        static readonly int Mode = Shader.PropertyToID("_Mode");
        static readonly int Formation = Shader.PropertyToID("_Formation");
        static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
        KarmaGame game;
        Transform follow;
        SpriteRenderer sigil;
        MaterialPropertyBlock properties;
        Vector3 baseScale;
        Color plantedTint;
        float age, life, mode;
        bool falling, corona, warning, planted, seal, forming, impact;

        static Material Celestial
        {
            get
            {
                if (celestial == null) celestial = Resources.Load<Material>("SW05/SW05Celestial");
                return celestial;
            }
        }
        public static void ApplySword(SpriteRenderer renderer, Vector2 size, Vector2 velocity)
        {
            if (judgementSword == null) judgementSword = KarmaArtwork.Sprite("SwordBossVFX", "SW05_JudgementSword_v2");
            if (judgementSword == null) throw new System.InvalidOperationException("Missing SW05 judgement sword artwork");
            renderer.sprite = judgementSword;
            if (crimsonBlade == null) crimsonBlade = Resources.Load<Material>("SW05/SW05CrimsonBlade");
            if (crimsonBlade == null) throw new System.InvalidOperationException("Missing SW05 crimson blade material");
            renderer.sharedMaterial = crimsonBlade;
            renderer.flipX = renderer.flipY = false;
            // The opaque weapon (not its transparent aura) occupies the
            // same long/transverse rectangle that the existing planner reserves.
            // v2 opaque core: 540 x 1476 px at PPU100, with its authored
            // center pivot halfway between pommel and downward tip.
            renderer.transform.localScale = new Vector3(size.y * 100 / 540, size.x * 100 / 1476, 1);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90);
            renderer.color = Color.white;
        }
        public static void ApplyDepth(SpriteRenderer sword, float depth, float opacity = 1)
        {
            Color tint = Color.Lerp(new Color(0.7f, 0.55f, 0.51f, 0.78f), Color.white, depth);
            tint.a *= opacity;
            sword.color = tint;
            sword.sortingOrder = 23 + Mathf.RoundToInt(depth * 6);
        }
        static KarmaSW05VFX Create(Transform root, KarmaGame game, Vector2 at, Vector2 size, float life, float mode, Color color)
        {
            var sr = KarmaVisuals.Box(root, "SW05 celestial radiance / no damage", at, size, color, 19);
            if (Celestial != null) sr.sharedMaterial = Celestial;
            var fx = sr.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game; fx.sigil = sr; fx.life = life; fx.mode = mode;
            fx.baseScale = sr.transform.localScale;
            fx.properties = new MaterialPropertyBlock();
            fx.Paint(0);
            return fx;
        }
        void Paint(float progress)
        {
            properties.SetFloat(Progress, progress);
            properties.SetFloat(Mode, mode);
            sigil.SetPropertyBlock(properties);
        }
        public static SpriteRenderer Warning(Transform root, KarmaGame game, Vector2 at, Vector2 size)
        {
            var fx = Create(root, game, at, size, float.PositiveInfinity, 0, Ember);
            fx.warning = true;
            fx.sigil.name = "SW05 locked warning / no damage";
            return fx.sigil;
        }
        public static void Corona(Transform root, KarmaGame game, Transform boss)
        {
            var fx = Create(root, game, boss.position, new Vector2(10, 10), float.PositiveInfinity, 0, new Color(0.95f, 0.22f, 0.12f, 0.65f));
            fx.corona = true; fx.follow = boss;
        }
        static void Trail(SpriteRenderer blade, float width, float duration)
        {
            var go = new GameObject("SW05 tapered comet wake / no damage");
            go.transform.SetParent(blade.transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = KarmaCombatVFX.GlowMaterial;
            trail.time = duration; trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(0.18f, width * 0.65f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1, 0.91f, 0.72f), 0), new GradientColorKey(Radiance, 0.25f), new GradientColorKey(new Color(0.24f, 0.025f, 0.018f), 1) },
                new[] { new GradientAlphaKey(0.64f, 0), new GradientAlphaKey(0.20f, 0.3f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient; trail.sortingOrder = 23;
            trail.numCapVertices = 4; trail.numCornerVertices = 4;
        }
        public static void Ascent(SpriteRenderer blade, Vector2 size)
        {
            Trail(blade, size.y * 0.3f, 0.28f);
            // Reveal is material-only: the pattern still owns ascent, scale,
            // rotation, stagger, opacity and the moment this becomes damage.
            var fx = blade.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = Object.FindAnyObjectByType<KarmaGame>();
            fx.sigil = blade; fx.forming = true; fx.life = 0.42f;
            fx.properties = new MaterialPropertyBlock();
            fx.properties.SetFloat(Formation, 0);
            blade.SetPropertyBlock(fx.properties);
        }
        public static void CastPillar(Transform root, KarmaGame game, Vector2 caster, float life)
        {
            float height = caster.y + 10;
            var beam = Create(root, game, new Vector2(caster.x, height / 2), new Vector2(5, height),
                life, 2, new Color(0.9f, 0.18f, 0.09f, 0.18f));
            beam.sigil.name = "SW05 summoning pillar / no damage";
            var ring = Create(root, game, caster + Vector2.up * 6, new Vector2(70, 10.56f), life, 0,
                new Color(0.9f, 0.16f, 0.09f, 0.58f));
            ring.seal = true;
            ring.sigil.name = "SW05 circular summoning seal / no damage";
        }
        public static void Falling(Transform root, KarmaGame game, Transform flight, SpriteRenderer sword,
            Vector2 size, float flightLife, Vector2 axis)
        {
            Trail(sword, size.y * 0.42f, 0.26f);
            var fx = Create(root, game, flight.position, new Vector2(size.y * 1.7f, size.x * 3), flightLife, 1, Radiance);
            fx.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg + 90);
            fx.falling = true; fx.follow = flight;
        }
        public static void Strike(Transform root, KarmaGame game, Vector2 center, Vector2 size, Vector2 axis, float depth)
        {
            Vector2 tip = center + axis * (size.x / 2);
            WoodContact(root, game, new Vector2(tip.x, 0.05f), size);
            GroundImpact(root, game, new Vector2(tip.x, 0.05f), size, depth);
            // A low, brief contact pulse accompanies the existing wood shadow.
            // It has no collider, debris, screen shake or gameplay callback.
            var contact = Create(root, game, new Vector2(tip.x, 0.08f),
                new Vector2(Mathf.Max(0.7f, size.y * 1.8f), 0.42f), 0.20f, 3, new Color(1, 0.43f, 0.17f, 0.42f));
            contact.sigil.name = "SW05 contact pressure pulse / no damage";
            var sword = KarmaVisuals.Box(root, "SW05 wood-embedded sword / no damage",
                center + axis * (size.x * 0.14f), Vector2.one, Color.white, 24);
            // Keep exactly the airborne picture, palette and depth tint. A
            // separate floor-clipping material buries its lower 14% in the wood.
            ApplySword(sword, size, axis);
            if (woodMaterial == null) woodMaterial = Resources.Load<Material>("SW05/SW05EmbeddedWood");
            sword.sharedMaterial = woodMaterial;
            ApplyDepth(sword, depth);
            var fx = sword.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game; fx.sigil = sword; fx.planted = true; fx.life = 0.65f; fx.plantedTint = sword.color;
            fx.properties = new MaterialPropertyBlock();
        }
        static void GroundImpact(Transform root, KarmaGame game, Vector2 at, Vector2 size, float depth)
        {
            for (int i = 0; i < impactFrames.Length; i++)
                if (impactFrames[i] == null) impactFrames[i] = KarmaArtwork.Sprite("SwordBossVFX", "SW05_Impact_" + i);
            if (impactFrames[0] == null) throw new System.InvalidOperationException("Missing SW05 ground impact artwork");
            var sr = KarmaVisuals.Box(root, "SW05 animated ground impact / no damage", at, Vector2.one, Color.white, 30);
            sr.sprite = impactFrames[0];
            if (crimsonBlade == null) crimsonBlade = Resources.Load<Material>("SW05/SW05CrimsonBlade");
            sr.sharedMaterial = crimsonBlade;
            // One constant sheet scale, per-frame contact pivots. Never fit
            // individual frame bounds: the expanding ring must stay grounded.
            float scale = Mathf.Max(0.65f, size.y * 2.8f) / 5.06f;
            sr.transform.localScale = Vector3.one * scale;
            ApplyDepth(sr, depth); sr.sortingOrder += 3;
            var fx = sr.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game; fx.sigil = sr; fx.impact = true; fx.life = 0.36f; fx.plantedTint = sr.color;
        }
        static void WoodContact(Transform root, KarmaGame game, Vector2 at, Vector2 size)
        {
            if (contactMaterial == null) contactMaterial = Resources.Load<Material>("SW05/SW05WoodContact");
            var shadow = KarmaVisuals.Box(root, "SW05 wood penetration / no damage", at,
                new Vector2(Mathf.Max(0.3f, size.y * 0.38f), 0.1f), new Color(0.035f, 0.016f, 0.01f, 0.88f), 30);
            shadow.sharedMaterial = contactMaterial;
            var fx = shadow.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game; fx.sigil = shadow; fx.planted = true; fx.life = 0.65f; fx.plantedTint = shadow.color;
        }
        void LateUpdate()
        {
            if (game == null || game.Phase != RunPhase.Combat) { Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (forming)
            {
                properties.SetFloat(Formation, Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / life)));
                sigil.SetPropertyBlock(properties);
                if (age >= life) Destroy(this);
                return;
            }
            if (impact)
            {
                int frame = age < 0.045f ? 0 : age < 0.105f ? 1 : age < 0.215f ? 2 : 3;
                sigil.sprite = impactFrames[frame];
                var tint = plantedTint;
                tint.a *= 1 - Mathf.SmoothStep(0.215f, life, age); sigil.color = tint;
                if (age >= life) Destroy(gameObject);
                return;
            }
            if (falling)
            {
                if (follow != null && follow.gameObject.activeInHierarchy)
                { transform.position = follow.position; Paint(Mathf.Clamp01(age / Mathf.Max(0.01f, life))); return; }
                Destroy(gameObject); return;
            }
            if (planted)
            {
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, life, age));
                var tint = plantedTint; tint.a *= fade; sigil.color = tint;
                if (properties != null)
                {
                    properties.SetFloat(Dissolve, 1 - fade);
                    sigil.SetPropertyBlock(properties);
                }
                if (age >= life) Destroy(gameObject);
                return;
            }
            if (corona)
            {
                if (follow == null) { Destroy(gameObject); return; }
                transform.position = follow.position;
                transform.rotation = Quaternion.Euler(0, 0, age * 12);
                Paint(0.35f + Mathf.Sin(age * 2.5f) * 0.04f);
                return;
            }
            if (seal)
            {
                float progress = Mathf.Clamp01(age / life);
                transform.localScale = baseScale * Mathf.Lerp(0.35f, 1, Mathf.SmoothStep(0, 1, progress * 4));
                sigil.color = new Color(0.9f, 0.16f, 0.09f, 0.58f * (1 - Mathf.SmoothStep(0.7f, 1, progress)));
                Paint(0.15f + progress * 0.28f);
                if (age >= life) Destroy(gameObject);
                return;
            }
            if (warning)
            {
                Paint(0.18f + 0.045f * Mathf.Sin(age * 9));
                // Pattern owns warning lifetime, including the final yield frame.
                return;
            }
            float t = Mathf.Clamp01(age / life);
            transform.localScale = baseScale * Mathf.Lerp(0.7f, 1.45f, 1 - Mathf.Pow(1 - t, 3));
            Paint(t);
            if (t >= 1) Destroy(gameObject);
        }
    }

}
