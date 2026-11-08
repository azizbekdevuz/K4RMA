using UnityEngine;

namespace KarmaPrototype
{
    // Only SW03/SW05 use the supplied pack. All frames have a fixed authored
    // pivot and canvas scale; changing frames never resizes a damage collider.
    public sealed class KarmaSwordPackedVfx : MonoBehaviour
    {
        public enum Clip { GroundCharge, BladeSurge, FallingAura, LandingShock, Overcharge, Detonation }
        static readonly string[] Names = { "SW03_GroundCharge_6f", "SW03_BladeSurge_6f", "SW05_FallingSwordAura_6f",
            "SW05_LandingShock_6f", "SW05_FinalOvercharge_6f", "SW05_FinalDetonation_8f" };
        static readonly Sprite[][] frames = new Sprite[6][];
        // Pixel extents above the fixed ground pivot (96 px), measured from
        // the supplied sheets. Aura uses its full vertical content instead.
        static readonly Vector2[] Content = { new Vector2(381,50), new Vector2(414,342), new Vector2(220,392),
            new Vector2(477,174), new Vector2(403,60), new Vector2(457,181) };
        KarmaGame game; Transform follow; KarmaSW05RainBlade rain;
        SpriteRenderer picture; MaterialPropertyBlock properties;
        float age, seconds, opacity; bool driven;
        public Clip ClipId { get; private set; }
        public int FrameIndex { get; private set; }
        public static Sprite Frame(Clip clip, int index)
        {
            int c = (int)clip;
            if (frames[c] == null)
            {
                frames[c] = new Sprite[clip == Clip.Detonation ? 8 : 6];
                for (int i = 0; i < frames[c].Length; i++)
                {
                    frames[c][i] = KarmaArtwork.Sprite("SwordBossVFX",Names[c]+"_"+i);
                    if (frames[c][i] == null) throw new System.InvalidOperationException("Missing packed VFX frame: " + Names[c]+"_"+i);
                }
            }
            return frames[c][index];
        }
        public static KarmaSwordPackedVfx Spawn(Transform root, KarmaGame game, Clip clip, Vector2 at,
            Vector2 size, float seconds, int order, float opacity = 0.7f, bool driven = false, Transform follow = null)
        {
            var node = new GameObject(Names[(int)clip]+" / animated light / no damage");
            node.transform.SetParent(root,false); node.transform.position = at;
            var fx = node.AddComponent<KarmaSwordPackedVfx>(); fx.game = game; fx.follow = follow;
            fx.rain = follow != null ? follow.GetComponent<KarmaSW05RainBlade>() : null;
            fx.picture = node.AddComponent<SpriteRenderer>(); fx.picture.sortingOrder = order;
            fx.picture.sharedMaterial = Resources.Load<Material>("SW05/SW05Celestial");
            fx.properties = new MaterialPropertyBlock(); fx.properties.SetFloat("_Mode",7);
            fx.SetBounds(new Rect(-10000,clip == Clip.FallingAura ? -10000 : 0,20000,20000));
            fx.Play(clip,size,seconds,opacity,driven); return fx;
        }
        public void SetBounds(Rect rect)
        {
            properties.SetVector("_ClipRect",new Vector4(rect.xMin,rect.yMin,rect.xMax,rect.yMax));
            picture.SetPropertyBlock(properties);
        }
        public void Play(Clip clip, Vector2 size, float duration, float strength, bool manual = false)
        {
            ClipId = clip; age = 0; seconds = Mathf.Max(0.01f,duration); opacity = strength; driven = manual;
            var content = Content[(int)clip];
            transform.localScale = new Vector3(size.x*100/content.x,size.y*100/content.y,1);
            SetProgress(0);
        }
        public void SetProgress(float progress)
        {
            float t = Mathf.Clamp01(progress);
            FrameIndex = Mathf.Min(ClipId == Clip.Detonation ? 7 : 5,(int)(t*(ClipId == Clip.Detonation ? 8 : 6)));
            picture.sprite = Frame(ClipId,FrameIndex);
            bool charge = ClipId == Clip.GroundCharge || ClipId == Clip.Overcharge;
            float envelope = charge ? 0.25f+0.75f*t : 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.82f,1,t));
            picture.color = new Color(0.75f,0.94f,1,opacity*envelope);
            var rect=picture.sprite.textureRect;var tex=picture.sprite.texture;
            properties.SetVector("_FrameUV",new Vector4(rect.x/tex.width,rect.y/tex.height,rect.width/tex.width,rect.height/tex.height));
            properties.SetFloat("_PackStyle",(int)ClipId);properties.SetFloat("_Progress",t);
            // Rebind after the SpriteRenderer installs the new sprite's data.
            picture.SetPropertyBlock(properties);
        }
        void LateUpdate()
        {
            if (game == null || game.Phase != RunPhase.Combat
                || follow != null && (!follow.gameObject.activeInHierarchy || rain != null && rain.Landed))
            { Destroy(gameObject); return; }
            if (game.Paused) return;
            if (follow != null) transform.position = follow.position;
            if (driven) return;
            age += Time.deltaTime; SetProgress(age/seconds);
            if (age >= seconds) Destroy(gameObject);
        }
    }

    // SW05 presentation only. Damage, timing, escape reservations and cancellation
    // remain owned by SW05Pattern, its rain blades and the existing planner.
    public sealed class KarmaSW05VFX : MonoBehaviour
    {
        static readonly Color Radiance = new Color(0.16f, 0.58f, 0.95f);
        static readonly Color Ember = new Color(0.42f, 0.86f, 1);
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
        bool falling, corona, warning, planted, seal, forming, impact, explosion, explosionWarning;
        bool charging;
        float chargeAge, chargeSeconds;

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
            if (judgementSword == null) judgementSword = KarmaArtwork.Sprite("SwordBossVFX", "SW05_HeavyJudgementSword_optional");
            if (judgementSword == null) throw new System.InvalidOperationException("Missing SW05 judgement sword artwork");
            renderer.sprite = judgementSword;
            if (crimsonBlade == null) crimsonBlade = Resources.Load<Material>("SW05/SW05CrimsonBlade");
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
        public static void Corona(Transform root, KarmaGame game, Transform boss)
        {
            var fx = Create(root, game, boss.position, new Vector2(10, 10), float.PositiveInfinity, 0, new Color(0.3f, 0.75f, 1, 0.65f));
            fx.corona = true; fx.follow = boss;
            KarmaSW05Energy.Summon(root, game, boss);
        }
        static void Trail(SpriteRenderer blade, float width, float duration)
        {
            var go = new GameObject("SW05 tapered comet wake / no damage");
            go.transform.SetParent(blade.transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = KarmaCombatVFX.GlowMaterial;
            trail.time = duration; trail.minVertexDistance = 0.16f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(0.18f, width * 0.65f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.83f, 0.97f, 1), 0), new GradientColorKey(Radiance, 0.25f), new GradientColorKey(new Color(0.025f, 0.12f, 0.24f), 1) },
                new[] { new GradientAlphaKey(0.64f, 0), new GradientAlphaKey(0.20f, 0.3f), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient; trail.sortingOrder = 23;
            trail.numCapVertices = 0; trail.numCornerVertices = 0;
        }
        public static void Ascent(SpriteRenderer blade, Vector2 size)
        {
            Trail(blade, size.y * 0.22f, 0.07f);
            KarmaSW05Energy.Blade(blade.transform.parent, Object.FindAnyObjectByType<KarmaGame>(), blade.transform, size, false);
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
                life, 2, new Color(0.2f, 0.65f, 1, 0.18f));
            beam.sigil.name = "SW05 summoning pillar / no damage";
            var ring = Create(root, game, caster + Vector2.up * 6, new Vector2(70, 10.56f), life, 0,
                new Color(0.3f, 0.75f, 1, 0.58f));
            ring.seal = true;
            ring.sigil.name = "SW05 circular summoning seal / no damage";
        }
        public static void Falling(Transform root, KarmaGame game, Transform flight, SpriteRenderer sword,
            Vector2 size, float flightLife, Vector2 axis)
        {
            // The accelerated rain crosses the viewport in a few frames. Bound
            // the wake in space instead of recording a long, curved trajectory.
            KarmaSW05Energy.Blade(root, game, sword.transform, size, true);
            var aura = KarmaSwordPackedVfx.Spawn(root,game,KarmaSwordPackedVfx.Clip.FallingAura,flight.position,
                new Vector2(size.y*1.4f,size.x*1.05f),flightLife,21,0.85f,follow: flight);
            aura.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(axis.y,axis.x)*Mathf.Rad2Deg+90);
            var fx = Create(root, game, flight.position, new Vector2(size.y * 1.4f, size.x * 1.25f), flightLife, 1, Radiance);
            fx.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg + 90);
            fx.falling = true; fx.follow = flight;
        }
        public static void RainRelease(Transform root, KarmaGame game, Vector2 caster)
        {
            var fx = Create(root, game, caster, new Vector2(8, 5), 0.14f, 3, new Color(0.65f, 0.94f, 1, 0.75f));
            fx.sigil.name = "SW05 rain release ignition / no damage";
            fx.sigil.sortingOrder = 29;
        }
        public static KarmaSW05VFX Strike(Transform root, KarmaGame game, Vector2 center, Vector2 size, Vector2 axis, float depth)
        {
            Vector2 tip = center + axis * (size.x / 2);
            WoodContact(root, game, new Vector2(tip.x, 0.05f), size);
            GroundImpact(root, game, new Vector2(tip.x, 0.05f), size, depth);
            KarmaSwordPackedVfx.Spawn(root,game,KarmaSwordPackedVfx.Clip.LandingShock,new Vector2(tip.x,0.05f),
                new Vector2(Mathf.Max(2,size.y*3),2.5f),0.3f,32,0.75f);
            KarmaSW05Energy.Landing(root, game, new Vector2(tip.x, 0.06f), Mathf.Max(1, size.y * 1.5f));
            var ignition = Create(root, game, new Vector2(tip.x, 0.25f),
                new Vector2(Mathf.Max(1.6f,size.y*2),0.55f),0.085f,4,new Color(0.85f,0.98f,1));
            ignition.explosion = true; ignition.sigil.sortingOrder = 34;
            ignition.sigil.name = "SW05 landing ignition / no damage";
            // A low, brief contact pulse accompanies the existing wood shadow.
            // It has no collider, debris, screen shake or gameplay callback.
            var contact = Create(root, game, new Vector2(tip.x, 0.08f),
                new Vector2(Mathf.Max(0.7f, size.y * 1.8f), 0.42f), 0.20f, 3, new Color(0.55f, 0.9f, 1, 0.7f));
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
            fx.game = game; fx.sigil = sword; fx.planted = true; fx.life = float.PositiveInfinity; fx.plantedTint = sword.color;
            fx.properties = new MaterialPropertyBlock();
            return fx;
        }
        public void DissolvePlanted()
        { charging = false; age = 0; life = 0.22f; }
        public void ChargePlanted(float seconds)
        { charging = true; chargeAge = 0; chargeSeconds = seconds; }
        public static void Explosion(Transform root, KarmaGame game, KarmaEnemy owner, Vector2 at, Vector2 size)
        {
            // Overlapping sites share damage, and their light must not add up
            // to an opaque white wall. The coordinated mesh carries the peak.
            var flash = Create(root, game, at, size, 0.42f, 4, new Color(0.65f, 0.94f, 1, 0.075f));
            flash.explosion = true;
            flash.sigil.sortingOrder = 34;
            flash.sigil.name = "SW05 final explosion / damage volume";
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
            // The same material also renders the impact sheet. Broaden only
            // actual swords, leaving this existing four-frame artwork intact.
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_BladePresence",0); sr.SetPropertyBlock(properties);
            // One constant sheet scale, per-frame contact pivots. Never fit
            // individual frame bounds: the expanding ring must stay grounded.
            float scale = Mathf.Max(0.65f, size.y * 3.5f) / 5.06f;
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
                if (follow != null && follow.gameObject.activeInHierarchy && age < life
                    && !(follow.GetComponent<KarmaSW05RainBlade>()?.Landed ?? false))
                { transform.position = follow.position; Paint(Mathf.Clamp01(age / Mathf.Max(0.01f, life))); return; }
                Destroy(gameObject); return;
            }
            if (planted)
            {
                if (charging)
                {
                    chargeAge += Time.deltaTime;
                    float chargeProgress = Mathf.Clamp01(chargeAge / chargeSeconds);
                    float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(chargeProgress * 12 + chargeProgress * chargeProgress * 35), 4);
                    sigil.color = Color.Lerp(new Color(0.18f, 0.55f, 0.85f, 0.5f),
                        new Color(0.9f, 1, 1, 1), Mathf.Clamp01(chargeProgress * 0.8f + pulse * 0.35f));
                    sigil.sortingOrder = 27;
                    return;
                }
                if (float.IsPositiveInfinity(life))
                {
                    // A newly struck sword flashes briefly; old swords recede
                    // beneath the next warning without changing their blast sites.
                    float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.10f, 0.38f, age));
                    sigil.color = Color.Lerp(plantedTint, new Color(0.36f, 0.48f, 0.62f, 0.32f), settle);
                    sigil.sortingOrder = settle >= 0.9f ? 15 : 27;
                    return;
                }
                float hold = Mathf.Min(0.25f, life * 0.25f);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hold, life, age));
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
                sigil.color = new Color(0.3f, 0.75f, 1, 0.58f * (1 - Mathf.SmoothStep(0.7f, 1, progress)));
                Paint(0.15f + progress * 0.28f);
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

    // SW05-only, one draw per layer. Fixed buffers and recycled meshes replace
    // hundreds of particle/line objects; motion never touches gameplay geometry.
    public sealed class KarmaSW05Energy : MonoBehaviour
    {
        enum Kind { Summon, Form, Fall, Landing, Charge, Detonation, Eruption, Scar, ScarWarning, ScarFade, WardRelease, WardImpact, WardCollapse, WardFlight }
        sealed class Buffer
        {
            public readonly Mesh mesh;
            public readonly Vector3[] vertices;
            public readonly Color[] colors;
            public readonly int capacity;
            public Buffer(int segments)
            {
                capacity = segments; vertices = new Vector3[segments * 4]; colors = new Color[vertices.Length];
                var indices = new int[segments * 6];
                for (int i = 0; i < segments; i++)
                { int n = i * 6, v = i * 4; indices[n] = v; indices[n+1] = v+1; indices[n+2] = v+2; indices[n+3] = v+2; indices[n+4] = v+1; indices[n+5] = v+3; }
                mesh = new Mesh { name = "SW05 recycled energy",
                    indexFormat = segments * 4 > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = indices;
            }
        }
        static readonly System.Collections.Generic.List<Buffer> spare = new System.Collections.Generic.List<Buffer>(72);
        static int nextSeed;
        static readonly Color Blue = new Color(0.08f, 0.55f, 1.1f), White = new Color(0.75f, 1.15f, 1.4f);
        KarmaGame game; Transform follow; KarmaSW05RainBlade rain;
        MeshRenderer display; MeshFilter filter; Buffer buffer;
        Kind kind; Vector2 at, size; Vector3[] sites, zones;
        KarmaSwordPackedVfx[] chargedLayers;
        Vector2[] chargedSizes;
        float age, life; int count, seed;
        float charge;
        public bool Detonated => kind == Kind.Detonation;
        public bool CapacityExceeded { get; private set; }
        public float Age => age;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        { foreach (var b in spare) if (b.mesh != null) Object.Destroy(b.mesh); spare.Clear(); nextSeed = 0; }
        static KarmaSW05Energy Create(Transform root, KarmaGame game, Kind kind, float life, int segments = 256)
        {
            var go = new GameObject("SW05 " + kind + " animated energy / no damage"); go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var fx = go.AddComponent<KarmaSW05Energy>(); fx.game = game; fx.kind = kind; fx.life = life; fx.seed = ++nextSeed;
            for (int i = spare.Count - 1; i >= 0; i--)
                if (spare[i].capacity == segments && spare[i].mesh != null)
                { fx.buffer = spare[i]; spare.RemoveAt(i); break; }
            if (fx.buffer == null) fx.buffer = new Buffer(segments);
            fx.filter = go.AddComponent<MeshFilter>(); fx.filter.sharedMesh = fx.buffer.mesh;
            fx.display = go.AddComponent<MeshRenderer>(); fx.display.sharedMaterial = Resources.Load<Material>("SW05/SW05Celestial");
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_Mode",6); fx.display.SetPropertyBlock(properties);
            fx.display.sortingOrder = kind == Kind.Charge ? 28 : 30;
            fx.display.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fx.display.receiveShadows = false;
            fx.Upload(); return fx;
        }
        public static void Summon(Transform root, KarmaGame game, Transform boss)
        { var fx = Create(root, game, Kind.Summon, float.PositiveInfinity, 512); fx.follow = boss; }
        public static void Blade(Transform root, KarmaGame game, Transform blade, Vector2 size, bool falling)
        {
            var fx = Create(root, game, falling ? Kind.Fall : Kind.Form, float.PositiveInfinity);
            fx.follow = blade; fx.size = size; fx.rain = blade.GetComponentInParent<KarmaSW05RainBlade>();
        }
        public static void Landing(Transform root, KarmaGame game, Vector2 at, float width)
        { var fx = Create(root, game, Kind.Landing, 0.32f); fx.at = at; fx.size = new Vector2(width, 1); }
        public static void Eruption(Transform root, KarmaGame game, Vector2 at, float width)
        {
            var fx = Create(root,game,Kind.Eruption,0.2f);
            fx.name = "SW03 synchronized eruption flash / no damage";
            fx.at = at; fx.size = new Vector2(width,1); fx.display.sortingOrder = 34;
        }
        public static KarmaSW05Energy Scar(Transform root,KarmaGame game,Transform attack,Vector2 ground,Vector2 size,float life)
        {
            var fx=Create(root,game,Kind.Scar,life,512);fx.follow=attack;fx.at=ground;fx.size=size;
            fx.name="SW03 travelling blade lightning / no damage";fx.display.sortingOrder=32;return fx;
        }
        public void SetScarBounds(Vector2 ground,Vector2 footprint){at=ground;size=footprint;}
        public void SetCharge(float progress){charge=Mathf.Clamp01(progress);}
        public static KarmaSW05Energy ScarWarning(Transform root,KarmaGame game,Vector2 ground,float width,float life)
        {
            var fx=Create(root,game,Kind.ScarWarning,float.PositiveInfinity);fx.at=ground;fx.size=new Vector2(width,1);
            fx.name="SW03 electrically carved warning / no damage";fx.display.sortingOrder=11;return fx;
        }
        public static void ScarDissolve(Transform root,KarmaGame game,Vector2 ground,Vector2 size)
        {var fx=Create(root,game,Kind.ScarFade,0.22f);fx.at=ground;fx.size=size;fx.display.sortingOrder=33;}
        public static void WardBurst(Transform root,KarmaGame game,Vector2 at,Vector2 axis,float radius,int style)
        {
            var fx=Create(root,game,style==2?Kind.WardCollapse:style==1?Kind.WardImpact:Kind.WardRelease,0.30f,512);
            fx.at=at;fx.size=axis.normalized*radius;fx.display.sortingOrder=37;
            fx.name="SW04 "+(style==2?"barrier disruption":style==1?"defensive contact shock":"directional counter release")+" / no damage";
        }
        public static void CounterFlight(Transform root,KarmaGame game,Transform shot,Vector2 axis,Vector2 size)
        {var fx=Create(root,game,Kind.WardFlight,float.PositiveInfinity);fx.follow=shot;fx.at=axis;fx.size=size;fx.display.sortingOrder=31;}
        public static KarmaSW05Energy Prepare(Transform root, KarmaGame game,
            System.Collections.Generic.List<KarmaSW05RainBlade> swords, float seconds)
        {
            // Sixteen thousand quads cover all 54 sites, their branches and
            // both shock fronts. Never silently truncate the rightmost blasts.
            var fx = Create(root, game, Kind.Charge, seconds, 16384);
            fx.sites = new Vector3[swords.Count];
            for (int i = 0; i < swords.Count; i++)
            {
                var blade = swords[i]; var p = blade.ExplosionCenter;
                fx.sites[i] = new Vector3(p.x, p.y, blade.ExplosionSize.x);
            }
            System.Array.Sort(fx.sites, (a, b) => a.x.CompareTo(b.x));
            // Show the exact union of the colliders once, avoiding dozens of
            // translucent overlapping fills. Genuine horizontal gaps stay open.
            var merged = new System.Collections.Generic.List<Vector3>();
            var intervals = (Vector3[])fx.sites.Clone();
            System.Array.Sort(intervals, (a, b) => (a.x-a.z/2).CompareTo(b.x-b.z/2));
            foreach (var site in intervals)
            {
                float left = site.x - site.z / 2, right = site.x + site.z / 2;
                if (merged.Count > 0 && left <= merged[merged.Count - 1].y)
                {
                    var zone = merged[merged.Count - 1]; zone.y = Mathf.Max(zone.y, right);
                    merged[merged.Count - 1] = zone;
                }
                else merged.Add(new Vector3(left, right, site.y * 2));
            }
            fx.zones = merged.ToArray();
            var layers = new System.Collections.Generic.List<KarmaSwordPackedVfx>();
            var sizes = new System.Collections.Generic.List<Vector2>();
            foreach (var zone in fx.zones)
            {
                int tiles = Mathf.Max(1,Mathf.CeilToInt((zone.y-zone.x)/13));
                float width = (zone.y-zone.x)/tiles;
                for (int i = 0; i < tiles; i++)
                {
                    float left = zone.x+i*width;
                    var layer = KarmaSwordPackedVfx.Spawn(fx.transform,game,KarmaSwordPackedVfx.Clip.Overcharge,
                        new Vector2(left+width/2,0),new Vector2(width,Mathf.Min(zone.z*0.40f,2.8f)),seconds,29,0.95f,true);
                    layer.SetBounds(new Rect(left,0,width,zone.z));
                    layers.Add(layer); sizes.Add(new Vector2(width,zone.z));
                }
            }
            fx.chargedLayers = layers.ToArray(); fx.chargedSizes = sizes.ToArray();
            return fx;
        }
        public void Ignite()
        {
            kind = Kind.Detonation; age = 0; life = 0.48f; display.sortingOrder = 36;
            for (int i = 0; i < chargedLayers.Length; i++)
                if (chargedLayers[i] != null)
                    chargedLayers[i].Play(KarmaSwordPackedVfx.Clip.Detonation,chargedSizes[i],0.32f,1);
        }
        float Noise(int index)
        { float v = Mathf.Sin(index * 127.1f + seed * 0.013f) * 43758.5453f; return v - Mathf.Floor(v); }
        static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        void Quad(Vector2 a, Vector2 b, float width, Color tint)
        {
            if (count >= buffer.capacity) { CapacityExceeded=true;return; }
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized * width;
            int v = count++ * 4;
            buffer.vertices[v] = a-n; buffer.vertices[v+1] = a+n; buffer.vertices[v+2] = b-n; buffer.vertices[v+3] = b+n;
            for (int i = 0; i < 4; i++) buffer.colors[v+i] = tint;
        }
        void Line(Vector2 a, Vector2 b, float width, float alpha)
        { var c = Blue; c.a = alpha * 0.32f; Quad(a,b,width*2.4f,c); c = White; c.a = alpha; Quad(a,b,width*0.38f,c); }
        void Shard(Vector2 p, Vector2 direction, float length, float alpha)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                if (count >= buffer.capacity) { CapacityExceeded=true;return; }
                float width = length * (layer == 0 ? 0.12f : 0.035f);
                Vector2 n = new Vector2(-direction.y,direction.x)*width;
                int v = count++*4;
                buffer.vertices[v] = p-n; buffer.vertices[v+1] = p+n;
                buffer.vertices[v+2] = buffer.vertices[v+3] = p+direction*length;
                Color c = layer == 0 ? Blue : White; c.a = alpha*(layer == 0 ? 0.5f : 1);
                for (int i = 0; i < 4; i++) buffer.colors[v+i] = c;
            }
        }
        void SwordShard(Vector2 p,Vector2 axis,float length,float alpha)
        {
            Shard(p,axis,length,alpha);
            var n=new Vector2(-axis.y,axis.x);
            Line(p+axis*length*0.16f-n*length*0.12f,p+axis*length*0.16f+n*length*0.12f,length*0.025f,alpha);
        }
        void ScarEnergy()
        {
            float w=size.x,h=size.y;
            if(kind==Kind.ScarWarning)
            {
                float pulse=0.55f+charge*0.45f;
                for(int side=-1;side<=1;side+=2)
                {
                    float x=at.x+side*w*0.46f;
                    Line(new Vector2(x,0.05f),new Vector2(x,0.65f+charge*0.45f),0.055f,pulse);
                    Bolt(at+new Vector2(-w*0.45f,0.10f),at+new Vector2(w*0.45f,0.10f),0.035f,pulse,side+22);
                    float u=Mathf.Repeat(age*(2+charge*4),1);
                    Shard(at+new Vector2(side*w*(0.45f-u*0.35f),0.08f),Vector2.up,0.25f+charge*0.55f,pulse*(1-u));
                }
                Ring(at+Vector2.up*0.08f,new Vector2(w*0.44f,0.17f+charge*0.12f),age*2,0.65f+charge*0.35f,20);
                return;
            }
            if(kind==Kind.ScarFade)
            {
                float t=Mathf.Clamp01(age/life),fade=(1-t)*(1-t);
                for(int i=0;i<9;i++)
                {
                    float y=h*(0.08f+Noise(i+31)*0.84f);
                    Vector2 axis=new Vector2((Noise(i+41)-0.5f)*1.6f,0.5f).normalized;
                    Vector2 p=at+new Vector2((Noise(i+51)-0.5f)*w,y)+axis*t*1.4f;
                    SwordShard(p,axis,(0.3f+Noise(i+61)*0.6f)*fade,fade);
                }
                Bolt(at+Vector2.up*h*0.2f,at+Vector2.up*h*(1-t),0.055f,fade,81);
                return;
            }
            // These outlines track the actual current visible/hit rectangle.
            // The existing imported sword remains the opaque silhouette.
            float onset=Mathf.Exp(-age*32),crest=age>=0.1f?Mathf.Exp(-(age-0.1f)*65):0;
            if(age<0.20f)Burst(at,w*1.15f,Mathf.Clamp01(age/0.20f),false,seed);
            for(int side=-1;side<=1;side+=2)
            {
                Vector2 low=at+new Vector2(side*w*0.25f,0.08f),high=at+new Vector2(side*w*0.10f,h*0.94f);
                Line(low,high,0.035f,0.65f);
                for(int j=0;j<2;j++)
                {
                    float u=Mathf.Repeat(age*5+j*0.5f,1);
                    Vector2 p=at+new Vector2(side*w*0.26f,h*u);
                    Bolt(p,p+new Vector2(-side*w*0.3f,Mathf.Min(h*(1-u),1.1f)),0.05f,0.85f,side*11+j+100);
                }
                SwordShard(at+new Vector2(side*w*0.2f,0.12f),new Vector2(side*0.12f,1).normalized,
                    h*0.65f*onset,onset);
                for(int j=0;j<3;j++)
                {
                    Vector2 tip=at+Vector2.up*h;
                    SwordShard(tip,new Vector2(side*(0.2f+j*0.25f),0.35f).normalized,(0.45f+j*0.20f)*crest,crest);
                }
            }
            float front=Mathf.Repeat(age*4.5f,1);
            Line(at+new Vector2(-w*0.27f,h*front),at+new Vector2(w*0.27f,h*front),0.075f,0.95f);
        }
        void WardEnergy()
        {
            if(kind==Kind.WardFlight)
            {
                Vector2 p=follow.position,flightAxis=at,flightNormal=new Vector2(-flightAxis.y,flightAxis.x);
                Line(p-flightAxis*size.x*0.35f,p+flightAxis*size.x*0.48f,size.y*0.05f,1);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector2 edge=p+flightNormal*size.y*0.34f*side;
                    Bolt(edge-flightAxis*size.x*0.25f,edge+flightAxis*size.x*0.38f,0.065f,0.85f,side+7);
                    Line(edge-flightAxis*size.x*0.6f,edge-flightAxis*size.x*0.1f,0.035f,0.7f);
                }
                return;
            }
            float t=Mathf.Clamp01(age/life),fade=(1-t)*(1-t),radius=size.magnitude;
            Vector2 axis=size.normalized,n=new Vector2(-axis.y,axis.x);
            if(kind==Kind.WardRelease)
            {
                for(int i=0;i<3;i++)
                {
                    float a=Mathf.Lerp(-0.65f,0.65f,i/2f);
                    Vector2 ray=axis*Mathf.Cos(a)+n*Mathf.Sin(a);
                    SwordShard(at,ray,radius*(0.4f+t)*fade,fade);
                }
                Bolt(at-axis*0.3f,at+axis*radius*0.8f,0.14f,fade,7);
                return;
            }
            Ring(at,Vector2.one*radius*(0.55f+t*0.65f),age*2,fade,48);
            Ring(at,Vector2.one*radius*(0.35f+t*0.75f),-age*3,fade*0.65f,32);
            for(int i=0;i<12;i++)
            {
                Vector2 ray=Direction(i*Mathf.PI/6+0.2f);
                Vector2 p=at+ray*radius*(kind==Kind.WardCollapse?0.6f+t*0.5f:0.2f);
                SwordShard(p,ray,(kind==Kind.WardCollapse?1.1f:0.65f)*fade,fade);
                if(i%3==0)Bolt(at+ray*radius*0.4f,p,0.07f,fade,i+100);
            }
        }
        void Bolt(Vector2 a, Vector2 b, float width, float alpha, int id)
        {
            Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized;
            Vector2 p = a; int flicker = Mathf.FloorToInt(age * 35);
            for (int i = 1; i <= 6; i++)
            {
                Vector2 q = Vector2.Lerp(a,b,i/6f) + n * (i==6 ? 0 : (Noise(id*71+i+flicker*43)-0.5f)*0.42f);
                Line(p,q,width,alpha); p=q;
            }
        }
        void Ring(Vector2 center, Vector2 radius, float turn, float alpha, int steps = 32)
        {
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2 / steps + turn, b = (i+0.82f) * Mathf.PI * 2 / steps + turn;
                Line(center+Vector2.Scale(Direction(a),radius),center+Vector2.Scale(Direction(b),radius),0.025f,alpha);
            }
        }
        void Summoning()
        {
            Vector2 p = follow.position;
            float pulse = 0.65f + 0.35f * Mathf.Sin(age * 12);
            Ring(p, new Vector2(7.2f,4.4f),age*0.65f,0.85f,64);
            Ring(p, new Vector2(5.5f,5.5f),-age*0.8f,0.55f,48);
            Ring(p, new Vector2(3.3f,3.3f),age*1.4f,0.7f,32);
            for (int i = 0; i < 12; i++)
            {
                Vector2 axis = Direction(i*Mathf.PI/6 + age*0.65f), mark = p+Vector2.Scale(axis,new Vector2(7.2f,4.4f));
                SwordShard(mark,axis,0.65f+0.2f*pulse,0.85f);
                Line(mark-axis*0.45f,mark+axis*0.45f,0.055f,0.8f);
                Line(mark+axis*0.45f,mark+new Vector2(-axis.y,axis.x)*0.22f,0.025f,0.75f);
                if(i%2==0)Bolt(p+axis*(4.5f+Noise(i)*1.4f),p+axis*1.4f,0.075f,pulse,i);
            }
            for (int i = 0; i < 18; i++)
            {
                float t = Mathf.Repeat(age*1.8f+Noise(i),1); Vector2 axis = Direction(Noise(i+31)*Mathf.PI*2);
                Vector2 q = p+axis*Mathf.Lerp(5.7f,0.8f,t);
                Line(q,q-axis*0.35f,0.025f,t*0.7f);
            }
        }
        void BladeEnergy()
        {
            Vector2 p = follow.position, axis = -follow.up, n = new Vector2(-axis.y,axis.x);
            float span = size.x * (kind == Kind.Form ? 0.40f : 0.50f);
            // A wide luminous fuller, two cyan bevels and travelling narrow
            // highlights add mass without enlarging the hilt or weapon bounds.
            float power = kind == Kind.Fall ? 1 : 0.55f;
            Line(p-axis*size.x*0.08f,p+axis*size.x*0.40f,size.y*0.075f,power);
            for (int i = -1; i <= 1; i+=2)
            {
                Line(p-axis*size.x*0.06f+n*size.y*0.27f*i,
                    p+axis*size.x*0.38f+n*size.y*0.12f*i,size.y*0.035f,power);
                for (int j = 0; j < 2; j++)
                {
                    float u = Mathf.Repeat(age*3.6f+j*0.5f+Noise(i+8)*0.2f,1);
                    Vector2 at = p+axis*(Mathf.Lerp(-0.12f,0.38f,u)*size.x)+n*(size.y*0.30f*i);
                    float length = Mathf.Min(3.2f,size.x*0.32f);
                    Bolt(at-axis*length*0.5f,at+axis*length*0.5f,0.11f,power,i*13+j+20);
                }
                if (kind == Kind.Fall)
                {
                    Vector2 tip = p+axis*span;
                    Vector2 streak = tip+n*size.y*0.48f*i;
                    Line(streak,streak-axis*Mathf.Min(5,size.x*0.6f),0.07f,0.95f);
                    Shard(streak-axis*size.x*0.35f,axis,size.x*0.28f,0.8f);
                }
            }
        }
        void Burst(Vector2 p, float width, float t, bool final, int id)
        {
            float fade = (1-t)*(1-t), travel = 1-Mathf.Pow(1-t,3);
            Ring(p,new Vector2(width*(0.25f+travel*0.9f),0.10f+travel*(final?0.65f:0.38f)),0,fade,final?20:24);
            // Thin horizontal ground fractures and short angular fragments.
            // The filled damage flash stays low; these separated sparks are decoration.
            for (int i = 0; i < (final?10:7); i++)
            {
                float a = (0.10f+Noise(i+id)*0.8f)*Mathf.PI;
                Vector2 direction = Direction(a), q = p+Vector2.Scale(direction,new Vector2(width,final?5:1.8f))*travel;
                if (final) Shard(q,direction,(0.9f+Noise(i+id+30)*1.1f)*fade,fade);
                else Shard(q,direction,(0.6f+Noise(i+id)*0.65f)*(1-t),fade);
            }
            if (final)
                Bolt(p,p+new Vector2((Noise(id+99)-0.5f)*width*0.7f,2.4f+Noise(id+100)*1.4f),
                    0.12f,Mathf.Exp(-t*6),id+300);
            else Bolt(p,p+new Vector2((Noise(id+9)-0.5f)*width*0.4f,2.3f),0.095f,fade,id+400);
            if (t < 0.45f)
                for (int side = -1; side <= 1; side+=2)
                {
                    // A stable branched fracture grows outwards; its brightness
                    // flickers, but the crack itself does not jump every frame.
                    Vector2 end = p+new Vector2(side*width*(0.3f+travel*0.45f),0.12f);
                    Vector2 elbow = Vector2.Lerp(p,end,0.55f)+Vector2.up*0.16f;
                    Line(p,elbow,0.055f,fade); Line(elbow,end,0.04f,fade);
                    Line(elbow,elbow+new Vector2(side*width*0.16f,-0.1f),0.025f,fade);
                }
        }
        void HazardVolumes(float t, bool detonation)
        {
            foreach (var zone in zones)
            {
                float left = zone.x, right = zone.y, height = zone.z;
                float pulse = detonation ? Mathf.Exp(-t*7) : 0.5f+t*0.5f;
                var tint = Blue; tint.a = detonation ? pulse*0.16f : 0.025f+t*0.035f;
                Quad(new Vector2(left,height/2),new Vector2(right,height/2),height/2,tint);
                // Exact ceiling and sides; the player sees where feet must go.
                Line(new Vector2(left,height),new Vector2(right,height),0.035f,pulse*0.75f);
                Line(new Vector2(left,0),new Vector2(left,height),0.035f,pulse);
                Line(new Vector2(right,0),new Vector2(right,height),0.035f,pulse);
                for (float x = left+0.6f; x < right; x += 2.4f)
                {
                    float y = detonation ? height*(0.1f+0.85f*(1-Mathf.Pow(1-t,3))) : height*Mathf.Repeat(t*1.5f,1);
                    Line(new Vector2(x,y),new Vector2(x,y+Mathf.Min(0.35f,height-y)),0.035f,pulse*0.55f);
                }
            }
        }
        void FinalEnergy()
        {
            float t = Mathf.Clamp01(age/life);
            HazardVolumes(t,kind == Kind.Detonation);
            if (kind == Kind.Charge)
                foreach (var layer in chargedLayers) if (layer != null) layer.SetProgress(t);
            if (kind == Kind.Detonation)
            {
                // Compose a few broad blast fans inside the actual connected
                // zones, not dozens of identical landing-sized firecrackers.
                float travel=1-Mathf.Pow(1-t,3),peak=Mathf.Exp(-t*8);
                foreach(var zone in zones)
                {
                    float width=zone.y-zone.x,height=zone.z;float x=(zone.x+zone.y)/2;
                    Ring(new Vector2(x,height*0.45f),new Vector2(width*0.48f*(0.2f+travel*0.8f),height*0.42f*travel),0,peak,64);
                    Ring(new Vector2(x,0.13f),new Vector2(width*0.49f*(0.15f+travel*0.85f),0.15f+travel*0.65f),0,(1-t)*(1-t),48);
                    if(t>0.12f)Ring(new Vector2(x,height*0.32f),new Vector2(width*0.48f*travel,height*0.28f*travel),0,(1-t)*0.65f,48);
                    int clusters=Mathf.Max(1,Mathf.CeilToInt(width/16));
                    for(int c=0;c<clusters;c++)
                    {
                        float cx=zone.x+width*(c+0.5f)/clusters;
                        Vector2 foot=new Vector2(cx,0.12f);
                        for(int ray=-2;ray<=2;ray++)
                        {
                            Vector2 end=new Vector2(Mathf.Clamp(cx+ray*width/(clusters*6),zone.x+0.1f,zone.y-0.1f),height*(0.9f-Mathf.Abs(ray)*0.09f));
                            Vector2 axis=(end-foot).normalized;
                            SwordShard(foot,axis,(end-foot).magnitude*(0.8f+travel*0.2f),peak);
                            if(ray%2==0)Bolt(foot,end,0.12f,peak,c*37+ray+800);
                        }
                    }
                    for(int j=0;j<12;j++)
                    {
                        float u=(j+0.5f)/12;
                        Vector2 p=new Vector2(zone.x+width*u,height*(0.12f+Noise(j+700)*0.7f)*travel);
                        Vector2 axis=new Vector2((u-0.5f)*1.3f,0.65f).normalized;
                        SwordShard(p,axis,(0.6f+Noise(j+800)*0.8f)*(1-t),peak*0.8f);
                    }
                }
            }
            for (int i = 0; i < sites.Length; i++)
            {
                Vector2 p = new Vector2(sites[i].x,0.12f); float width = sites[i].z;
                if (kind == Kind.Detonation)
                {
                    if (i%4 == 0)
                        Bolt(p,new Vector2(p.x,sites[i].y*2),0.14f,Mathf.Exp(-t*6),i+1000);
                    continue;
                }
                float pulse = 0.5f + 0.5f * Mathf.Pow(0.5f+0.5f*Mathf.Sin(t*12+t*t*35),3);
                if (i > 0 && p.x-sites[i-1].x < 7)
                    Bolt(new Vector2(sites[i-1].x,2.4f+t*1.5f),new Vector2(p.x,2.4f+t*1.5f),0.08f+t*0.06f,pulse,i);
                Bolt(p+new Vector2(-width*0.3f,0.3f+t*0.6f),p+new Vector2(width*0.3f,0.2f),0.035f,pulse,i+100);
                float u = Mathf.Repeat(age*(2+t*4)+Noise(i),1);
                Line(p+Vector2.up*(1-u)*1.8f,p+Vector2.up*((1-u)*1.8f+0.18f),0.025f,t*0.6f);
            }
        }
        void Upload()
        {
            buffer.mesh.SetVertices(buffer.vertices); buffer.mesh.SetColors(buffer.colors);
            var bounds = new Bounds(new Vector3(KarmaStage.ArenaCenter,12,0),new Vector3(100,60,4));
            buffer.mesh.SetSubMesh(0,new UnityEngine.Rendering.SubMeshDescriptor(0,count*6) { bounds = bounds, vertexCount = count*4 },
                UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            buffer.mesh.bounds = bounds;
        }
        void LateUpdate()
        {
            if (game == null || game.Phase != RunPhase.Combat || buffer == null)
            { Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (kind == Kind.Summon || kind == Kind.Form || kind == Kind.Fall || kind == Kind.Scar || kind == Kind.WardFlight)
            {
                if (follow == null || !follow.gameObject.activeInHierarchy || rain != null && rain.Landed)
                { Destroy(gameObject); return; }
            }
            else if (age >= life && kind != Kind.Charge) { Destroy(gameObject); return; }
            count = 0;CapacityExceeded=false;
            if (kind == Kind.Summon) Summoning();
            else if (kind == Kind.Form || kind == Kind.Fall) BladeEnergy();
            else if(kind==Kind.Scar||kind==Kind.ScarWarning||kind==Kind.ScarFade)ScarEnergy();
            else if(kind==Kind.WardRelease||kind==Kind.WardImpact||kind==Kind.WardCollapse||kind==Kind.WardFlight)WardEnergy();
            else if (kind == Kind.Landing || kind == Kind.Eruption)
            {
                float t = Mathf.Clamp01(age/life);
                Burst(at,size.x,t,false,seed);
                if (kind == Kind.Eruption)
                    for (int side = -1; side <= 1; side+=2)
                        Shard(at+new Vector2(side*size.x*0.25f,0.1f),new Vector2(side*0.3f,1).normalized,
                            2.8f*(1-t)*(1-t),1-t);
            }
            else FinalEnergy();
            Upload();
        }
        void OnDisable()
        {
            if (filter != null) filter.sharedMesh = null;
            if (display != null) display.enabled = false;
            if (buffer == null) return;
            if (spare.Count < 72) spare.Add(buffer); else Object.Destroy(buffer.mesh);
            buffer = null;
        }
        void OnDestroy() { OnDisable(); }
    }
}
