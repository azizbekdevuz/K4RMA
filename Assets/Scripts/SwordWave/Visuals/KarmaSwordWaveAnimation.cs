using UnityEngine;

namespace KarmaPrototype
{
    // SW01/SW02 cosmetics only. Imported Sprite subassets supply every frame;
    // attack movement, contacts and lifetime remain owned by KarmaBossAttack.
    [DefaultExecutionOrder(360)]
    public sealed class KarmaSwordWaveAnimation : MonoBehaviour
    {
        enum Playback { Charge, Flight, Burst, ChargeRelease }
        const int FrameCount = 5;
        static readonly int[] FlightDrawings = { 0, 3 };
        static readonly string[] Clips = { "Charge", "SW01Flight", "SW02Flight", "Impact" };
        static readonly Sprite[,] frames = new Sprite[4, FrameCount];
        static readonly System.Collections.Generic.Dictionary<KarmaEnemy, KarmaSwordWaveAnimation> charges =
            new System.Collections.Generic.Dictionary<KarmaEnemy, KarmaSwordWaveAnimation>();
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer picture, swordPicture;
        KarmaCharacterAnimator swordAnimator;
        Transform chargeScope;
        Playback playback;
        Vector2 size, velocity;
        float age, duration, nextEcho, chargeBodyHeight;
        int pattern, clip, first, count;
        bool heavy, aiming, terminal, basicRanged;
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
        public static void Charge(KarmaGame game, KarmaEnemy owner, Transform art, Transform scope, float seconds, bool heavy, bool aiming = false, bool basicRanged = false)
        {
            // Explicit ranged mode: sharing SW01 flight art never grants a charge.
            if (owner.CurrentPattern != (int)EnemyAttack.SW01 && owner.CurrentPattern != (int)EnemyAttack.SW02
                && !(owner.CurrentPattern == (int)EnemyAttack.SWBasic && basicRanged)) return;
            StopCharge(owner);
            var node = new GameObject("Sword energy charge frames / no damage");
            node.transform.SetParent(game.World, false);
            var animation = node.AddComponent<KarmaSwordWaveAnimation>();
            animation.Initialize(game, owner, node.AddComponent<SpriteRenderer>());
            animation.playback = Playback.Charge; animation.duration = Mathf.Max(0.01f, seconds);
            animation.heavy = heavy; animation.aiming = aiming;
            animation.basicRanged = owner.CurrentPattern == (int)EnemyAttack.SWBasic && basicRanged;
            animation.chargeScope = scope;
            animation.chargeBodyHeight = Mathf.Max(2, owner.DamageBounds.size.y);
            charges[owner] = animation;
            var sword = art.Find("Guardian sprite");
            animation.swordPicture = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            animation.swordAnimator = sword != null ? sword.GetComponentInParent<KarmaCharacterAnimator>() : null;
            if (animation.swordAnimator != null) animation.chargeBodyHeight = animation.swordAnimator.SwordChargeBodyHeight;
            animation.picture.sortingLayerID = animation.swordPicture != null ? animation.swordPicture.sortingLayerID : 0;
            animation.picture.sortingOrder = animation.swordPicture != null ? animation.swordPicture.sortingOrder + 1 : 27;
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
            if (animation.pattern != (int)EnemyAttack.SWBasic) animation.AttachEnergy(SwordEnergyPhase.Flight);
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
            if (owner.CurrentPattern == (int)EnemyAttack.SW01 || owner.CurrentPattern == (int)EnemyAttack.SW02
                || (owner.CurrentPattern == (int)EnemyAttack.SWBasic && charges.TryGetValue(owner, out var charge) && charge.basicRanged))
                ReleaseCharge(owner, facing);
            else StopCharge(owner);
            bool basicRelease = owner.CurrentPattern == (int)EnemyAttack.SWBasic;
            var burst = Burst(game, owner, at, new Vector2(Mathf.Min(size.x, heavy ? 4.2f : 2.8f), Mathf.Min(size.y, heavy ? 4.7f : 3.2f)),
                0, 3, heavy ? 0.23f : 0.18f, facing < 0, heavy: heavy);
            var sword = owner.transform.Find("Artwork/Guardian sprite");
            var renderer = sword != null ? sword.GetComponent<SpriteRenderer>() : null;
            var bounds = renderer != null ? renderer.bounds : owner.DamageBounds;
            bool bladeCharge = owner.CurrentPattern == (int)EnemyAttack.SW01 || owner.CurrentPattern == (int)EnemyAttack.SW02;
            Vector2 anchor = BladePoints(renderer, out _, out var bend, out var tip) ? (bladeCharge ? tip : bend)
                : (Vector2)bounds.center + new Vector2(facing * bounds.extents.x * 0.42f, 0.15f);
            // A short flash at the rendered sword bridges to the real launch
            // lane. Opposite slash sweeps distinguish the low and high shots.
            burst.energy?.ConfigureRelease(basicRelease ? at : anchor, heavy || at.y < 4 ? 1 : -1);
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
            if (playback == Playback.Charge || playback == Playback.ChargeRelease)
            {
                if (!BladePoints(swordPicture, out var heel, out var bend, out var tip))
                {
                    heel = bend = tip = (Vector2)transform.position;
                }
                energy.ConfigureBlade(heel - (Vector2)transform.position, bend - (Vector2)transform.position,
                    tip - (Vector2)transform.position, unitsPerPixel, chargeBodyHeight, basicRanged, Mathf.Max(0, duration - age));
            }
            else energy.ConfigureVisual(picture.bounds, transform.position, unitsPerPixel);
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
        bool basicVisualOrigin;
        internal static Vector2 BasicTorsoOrigin(Vector2 sole, float height, float facing)
            => sole + new Vector2(Mathf.Sign(facing) * height * 0.18f, height * 0.55f);
        internal static Vector2 BasicLaunchOrigin(KarmaEnemy owner, Vector2 from, float facing)
        {
            var art = owner.transform.Find("Artwork");
            var animator = art != null ? art.GetComponent<KarmaCharacterAnimator>() : null;
            if (animator == null) return from;
            Vector2 sole = art.TransformPoint(animator.SwordVisualHome + animator.SwordFoot);
            Vector2 origin = BasicTorsoOrigin(sole, animator.SwordChargeBodyHeight, facing) + from - owner.Position;
            origin.x = Mathf.Clamp(origin.x, KarmaStage.ArenaLeft, KarmaStage.ArenaRight);
            return origin;
        }
        internal static void AlignBasicVisual(KarmaBossAttack shot)
        {
            var animation = shot.GetComponent<KarmaSwordWaveAnimation>();
            if (animation == null || animation.pattern != (int)EnemyAttack.SWBasic) return;
            animation.basicVisualOrigin = true;
            animation.ApplyBasicVisualOrigin();
        }
        void ApplyBasicVisualOrigin()
        {
            if (!basicVisualOrigin) return;
            // Warning, aim and collision now share the waist launch root.
            picture.transform.position = transform.position;
        }
        void ApplyFlight()
        {
            // A fixed footprint and pivot keep the cutting edge legible at the
            // existing fast speed; only the authored energy shapes change.
            Paint(clip, FlightDrawings[(int)(age * KarmaBootstrap.FramesPerSecond) % FlightDrawings.Length], heavy ? size : new Vector2(size.x * 1.12f, size.y),
                heavy ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f));
            picture.flipX = velocity.x < 0; picture.flipY = false;
            if (pattern == (int)EnemyAttack.SWBasic && velocity.sqrMagnitude > 0.000001f)
                picture.transform.rotation = Quaternion.Euler(0, 0, BasicFlightAngle(velocity));
            else picture.transform.localRotation = Quaternion.identity;
            ApplyBasicVisualOrigin();
        }
        internal static float BasicFlightAngle(Vector2 direction)
            => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - (direction.x < 0 ? 180 : 0);
        void ApplyCharge()
        {
            // No floating charge sprite: only the mesh sheath on the actual blade.
            picture.enabled = false;
            if (BladePoints(swordPicture, out var heel, out _, out _)) transform.position = heel;
        }

