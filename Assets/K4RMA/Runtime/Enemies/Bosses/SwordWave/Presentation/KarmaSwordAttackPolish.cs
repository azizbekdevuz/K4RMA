using UnityEngine;

namespace KarmaPrototype
{
    // Attack-only cosmetics. No contact, timers, time scale or camera writes.
    public static class KarmaSwordAttackPolish
    {
        public static readonly Color Blue = new Color(0.22f, 0.72f, 1);
        public static void Warning(SpriteRenderer marker)
        {
            if (marker == null) return;
            marker.GetComponent<KarmaAttackWarning>()?.SetTint(new Color(0.22f, 0.72f, 1, 0.42f));
        }
        public static KarmaSwordChargePolish Charge(KarmaGame game, KarmaEnemy owner, Transform art, Vector2 at, float seconds, bool heavy, bool locked = true, bool followOwner = false)
        {
            var node = new GameObject(locked ? "Sword locked concentration / no damage" : "Sword tracking concentration / no damage");
            node.transform.SetParent(art, false);
            var cue = node.AddComponent<KarmaSwordChargePolish>();
            cue.Initialize(game, owner, at, seconds, heavy, locked, followOwner);
            if (!followOwner && game.IsCombat && owner.Alive)
                KarmaAudio.Play(KarmaSfxId.SwEnergyCharge, seconds, cue);
            return cue;
        }
        public static void Release(KarmaGame game, KarmaEnemy owner, Transform art, Vector2 at, Vector2 velocity, Vector2 size, bool heavy, Transform scope = null)
        {
            if (scope == null) KarmaAudio.StopSlot("sw-bed");
            var pool = KarmaSwordVfxPool.For(game);
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            bool animated = owner.CurrentPattern == (int)EnemyAttack.SW01 || owner.CurrentPattern == (int)EnemyAttack.SW02;
            if (animated) KarmaSwordWaveAnimation.Release(game, owner, at, size, heavy, velocity.x);
            else
            pool.Flash(owner, "Sword_Release_v6", at, new Vector2(Mathf.Min(size.x * 0.65f, 3.4f), Mathf.Min(size.y * 0.7f, 5.8f)), 0.13f, angle, scope);
            pool.Sparks(owner, at, velocity.normalized, heavy ? 6 : 3, heavy ? 10 : 6, scope);
            if (owner.CurrentPattern == (int)EnemyAttack.SW02)
                pool.Flash(owner, "Sword_Release_v6", at - velocity.normalized * 1.2f, new Vector2(5.8f, 0.28f), 0.09f, angle, scope);
            else if (owner.CurrentPattern == (int)EnemyAttack.SW01)
                pool.Flash(owner, "Sword_Release_v6", at, new Vector2(3.5f, 0.22f), 0.10f, angle + (at.y < 4 ? -12 : 12), scope);
            var accent = art.GetComponent<KarmaSwordAttackAccent>();
            if (accent == null) accent = art.gameObject.AddComponent<KarmaSwordAttackAccent>();
            accent.Fire(game, owner, heavy, scope);
        }
        public static void Projectile(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot, Vector2 velocity, Vector2 size)
        {
            Core(shot);
            if (owner.CurrentPattern == (int)EnemyAttack.SWBasic)
            {
                KarmaSwordBasicPresentation.Projectile(game, owner, shot, velocity, size);
                return;
            }
            if (owner.CurrentPattern == (int)EnemyAttack.SW01 || owner.CurrentPattern == (int)EnemyAttack.SW02)
            {
                KarmaSwordWaveAnimation.Flight(game, owner, shot, velocity, size);
                if (game.IsCombat && owner.Alive)
                    KarmaAudio.Play(KarmaSfxId.SwElectricDischarge, lifetimeOwner: shot);
                return;
            }
            KarmaSwordSkillSurface.Apply(shot.Visual, KarmaSwordSkillSurface.ProjectileStyle(owner.CurrentPattern));
            shot.gameObject.AddComponent<KarmaSwordProjectilePolish>().Initialize(game, owner, shot.Visual, velocity, size);
            if (game.IsCombat && owner.Alive && velocity.sqrMagnitude > 0.01f)
                KarmaAudio.Play(KarmaSfxId.SwWaveLaunch, lifetimeOwner: shot);
        }
        public static void Core(KarmaBossAttack shot)
        {
            // Replace this shot's generic radiance and allocating afterimages;
            // SW05 and other bosses never call this entry point.
            var generic = shot.Visual.GetComponent<KarmaCombatVFX>();
            if (generic != null) generic.enabled = false;
            var light = shot.Visual.transform.Find("Attack radiance");
            if (light != null) light.gameObject.SetActive(false);
            shot.Visual.color = Color.white;
        }
        public static void Hit(KarmaGame game, KarmaEnemy owner, Vector2 at, bool strong)
        {
            var pool = KarmaSwordVfxPool.For(game);
            pool.Flash(owner, "Sword_Impact_v6", at, Vector2.one * (strong ? 3.4f : 2.3f), strong ? 0.18f : 0.13f);
            pool.Sparks(owner, at, Vector2.up, strong ? 6 : 3, 8);
        }
        public static void ParrySuccess(KarmaGame game, KarmaEnemy owner, Vector2 at)
        {
            // A stationary contact flash and circular pulse, never a launch fan.
            var pool = KarmaSwordVfxPool.For(game);
            pool.Flash(owner, "Sword_Impact_v6", at, Vector2.one * 1.5f, 0.10f);
            pool.Flash(owner, "Sword_Release_v6", at, new Vector2(2.2f, 0.12f), 0.08f, 45);
            pool.Flash(owner, "Sword_Release_v6", at, new Vector2(2.2f, 0.12f), 0.08f, -45);
            var node = new GameObject("SW04 parry success / no damage");
            node.transform.SetParent(game.World, false); node.transform.position = at;
            node.AddComponent<KarmaSwordParryFeedback>().Initialize(game, owner);
        }
    }

