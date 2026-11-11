using UnityEngine;

namespace KarmaPrototype
{
    // SW01/SW02 cosmetics only. Imported Sprite subassets supply every frame;
    // attack movement, contacts and lifetime remain owned by KarmaBossAttack.
    [DefaultExecutionOrder(360)]
    public sealed class KarmaSwordWaveAnimation : MonoBehaviour
    {
        enum Playback { Charge, Flight, Burst }
        const int FrameCount = 5;
        const float FlightFPS = 30;
        static readonly string[] Clips = { "Charge", "SW01Flight", "SW02Flight", "Impact" };
        static readonly Sprite[,] frames = new Sprite[4, FrameCount];
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer picture, swordPicture;
        Transform chargeScope;
        Playback playback;
        Vector2 size, velocity;
        float age, duration, nextEcho;
        int pattern, clip, first, count;
        bool heavy, aiming, terminal;
        KarmaSwordWaveEnergyPool energyPool;
        KarmaSwordWaveEnergyPool.Rig energy;
        KarmaSwordVfxPool echoes;

        static Sprite Frame(int clip, int frame)
        {
            var sprite = frames[clip, frame];
            if (sprite == null) sprite = KarmaArtwork.Sprite("SwordBossVFX", "SwordEnergy_" + Clips[clip] + "_" + frame);
            if (sprite == null) throw new System.InvalidOperationException("Missing SwordEnergy frame: " + Clips[clip] + "/" + frame);
            frames[clip, frame] = sprite;
            return sprite;
        }
        void Initialize(KarmaGame director, KarmaEnemy enemy, SpriteRenderer renderer)
        {
            game = director; owner = enemy; picture = renderer; pattern = enemy.CurrentPattern;
            // Preload before release, through the existing boss sprite library.
            for (int c = 0; c < Clips.Length; c++)
                for (int f = 0; f < FrameCount; f++) Frame(c, f);
            KarmaVisuals.ApplyMaterial(picture);
            picture.color = Color.white;
        }
        public static void Charge(KarmaGame game, KarmaEnemy owner, Transform art, Transform scope, float seconds, bool heavy, bool aiming = false)
        {
            var node = new GameObject("Sword energy charge frames / no damage");
            node.transform.SetParent(game.World, false);
            var animation = node.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, node.AddComponent<SpriteRenderer>());
            animation.playback = Playback.Charge; animation.duration = Mathf.Max(0.01f, seconds);
            animation.heavy = heavy; animation.aiming = aiming;
            animation.chargeScope = scope;
            var sword = art.Find("Guardian sprite");
            animation.swordPicture = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            animation.picture.sortingOrder = 27;
            animation.size = heavy ? new Vector2(2.6f, 3.5f) : new Vector2(1.7f, 2.5f);
            animation.ApplyCharge();
            animation.AttachEnergy(SwordEnergyPhase.Charge);
        }
        public static void Flight(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot, Vector2 velocity, Vector2 size)
        {
            var animation = shot.gameObject.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, shot.Visual);
            animation.playback = Playback.Flight; animation.velocity = velocity; animation.size = size;
            animation.heavy = owner.CurrentPattern == (int)EnemyAttack.SW02;
            animation.clip = animation.heavy ? 2 : 1;
            animation.echoes = KarmaSwordVfxPool.For(game);
            animation.ApplyFlight();
            animation.AttachEnergy(SwordEnergyPhase.Flight);
        }
        static KarmaSwordWaveAnimation Burst(KarmaGame game, KarmaEnemy owner, Vector2 at, Vector2 size, int first, int count, float seconds, bool flip, float angle = 0, bool heavy = false)
        {
            var node = new GameObject("Sword energy slash fragments / no damage");
            node.transform.SetParent(game.World, false); node.transform.position = at;
            node.transform.rotation = Quaternion.Euler(0, 0, angle);
            var animation = node.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, node.AddComponent<SpriteRenderer>());
            animation.playback = Playback.Burst; animation.size = size;
            animation.first = first; animation.count = count; animation.duration = seconds;
            animation.heavy = heavy;
            animation.picture.sortingOrder = 37; animation.picture.flipX = flip;
            animation.Paint(3, first, size, new Vector2(0.74f, 0.74f));
            animation.AttachEnergy(first == 2 ? SwordEnergyPhase.Dissipate : count == 5 ? SwordEnergyPhase.Impact : SwordEnergyPhase.Release);
            return animation;
        }
        public static void Release(KarmaGame game, KarmaEnemy owner, Vector2 at, Vector2 size, bool heavy, float facing)
        {
            var burst = Burst(game, owner, at, new Vector2(Mathf.Min(size.x, heavy ? 4.2f : 2.8f), Mathf.Min(size.y, heavy ? 4.7f : 3.2f)),
                0, 3, heavy ? 0.23f : 0.18f, facing < 0, heavy: heavy);
            var sword = owner.transform.Find("Artwork/Guardian sprite");
            var renderer = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            var bounds = renderer != null ? renderer.bounds : owner.DamageBounds;
            Vector2 anchor = bounds.center + new Vector3(facing * bounds.extents.x * 0.42f, 0.15f, 0);
            // A short flash at the rendered sword bridges to the real launch
            // lane. Opposite slash sweeps distinguish the low and high shots.
            burst.energy?.ConfigureRelease(anchor, heavy || at.y < 4 ? 1 : -1);
            burst.DrawEnergy(0);
        }
        public static void Impact(KarmaGame game, KarmaEnemy owner, Vector2 at, bool heavy = true)
        {
            Burst(game, owner, at, heavy ? new Vector2(3.2f, 3.8f) : new Vector2(2.1f, 2.7f),
                0, 5, heavy ? 0.22f : 0.18f, owner.Facing < 0, 18, heavy);
        }
        void AttachEnergy(SwordEnergyPhase phase)
        {
            energyPool = KarmaSwordWaveEnergyPool.For(game);
            energy = energyPool.Rent(owner, phase, heavy, aiming, size, picture.flipX ? -1 : 1,
                transform.position, picture.sortingLayerID, picture.sortingOrder + 1);
            DrawEnergy(0);
        }
        void DrawEnergy(float dt)
        {
            if (energy == null) return;
            var camera = game.GameCamera;
            float unitsPerPixel = camera != null && camera.orthographic && camera.pixelHeight > 0
                ? 2 * camera.orthographicSize / camera.pixelHeight : 0;
            energy.ConfigureVisual(picture.bounds, transform.position, unitsPerPixel);
            energy.Draw(transform.position, age, duration, dt);
        }
        void ReturnEnergy()
        {
            if (energyPool != null && energy != null) energyPool.Return(energy);
            energy = null;
        }
        void Paint(int sequence, int frame, Vector2 footprint, Vector2 coreFraction)
        {
            picture.sprite = Frame(sequence, frame);
            var bounds = picture.sprite.bounds.size;
            picture.transform.localScale = new Vector3(footprint.x / (bounds.x * coreFraction.x), footprint.y / (bounds.y * coreFraction.y), 1);
        }
        void ApplyFlight()
        {
            // A fixed footprint and pivot keep the cutting edge legible at the
            // existing fast speed; only the authored energy shapes change.
            Paint(clip, (int)(age * FlightFPS) % FrameCount, size,
                heavy ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f));
            picture.flipX = velocity.x < 0; picture.flipY = false;
            picture.transform.localRotation = Quaternion.identity;
        }
        void ApplyCharge()
        {
            float t = Mathf.Clamp01(age / duration);
            int frame = aiming ? Mathf.Min(2, (int)(t * 3)) : Mathf.Min(4, (heavy ? 1 : 0) + (int)(t * (heavy ? 4 : 5)));
            Paint(0, frame, size, new Vector2(0.72f, 0.82f));
            var bounds = swordPicture != null ? swordPicture.bounds : new Bounds(owner.Position, Vector3.one * 2);
            // Follow the rendered sword side after the existing pose animator.
            transform.position = bounds.center + new Vector3(owner.Facing * bounds.extents.x * 0.42f, 0.15f, 0);
            picture.flipX = owner.Facing < 0;
            picture.color = new Color(1, 1, 1, aiming ? Mathf.Lerp(0.45f, 0.7f, t) : Mathf.Lerp(0.65f, 1, t));
        }
        void LateUpdate()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned || game.Phase != RunPhase.Combat)
            { terminal = true; ReturnEnergy(); if (playback == Playback.Flight) enabled = false; else Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (playback == Playback.Charge)
            {
                if (age >= duration || owner.CurrentPattern != pattern || chargeScope == null || !chargeScope.gameObject.activeInHierarchy)
                { Destroy(gameObject); return; }
                ApplyCharge();
            }
            else if (playback == Playback.Flight)
            {
                ApplyFlight();
                if (age >= nextEcho)
                {
                    echoes.Echo(owner, picture, heavy ? 0.30f : 0.28f, heavy ? 0.05f : 0.04f, transform);
                    nextEcho = age + 0.035f;
                }
            }
            else
            {
                if (age >= duration) { Destroy(gameObject); return; }
                Paint(3, first + Mathf.Min(count - 1, (int)(age / duration * count)), size, new Vector2(0.74f, 0.74f));
            }
            DrawEnergy(Time.deltaTime);
        }
        void OnDisable()
        {
            ReturnEnergy();
            if (echoes != null) echoes.Cancel(transform);
            if (playback != Playback.Flight || terminal || game == null || owner == null || !owner.PresentationActive
                || owner.Stunned || game.Phase != RunPhase.Combat || !game.World.gameObject.activeInHierarchy) return;
            // Only an actual arena-boundary completion breaks into fragments.
            // Interrupting a projectile must not synthesize a hit or release.
            float edge = velocity.x > 0 ? KarmaStage.ArenaRight : KarmaStage.ArenaLeft;
            float endpoint = edge - (heavy ? Mathf.Sign(velocity.x) * size.x / 2 : 0);
            if (Mathf.Abs(transform.position.x - endpoint) > 0.05f) return;
            terminal = true;
            Vector2 at = transform.position;
            at.x = Mathf.Clamp(at.x, KarmaStage.ArenaLeft + size.x * 0.35f, KarmaStage.ArenaRight - size.x * 0.35f);
            Burst(game, owner, at, size * 0.7f, 2, 3, heavy ? 0.23f : 0.20f, velocity.x < 0, heavy: heavy);
        }
        void OnDestroy() { ReturnEnergy(); }
    }

    enum SwordEnergyPhase { Charge, Flight, Release, Impact, Dissipate }

    // Only SW01/SW02 rent these rigs. One additive draw per rig combines soft
    // blade-local lightning, directional cuts and a bounded spark simulation.
    // All buffers are allocated during the first charge, never during flight.
    [DefaultExecutionOrder(370)]
    sealed class KarmaSwordWaveEnergyPool : MonoBehaviour
    {
        const int Capacity = 8;
        readonly Rig[] rigs = new Rig[Capacity];
        KarmaGame game;
        uint serial;
        public static KarmaSwordWaveEnergyPool For(KarmaGame game)
        {
            var pool = game.World.GetComponent<KarmaSwordWaveEnergyPool>();
            if (pool != null) return pool;
            pool = game.World.gameObject.AddComponent<KarmaSwordWaveEnergyPool>();
            pool.game = game;
            for (int i = 0; i < Capacity; i++) pool.rigs[i] = new Rig(game.World, i);
            return pool;
        }
        public Rig Rent(KarmaEnemy owner, SwordEnergyPhase phase, bool heavy, bool aiming, Vector2 size,
            float facing, Vector3 at, int layer, int order)
        {
            foreach (var rig in rigs)
                if (!rig.Active)
                {
                    rig.Begin(owner, phase, heavy, aiming, size, facing, at, layer, order, ++serial);
                    return rig;
                }
            // Keep existing sprites readable if an unusually crowded frame
            // exhausts the cosmetic budget; never evict a live attack's cue.
            return null;
        }
        public void Return(Rig rig) { rig.Stop(); }
        void LateUpdate()
        {
            foreach (var rig in rigs)
                if (rig != null && rig.Active && (game == null || game.Phase != RunPhase.Combat
                    || rig.Owner == null || !rig.Owner.PresentationActive || rig.Owner.Stunned)) rig.Stop();
        }
        void OnDisable() { foreach (var rig in rigs) if (rig != null) rig.Stop(); }
        void OnDestroy() { foreach (var rig in rigs) if (rig != null) rig.Dispose(); }

        public sealed class Rig
        {
            const int Strokes = 96, Points = 13, Lanes = 4, MoteCount = 40;
            const int VerticesPerStroke = Points * Lanes;
            readonly Vector3[] vertices = new Vector3[Strokes * VerticesPerStroke];
            readonly Color[] colors = new Color[Strokes * VerticesPerStroke];
            readonly Vector2[] path = new Vector2[Points];
            readonly Mote[] motes = new Mote[MoteCount];
            readonly GameObject node;
            readonly Mesh mesh;
            readonly MeshRenderer renderer;
            SwordEnergyPhase phase;
            Vector2 size;
            Vector2 electricSize, electricOffset;
            float electricWidth;
            uint arcSeed;
            Vector2 swordOffset;
            Vector3 center;
            float facing, age, power, nextSpark, slashSign;
            bool heavy, aiming;
            int stroke, cursor;
            uint seed;
            public bool Active { get; private set; }
            public KarmaEnemy Owner { get; private set; }
            struct Mote { public Vector2 at, velocity; public float age, life, length, width, spin; }
            static readonly Color Cyan = new Color(0.14f, 1.1f, 2.0f, 1);
            static readonly Color White = new Color(1.25f, 1.7f, 2.1f, 1);

            public Rig(Transform root, int index)
            {
                node = new GameObject("SW01 SW02 pooled electricity " + index);
                node.transform.SetParent(root, false);
                mesh = new Mesh { name = "Sword energy ribbon buffer" }; mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.colors = colors;
                var indices = new int[Strokes * (Points - 1) * (Lanes - 1) * 6];
                int n = 0;
                for (int s = 0; s < Strokes; s++)
                    for (int p = 0; p < Points - 1; p++)
                        for (int lane = 0; lane < Lanes - 1; lane++)
                        {
                            int v = s * VerticesPerStroke + p * Lanes + lane;
                            indices[n++] = v; indices[n++] = v + Lanes; indices[n++] = v + 1;
                            indices[n++] = v + 1; indices[n++] = v + Lanes; indices[n++] = v + Lanes + 1;
                        }
                mesh.triangles = indices;
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(42, 34, 2));
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                node.SetActive(false);
            }
            public void Begin(KarmaEnemy owner, SwordEnergyPhase mode, bool strong, bool tracking, Vector2 footprint,
                float direction, Vector3 at, int layer, int order, uint serial)
            {
                Owner = owner; phase = mode; heavy = strong; aiming = tracking; size = footprint; facing = direction;
                seed = serial * 747796405u; arcSeed = seed; age = nextSpark = 0; cursor = 0; center = at;
                electricSize = size; electricOffset = Vector2.zero; electricWidth = size.y * 0.02f;
                swordOffset = Vector2.zero; slashSign = 1;
                System.Array.Clear(motes, 0, motes.Length);
                renderer.sortingLayerID = layer; renderer.sortingOrder = order;
                node.transform.position = at; node.SetActive(true); Active = true;
                if (phase == SwordEnergyPhase.Release || phase == SwordEnergyPhase.Impact || phase == SwordEnergyPhase.Dissipate)
                    Emit(phase == SwordEnergyPhase.Dissipate ? (heavy ? 2 : 1) : (heavy ? 4 : 2),
                        phase == SwordEnergyPhase.Dissipate ? 1.2f : heavy ? 2.6f : 2.0f);
            }
            public void ConfigureVisual(Bounds bounds, Vector3 origin, float unitsPerPixel)
            {
                // Measure the unchanged SpriteRenderer, including its parent scale.
                // Match Paint's authored core fractions; transparent margins are not blade width.
                if (phase == SwordEnergyPhase.Charge) return;
                Vector2 fraction = phase == SwordEnergyPhase.Flight
                    ? (heavy ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f)) : new Vector2(0.74f, 0.74f);
                electricSize = Vector2.Scale((Vector2)bounds.size, fraction);
                electricOffset = (Vector2)(bounds.center - origin);
                // Core spans 0.72 * width: target ~1.5/1.9 screen pixels,
                // capped to 3.5% of blade height at unusually distant zooms.
                electricWidth = Mathf.Min(electricSize.y * 0.035f,
                    Mathf.Max(electricSize.y * (heavy ? 0.020f : 0.016f), unitsPerPixel * (heavy ? 2.65f : 2.1f)));
            }
            float CrackleFlicker(int line, int point)
            {
                uint tick = (uint)(age * 12 + line * 0.37f);
                return Noise(arcSeed + (uint)(line * 199 + point * 31) + tick * 977) * 2 - 1;
            }
            Vector2 ElectricEdge(float t)
            {
                float y = t * 2 - 1;
                return electricOffset + new Vector2(facing * electricSize.x * (0.395f - 0.54f * y * y),
                    electricSize.y * y * 0.48f);
            }
            public void ConfigureRelease(Vector2 sword, float sweep)
            { swordOffset = sword - (Vector2)center; slashSign = sweep; }
            // A local hash supplies flicker and varied particles without consuming
            // UnityEngine.Random (which owns the independent SW01 attack order).
            static float Noise(uint value)
            {
                unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; }
                return (value & 0xffff) / 65535f;
            }
            float Flicker(int line, int point)
            { return Noise(seed + (uint)(line * 199 + point * 31 + (int)(age * (heavy ? 42 : 35)) * 977)) * 2 - 1; }
            float Sample() { return Noise(++seed); }
            public void Draw(Vector3 at, float seconds, float duration, float dt)
            {
                if (!Active) return;
                if (phase == SwordEnergyPhase.Charge && Owner != null) facing = Owner.Facing;
                center = at; age = seconds; node.transform.position = center;
                stroke = 0; System.Array.Clear(colors, 0, colors.Length);
                power = heavy ? 1 : 0.72f;
                if (phase == SwordEnergyPhase.Charge) Charge(Mathf.Clamp01(age / duration));
                else if (phase == SwordEnergyPhase.Flight) Flight();
                else Burst(Mathf.Clamp01(age / duration));
                DrawMotes(dt);
                mesh.SetVertices(vertices, 0, vertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetColors(colors);
                // The fixed buffers are reused; only occupied ribbons are drawn.
                mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, stroke * (Points - 1) * (Lanes - 1) * 6)
                    { firstVertex = 0, vertexCount = vertices.Length, bounds = new Bounds(Vector3.zero, new Vector3(42, 34, 2)) },
                    UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
                // Explicit bounds avoid a scan over invisible buffer slots.
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(42, 34, 2));
            }
            void Ribbon(float width, Color color, bool angular = false, float core = 0.16f)
            {
                if (stroke >= Strokes) return;
                int start = stroke++ * VerticesPerStroke;
                for (int p = 0; p < Points; p++)
                {
                    Vector2 tangent = path[Mathf.Min(p + 1, Points - 1)] - path[Mathf.Max(0, p - 1)];
                    float t = p / (float)(Points - 1);
                    Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * width
                        * (angular ? 1 - Mathf.Abs(t * 2 - 1) : 0.18f + Mathf.Sin(t * Mathf.PI) * 0.82f);
                    float fade = Mathf.Sin(t * Mathf.PI);
                    for (int lane = 0; lane < Lanes; lane++)
                    {
                        float offset = lane == 0 ? -1 : lane == 1 ? -core : lane == 2 ? core : 1;
                        int v = start + p * Lanes + lane;
                        vertices[v] = path[p] + normal * offset;
                        colors[v] = new Color(color.r, color.g, color.b, lane == 0 || lane == 3 ? 0 : color.a * fade);
                    }
                }
            }
            void Segment(Vector2 from, Vector2 to, float width, Color color, float jitter = 0, int key = 0, bool angular = false)
            {
                Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
                for (int p = 0; p < Points; p++)
                {
                    float t = p / (float)(Points - 1);
                    path[p] = Vector2.Lerp(from, to, t) + normal * (Flicker(key, p) * jitter * Mathf.Sin(t * Mathf.PI));
                }
                Ribbon(width, color, angular);
            }
            static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
            void ElectricRibbon(float width, float alpha)
            {
                // A broad azure sheath and a separate crisp white cutting core.
                // Their silhouettes taper independently instead of flattening
                // the sprite into one uniformly overexposed glow.
                Ribbon(width * 2.4f, Alpha(Cyan, alpha * 0.48f));
                Ribbon(width * 0.62f, Alpha(White, alpha));
            }
            void Lightning(Vector2 from, Vector2 to, float width, float alpha, float irregularity, int key)
            {
                Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
                for (int p = 0; p < Points; p++)
                {
                    float t = p / (float)(Points - 1);
                    float kink = Flicker(key, p) * irregularity + Mathf.Sin(t * 14 + age * 23 + key) * irregularity * 0.3f;
                    path[p] = Vector2.Lerp(from, to, t) + normal * (kink * Mathf.Sin(t * Mathf.PI));
                }
                ElectricRibbon(width, alpha);
            }
            void BladeBolt(Vector2 from, Vector2 to, float width, float alpha, float kink, int key)
            {
                Vector2 delta = to - from;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
                // Four straight sections produce hard electrical corners. No
                // sinusoidal spine or delayed world-space tail follows the blade.
                for (int p = 0; p < Points; p++)
                {
                    float joint = p / (float)(Points - 1) * 4;
                    int a = Mathf.Min((int)joint, 3), b = a + 1;
                    Vector2 start = from + delta * (a * 0.25f)
                        + normal * (a == 0 ? 0 : CrackleFlicker(key, a) * kink);
                    Vector2 end = from + delta * (b * 0.25f)
                        + normal * (b == 4 ? 0 : CrackleFlicker(key, b) * kink);
                    path[p] = Vector2.Lerp(start, end, joint - a);
                }
                // One restrained cyan fringe and a screen-readable white-blue core.
                Ribbon(width * 1.6f, Alpha(new Color(0.10f, 0.85f, 1.15f), alpha * 0.32f));
                Ribbon(width * 0.8f, Alpha(new Color(0.85f, 1.1f, 1.3f), alpha), core: 0.45f);
            }
            void Crescent(Vector2 offset, Vector2 footprint, float width, float alpha, bool white)
            {
                for (int p = 0; p < Points; p++)
                {
                    float y = p / (float)(Points - 1) * 2 - 1;
                    path[p] = offset + new Vector2(facing * footprint.x * (0.37f - 0.54f * y * y), footprint.y * y * 0.48f);
                }
                if (white) ElectricRibbon(width, alpha);
                else Ribbon(width, Alpha(Cyan, alpha));
            }
            void Glint(Vector2 at, float reach, float alpha)
            {
                Segment(at - Vector2.right * reach, at + Vector2.right * reach, 0.24f, Alpha(Cyan, alpha * 0.5f));
                Segment(at - Vector2.right * reach, at + Vector2.right * reach, 0.08f, Alpha(White, alpha));
                Segment(at - Vector2.up * reach * 0.7f, at + Vector2.up * reach * 0.7f, 0.16f, Alpha(Cyan, alpha * 0.5f));
                Segment(at - Vector2.up * reach * 0.7f, at + Vector2.up * reach * 0.7f, 0.06f, Alpha(White, alpha));
            }
            Vector2 Edge(float t)
            {
                float y = t * 2 - 1;
                return new Vector2(facing * size.x * (0.37f - 0.54f * y * y), size.y * y * 0.48f);
            }
            void Charge(float t)
            {
                // Orbiting convergence becomes a compressed white spine at lock.
                // Long irregular bolts, motes and rings have separate clocks.
                float focus = aiming ? 0.58f + t * 0.22f : 0.55f + t * 0.45f;
                float climax = aiming ? 0 : Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.72f) / 0.28f));
                Vector2 bottom = new Vector2(-facing * 0.22f, -size.y * 0.34f);
                Vector2 tip = new Vector2(facing * 0.34f, size.y * 0.40f);
                Lightning(bottom, tip, heavy ? 0.17f : 0.10f, focus * power, 0.07f, 0);
                int arcs = heavy ? 6 : 4;
                for (int a = 0; a < arcs; a++)
                {
                    float angle = a * Mathf.PI * 2 / arcs + age * (a % 2 == 0 ? 2.1f : -1.7f);
                    float radius = (heavy ? 3.5f : 2.1f) * (1 - climax * 0.4f) * (0.9f + 0.16f * Flicker(a, 0));
                    Vector2 outer = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f) * radius;
                    float u = Mathf.Repeat(age * 2.8f + a * 0.31f, 1);
                    Vector2 target = Vector2.Lerp(bottom, tip, u);
                    Lightning(outer, target, heavy ? 0.15f : 0.10f, focus * power * 0.85f, heavy ? 0.48f : 0.32f, a + 1);
                }
                int particles = heavy ? 22 : 14;
                for (int i = 0; i < particles; i++)
                {
                    float u = Mathf.Repeat(age * (heavy ? 3.4f : 2.4f) + Noise((uint)i * 73 + seed), 1);
                    float angle = i * 2.39996f + age * 2.6f + u * 1.4f;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f);
                    Vector2 end = axis * ((1 - u) * (heavy ? 4.1f : 2.65f));
                    Segment(end + axis * (0.3f + u * 0.4f), end, heavy ? 0.09f : 0.065f,
                        Alpha(i % 3 == 0 ? White : Cyan, Mathf.Sin(u * Mathf.PI) * focus));
                }
                float pulse = 1 + Mathf.Sin(age * (heavy ? 34 : 26)) * 0.10f;
                Ring((heavy ? Mathf.Lerp(2.25f, 1.1f, t) : 1.4f) * pulse, 0.42f,
                    age * 4, focus * 0.8f, heavy ? 0.12f : 0.085f, 2);
                if (heavy)
                {
                    Ring(Mathf.Lerp(1.5f, 2.4f, t) * pulse, 0.76f, -age * 5, focus * 0.6f, 0.09f, 2);
                    Glint(tip * 0.25f, 1.1f + climax, climax * (0.75f + 0.25f * Mathf.Sin(age * 70)));
                }
            }
            void Flight()
            {
                // Keep the authored crescent and white cutting edge; every extra
                // bolt, chip and speed cut stays close to that silhouette.
                Crescent(Vector2.zero, size, 0.11f, power * 0.95f, true);
                if (!heavy) StaggeredMantle();
                // Three/four staggered branches remain visible while individual
                // kinks change at 12 Hz. Never gate the entire electrical edge off.
                int arcs = heavy ? 4 : 3;
                float ignition = 1 + Mathf.Clamp01(1 - age / 0.10f) * 0.20f;
                for (int a = 0; a < arcs; a++)
                {
                    float span = heavy ? 0.14f : 0.105f;
                    float head = 0.08f + a * (0.84f - span) / (arcs - 1);
                    float pulse = 0.70f + 0.30f * Mathf.Sin(age * (15 + a * 1.7f) + a * 2.1f + Noise(arcSeed) * 6);
                    BladeBolt(ElectricEdge(head), ElectricEdge(head + span), electricWidth,
                        Mathf.Min(1, (heavy ? 0.95f : 0.80f) * pulse * ignition), electricSize.y * (heavy ? 0.018f : 0.013f), a + 1);
                }
                if (heavy)
                {
                    // A compact blade-local tail, no world-space history or sag.
                    Vector2 tail = ElectricEdge(0.48f);
                    BladeBolt(tail, tail + new Vector2(-facing * electricSize.x * 0.16f, electricSize.y * 0.025f),
                        electricWidth * 0.7f, 0.48f, electricSize.y * 0.012f, 17);
                }
                // Short horizontal blades flash in place along the moving wave.
                // They have no history buffer, curve, sag or expanding ghost arc.
                int cuts = heavy ? 8 : 5;
                uint tick = (uint)(age * 22);
                for (int a = 0; a < cuts; a++)
                {
                    uint key = seed + (uint)a * 131 + tick * 977;
                    float height = 0.08f + Noise(key) * 0.84f;
                    Vector2 tip = Edge(height) - Vector2.right * facing * size.x * 0.06f;
                    float length = heavy ? 0.7f + Noise(key + 7) * 0.6f : 0.45f + Noise(key + 7) * 0.45f;
                    for (int p = 0; p < Points; p++)
                    {
                        float t = p / (float)(Points - 1);
                        path[p] = tip - Vector2.right * facing * length * (1 - t);
                    }
                    float pulse = 0.65f + 0.35f * Mathf.Sin(Mathf.Repeat(age * 18 + a * 0.17f, 1) * Mathf.PI);
                    ElectricRibbon(heavy ? 0.16f : 0.115f, power * pulse);
                }
                if (age >= nextSpark)
                {
                    Emit(heavy ? 2 : 1, heavy ? 2.2f : 1.6f);
                    nextSpark = age + (heavy ? 0.075f : 0.11f) + Sample() * 0.035f;
                }
            }
            void StaggeredMantle()
            {
                // SW01 only: a 25% larger, broken cyan mantle adds presence.
                // The opaque sprite and white cutting spine keep their original
                // hitbox-sized footprint. No white edge claims the outer reach.
                Vector2 outer = size * new Vector2(1.2f, 1.25f);
                for (int a = 0; a < 5; a++)
                {
                    for (int p = 0; p < Points; p++)
                    {
                        float t = (a + 0.08f + p / (float)(Points - 1) * 0.76f) / 5;
                        float y = t * 2 - 1;
                        path[p] = new Vector2(facing * outer.x * (0.37f - 0.54f * y * y), outer.y * y * 0.48f);
                    }
                    Ribbon(0.22f, Alpha(Cyan, 0.64f + 0.10f * Flicker(a + 70, 0)));
                }
            }
            void Ring(float radius, float flatten, float rotation, float alpha, float width, int pieces, bool white = false)
            {
                for (int a = 0; a < pieces; a++)
                {
                    for (int p = 0; p < Points; p++)
                    {
                        float angle = rotation + a * Mathf.PI * 2 / pieces + p / (float)(Points - 1) * (pieces == 1 ? 5.4f : 2.4f);
                        float r = radius * (1 + Flicker(a + 21, p) * 0.055f);
                        path[p] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * flatten);
                    }
                    if (white) ElectricRibbon(width, alpha);
                    else Ribbon(width, Alpha(Cyan, alpha));
                }
            }
            void Burst(float t)
            {
                bool hit = phase == SwordEnergyPhase.Impact, dissolve = phase == SwordEnergyPhase.Dissipate;
                // A two-frame white peak, then faster azure expansion and a short
                // fragment tail. Cosmetic time never feeds the attack coroutine.
                float fade = Mathf.Pow(1 - t, 1.5f);
                float flash = Mathf.Clamp01(1 - age / (heavy ? 0.065f : 0.05f));
                if (!dissolve)
                {
                    int rings = heavy ? 2 : 1;
                    for (int a = 0; a < rings; a++)
                    {
                        float u = Mathf.Clamp01((t - a * 0.14f) / (1 - a * 0.14f));
                        float radius = Mathf.Lerp(0.3f, heavy ? 4.3f : 2.8f, Mathf.Sqrt(u));
                        Ring(radius, a == 0 ? 0.66f : 0.95f, -age * 6 + a * 1.4f,
                            (1 - u) * fade * power, heavy ? 0.14f : 0.10f, 3, a == 0);
                    }
                    Glint(hit ? Vector2.zero : swordOffset, heavy ? 3.2f : 2.2f, flash);
                    // Staggered razor streaks fan outward at different speeds.
                    // SW01's low/high lanes sweep in opposite vertical directions.
                    int slashes = heavy ? 5 : 3;
                    for (int a = 0; a < slashes; a++)
                    {
                        float u = Mathf.Clamp01((t - a * 0.055f) / (1 - a * 0.055f));
                        float angle = hit ? 0.92f + (a - slashes / 2) * 0.47f
                            : slashSign * (a - slashes / 2) * 0.17f;
                        Vector2 axis = new Vector2(facing * Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector2 offset = new Vector2(facing * u * (heavy ? 1.5f : 0.8f), (a - slashes / 2) * (hit ? 0.16f : 0.28f));
                        float length = (heavy ? 5.5f : 4.4f) * (0.35f + Mathf.Sqrt(u) * 0.65f);
                        for (int p = 0; p < Points; p++)
                        {
                            float v = p / (float)(Points - 1);
                            path[p] = offset + axis * Mathf.Lerp(-length * 0.45f, length, v)
                                + Vector2.up * (Mathf.Sin(v * Mathf.PI) * (hit ? 0.2f : slashSign * 0.45f));
                        }
                        ElectricRibbon((heavy ? 0.19f : 0.13f) * (1 - u), fade * (1 - u * 0.5f));
                    }
                    if (!hit)
                    {
                        // Broad forward crescents peel off the actual launch
                        // point; the sword flash remains anchored above the lane.
                        for (int a = 0; a < (heavy ? 2 : 1); a++)
                        {
                            float u = Mathf.Clamp01((t - a * 0.12f) / (1 - a * 0.12f));
                            Crescent(new Vector2(facing * u * (heavy ? 3.8f : 2.4f), 0),
                                new Vector2(heavy ? 3.0f : 2.1f, (heavy ? 6.8f : 4.0f) * (0.6f + u * 0.4f)),
                                heavy ? 0.23f : 0.16f, fade * (1 - u * 0.4f), true);
                        }
                        if (flash > 0)
                            BladeBolt(electricOffset, electricOffset + new Vector2(facing * electricSize.x * 0.22f, electricSize.y * 0.12f),
                                electricWidth, flash * (heavy ? 1.0f : 0.85f), electricSize.y * 0.025f, 51);
                    }
                }
                else
                {
                    Ring(Mathf.Lerp(0.45f, heavy ? 2.8f : 2.0f, Mathf.Sqrt(t)), 0.72f, age * 5,
                        fade * power, heavy ? 0.14f : 0.10f, 3);
                }
                // Confirmed impact gets a compact sharp discharge; expiry has
                // only a small after-spark. Existing contact routing owns the outcome.
                int arcs = dissolve ? (heavy ? 2 : 1) : (heavy ? 5 : 3);
                for (int a = 0; a < arcs; a++)
                {
                    float angle = a * 2.39996f + Noise(seed + (uint)a) * 0.8f;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float radius = Mathf.Min(electricSize.x, electricSize.y) * (0.12f + Mathf.Sqrt(t) * (heavy ? 0.25f : 0.20f))
                        * (dissolve ? 0.45f : 1);
                    BladeBolt(electricOffset + axis * radius * 0.25f, electricOffset + axis * radius,
                        electricWidth * (1 - t) * (dissolve ? 0.6f : 1),
                        fade * (heavy ? 1.0f : 0.85f) * (dissolve ? 0.55f : 1), radius * 0.16f * (1 - t), a + 30);
                }
            }
            void Emit(int count, float speed)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = Sample() * Mathf.PI * 2;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    bool flying = phase == SwordEnergyPhase.Flight;
                    // Flight chips use blade-local coordinates and a very short
                    // life, so rapid projectile travel cannot stretch them into
                    // a detached stream. Impact fragments remain in world space.
                    Vector2 at = flying ? ElectricEdge(0.08f + Sample() * 0.84f) : (Vector2)center + axis * 0.18f;
                    Vector2 velocity = flying ? new Vector2(-facing * (0.3f + Sample() * speed * 0.35f), axis.y * speed * 0.18f)
                        : axis * speed * (0.35f + Sample() * 0.65f);
                    motes[cursor] = new Mote { at = at, velocity = velocity, life = flying ? 0.11f + Sample() * 0.045f : 0.10f + Sample() * 0.06f,
                        length = size.y * (0.035f + Sample() * (heavy ? 0.035f : 0.025f)),
                        width = heavy ? 0.8f : 0.65f, spin = (Sample() - 0.5f) * 14 };
                    cursor = (cursor + 1) % MoteCount;
                }
            }
            void DrawMotes(float dt)
            {
                for (int i = 0; i < motes.Length; i++)
                {
                    ref var mote = ref motes[i];
                    if (mote.life <= 0 || mote.age >= mote.life) continue;
                    mote.age += dt; mote.at += mote.velocity * dt; mote.velocity *= Mathf.Exp(-dt * 3);
                    float fade = Mathf.Clamp01(1 - mote.age / mote.life);
                    bool flying = phase == SwordEnergyPhase.Flight;
                    Vector2 at = flying ? mote.at : mote.at - (Vector2)center;
                    Vector2 axis = mote.velocity.normalized;
                    if (i % 3 == 0)
                    {
                        float angle = mote.age * mote.spin;
                        axis = new Vector2(axis.x * Mathf.Cos(angle) - axis.y * Mathf.Sin(angle),
                            axis.x * Mathf.Sin(angle) + axis.y * Mathf.Cos(angle));
                    }
                    BladeBolt(at, at - axis * mote.length * fade, electricWidth * mote.width * fade,
                        fade * (heavy ? 0.85f : 0.70f), mote.length * 0.12f, i + 80);
                }
            }
            public void Stop() { Active = false; Owner = null; if (node != null) node.SetActive(false); }
            public void Dispose() { if (mesh != null) Object.Destroy(mesh); if (node != null) Object.Destroy(node); }
        }
    }
}
