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
        public static void FlowWarning(KarmaGame game, SpriteRenderer marker, Vector2 from, Vector2 to)
        {
            if (marker == null) return;
            var flow = marker.GetComponent<KarmaSwordPathWarning>();
            if (flow == null) flow = marker.gameObject.AddComponent<KarmaSwordPathWarning>();
            flow.SetPath(game, marker, to.x >= from.x ? 1 : -1);
        }
        public static void CounterWarning(KarmaGame game, SpriteRenderer marker, Vector2 from, Vector2 to, Vector2 size, bool locked)
        {
            if (marker == null) return;
            var flow = marker.GetComponent<KarmaSwordPathWarning>();
            if (flow == null) flow = marker.gameObject.AddComponent<KarmaSwordPathWarning>();
            flow.SetCounterPath(game, marker, from, to, size, locked);
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

    // SW01/SW02/SW04 keep the shared swept footprint and lifetime, replacing only
    // its box outline with open blue currents. No body or damage geometry writes.
    [DefaultExecutionOrder(450)]
    public sealed class KarmaSwordPathWarning : MonoBehaviour
    {
        const int Segments = 48, Capacity = Segments * 6;
        readonly Vector3[] vertices = new Vector3[Capacity * 4];
        readonly Color[] colors = new Color[Capacity * 4];
        Mesh mesh; MeshRenderer display; SpriteRenderer source; LineRenderer[] genericLines;
        KarmaGame game; float age, direction; int count;
        Vector2 pathAlong = Vector2.right, pathAcross = Vector2.up;
        float pathWidth, strength = 1;
        public void SetCounterPath(KarmaGame director, SpriteRenderer marker, Vector2 from, Vector2 to, Vector2 size, bool locked)
        {
            Vector2 delta = to - from;
            Vector2 axis = delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector2.right;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            // The shared marker is an unrotated world bounding box. Map the
            // current into its actual swept blade rectangle, including both
            // end caps, so diagonal safe corners remain visibly open.
            pathAlong = transform.InverseTransformVector(axis * (delta.magnitude + size.x));
            pathAcross = transform.InverseTransformVector(normal * size.y);
            pathWidth = size.y;
            strength = locked ? 1 : 0.65f;
            SetPath(director, marker, 1);
        }
        public void SetPath(KarmaGame director, SpriteRenderer marker, float facing)
        {
            game = director; source = marker; direction = facing;
            if (mesh == null)
            {
                mesh = new Mesh { name = "Sword flowing path warning" }; mesh.MarkDynamic();
                var indices = new int[Capacity * 6];
                for (int i = 0; i < Capacity; i++)
                { int v = i * 4, n = i * 6; indices[n] = v; indices[n+1] = v+1; indices[n+2] = v+2; indices[n+3] = v+2; indices[n+4] = v+1; indices[n+5] = v+3; }
                mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = indices;
                var node = new GameObject("Open sword current / warning only"); node.transform.SetParent(transform, false);
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                display = node.AddComponent<MeshRenderer>();
                display.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
                display.sortingLayerID = source.sortingLayerID; display.sortingOrder = 16;
                display.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; display.receiveShadows = false;
                genericLines = source.GetComponentsInChildren<LineRenderer>();
            }
            Paint();
        }
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float alphaA, float alphaB)
        {
            int v = count++ * 4;
            vertices[v] = Project(a); vertices[v+1] = Project(b); vertices[v+2] = Project(c); vertices[v+3] = Project(d);
            alphaA *= strength; alphaB *= strength;
            colors[v] = colors[v+1] = new Color(0.20f + 0.45f * EdgeGlint(a.x + 0.5f),0.68f + 0.30f * EdgeGlint(a.x + 0.5f),1.15f,alphaA);
            colors[v+2] = colors[v+3] = new Color(0.20f + 0.45f * EdgeGlint(c.x + 0.5f),0.68f + 0.30f * EdgeGlint(c.x + 0.5f),1.15f,alphaB);
        }
        Vector2 Project(Vector2 point) => pathAlong * point.x + pathAcross * point.y;
        float Envelope(float u) => 0.12f + 0.88f * Mathf.Pow(Mathf.Clamp01(Mathf.Sin(u * Mathf.PI)), 0.45f);
        float EdgeGlint(float u)
        {
            if (direction < 0) u = 1 - u;
            // Broad, quiet blade facets along the border; the center stays open.
            float crest = 0;
            for (int i = 0; i < 3; i++)
            {
                float d = u - (0.24f + i * 0.28f);
                float taper = 1 - Mathf.Clamp01(Mathf.Abs(d) / (d < 0 ? 0.16f : 0.07f));
                crest = Mathf.Max(crest, taper * taper);
            }
            return crest;
        }
        void Paint()
        {
            if (mesh == null) return;
            source.enabled = false;
            source.forceRenderingOff = true;
            foreach (var line in genericLines) if (line != null) line.enabled = false;
            count = 0;
            float pulse = 0.85f + 0.15f * Mathf.Sin(age * 7);
            // Keep the full damage footprint legible, but feather the two
            // borders inward. There are no center waves or travelling stripes.
            for (int i = 0; i < Segments; i++)
            {
                float u = i/(float)Segments, v = (i+1f)/Segments;
                float x = u-0.5f, z = v-0.5f;
                float a = Envelope(u), b = Envelope(v);
                Quad(new Vector2(x,-0.5f),new Vector2(x,0),new Vector2(z,-0.5f),new Vector2(z,0),0.03f,0.03f);
                Quad(new Vector2(x,0),new Vector2(x,0.5f),new Vector2(z,0),new Vector2(z,0.5f),0.03f,0.03f);
                float insetA=0.012f+0.012f*Mathf.Sin(u*Mathf.PI)*Mathf.Sin(u*Mathf.PI*2-age*1.5f);
                float insetB=0.012f+0.012f*Mathf.Sin(v*Mathf.PI)*Mathf.Sin(v*Mathf.PI*2-age*1.5f);
                for(int side=-1;side<=1;side+=2)
                {
                    float edge=side*0.5f,crestA=side*(0.48f-insetA),crestB=side*(0.48f-insetB);
                    float gainA=(0.65f+a*0.35f)*pulse*(0.17f+0.06f*EdgeGlint(u)),gainB=(0.65f+b*0.35f)*pulse*(0.17f+0.06f*EdgeGlint(v));
                    Quad(new Vector2(x,edge),new Vector2(x,crestA),new Vector2(z,edge),new Vector2(z,crestB),gainA,gainB);
                    colors[(count-1)*4].a=colors[(count-1)*4+2].a=0;
                    Quad(new Vector2(x,crestA),new Vector2(x,side*(0.40f-insetA)),
                        new Vector2(z,crestB),new Vector2(z,side*(0.40f-insetB)),gainA,gainB);
                    colors[(count-1)*4+1].a=colors[(count-1)*4+3].a=0;
                }
            }
            mesh.SetVertices(vertices); mesh.SetColors(colors);
            // Aimed Basic paths rotate inside the marker; include their real extents.
            mesh.RecalculateBounds();
        }
        void LateUpdate() { if (game != null && !game.Paused) age += Time.deltaTime; Paint(); }
        void OnDisable() { if (display != null) display.enabled = false; }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }

    // A room-local, fixed-size sprite pool. Dense SW03 rounds never allocate
    // cosmetic GameObjects per spark or duplicate full-height glow columns.
    public sealed class KarmaSwordVfxPool : MonoBehaviour
    {
        public const int Capacity = 96;
        sealed class Entry
        {
            public SpriteRenderer renderer;
            public KarmaSwordSkillSurface surface;
            public Material material;
            public KarmaEnemy owner;
            public Transform scope;
            public Vector3 velocity, scale;
            public Color color;
            public float age, life, growth;
            public bool active, echo;
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
                // Allocate the per-slot component/property block during pool
                // preparation, before a dense release first fills all 96 slots.
                KarmaSwordSkillSurface.Apply(sr, 4);
                node.SetActive(false); entries[i] = new Entry { renderer = sr, surface = sr.GetComponent<KarmaSwordSkillSurface>(), material = sr.sharedMaterial };
            }
        }
        Entry Next(KarmaEnemy owner, Sprite sprite, Vector2 at, Vector3 scale, float seconds, Color color, float angle, Vector2 velocity, float growth, Transform scope = null)
        {
            Ensure(); Entry e = null;
            for (int i = 0; i < entries.Length; i++) { var candidate = entries[(cursor + i) % entries.Length]; if (!candidate.active) { e = candidate; cursor = (cursor + i + 1) % entries.Length; break; } }
            if (e == null) { e = entries[cursor]; cursor = (cursor + 1) % entries.Length; RecycledLive++; }
            e.owner = owner; e.scope = scope; e.active = true; e.echo = false; e.age = 0; e.life = Mathf.Max(0.04f, seconds); e.color = color; e.velocity = velocity; e.scale = scale; e.growth = growth;
            var sr = e.renderer; sr.sprite = sprite; sr.color = color; sr.flipX = sr.flipY = false; sr.sortingOrder = 29;
            if (e.surface != null) { sr.sharedMaterial = e.material; e.surface.Restart(4); }
            else
            {
                // Preserve retry behavior if the material was unavailable when
                // the pool was prepared (for example during an Editor import).
                KarmaSwordSkillSurface.Apply(sr, 4);
                e.surface = sr.GetComponent<KarmaSwordSkillSurface>();
                e.material = sr.sharedMaterial;
            }
            sr.transform.position = at; sr.transform.rotation = Quaternion.Euler(0, 0, angle); sr.transform.localScale = scale;
            sr.gameObject.SetActive(true); return e;
        }
        public void Flash(KarmaEnemy owner, string pose, Vector2 at, Vector2 size, float seconds, float angle = 0, Transform scope = null, Material material = null)
        {
            var sprite = KarmaWaveArtwork.Sprite(pose); if (sprite == null) return;
            var entry = Next(owner, sprite, at, new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1), seconds, Color.white, angle, Vector2.zero, 0.15f, scope);
            if (material != null) entry.renderer.sharedMaterial = material;
        }
        public void Echo(KarmaEnemy owner, SpriteRenderer source, float alpha, float seconds, Transform scope = null)
        {
            var e = Next(owner, source.sprite, source.transform.position, source.transform.lossyScale, seconds,
                new Color(0.3f, 0.78f, 1, alpha), source.transform.eulerAngles.z, Vector2.zero, -0.08f, scope != null ? scope : source.transform);
            e.echo = true;
            e.renderer.flipX = source.flipX; e.renderer.flipY = source.flipY; e.renderer.sortingOrder = source.sortingOrder - 1;
            // A basic crescent echo uses the same emitted-light surface as its
            // source. Other skill slots retain their existing surface behavior.
            if (source.sharedMaterial == KarmaSwordBasicPresentation.CrescentMaterial)
                e.renderer.sharedMaterial = source.sharedMaterial;
        }
        public void Sparks(KarmaEnemy owner, Vector2 at, Vector2 direction, int count, float speed, Transform scope = null, Material material = null)
        {
            var sprite = KarmaWaveArtwork.Sprite("Sword_Shard_v6");
            if (sprite == null) return;
            float center = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < count; i++)
            {
                float angle = center + (i - (count - 1) * 0.5f) * 0.55f;
                Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float length = 0.35f + (i % 3) * 0.17f;
                var entry = Next(owner, sprite, at, new Vector3(length / sprite.bounds.size.x, 0.05f / sprite.bounds.size.y, 1),
                    0.16f + (i % 3) * 0.035f, Color.white, angle * Mathf.Rad2Deg, axis * speed, -0.35f, scope);
                if (material != null) entry.renderer.sharedMaterial = material;
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
                float t = e.age / e.life; var color = e.color;
                color.a *= e.echo ? (1 - t) * (1 - t) : 1 - t * t;
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

    // Basic curved anticipation and melee use recycled meshes. Ranged release delegates
    // to the unchanged SW01 flight presentation without attaching this component.
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
        static readonly Color Cyan = new Color(0.18f, 1.05f, 1.7f, 1), White = new Color(0.85f, 1.25f, 1.6f, 1);
        KarmaGame game; KarmaEnemy owner; KarmaSwordVfxPool pool;
        SpriteRenderer sword, source; KarmaCharacterAnimator animator;
        Transform scope;
        MeshRenderer geometry; MeshFilter filter; Buffers buffer;
        Vector2 size;
        float age, life;
        int stroke, strikeIndex;
        float facing;
        bool released, meleeWarning;
        LineRenderer[] warningLines;
        static Material crescentMaterial;
        internal static Material CrescentMaterial => crescentMaterial != null ? crescentMaterial
            : crescentMaterial = Resources.Load<Material>("SwordWave/Shared/SWBasicCrescent");

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
        public static void MeleeWarning(KarmaGame game, KarmaEnemy owner, SpriteRenderer marker,
            Vector2 size, float seconds, int strike = 0)
        {
            var node = new GameObject("SWBasic curved cut anticipation / no damage");
            node.transform.SetParent(game.World, false);
            var fx = node.AddComponent<KarmaSwordBasicPresentation>(); fx.Initialize(game, owner);
            fx.meleeWarning = true; fx.scope = marker.transform; fx.life = seconds;
            fx.source = marker; fx.size = size; fx.facing = owner.Facing; fx.strikeIndex = strike;
            fx.warningLines = marker.GetComponentsInChildren<LineRenderer>();
            fx.geometry.sortingLayerID = marker.sortingLayerID; fx.geometry.sortingOrder = 17;
            fx.PaintWarning(); fx.UploadGeometry();
        }
        public static void Ranged(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot,
            Vector2 velocity, Vector2 size, Vector2 sw01Footprint)
        {
            KarmaSwordAttackPolish.Core(shot);
            // Share SW01's actual sprite renderer, frame fitting, material,
            // electrical geometry and echoes. The shot retains Basic's size,
            // speed, source ID and contact rules; size is never a visual scale.
            KarmaSwordWaveAnimation.Flight(game, owner, shot, velocity, sw01Footprint);
            KarmaSwordWaveAnimation.Release(game, owner, shot.transform.position, sw01Footprint, false, velocity.x);
        }
        public static void Projectile(KarmaGame game, KarmaEnemy owner, KarmaBossAttack shot, Vector2 velocity, Vector2 size)
        {
            var fx = shot.gameObject.AddComponent<KarmaSwordBasicPresentation>(); fx.Initialize(game, owner);
            fx.source = shot.Visual; fx.size = size; fx.scope = shot.transform;
            fx.strikeIndex = fx.animator != null ? fx.animator.BasicStrikeIndex : 0;
            fx.facing = owner.Facing;
            fx.source.enabled = false;
        }

        public static void Impact(KarmaGame game, KarmaEnemy owner, Vector2 at)
        {
            var pool = KarmaSwordVfxPool.For(game);
            pool.Flash(owner, "Sword_Impact_v6", at, new Vector2(1.5f, 1.75f), 0.12f, 18 * owner.Facing, material: CrescentMaterial);
            pool.Sparks(owner, at, new Vector2(owner.Facing, 0.3f), 5, 5, material: CrescentMaterial);
        }
        Vector2 SwordAnchor()
        {
            if (KarmaSwordWaveAnimation.BladePoints(sword, out _, out var bend, out _)) return bend;
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
        void PaintMelee()
        {
            float sweep = Mathf.Clamp01(age / KarmaCharacterAnimator.BasicSwingSeconds), fade = 1 - Mathf.Clamp01(age / KarmaCharacterAnimator.BasicSwingSeconds);
            float start = Mathf.Max(0, sweep - 0.85f), end = Mathf.Min(1, sweep + 0.15f);
            for (int p = 0; p < Points; p++)
            {
                float angle = Mathf.Lerp(0.95f, -0.95f, Mathf.Lerp(start, end, p / (float)(Points - 1)))
                    * (strikeIndex == 0 ? 1 : -1);
                buffer.path[p] = new Vector2(facing * size.x * (0.11f + Mathf.Cos(angle) * 0.34f), Mathf.Sin(angle) * size.y * 0.44f);
            }
            Vector2 root = buffer.path[0]; Edge(0.16f, fade);
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
        void PaintWarning()
        {
            // A quiet blade-shaped arc describes the melee cut, unlike a travelling
            // wave's two lane borders. Four tiny notches retain the committed box.
            source.enabled = false; source.forceRenderingOff = true;
            foreach (var line in warningLines) if (line != null) line.enabled = false;
            transform.position = scope.position;
            float t = Mathf.Clamp01(age / Mathf.Max(life, 0.001f));
            float ready = Mathf.SmoothStep(0, 1, t);
            float sign = strikeIndex == 0 ? 1 : -1;
            for (int p = 0; p < Points; p++)
            {
                float angle = Mathf.Lerp(0.95f, -0.95f, p / (float)(Points - 1)) * sign;
                buffer.path[p] = new Vector2(facing * size.x * (0.11f + Mathf.Cos(angle) * 0.34f),
                    Mathf.Sin(angle) * size.y * 0.44f);
            }
            Stroke(0.045f, new Color(0.52f, 0.80f, 0.96f, 0.20f + ready * 0.22f));
            // The bright short stroke travels in the actual first/return-cut direction.
            for (int p = 0; p < Points; p++)
            {
                float u = Mathf.Lerp(Mathf.Max(0, ready - 0.22f), ready, p / (float)(Points - 1));
                float angle = Mathf.Lerp(0.95f, -0.95f, u) * sign;
                buffer.path[p] = new Vector2(facing * size.x * (0.11f + Mathf.Cos(angle) * 0.34f),
                    Mathf.Sin(angle) * size.y * 0.44f);
            }
            Stroke(0.055f, new Color(0.88f, 0.96f, 1, 0.45f + ready * 0.30f));
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
            {
                Vector2 edge = new Vector2(x * size.x * 0.49f, y * size.y * 0.46f);
                for (int p = 0; p < Points; p++)
                    buffer.path[p] = edge - Vector2.right * x * size.x * 0.055f * p / (Points - 1);
                Stroke(0.022f, new Color(0.42f, 0.68f, 0.86f, 0.25f + ready * 0.16f));
            }
        }
        void LateUpdate()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned
                || owner.CurrentPattern != (int)EnemyAttack.SWBasic
                || scope == null || !scope.gameObject.activeInHierarchy)
            {
                if (meleeWarning) { if (geometry != null) geometry.enabled = false; Destroy(gameObject); }
                else { if (source != null) source.enabled = false; enabled = false; }
                return;
            }
            if (game.Paused || buffer == null) return;
            age += Time.deltaTime;
            // The attack owns a melee warning's end, including frame drops.
            // Hold the fully ready arc until its marker is actually retired.
            stroke = 0; System.Array.Clear(buffer.colors, 0, buffer.colors.Length);
            if (meleeWarning) PaintWarning();
            else
            {
                if (!released)
                {
                    released = true; Vector2 axis = new Vector2(facing, strikeIndex == 0 ? -0.35f : 0.35f).normalized;
                    pool.Flash(owner, "Sword_Release_v6", SwordAnchor(), new Vector2(1.8f, 0.28f), 0.10f,
                        Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg, transform, CrescentMaterial);
                    pool.Sparks(owner, SwordAnchor(), axis, 3, 4, transform, CrescentMaterial);
                }
                PaintMelee();
            }
            UploadGeometry();
        }
        void UploadGeometry()
        {
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