    // A room-local, fixed-size sprite pool. Dense SW03 rounds never allocate
    // cosmetic GameObjects per spark or duplicate full-height glow columns.
    public sealed class KarmaSwordVfxPool : MonoBehaviour
    {
        public const int Capacity = 96;
        sealed class Entry
        {
            public SpriteRenderer renderer;
            public KarmaEnemy owner;
            public Transform scope;
            public Vector3 velocity, scale;
            public Color color;
            public float age, life, growth;
            public bool active;
        }
        Entry[] entries;
        KarmaGame game;
        Transform poolRoot;
        int cursor;
        static readonly string[] WarmSprites = { "Sword_Crescent_v6", "SW03_GroundScar_v6", "SW03_RisingBlade_v6", "SW04_CounterBlade_v6", "Sword_Impact_v6", "Sword_Release_v6", "Sword_GuardRing_v6", "Sword_Shard_v6" };
        public int ActiveCount { get; private set; }
        public int PeakCount { get; private set; }
        public int RecycledLive { get; private set; }
        public static KarmaSwordVfxPool For(KarmaGame game)
        {
            var pool = game.World.GetComponent<KarmaSwordVfxPool>();
            if (pool == null) pool = game.World.gameObject.AddComponent<KarmaSwordVfxPool>();
            pool.game = game; pool.Ensure(); return pool;
        }
        void Ensure()
        {
            if (poolRoot != null) return;
            // Load at the first preparation cue, before the release frame.
            foreach (var pose in WarmSprites) KarmaWaveArtwork.Sprite(pose);
            poolRoot = new GameObject("Sword cosmetic pool / no collision").transform;
            poolRoot.SetParent(transform, false); entries = new Entry[Capacity]; cursor = 0; ActiveCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                var node = new GameObject("Sword pooled cosmetic " + i); node.transform.SetParent(poolRoot, false);
                var sr = node.AddComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(sr); sr.sortingOrder = 29;
                node.SetActive(false); entries[i] = new Entry { renderer = sr };
            }
        }
        Entry Next(KarmaEnemy owner, Sprite sprite, Vector2 at, Vector3 scale, float seconds, Color color, float angle, Vector2 velocity, float growth, Transform scope = null)
        {
            Ensure(); Entry e = null;
            for (int i = 0; i < entries.Length; i++) { var candidate = entries[(cursor + i) % entries.Length]; if (!candidate.active) { e = candidate; cursor = (cursor + i + 1) % entries.Length; break; } }
            if (e == null) { e = entries[cursor]; cursor = (cursor + 1) % entries.Length; RecycledLive++; }
            e.owner = owner; e.scope = scope; e.active = true; e.age = 0; e.life = Mathf.Max(0.04f, seconds); e.color = color; e.velocity = velocity; e.scale = scale; e.growth = growth;
            var sr = e.renderer; sr.sprite = sprite; sr.color = color; sr.flipX = sr.flipY = false; sr.sortingOrder = 29;
            KarmaSwordSkillSurface.Apply(sr, 4);
            sr.transform.position = at; sr.transform.rotation = Quaternion.Euler(0, 0, angle); sr.transform.localScale = scale;
            sr.gameObject.SetActive(true); return e;
        }
        public void Flash(KarmaEnemy owner, string pose, Vector2 at, Vector2 size, float seconds, float angle = 0, Transform scope = null)
        {
            var sprite = KarmaWaveArtwork.Sprite(pose); if (sprite == null) return;
            Next(owner, sprite, at, new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1), seconds, Color.white, angle, Vector2.zero, 0.15f, scope);
        }
        public void Echo(KarmaEnemy owner, SpriteRenderer source, float alpha, float seconds, Transform scope = null)
        {
            var e = Next(owner, source.sprite, source.transform.position, source.transform.lossyScale, seconds,
                new Color(0.3f, 0.78f, 1, alpha), source.transform.eulerAngles.z, Vector2.zero, -0.08f, scope != null ? scope : source.transform);
            e.renderer.flipX = source.flipX; e.renderer.flipY = source.flipY; e.renderer.sortingOrder = source.sortingOrder - 1;
        }
        public void Sparks(KarmaEnemy owner, Vector2 at, Vector2 direction, int count, float speed, Transform scope = null)
        {
            var sprite = KarmaWaveArtwork.Sprite("Sword_Shard_v6");
            if (sprite == null) return;
            float center = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < count; i++)
            {
                float angle = center + (i - (count - 1) * 0.5f) * 0.55f;
                Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float length = 0.35f + (i % 3) * 0.17f;
                Next(owner, sprite, at, new Vector3(length / sprite.bounds.size.x, 0.05f / sprite.bounds.size.y, 1),
                    0.16f + (i % 3) * 0.035f, Color.white, angle * Mathf.Rad2Deg, axis * speed, -0.35f, scope);
            }
        }
        public void Cancel(Transform scope)
        {
            if (entries == null || scope == null) return;
            foreach (var e in entries)
                if (e.active && e.scope != null && (e.scope == scope || e.scope.IsChildOf(scope)))
                { e.active = false; e.renderer.gameObject.SetActive(false); }
        }
        public void Dissolve(KarmaEnemy owner, SpriteRenderer source)
        {
            if (source == null || source.sprite == null) return;
            // Copy the canceled blade at rest. Shrink/fade in place, without
            // velocity, release flash, new trail, projectile or forward shards.
            var e = Next(owner, source.sprite, source.transform.position, source.transform.lossyScale,
                0.18f, new Color(0.4f, 0.82f, 1, 0.5f), source.transform.eulerAngles.z,
                Vector2.zero, -0.65f);
            e.renderer.flipX = source.flipX; e.renderer.flipY = source.flipY;
        }
        void Update()
        {
            if (poolRoot == null || game == null || game.Paused) return;
            int active = 0; float dt = Time.deltaTime;
            foreach (var e in entries)
            {
                if (!e.active) continue;
                e.age += dt;
                if (e.age >= e.life || e.owner == null || !e.owner.PresentationActive || e.owner.Stunned)
                { e.active = false; e.renderer.gameObject.SetActive(false); continue; }
                float t = e.age / e.life; var color = e.color; color.a *= 1 - t * t;
                e.renderer.color = color; e.renderer.transform.position += e.velocity * dt;
                e.velocity *= Mathf.Exp(-6 * dt); e.renderer.transform.localScale = e.scale * (1 + e.growth * t); active++;
            }
            ActiveCount = active; PeakCount = Mathf.Max(PeakCount, active);
        }
    }

    [DefaultExecutionOrder(350)]
    public sealed class KarmaSwordProjectilePolish : MonoBehaviour
    {
        KarmaGame game; KarmaEnemy owner; SpriteRenderer source; Vector2 velocity, size; Transform scope;
        Vector2 previous; float age, nextEcho, nextShard;
        public void Initialize(KarmaGame director, KarmaEnemy enemy, SpriteRenderer picture, Vector2 travel, Vector2 footprint)
        { game = director; owner = enemy; source = picture; velocity = travel; size = footprint; previous = transform.position; scope = transform.parent; }
        void LateUpdate()
        {
            if (game == null || game.Paused || source == null || !source.enabled) return;
            age += Time.deltaTime; Vector2 at = transform.position;
            if ((at - previous).sqrMagnitude > 0.0001f && age >= nextEcho)
            { KarmaSwordVfxPool.For(game).Echo(owner, source, size.y > 5 ? 0.15f : 0.22f, 0.12f, scope); nextEcho = age + 0.035f; }
            if (velocity.sqrMagnitude > 0.01f && age >= nextShard)
            { KarmaSwordVfxPool.For(game).Sparks(owner, at, -velocity.normalized, 1, 5, scope); nextShard = age + 0.085f; }
            previous = at;
        }
    }

    public sealed class KarmaSwordChargePolish : MonoBehaviour
    {
        KarmaGame game; KarmaEnemy owner; SpriteRenderer ring;
        Vector2 at, offset; float life, age, nextMote; bool heavy, locked, follow;
        public void Initialize(KarmaGame director, KarmaEnemy enemy, Vector2 position, float seconds, bool strong, bool committed, bool followOwner)
        {
            game = director; owner = enemy; at = position; offset = position - owner.Position; follow = followOwner; life = Mathf.Max(0.05f, seconds); heavy = strong; locked = committed;
            ring = gameObject.AddComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(ring); ring.sortingOrder = 23;
            ring.sprite = KarmaWaveArtwork.Sprite("Sword_GuardRing_v6"); transform.SetParent(game.World, true); transform.position = at;
            KarmaSwordSkillSurface.Apply(ring, 6);
            KarmaSwordVfxPool.For(game);
        }
        void Update()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned || owner.CurrentPattern == (int)EnemyAttack.SW05)
            { Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime; if (age >= life) { Destroy(gameObject); return; }
            if (follow) { at = owner.Position + offset; transform.position = at; }
            float t = age / life; float diameter = follow ? 6.4f * (0.98f + 0.02f * Mathf.Sin(age * 8)) : (heavy ? 2.8f : 1.5f) * Mathf.Lerp(1.1f, 0.48f, t);
            bool aimed = owner.CurrentPattern == (int)EnemyAttack.SW02;
            bool staggered = owner.CurrentPattern == (int)EnemyAttack.SW01;
            float focus = aimed && !follow ? Mathf.Lerp(1.12f, 0.72f, t * t) : 1;
            transform.localScale = new Vector3(diameter * focus * (staggered ? 1.3f : 1) / ring.sprite.bounds.size.x,
                diameter * focus * (staggered ? 0.72f : 1) / ring.sprite.bounds.size.y, 1);
            transform.rotation = Quaternion.Euler(0, 0, (locked ? -150 : 90) * t);
            ring.color = aimed ? new Color(Mathf.Lerp(0.3f, 0.88f, t), Mathf.Lerp(0.7f, 0.96f, t), 1, Mathf.Lerp(0.25f, 0.88f, t))
                : new Color(0.4f, 0.82f, 1, follow ? 0.55f : Mathf.Lerp(0.25f, 0.88f, t));
            if (age >= nextMote) { KarmaSwordVfxPool.For(game).Sparks(owner, at, Vector2.up, 1, 1.2f, transform); nextMote = age + 0.10f; }
        }
    }

    [DefaultExecutionOrder(325)]
    public sealed class KarmaSwordAttackAccent : MonoBehaviour
    {
        KarmaEnemy owner; KarmaGame game; Transform picture, scope; float age = 1; bool heavy;
        public void Fire(KarmaGame director, KarmaEnemy enemy, bool strong, Transform source = null) { game = director; owner = enemy; heavy = strong; age = 0; scope = source; if (picture == null) picture = transform.Find("Guardian sprite"); }
        public void Cancel(Transform source) { if (scope != null && (scope == source || scope.IsChildOf(source))) age = 1; }
        void LateUpdate()
        {
            // Cancel rather than freeze a release impulse across interruption
            // or an ultimate; it must never reappear after the state ends.
            if (owner == null || !owner.PresentationActive || owner.Stunned || owner.CurrentPattern == (int)EnemyAttack.SW05)
            { age = 1; return; }
            if (picture == null || game.Paused || age >= 0.22f) return;
            age += Time.deltaTime; float impulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / 0.22f)) * Mathf.Exp(-age * 10);
            // Add after KarmaWaveMotion has rebuilt its authored foot transform.
            picture.localPosition += Vector3.left * impulse * (heavy ? 0.055f : 0.025f);
            picture.localRotation *= Quaternion.Euler(0, 0, impulse * (heavy ? 4 : 2));
        }
    }

    // Basic-only presentation. Its own recycled mesh buffers and the existing
    // sprite pool keep it independent of SW01/SW02's electrical random seeds.
    [DefaultExecutionOrder(375)]
    public sealed class KarmaSwordBasicPresentation : MonoBehaviour
    {
        const int Points = 9, Strokes = 16, VerticesPerStroke = Points * 4;
        sealed class Buffers
        {
            public readonly Vector3[] vertices = new Vector3[Strokes * VerticesPerStroke];
            public readonly Color[] colors = new Color[Strokes * VerticesPerStroke];
            public readonly Vector2[] path = new Vector2[Points];
            public Mesh mesh;
            public Buffers()
            {
                mesh = new Mesh { name = "SWBasic recycled slash geometry" }; mesh.MarkDynamic();
                mesh.vertices = vertices; mesh.colors = colors;
                var indices = new int[Strokes * (Points - 1) * 18]; int n = 0;
                for (int s = 0; s < Strokes; s++) for (int p = 0; p < Points - 1; p++) for (int lane = 0; lane < 3; lane++)
                {
                    int v = s * VerticesPerStroke + p * 4 + lane;
                    indices[n++] = v; indices[n++] = v + 4; indices[n++] = v + 1;
                    indices[n++] = v + 1; indices[n++] = v + 4; indices[n++] = v + 5;
                }
                mesh.triangles = indices;
            }
        }
        static readonly System.Collections.Generic.Stack<Buffers> spare = new System.Collections.Generic.Stack<Buffers>(4);
        static readonly Sprite[] rangedFrames = new Sprite[5];
        static readonly Color Cyan = new Color(0.18f, 1.05f, 1.7f, 1), White = new Color(0.85f, 1.25f, 1.6f, 1);
        KarmaGame game; KarmaEnemy owner; KarmaSwordVfxPool pool;
        SpriteRenderer sword, source; KarmaCharacterAnimator animator;
        Transform scope;
        MeshRenderer geometry; MeshFilter filter; Buffers buffer;
        Vector2 size, velocity, rangedFootprint;
        float age, life, nextEcho;
        int stroke;
        bool preparing, melee, released, boundaryFlight;

        static Sprite RangedFrame(int index)
        {
            if (rangedFrames[index] == null)
                rangedFrames[index] = KarmaArtwork.Sprite("SwordBossVFX", "SwordEnergy_SW01Flight_" + index);
            return rangedFrames[index];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearBuffers()
        {
            while (spare.Count > 0) { var b = spare.Pop(); if (b.mesh != null) Object.Destroy(b.mesh); }
        }
        void Initialize(KarmaGame director, KarmaEnemy enemy)
        {
            game = director; owner = enemy; pool = KarmaSwordVfxPool.For(game);
            var art = owner.transform.Find("Artwork/Guardian sprite"); sword = art != null ? art.GetComponent<SpriteRenderer>() : null;
            animator = art != null ? art.GetComponentInParent<KarmaCharacterAnimator>() : null;
            buffer = spare.Count > 0 ? spare.Pop() : new Buffers();
            if (buffer.mesh == null) buffer = new Buffers();
            buffer.mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, 0),
                UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            filter = gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = buffer.mesh;
            geometry = gameObject.AddComponent<MeshRenderer>(); geometry.sortingOrder = 27;
            geometry.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            geometry.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; geometry.receiveShadows = false;
            buffer.mesh.bounds = new Bounds(Vector3.zero, new Vector3(16, 16, 2));
        }
        public static void Charge(KarmaGame game, KarmaEnemy owner, Transform marker, float seconds)
        {
            for (int i = 0; i < rangedFrames.Length; i++) RangedFrame(i);
            var node = new GameObject("SWBasic sword preparation / no damage"); node.transform.SetParent(game.World, false);
            var fx = node.AddComponent<KarmaSwordBasicPresentation>(); fx.Initialize(game, owner);
            fx.preparing = true; fx.scope = marker; fx.life = seconds;
        }
        public static void Ranged(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot,
            Vector2 velocity, Vector2 size, Vector2 sw01Footprint)
        {
            KarmaSwordAttackPolish.Core(shot);
            Projectile(game, owner, shot, velocity, size);
            var fx = shot.GetComponent<KarmaSwordBasicPresentation>();
            fx.boundaryFlight = true; fx.rangedFootprint = sw01Footprint * 0.5f;
            fx.PaintRangedSprite();
        }
        void PaintRangedSprite()
        {
            // Read the existing frames without changing SW01's assets or VFX.
            // Use its exact source-frame fit at half the world-space dimensions,
            // so the rendered crescent matches at corresponding playback frames.
            source.sprite = RangedFrame((int)(age * 30) % rangedFrames.Length);
            var bounds = source.sprite.bounds.size;
            source.transform.localScale = new Vector3(rangedFootprint.x / (bounds.x * 0.65f),
                rangedFootprint.y / (bounds.y * 0.81f), 1);
        }
        public static void Projectile(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot, Vector2 velocity, Vector2 size)
        {
            var fx = shot.gameObject.AddComponent<KarmaSwordBasicPresentation>(); fx.Initialize(game, owner);
            fx.source = shot.Visual; fx.size = size; fx.velocity = velocity; fx.scope = shot.transform;
            fx.melee = velocity.sqrMagnitude < 0.001f;
            if (fx.melee) fx.source.enabled = false;
            else
            {
                // Use a compact portrait crescent inside the existing hitbox,
                // not the horizontal shard/shaft that read as an arrow.
                Vector2 footprint = new Vector2(Mathf.Min(size.x * 0.42f, size.y * 0.60f), size.y * 0.94f);
                KarmaWaveArtwork.Apply(fx.source, "Sword_Crescent_v6", footprint, Vector2.right);
                fx.source.flipX = velocity.x < 0;
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - (velocity.x < 0 ? 180 : 0);
                fx.source.transform.localRotation = Quaternion.Euler(0, 0, angle);
                fx.source.color = Color.white;
            }
        }
        public static void Impact(KarmaGame game, KarmaEnemy owner, Vector2 at)
        {
            var pool = KarmaSwordVfxPool.For(game);
            pool.Flash(owner, "Sword_Impact_v6", at, new Vector2(1.25f, 1.55f), 0.10f, 18 * owner.Facing);
            pool.Sparks(owner, at, new Vector2(owner.Facing, 0.3f), 3, 5);
        }
        Vector2 SwordAnchor()
        {
            var bounds = sword != null ? sword.bounds : owner.DamageBounds;
            string pose = animator != null ? animator.CurrentPose : null;
            Vector2 socket = pose == "Windup" ? new Vector2(-0.12f, 0.58f)
                : pose == "Wave" || pose == "Slash" ? new Vector2(0.55f, 0.28f) : new Vector2(0.55f, -0.55f);
            return (Vector2)bounds.center + new Vector2(owner.Facing * bounds.extents.x * socket.x, bounds.extents.y * socket.y);
        }
        void Stroke(float width, Color color)
        {
            if (stroke >= Strokes) return;
            int offset = stroke++ * VerticesPerStroke;
            for (int p = 0; p < Points; p++)
            {
                float t = p / (float)(Points - 1), taper = 1 - Mathf.Abs(2 * t - 1);
                Vector2 tangent = buffer.path[Mathf.Min(p + 1, Points - 1)] - buffer.path[Mathf.Max(0, p - 1)];
                Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * width * taper;
                for (int lane = 0; lane < 4; lane++)
                {
                    float side = lane == 0 ? -1 : lane == 1 ? -0.22f : lane == 2 ? 0.22f : 1;
                    int v = offset + p * 4 + lane;
                    buffer.vertices[v] = buffer.path[p] + normal * side;
                    buffer.colors[v] = new Color(color.r, color.g, color.b, lane == 0 || lane == 3 ? 0 : color.a * taper);
                }
            }
        }
        static Color Alpha(Color c, float a) { c.a = a; return c; }
        void Edge(float width, float alpha)
        { Stroke(width * 2.2f, Alpha(Cyan, alpha * 0.5f)); Stroke(width * 0.6f, Alpha(White, alpha)); }
        void Line(Vector2 from, Vector2 to, float width, float alpha)
        {
            for (int p = 0; p < Points; p++) buffer.path[p] = Vector2.Lerp(from, to, p / (float)(Points - 1));
            Edge(width, alpha);
        }
        void PaintCharge()
        {
            transform.position = SwordAnchor(); float t = Mathf.Clamp01(age / Mathf.Max(life, 0.01f));
            Vector2 axis = animator != null && animator.CurrentPose == "Windup"
                ? new Vector2(-owner.Facing, 0.20f).normalized : new Vector2(owner.Facing, -0.3f).normalized;
            Line(-axis * 0.25f, axis * 1.1f, 0.085f, 0.4f + t * 0.45f);
            for (int a = 0; a < 2; a++)
            {
                float y = Mathf.Repeat(age * 4 + a * 0.5f, 1) - 0.35f;
                Line(new Vector2(-0.38f, y), new Vector2(0.1f, y + 0.22f), 0.045f, 0.6f);
            }
        }
        void PaintMelee()
        {
            float sweep = Mathf.Clamp01(age / KarmaCharacterAnimator.BasicSwingSeconds), fade = 1 - Mathf.Clamp01((age - 0.09f) / 0.17f);
            float start = Mathf.Max(0, sweep - 0.85f), end = Mathf.Min(1, sweep + 0.15f);
            for (int p = 0; p < Points; p++)
            {
                float angle = Mathf.Lerp(0.95f, -0.95f, Mathf.Lerp(start, end, p / (float)(Points - 1)));
                buffer.path[p] = new Vector2(owner.Facing * size.x * (0.11f + Mathf.Cos(angle) * 0.34f), Mathf.Sin(angle) * size.y * 0.44f);
            }
            Vector2 root = buffer.path[0]; Edge(0.28f, fade);
            if (age < 0.09f)
            {
                Vector2 anchor = SwordAnchor() - (Vector2)transform.position;
                // Keep the existing connecting cut attached to the sword;
                // clamping its origin to the low hitbox left a visible gap.
                // Only the faint cyan sweep reaches upward. The white cutting
                // arc remains inside the unchanged damaging footprint.
                for (int p = 0; p < Points; p++) buffer.path[p] = Vector2.Lerp(anchor, root, p / (float)(Points - 1));
                Stroke(0.075f, Alpha(Cyan, (1 - age / 0.09f) * 0.55f));
            }
        }
        void PaintRanged()
        {
            if (boundaryFlight) PaintRangedSprite();
            Vector2 axis = velocity.normalized, normal = new Vector2(-axis.y, axis.x) * (velocity.x < 0 ? -1 : 1);
            // Occlude the first few frames inside the boss body, then reveal the
            // real projectile as it clears the sword. Spawn/contact stay intact.
            source.sortingOrder = geometry.sortingOrder = age < 0.05f ? 4 : 26;
            float width = boundaryFlight ? rangedFootprint.x : Mathf.Min(size.x * 0.42f, size.y * 0.60f);
            float height = boundaryFlight ? rangedFootprint.y : size.y * 0.94f;
            for (int p = 0; p < Points; p++)
            {
                float y = p / (float)(Points - 1) * 2 - 1;
                buffer.path[p] = axis * (width * (0.37f - 0.54f * y * y)) + normal * (height * y * 0.48f);
            }
            Edge(0.065f, 0.9f);
            for (int a = -1; a <= 1; a += 2)
            {
                // Short, changing zigzags hug the curved blade. No shaft,
                // arrowhead, trailing ribbon or long horizontal core.
                for (int p = 0; p < Points; p++)
                {
                    float t = p / (float)(Points - 1), y = a * (0.18f + t * 0.24f);
                    float jag = p % 2 == 0 ? 0 : Mathf.Sin(Mathf.Floor(age * 28) * 2.3f + p + a) * 0.055f;
                    buffer.path[p] = axis * (width * (0.37f - 0.54f * y * y * 4) + jag)
                        + normal * (height * y);
                }
                Edge(0.035f, 0.65f);
            }
            if (age >= nextEcho)
            { pool.Echo(owner, source, 0.16f, 0.045f, transform); nextEcho = age + 0.05f; }
        }
        void LateUpdate()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned
                || !boundaryFlight && owner.CurrentPattern != (int)EnemyAttack.SWBasic
                || scope == null || !scope.gameObject.activeInHierarchy)
            {
                if (preparing) Destroy(gameObject);
                else if (boundaryFlight) { gameObject.SetActive(false); Destroy(gameObject); }
                else { if (source != null) source.enabled = false; enabled = false; }
                return;
            }
            if (game.Paused || buffer == null) return;
            age += Time.deltaTime;
            if (preparing && age >= life) { Destroy(gameObject); return; }
            stroke = 0; System.Array.Clear(buffer.colors, 0, buffer.colors.Length);
            if (preparing) PaintCharge();
            else
            {
                if (!released)
                {
                    released = true; Vector2 axis = melee ? new Vector2(owner.Facing, -0.25f).normalized : velocity.normalized;
                    pool.Flash(owner, "Sword_Release_v6", SwordAnchor(), new Vector2(1.55f, 0.22f), 0.08f,
                        Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg, transform);
                    pool.Sparks(owner, SwordAnchor(), axis, 2, 4, transform);
                }
                if (melee) PaintMelee(); else PaintRanged();
            }
            buffer.mesh.SetVertices(buffer.vertices, 0, buffer.vertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            buffer.mesh.SetColors(buffer.colors);
            var bounds = new Bounds(Vector3.zero, new Vector3(16, 16, 2));
            buffer.mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, stroke * (Points - 1) * 18)
                { firstVertex = 0, vertexCount = buffer.vertices.Length, bounds = bounds },
                UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds | UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            buffer.mesh.bounds = bounds;
        }
        void OnDisable()
        {
            if (pool != null) pool.Cancel(transform);
            if (filter != null) filter.sharedMesh = null;
            if (geometry != null) geometry.enabled = false;
            if (buffer == null) return;
            if (spare.Count < 4) spare.Push(buffer); else if (buffer.mesh != null) Object.Destroy(buffer.mesh);
            buffer = null;
        }
        void OnDestroy() { OnDisable(); }
    }

    // SW04-only success cue. An ice-white ring stays at the parry point.
    // No text or combat component.
    public sealed class KarmaSwordParryFeedback : MonoBehaviour
    {
        KarmaGame game; KarmaEnemy owner; LineRenderer ring; float age;
        const float Life = 0.26f;
        public void Initialize(KarmaGame director, KarmaEnemy enemy)
        {
            game = director; owner = enemy;
            ring = gameObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 40;
            ring.sortingOrder = 38; ring.numCornerVertices = 2;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            Apply();
        }
        void Update()
        {
            if (game == null || owner == null || !owner.PresentationActive)
            { Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime;
            if (age >= Life) { Destroy(gameObject); return; }
            Apply();
        }
        void Apply()
        {
            float t = Mathf.Clamp01(age / Life);
            transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.45f, 1 - (1-t)*(1-t));
            ring.widthMultiplier = Mathf.Lerp(0.11f, 0.018f, t);
            ring.startColor = ring.endColor = Color.Lerp(new Color(0.9f, 0.98f, 1, 1), new Color(0.25f, 0.72f, 1, 0), t);
        }
    }
}
