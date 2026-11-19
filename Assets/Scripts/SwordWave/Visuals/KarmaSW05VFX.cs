using UnityEngine;

namespace KarmaPrototype
{
    // SW05 presentation only. Damage, timing, escape reservations and cancellation
    // remain owned by SW05Pattern, its rain blades and the existing planner.
    [DefaultExecutionOrder(-10)]
    public sealed class KarmaSW05VFX : MonoBehaviour
    {
        static readonly Color Ember = new Color(0.42f, 0.86f, 1);
        static Material celestial, crimsonBlade, woodMaterial, contactMaterial;
        static Sprite judgementSword;
        static readonly int Progress = Shader.PropertyToID("_Progress");
        static readonly int Mode = Shader.PropertyToID("_Mode");
        static readonly int Formation = Shader.PropertyToID("_Formation");
        static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
        static readonly int VisualTime = Shader.PropertyToID("_VisualTime");
        static readonly int Energy = Shader.PropertyToID("_Energy");
        KarmaGame game;
        Transform follow;
        SpriteRenderer sigil, afterimage;
        KarmaSW05RainBlade rain;
        MaterialPropertyBlock properties;
        Vector3 baseScale, plantedStart, plantedEnd, previousPosition;
        Color plantedTint, chargeTint;
        float age, visualAge, life, mode;
        bool falling, warning, planted, forming, explosion, explosionWarning;
        bool charging, embedding;
        float chargeAge, chargeSeconds;
        public SpriteRenderer Sword => sigil;