        // Endpoints are measured on the displayed 96-unit cell, before sprite and parent mirroring.
        static System.Collections.Generic.Dictionary<string, Vector4> chargeBladeSockets;
        static bool TryBladeEndpoints(string key, out Vector4 endpoints)
        {
            if (chargeBladeSockets == null)
            {
                chargeBladeSockets = new System.Collections.Generic.Dictionary<string, Vector4>();
                foreach (var line in Resources.Load<TextAsset>("Motions12/BladeSockets").text.Split(new[] {'\n'}, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = line.Split('\t');
                    chargeBladeSockets.Add(f[0], new Vector4(int.Parse(f[1]), int.Parse(f[2]), int.Parse(f[3]), int.Parse(f[4])));
                }
            }
            return chargeBladeSockets.TryGetValue(key, out endpoints);
        }
        static Vector2 ChargeBladePoint(SpriteRenderer renderer, Vector2 socket)
        {
            var sprite = renderer.sprite;
            var pixel = new Vector2(socket.x / 96 * sprite.rect.width, (1 - socket.y / 96) * sprite.rect.height);
            Vector2 local = (pixel - sprite.pivot) / sprite.pixelsPerUnit;
            if (renderer.flipX) local.x = -local.x;
            if (renderer.flipY) local.y = -local.y;
            return renderer.transform.TransformPoint(local);
        }

        internal static bool BladePoints(SpriteRenderer renderer, out Vector2 heel, out Vector2 bend, out Vector2 tip)
        {
            heel = bend = tip = default;
            if (renderer == null || renderer.sprite == null) return false;
            var sprite = renderer.sprite;
            var animator = renderer.GetComponentInParent<KarmaCharacterAnimator>();
            if (animator == null || !TryBladeEndpoints(animator.MotionForm + "/" + sprite.name, out var ends)) return false;
            heel = ChargeBladePoint(renderer, new Vector2(ends.x, ends.y));
            tip = ChargeBladePoint(renderer, new Vector2(ends.z, ends.w));
            bend = Vector2.Lerp(heel, tip, 0.5f);
            return true;
        }

        void LateUpdate()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned || game.Phase != RunPhase.Combat)
            { terminal = true; ReturnEnergy(); if (playback == Playback.Flight) enabled = false; else Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (playback == Playback.Charge)
            {
                if (owner.CurrentPattern != pattern || chargeScope == null || !chargeScope.gameObject.activeInHierarchy)
                { ReturnEnergy(); gameObject.SetActive(false); Destroy(gameObject); return; }
                ApplyCharge();
            }
            else if (playback == Playback.ChargeRelease)
            {
                if (owner.CurrentPattern != pattern || age >= duration
                    || !basicRanged && swordAnimator != null && swordAnimator.SwordBodyAction == null)
                { ReturnEnergy(); gameObject.SetActive(false); Destroy(gameObject); return; }
                ApplyCharge();
            }
            else if (playback == Playback.Flight)
            {
                ApplyFlight();
                if (age >= nextEcho)
                {
                    echoes.Echo(owner, picture, pattern == (int)EnemyAttack.SWBasic ? 0.22f : heavy ? 0.38f : 0.28f, heavy ? 0.11f : 0.075f, transform);
                    // Softer echoes leave the leading blade dominant.
                    nextEcho = age + 1f / 24;
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
        static void ReleaseCharge(KarmaEnemy owner, float facing)
        {
            if (!charges.TryGetValue(owner, out var charge)) return;
            charges.Remove(owner);
            if (charge == null) return;
            // Reuse this charge's rig and buffers. Only cosmetic time continues;
            // the projectile is already released on the unchanged attack event.
            charge.playback = Playback.ChargeRelease;
            charge.age = 0; charge.duration = charge.heavy ? 0.20f : 0.16f;
            charge.chargeScope = null;
            charge.energy?.BeginChargeRelease(facing);
            charge.ApplyCharge(); charge.DrawEnergy(0);
        }
        static void StopCharge(KarmaEnemy owner)
        {
            if (!charges.TryGetValue(owner, out var charge)) return;
            charges.Remove(owner);
            if (charge == null) return;
            charge.gameObject.SetActive(false);
            Destroy(charge.gameObject);
        }
        void OnDestroy()
        {
            ReturnEnergy();
            if (owner != null && charges.TryGetValue(owner, out var charge) && charge == this) charges.Remove(owner);
        }
    }

    enum SwordEnergyPhase { Charge, Flight, Release, Impact, Dissipate, ChargeRelease }

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
            readonly MaterialPropertyBlock chargeMaterialProperties;
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
                // The charge points are already mirrored in world space by the live sprite transform.
                // This mesh shares a sprite shader, so make its own shader flip neutral.
                chargeMaterialProperties = new MaterialPropertyBlock();
                chargeMaterialProperties.SetVector("unity_SpriteProps", new Vector4(1, 1, 0, 0));
                chargeMaterialProperties.SetColor("unity_SpriteColor", Color.white);
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
                renderer.SetPropertyBlock(phase == SwordEnergyPhase.Charge ? chargeMaterialProperties : null);
                node.transform.position = at; node.SetActive(true); Active = true;
                if (phase == SwordEnergyPhase.Release || phase == SwordEnergyPhase.Impact || phase == SwordEnergyPhase.Dissipate)
                    Emit(phase == SwordEnergyPhase.Dissipate ? (heavy ? 16 : 9) : (heavy ? 18 : 11),
                        phase == SwordEnergyPhase.Dissipate ? 2.8f : heavy ? 4.2f : 3.2f);
            }
            public void ConfigureVisual(Bounds bounds, Vector3 origin, float unitsPerPixel)
            {
                // Measure the unchanged SpriteRenderer, including its parent scale.
                // Match Paint's authored core fractions; transparent margins are not blade width.
                if (phase == SwordEnergyPhase.Charge || phase == SwordEnergyPhase.ChargeRelease) return;
                Vector2 fraction = phase == SwordEnergyPhase.Flight
                    ? (heavy ? new Vector2(0.83f, 0.86f) : new Vector2(0.65f, 0.81f)) : new Vector2(0.74f, 0.74f);
                electricSize = Vector2.Scale((Vector2)bounds.size, fraction);
                electricOffset = (Vector2)(bounds.center - origin);
                // Screen-readable electrical edges, bounded by the existing blade.
                electricWidth = Mathf.Min(electricSize.y * 0.05f,
                    Mathf.Max(electricSize.y * (heavy ? 0.030f : 0.024f), unitsPerPixel * (heavy ? 3.5f : 2.8f)));
            }
            float CrackleFlicker(int line, int point)
            {
                uint tick = (uint)(age * 24 + line * 0.37f);
                return Noise(arcSeed + (uint)(line * 199 + point * 31) + tick * 977) * 2 - 1;
            }
            Vector2 ElectricEdge(float t)
            {
                float y = t * 2 - 1;
                if (heavy && phase == SwordEnergyPhase.Flight)
                    return electricOffset + new Vector2(facing * electricSize.x * (0.38f - 0.74f * y * y), electricSize.y * y * 0.42f);
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
                else if (phase == SwordEnergyPhase.ChargeRelease) ChargeRelease(Mathf.Clamp01(age / duration));
                else if (phase == SwordEnergyPhase.Flight) Flight();
                else Burst(Mathf.Clamp01(age / duration));
                DrawMotes(dt);
                if (phase == SwordEnergyPhase.Charge || phase == SwordEnergyPhase.ChargeRelease)
                    ClipChargeToArena();
                mesh.SetVertices(vertices, 0, vertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetColors(colors);
                // Charge bounds follow its hand anchor and include outer intake
                // bolts plus the directional release. Flight bounds stay unchanged.
                var bounds = phase == SwordEnergyPhase.Charge || phase == SwordEnergyPhase.ChargeRelease
                    ? (smallCharge ? new Bounds(ChargeCenter(), Vector3.one * (ChargeRadius(1) * 6.4f))
                        : new Bounds((Vector3)((bladeHeel+bladeTip)*0.5f), Vector3.one * (Vector2.Distance(bladeHeel,bladeTip)+chargeBodyHeight*0.6f)))
                    : new Bounds(Vector3.zero, new Vector3(42, 34, 2));
                // The fixed buffers are reused; only occupied ribbons are drawn.
                mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, stroke * (Points - 1) * (Lanes - 1) * 6)
                    { firstVertex = 0, vertexCount = vertices.Length, bounds = bounds },
                    UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
                // Explicit bounds avoid a scan over invisible buffer slots.
                mesh.bounds = bounds;
            }
            void ClipChargeToArena()
            {
                // Sword anchors stay attached to the pose. Trim only electricity
                // at the physical walls, including its intake and release wisps.
                float left = KarmaStage.ArenaLeft - center.x, right = KarmaStage.ArenaRight - center.x;
                for (int i = 0; i < stroke * VerticesPerStroke; i++)
                {
                    var p = vertices[i];
                    colors[i].a *= Mathf.Clamp01(Mathf.Min(p.x - left, right - p.x) / 0.18f);
                    p.x = Mathf.Clamp(p.x, left, right);
                    vertices[i] = p;
                }
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
                // Crisp white current with a clearly visible blue fringe.
                Ribbon(width * 2.2f, Alpha(Cyan, alpha * 0.55f));
                Ribbon(width, Alpha(White, alpha), core: 0.45f);
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
                if (heavy && phase == SwordEnergyPhase.Flight) return ElectricEdge(t);
                float y = t * 2 - 1;
                return new Vector2(facing * size.x * (0.37f - 0.54f * y * y), size.y * y * 0.48f);
            }
            Vector2 bladeHeel, bladeBend, bladeTip;
            float bladePixelSize, chargeBodyHeight, chargeSecondsLeft;
            bool smallCharge;
            public void ConfigureBlade(Vector2 heel, Vector2 bend, Vector2 tip, float unitsPerPixel = 0, float bodyHeight = 4, bool small = false, float secondsLeft = float.MaxValue)
            {
                smallCharge = small; chargeSecondsLeft = secondsLeft;
                chargeBodyHeight = Mathf.Max(2, bodyHeight);
                bladeHeel = heel; bladeBend = bend; bladeTip = tip;
                bladePixelSize = Mathf.Max(0, unitsPerPixel);
            }
            Vector2 OnBlade(float t)
            {
                // Quadratic control passes through the measured midpoint.
                Vector2 control = bladeBend * 2 - (bladeHeel + bladeTip) * 0.5f;
                return bladeHeel * ((1 - t) * (1 - t)) + control * (2 * t * (1 - t)) + bladeTip * (t * t);
            }
            Vector2 ChargeCenter() { return OnBlade(0.78f); }
            float ChargeIntensity => smallCharge ? 0.50f : heavy ? 1.35f : 0.90f;
            float ChargeRadius(float focus)
            { return chargeBodyHeight * (heavy ? 0.39f : 0.30f) * 0.60f * (smallCharge ? 0.375f : 1) * Mathf.Lerp(0.85f, 1.25f, focus); }
            public void BeginChargeRelease(float direction)
            { phase = SwordEnergyPhase.ChargeRelease; facing = direction < 0 ? -1 : 1; }
            void ChargeDisc(Vector2 at, Vector2 axis, float radius, float flatten, Color color, int rows)
            {
                Vector2 normal = new Vector2(-axis.y, axis.x);
                // Overlapping filled strips create a soft area, independent of
                // the sword silhouette. No sprite, allocation or new material.
                for (int row = 0; row < rows; row++)
                {
                    float y = Mathf.Lerp(-0.9f, 0.9f, row / (float)(rows - 1));
                    float span = radius * Mathf.Sqrt(1 - y * y);
                    for (int p = 0; p < Points; p++)
                        path[p] = at + axis * Mathf.Lerp(-span, span, p / (float)(Points - 1))
                            + normal * (y * radius * flatten);
                    Ribbon(radius * 1.5f / rows, color, core: 0.78f);
                }
            }
            void ChargeCore(Vector2 at, Vector2 axis, float radius, float focus, float alpha)
            {
                // Translucent aura and a smaller white center leave the sword
                // and character readable while retaining sharp intake lightning.
                ChargeDisc(at, axis, radius * 1.35f, 0.90f,
                    Alpha(new Color(0.08f, 0.35f, 1.25f), alpha * 0.22f * ChargeIntensity), 9);
                ChargeDisc(at, axis, radius, 0.88f,
                    Alpha(Cyan, alpha * (0.32f + focus * 0.20f) * ChargeIntensity), 9);
                ChargeDisc(at, axis, radius * 0.34f, 0.90f,
                    Alpha(White, alpha * (0.85f + focus * 0.40f) * ChargeIntensity), 7);
            }
            float BladeFillProgress(float t)
            { return heavy ? (aiming ? Mathf.Lerp(0.06f,0.35f,t) : Mathf.Lerp(0.35f,1,t)) : Mathf.Lerp(0.06f,1,t); }
            void BladeFill(float t, float alpha = 1)
            {
                if (Vector2.Distance(bladeHeel,bladeTip)<0.01f || alpha<=0) return;
                float front = BladeFillProgress(t);
                float width = Mathf.Max(chargeBodyHeight*(heavy?0.034f:0.025f),bladePixelSize*2);
                float brightness = (0.35f+front*0.65f)*alpha;
                // Blue steel lights from the guard to the tip. A bounded aura
                // extends a little beyond the blade, with no free-floating core.
                for (int p=0;p<Points;p++) path[p]=OnBlade(front*p/(Points-1));
                Ribbon(width*2.3f,Alpha(new Color(0.04f,0.32f,1.4f),brightness*0.38f));
                Ribbon(width,Alpha(Cyan,brightness*(heavy?0.75f:0.60f)),core:0.65f);
                Ribbon(width*0.27f,Alpha(White,brightness*0.65f));
                int arcs = heavy?6:4;
                for (int a=0;a<arcs;a++)
                {
                    float u = front*(a+0.35f)/arcs;
                    Vector2 from=OnBlade(u),to=OnBlade(Mathf.Min(front,u+0.14f));
                    Vector2 axis=(to-from).normalized,normal=new Vector2(-axis.y,axis.x);
                    float blink=0.75f+0.25f*CrackleFlicker(a+120,0);
                    BladeBolt(from,to,width*0.22f,brightness*blink,width*0.6f,a+120);
                    // Short sparks protrude from the illuminated portion only.
                    Vector2 end=to+normal*(width*(a%2==0?2.8f:-2.8f));
                    BladeBolt(to,end,width*0.15f,brightness*blink*0.6f,width*0.4f,a+140);
                }
                BladeChargeAccents(front,width,brightness);
            }
            void BladeChargeAccents(float front, float width, float brightness)
            {
                // Two counter-winding filaments make the sheath feel alive.
                // Their reach stays within a few blade widths of the steel.
                for (int side=-1;side<=1;side+=2)
                {
                    for (int p=0;p<Points;p++)
                    {
                        float u=front*p/(Points-1);
                        Vector2 axis=(OnBlade(Mathf.Min(1,u+0.02f))-OnBlade(Mathf.Max(0,u-0.02f))).normalized;
                        Vector2 normal=new Vector2(-axis.y,axis.x);
                        float envelope=Mathf.Sin(p/(float)(Points-1)*Mathf.PI);
                        float winding=Mathf.Sin(u*16-age*(heavy?9:12)+side*1.5f);
                        path[p]=OnBlade(u)+normal*(side*width*(1.1f+winding*0.4f)*envelope);
                    }
                    Ribbon(width*0.38f,Alpha(Cyan,brightness*0.55f));
                    Ribbon(width*0.10f,Alpha(White,brightness*0.45f));
                }
                // Distinct short packets climb from the guard toward the front.
                int packets=heavy?4:2;
                for (int a=0;a<packets;a++)
                {
                    float u=Mathf.Repeat(age*(heavy?1.7f:2.2f)+a/(float)packets,1);
                    float head=front*u,tail=Mathf.Max(0,head-0.16f);
                    float pulse=Mathf.Sin(u*Mathf.PI)*brightness;
                    BladeBolt(OnBlade(tail),OnBlade(head),width*0.24f,pulse,width*0.25f,a+180);
                }
                // A small blade-aligned star gathers at the lit front just
                // before release. No floating sphere, ring or whole-body flash.
                float ready=Mathf.Clamp01((front-0.72f)/0.28f);
                Vector2 tip=OnBlade(front),direction=(bladeTip-bladeBend).normalized;
                Vector2 cross=new Vector2(-direction.y,direction.x);
                float reach=width*(heavy?3.8f:3.0f);
                Segment(tip-direction*reach,tip+direction*reach,width*0.35f,Alpha(Cyan,brightness*ready*0.55f));
                Segment(tip-direction*reach*0.6f,tip+direction*reach*0.6f,width*0.10f,Alpha(White,brightness*ready));
                Segment(tip-cross*reach*0.5f,tip+cross*reach*0.5f,width*0.12f,Alpha(Cyan,brightness*ready*0.70f));
            }
            void BladeChargeDischarge(float t)
            {
                float fade=(1-t)*(1-t);
                if (fade<=0) return;
                Vector2 tip=OnBlade(1),forward=Vector2.right*facing;
                Vector2 normal=Vector2.up;
                float width=chargeBodyHeight*(heavy?0.034f:0.025f);
                int rays=heavy?5:3;
                for (int a=0;a<rays;a++)
                {
                    float side=a/(float)(rays-1)*2-1;
                    Vector2 end=tip+forward*width*(2+t*5)+normal*(side*width*(1+t*2));
                    BladeBolt(tip,end,width*0.25f,fade*(heavy?0.95f:0.70f),width*0.35f,a+220);
                }
            }
            void Charge(float t)
            {
                if (!smallCharge) { BladeFill(t); return; }
                if (Vector2.Distance(bladeHeel, bladeTip) < 0.01f) return;
                float focus = heavy ? (aiming ? Mathf.Lerp(0.15f, 0.4f, t) : Mathf.Lerp(0.4f, 1, t))
                    : Mathf.Lerp(0.18f, 1, t);
                Vector2 at = ChargeCenter();
                Vector2 axis = (bladeBend - bladeHeel).normalized;
                Vector2 normal = new Vector2(-axis.y, axis.x) * facing;
                // Contract during the last two authored frames of preparation.
                // Tracking SW02 remains open until its existing locked warning.
                float contraction = aiming ? 0 : Mathf.Clamp01(1 - chargeSecondsLeft / (2f / 24));
                float radius = ChargeRadius(focus) * Mathf.Lerp(1, 0.28f, contraction);
                float width = Mathf.Max(radius * (heavy ? 0.10f : 0.085f), bladePixelSize * (smallCharge ? 1.5f : 3));
                ChargeCore(at, axis, radius, focus, 0.9f + focus * 0.35f);
                int arcs = smallCharge ? 2 : heavy ? 10 : 6;
                for (int a = 0; a < arcs; a++)
                {
                    uint key = arcSeed + (uint)a * 131;
                    float angle = a * 2.39996f + Noise(key) * 0.7f;
                    Vector2 outward = axis * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
                    float u = Mathf.Repeat(age * (heavy ? 2.4f : 3.0f) + Noise(key + 7), 1);
                    Vector2 target = at + outward * radius * 0.25f;
                    Vector2 from = at + outward * radius * (2.1f - u * 1.25f);
                    float blink = 0.85f + Flicker(a + 20, 0) * 0.15f;
                    BladeBolt(from, target, width, (1.1f + focus * 0.8f) * blink * ChargeIntensity,
                        radius * (0.20f + focus * 0.10f) * facing, a + 1);
                }
                int streams = smallCharge ? 1 : heavy ? 6 : 4;
                for (int a = 0; a < streams; a++)
                {
                    float angle = a * 2.39996f - age * 0.7f;
                    Vector2 outward = axis * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
                    float u = Mathf.Repeat(age * (2.0f + focus) + a * 0.31f, 1);
                    Vector2 head = at + outward * radius * (2.0f * (1 - u));
                    Vector2 tail = head + outward * radius * 0.40f;
                    BladeBolt(tail, head, width * 0.7f, Mathf.Sin(u * Mathf.PI) * (1 + focus) * ChargeIntensity,
                        radius * 0.05f * facing, a + 40);
                }
                if (heavy)
                    for (int a = 0; a < 3; a++)
                    {
                        float u = Mathf.Repeat(age * 3.2f + a / 3f, 1);
                        BladeBolt(OnBlade(Mathf.Lerp(0.30f, 0.72f, u)), OnBlade(Mathf.Lerp(0.55f, 1, u)),
                            width * 0.65f, (0.5f + focus) * ChargeIntensity, radius * 0.07f, a + 80);
                    }
                int particles = smallCharge ? 3 : heavy ? 16 : 10;
                for (int a = 0; a < particles; a++)
                {
                    float u = Mathf.Repeat(age * (heavy ? 2.8f : 3.4f) + Noise(arcSeed + (uint)a * 73), 1);
                    float angle = a * 2.39996f;
                    Vector2 outward = axis * Mathf.Cos(angle) + normal * Mathf.Sin(angle);
                    Vector2 head = at + outward * radius * (2.2f * (1 - u));
                    Segment(head + outward * radius * 0.10f, head, width * 0.45f,
                        Alpha(a % 3 == 0 ? White : Cyan, Mathf.Sin(u * Mathf.PI) * 1.3f * ChargeIntensity));
                }
            }
            void ChargeRelease(float t)
            {
                if (!smallCharge) { BladeFill(1, 1-t); BladeChargeDischarge(t); return; }
                Vector2 at = ChargeCenter();
                Vector2 axis = (bladeBend - bladeHeel).normalized;
                float radius = ChargeRadius(1);
                // The core already contracted before the actual launch event.
                // Discharge begins on that event, without delaying the projectile.
                float burst = Mathf.Clamp01(t);
                ChargeCore(at, axis, radius * 0.28f * (1 - burst), 1, 1 - burst);
                Vector2 forward = Vector2.right * facing;
                Vector2 normal = Vector2.up;
                int bolts = smallCharge ? 2 : heavy ? 12 : 6;
                for (int a = 0; a < bolts; a++)
                {
                    float spread = (a / (float)(bolts - 1) - 0.5f) * radius;
                    Vector2 end = at + forward * (radius * (0.35f + burst * 2.1f))
                        + normal * (spread * burst);
                    BladeBolt(at + normal * (spread * 0.12f), end,
                        radius * 0.11f * (1 - burst), (1 - burst) * 1.6f * ChargeIntensity,
                        radius * 0.08f * facing, a + 60);
                }
            }
            void Flight()
            {
                // Keep the authored crescent and white cutting edge; every extra
                // bolt, chip and speed cut stays close to that silhouette.
                if (!heavy) Crescent(Vector2.zero, size, 0.11f, power * 0.95f, true);
                if (!heavy) StaggeredMantle();
                for (int p = 0; p < Points; p++) path[p] = ElectricEdge(p / (float)(Points - 1));
                Ribbon(electricWidth * (heavy ? 6.0f : 4.0f), Alpha(Cyan, heavy ? 1.25f : 0.9f));
                Ribbon(electricWidth * 2.4f, Alpha(Cyan, heavy ? 1.1f : 0.85f));
                Ribbon(electricWidth * 1.2f, Alpha(White, heavy ? 1.25f : 0.95f));
                // Staggered short branches keep a readable continuous outline.
                int arcs = heavy ? 10 : 7;
                float ignition = 1 + Mathf.Clamp01(1 - age / 0.18f) * 0.7f;
                for (int a = 0; a < arcs; a++)
                {
                    float span = heavy ? 0.14f : 0.105f;
                    float head = 0.08f + a * (0.84f - span) / (arcs - 1);
                    float pulse = 0.70f + 0.30f * Mathf.Sin(age * (15 + a * 1.7f) + a * 2.1f + Noise(arcSeed) * 6);
                    BladeBolt(ElectricEdge(head), ElectricEdge(head + span), electricWidth,
                        Mathf.Min(1.8f, (heavy ? 1.6f : 1.3f) * pulse * ignition), electricSize.y * (heavy ? 0.034f : 0.026f), a + 1);
                }
                float launchTail = Mathf.Clamp01(1 - age / (heavy ? 0.24f : 0.16f));
                for (int a = 0; a < (heavy ? 5 : 3) && launchTail > 0; a++)
                {
                    Vector2 tail = ElectricEdge(0.18f + a * 0.15f);
                    BladeBolt(tail, tail + new Vector2(-facing * electricSize.x * (heavy ? 0.36f : 0.24f), electricSize.y * 0.025f),
                        electricWidth * 1.2f, launchTail * 1.4f, electricSize.y * 0.03f, 17 + a);
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
                    Emit(heavy ? 5 : 3, heavy ? 3.2f : 2.6f);
                    nextSpark = age + (heavy ? 0.04f : 0.055f) + Sample() * 0.015f;
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
                    Glint(hit ? Vector2.zero : swordOffset, heavy ? 3.2f : 2.2f, flash * (heavy ? 2.1f : 1.85f));
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
                        ElectricRibbon((heavy ? 0.27f : 0.19f) * (1 - u), fade * (1 - u * 0.5f) * 1.25f);
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
                // Expiry breaks into short electric fragments, without a hit flash.
                // Existing contact routing still owns the outcome.
                int arcs = dissolve ? (heavy ? 10 : 6) : (heavy ? 12 : 8);
                for (int a = 0; a < arcs; a++)
                {
                    float angle = a * 2.39996f + Noise(seed + (uint)a) * 0.8f;
                    Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float radius = Mathf.Min(electricSize.x, electricSize.y) * (0.12f + Mathf.Sqrt(t) * (heavy ? 0.25f : 0.20f))
                        * (dissolve ? 0.65f : 1);
                    BladeBolt(electricOffset + axis * radius * 0.25f, electricOffset + axis * radius,
                        electricWidth * (1 - t) * (dissolve ? 0.85f : 1.2f),
                        fade * (heavy ? 1.2f : 1.0f) * (dissolve ? 0.85f : 1), radius * 0.16f * (1 - t), a + 30);
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