        static Material Celestial
        {
            get
            {
                if (celestial == null) celestial = KarmaArtResource.Load<Material>("SwordWave/SW05/SW05Celestial");
                return celestial;
            }
        }
        public static void ApplySword(SpriteRenderer renderer, Vector2 size, Vector2 velocity)
        {
            if (judgementSword == null) judgementSword = KarmaArtwork.Sprite("SwordBossVFX", "SW05_HeavyJudgementSword_optional");
            if (judgementSword == null) throw new System.InvalidOperationException("Missing SW05 judgement sword artwork");
            renderer.sprite = judgementSword;
            if (crimsonBlade == null) crimsonBlade = KarmaArtResource.Load<Material>("SwordWave/SW05/SW05CrimsonBlade");
            if (crimsonBlade == null) throw new System.InvalidOperationException("Missing SW05 crimson blade material");
            renderer.sharedMaterial = crimsonBlade;
            renderer.flipX = renderer.flipY = false;
            // The opaque weapon (not its transparent aura) occupies the
            // same long/transverse rectangle that the existing planner reserves.
            // Supplied heavy sword's alpha>=128 core: 396 x 968 px;
            // pivot (255,509) is halfway between the pommel and downward tip.
            renderer.transform.localScale = new Vector3(size.y * 100 / 396, size.x * 100 / 968, 1);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + 90);
            renderer.color = Color.white;
        }
        public static void ApplyDepth(SpriteRenderer sword, float depth, float opacity = 1)
        {
            Color tint = Color.Lerp(new Color(0.55f, 0.7f, 0.85f, 0.78f), Color.white, depth);
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
            var fx = Create(root, game, at, size, float.PositiveInfinity, 5, Ember);
            fx.warning = true;
            fx.sigil.name = "SW05 locked warning / no damage";
            fx.sigil.sortingOrder = 32;
            return fx.sigil;
        }
        public static void EmphasizeExplosion(SpriteRenderer renderer)
        {
            var fx = renderer.GetComponent<KarmaSW05VFX>();
            fx.explosionWarning = true;
            renderer.sortingOrder = 33;
        }
        public static KarmaSW05VFX Ascent(SpriteRenderer blade, Vector2 size)
            => Ascent(blade, size, Object.FindAnyObjectByType<KarmaGame>());
        public static KarmaSW05VFX Ascent(SpriteRenderer blade, Vector2 size, KarmaGame game)
        {
            KarmaSW05Energy.Blade(blade.transform.parent, game, blade.transform, size, false);
            // Use the pattern's staggered pose progress for material reveal too.
            // This clock only drives shimmer and survives the locked sky hold.
            var fx = blade.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game;
            fx.sigil = blade; fx.forming = true; fx.life = float.PositiveInfinity;
            fx.properties = new MaterialPropertyBlock();
            fx.SetFormation(0);
            return fx;
        }
        public void SetFormation(float progress)
        {
            float t = Mathf.Clamp01(progress);
            properties.SetFloat(Formation, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.55f)));
            properties.SetFloat(Energy, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.72f, 1, t)) * 0.35f);
            sigil.SetPropertyBlock(properties);
        }
        public static void ReleaseFormation(SpriteRenderer source, SpriteRenderer destination)
        {
            // Gameplay spawns at the locked planner origin. The harmless
            // renderer has already settled there; retain its exact appearance.
            destination.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            destination.transform.localScale = source.transform.localScale;
            destination.color = source.color;
            destination.sortingOrder = source.sortingOrder;
            var from = source.GetComponent<KarmaSW05VFX>();
            var to = destination.GetComponent<KarmaSW05VFX>();
            if (from == null || to == null) return;
            to.visualAge = from.visualAge;
            source.GetPropertyBlock(to.properties);
            destination.SetPropertyBlock(to.properties);
        }
        public void ContinueClockFrom(SpriteRenderer source)
        {
            var previous = source.GetComponent<KarmaSW05VFX>();
            if (previous == null) return;
            visualAge = previous.visualAge;
            properties.SetFloat(VisualTime, visualAge);
            sigil.SetPropertyBlock(properties);
        }
        public static void Falling(Transform root, KarmaGame game, Transform flight, SpriteRenderer sword,
            Vector2 size, float flightLife, Vector2 axis)
        {
            // One sharp, short blade echo replaces two broad aura layers. It
            // inherits the live renderer's rotation/scale and stops on contact.
            KarmaSW05Energy.Blade(root, game, sword.transform, size, true);
            var fx = sword.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game; fx.sigil = sword; fx.life = flightLife;
            fx.falling = true; fx.follow = flight; fx.rain = flight.GetComponent<KarmaSW05RainBlade>();
            fx.previousPosition = flight.position;
            fx.properties = new MaterialPropertyBlock(); fx.properties.SetFloat(Formation, 1);
            fx.afterimage = KarmaVisuals.Box(sword.transform, "SW05 compact blade echo / no damage",
                new Vector2(0, 9.68f * 0.085f), Vector2.one, Color.clear, sword.sortingOrder - 1);
            fx.afterimage.sprite = sword.sprite; fx.afterimage.sharedMaterial = sword.sharedMaterial;
            fx.afterimage.transform.localScale = Vector3.one * 0.97f;
            fx.baseScale = new Vector3(size.x, size.y, 1);
        }
        public static KarmaSW05VFX Strike(Transform root, KarmaGame game, Vector2 center, Vector2 size, Vector2 axis, float depth)
        {
            Vector2 tip = center + axis * (size.x / 2);
            WoodContact(root, game, new Vector2(tip.x, 0.05f), size);
            float landingWidth=size.y/Mathf.Max(0.001f,Mathf.Abs(axis.y));
            KarmaSW05Energy.Landing(root,game,new Vector2(tip.x,0.06f),landingWidth,axis);
            var sword = KarmaVisuals.Box(root, "SW05 wood-embedded sword / no damage",
                center, Vector2.one, Color.white, 24);
            // Keep exactly the airborne picture, palette and depth tint. A
            // separate floor-clipping material buries its lower 14% in the wood.
            ApplySword(sword, size, axis);
            if (woodMaterial == null) woodMaterial = KarmaArtResource.Load<Material>("SwordWave/SW05/SW05EmbeddedWood");
            sword.sharedMaterial = woodMaterial;
            ApplyDepth(sword, depth);
            var fx = sword.gameObject.AddComponent<KarmaSW05VFX>();
            fx.game = game; fx.sigil = sword; fx.planted = true; fx.life = float.PositiveInfinity; fx.plantedTint = sword.color;
            fx.embedding = true; fx.plantedStart = center; fx.plantedEnd = center + axis * (size.x * 0.14f);
            fx.properties = new MaterialPropertyBlock();
            KarmaSW05Energy.Planted(root, game, sword, size);
            return fx;
        }
        public void DissolvePlanted()
        { charging = false; plantedTint = sigil.color; age = 0; life = 0.22f; }
        public void ChargePlanted(float seconds)
        { charging = true; chargeAge = 0; chargeSeconds = seconds; chargeTint = sigil.color; }
        static void WoodContact(Transform root, KarmaGame game, Vector2 at, Vector2 size)
        {
            if (contactMaterial == null) contactMaterial = KarmaArtResource.Load<Material>("SwordWave/SW05/SW05WoodContact");
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
            visualAge += Time.deltaTime;
            if (forming)
            {
                properties.SetFloat(VisualTime, visualAge);
                sigil.SetPropertyBlock(properties);
                return;
            }
            if (falling)
            {
                if (follow == null || !follow.gameObject.activeInHierarchy || rain != null && rain.Landed) return;
                float speed = Vector3.Distance(follow.position, previousPosition) / Mathf.Max(0.0001f, Time.deltaTime);
                previousPosition = follow.position;
                properties.SetFloat(VisualTime, visualAge);
                properties.SetFloat(Energy, 0.35f);
                sigil.SetPropertyBlock(properties);
                afterimage.SetPropertyBlock(properties);
                var tint = Color.Lerp(sigil.color, Ember, 0.65f);
                tint.a = sigil.color.a * 0.13f * Mathf.Clamp01(speed / Mathf.Max(1, baseScale.x * 8));
                afterimage.color = tint; afterimage.sortingOrder = sigil.sortingOrder - 1;
                return;
            }
            if (planted)
            {
                if (embedding)
                {
                    transform.position = Vector3.Lerp(plantedStart, plantedEnd, Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 0.07f)));
                    if (age >= 0.07f) embedding = false;
                }
                if (properties != null) properties.SetFloat(VisualTime, visualAge);
                if (charging)
                {
                    chargeAge += Time.deltaTime;
                    float chargeProgress = Mathf.Clamp01(chargeAge / chargeSeconds);
                    float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(chargeProgress * 12 + chargeProgress * chargeProgress * 35), 4);
                    sigil.color = Color.Lerp(chargeTint,
                        new Color(0.9f, 1, 1, 1), Mathf.Clamp01(chargeProgress * 0.8f + pulse * chargeProgress * 0.2f));
                    properties.SetFloat(Energy, chargeProgress);
                    sigil.SetPropertyBlock(properties);
                    sigil.sortingOrder = 27;
                    return;
                }
                if (float.IsPositiveInfinity(life))
                {
                    // A newly struck sword flashes briefly; old swords recede
                    // beneath the next warning without changing their blast sites.
                    float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.10f, 0.38f, age));
                    sigil.color = Color.Lerp(plantedTint, new Color(0.42f, 0.6f, 0.76f, 0.46f), settle);
                    properties.SetFloat(Energy, 0.12f + 0.035f * Mathf.Sin(age * 5));
                    sigil.SetPropertyBlock(properties);
                    sigil.sortingOrder = settle >= 0.9f ? 15 : 27;
                    return;
                }
                float hold = Mathf.Min(0.25f, life * 0.25f);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hold, life, age));
                var tint = plantedTint; tint.a *= fade; sigil.color = tint;
                if (properties != null)
                {
                    properties.SetFloat(Dissolve, 1 - fade);
                    properties.SetFloat(Energy, fade);
                    sigil.SetPropertyBlock(properties);
                }
                if (age >= life) Destroy(gameObject);
                return;
            }
            if (warning)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(age * (explosionWarning ? 22 : 12));
                sigil.color = Color.Lerp(Ember, Color.white, pulse * (explosionWarning ? 0.85f : 0.55f));
                Paint(age);
                // Pattern owns warning lifetime, including the final yield frame.
                return;
            }
            float t = Mathf.Clamp01(age / life);
            transform.localScale = explosion ? baseScale
                : baseScale * Mathf.Lerp(0.7f, 1.45f, 1 - Mathf.Pow(1 - t, 3));
            Paint(t);
            if (t >= 1) Destroy(gameObject);
        }
    }
}
