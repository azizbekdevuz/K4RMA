using UnityEngine;

namespace KarmaPrototype
{
    // SwordWave-only, one draw per layer. Fixed buffers and recycled meshes replace
    // hundreds of particle/line objects; motion never touches gameplay geometry.
    public sealed class KarmaSW05Energy : MonoBehaviour
    {
        enum Kind { Form = 1, Fall, Landing, Charge, Detonation, Eruption, Scar, ScarWarning, ScarFade, WardRelease, WardImpact, WardCollapse, Planted, Splinters, GroundStrike }
        sealed class Buffer
        {
            public readonly Mesh mesh;
            public readonly Vector3[] vertices;
            public readonly Color[] colors;
            public readonly int capacity;
            public Splinter[] splinters;
            public LandingSite[] landings;
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
        const int SplinterLimit = 384;
        const string SplinterName = "Sword pooled impact splinters / no damage";
        struct Splinter
        {
            public Vector2 origin, velocity;
            public float born, life, length, left, right, floor, ceiling;
        }
        int splinterCount, splinterFrame = -1;
        const string LandingName = "SW05 batched grounded landings / no damage";
        const float LandingSeconds = 0.34f;
        struct LandingSite { public Vector2 at, axis; public float width, born; public int seed; }
        int landingCount, sampledFrame = -1;
        static int nextSeed;
        static readonly Color Blue = new Color(0.08f, 0.55f, 1.1f), White = new Color(0.75f, 1.15f, 1.4f);
        KarmaGame game; Transform follow; KarmaSW05RainBlade rain;
        SpriteRenderer bladePicture;
        MeshRenderer display; MeshFilter filter; Buffer buffer;
        Kind kind; Vector2 at, size; Vector3[] sites, zones;
        struct ElectricSite
        {
            public Vector2 foot, hilt;
            public float width;
        }
        Vector2[] chargeFeet, chargeHilts;
        KarmaSW05RainBlade[] chargeSwords;
        const float DetonationVisualSeconds = 0.48f;
        SpriteRenderer[] plantedPictures;
        int plantedCount;
        const string PlantedName = "SW05 planted electrical lattice / no damage";
        float age, life; int count, seed, scarSiteCount;
        int detonationFrame = -1;
        float charge,scarFloor;
        public bool Detonated => kind == Kind.Detonation;
        public bool CapacityExceeded { get; private set; }
        public float Age => age;
        // Separate from sword tint, material radiance and final explosion power.
        [Range(0,2)] public float AuraIntensity = 1;
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
            fx.display = go.AddComponent<MeshRenderer>(); fx.display.sharedMaterial = Resources.Load<Material>("SwordWave/SW05/SW05Celestial");
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_Mode",6); fx.display.SetPropertyBlock(properties);
            fx.display.sortingOrder = kind == Kind.Charge ? 28 : 30;
            fx.display.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fx.display.receiveShadows = false;
            fx.Upload(); return fx;
        }
        public static void GroundStrike(Transform root, KarmaGame game, Vector2 ground, float bodyHeight)
        {
            var fx = Create(root, game, Kind.GroundStrike, 0.38f);
            fx.name = "SW03 first ground strike / no damage";
            fx.at = ground; fx.size = new Vector2(bodyHeight * 0.60f, bodyHeight * 0.12f);
            fx.count = 0; fx.GroundStrikeEnergy(); fx.Upload();
        }
        void GroundStrikeEnergy()
        {
            float t = Mathf.Clamp01(age / life);
            float reveal = Mathf.SmoothStep(0, 1, age / 0.045f);
            float fade = 1 - Mathf.SmoothStep(0, 1, (t - 0.15f) / 0.85f);
            // Fixed world-space cracks, shallow enough to read as a floor hit.
            // No random walk, expanding hazard or new collision geometry.
            for (int ray = 0; ray < 7; ray++)
            {
                float a = ray * Mathf.PI * 2 / 7;
                Vector2 previous = at;
                for (int j = 1; j <= 5; j++)
                {
                    float u = j / 5f;
                    Vector2 next = at + new Vector2(Mathf.Cos(a) * size.x * u,
                        Mathf.Sin(a) * size.y * u + Mathf.Sin(ray * 7 + j * 4) * size.y * 0.18f);
                    next.x = Mathf.Clamp(next.x, KarmaStage.ArenaLeft + 0.02f, KarmaStage.ArenaRight - 0.02f);
                    next.y = Mathf.Max(0.025f, next.y);
                    Vector2 tip = Vector2.Lerp(previous, next, Mathf.Clamp01(reveal * 5 - j + 1));
                    Line(previous, tip, 0.028f * (1 - u * 0.65f), fade * (1 - u * 0.55f));
                    if (j == 3) Line(tip, tip + new Vector2(Mathf.Cos(a + 0.65f) * size.x * 0.13f, 0.045f), 0.012f, fade * 0.45f);
                    previous = next;
                }
            }
            float flash = 1 - Mathf.SmoothStep(0, 1, age / 0.14f);
            Line(at - Vector2.right * size.x * 0.12f, at + Vector2.right * size.x * 0.12f, 0.075f, flash);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 p = at + new Vector2(side * size.x * (0.05f + t * (0.18f + i * 0.025f)),
                    Mathf.Max(0, size.y * (t * 4 - t * t * 5)));
                Shard(p, new Vector2(side * 0.7f, 1).normalized, size.y * 0.30f * (1 - t), fade * 0.45f);
            }
        }
        public static void Blade(Transform root, KarmaGame game, Transform blade, Vector2 size, bool falling)
        {
            var fx = Create(root, game, falling ? Kind.Fall : Kind.Form, float.PositiveInfinity);
            fx.follow = blade; fx.size = size; fx.rain = blade.GetComponentInParent<KarmaSW05RainBlade>();
            fx.bladePicture = blade.GetComponent<SpriteRenderer>();
        }
        // Measured from the approved 512 x 1024 heavy sword: opaque core
        // (57,25)..(453,993), pivot (255,509), crossguard centre (255,739).
        // Transform the actual renderer so formation scale and the short
        // planting settle cannot leave electricity suspended beside the blade.
        static Vector2 SwordPixel(SpriteRenderer sword, Vector2 pixel)
            => sword.transform.TransformPoint((pixel - sword.sprite.pivot) / sword.sprite.pixelsPerUnit);
        public static Vector2 GuardAnchor(SpriteRenderer sword) => SwordPixel(sword, new Vector2(255,739));
        public static Vector2 GroundAnchor(SpriteRenderer sword)
        {
            Vector2 center = sword.transform.position, axis = sword.transform.up;
            return Mathf.Abs(axis.y) > 0.001f ? center + axis * ((0.12f-center.y)/axis.y)
                : new Vector2(center.x,0.12f);
        }
        static ElectricSite PlantedSite(SpriteRenderer sword)
        {
            return new ElectricSite { foot = GroundAnchor(sword), hilt = GuardAnchor(sword),
                width = Mathf.Abs(sword.transform.lossyScale.x) * 3.96f };
        }
        static Transform FindEnergy(Transform root,string name)
        {
            // Effect labels contain '/', which Transform.Find treats as a
            // hierarchy separator. Compare direct children to reuse the batch.
            for(int i=0;i<root.childCount;i++)
            {var child=root.GetChild(i);if(child.gameObject.activeSelf&&child.name==name)return child;}
            return null;
        }
        public static void Planted(Transform root, KarmaGame game, SpriteRenderer sword, Vector2 size)
        {
            var node = FindEnergy(root,PlantedName);
            var fx = node == null ? null : node.GetComponent<KarmaSW05Energy>();
            if (fx == null)
            {
                // This extra SW05 layer must not advance SW04's noise sequence.
                int savedSeed = nextSeed;
                fx = Create(root,game,Kind.Planted,float.PositiveInfinity,4096);
                nextSeed = savedSeed;
                fx.name = PlantedName; fx.plantedPictures = new SpriteRenderer[54];
                fx.display.sortingOrder = 26;
            }
            if (fx.plantedCount >= fx.plantedPictures.Length)
                throw new System.InvalidOperationException("SW05 planted energy exceeds the 54-sword plan.");
            fx.plantedPictures[fx.plantedCount++] = sword;
        }
        public static void Landing(Transform root, KarmaGame game, Vector2 at, float width, Vector2 axis)
        {
            var node=FindEnergy(root,LandingName);
            var fx=node==null?null:node.GetComponent<KarmaSW05Energy>();
            if(fx==null)
            {
                int savedSeed=nextSeed;
                fx=Create(root,game,Kind.Landing,float.PositiveInfinity,4096);nextSeed=savedSeed;
                fx.name=LandingName;fx.display.sortingOrder=32;
                if(fx.buffer.landings==null)fx.buffer.landings=new LandingSite[54];
            }
            if(fx.landingCount>=fx.buffer.landings.Length)
                throw new System.InvalidOperationException("SW05 landing batch exceeds the 54-sword plan.");
            // Predict this frame's sampling time for new sites only. Existing
            // impacts keep advancing when another sword lands in the same batch.
            float born=fx.age+(fx.sampledFrame==Time.frameCount?0:Time.deltaTime);
            fx.buffer.landings[fx.landingCount++]=new LandingSite {
                at=at,axis=axis,width=width,born=born,seed=KarmaSwordScarRise.EnergySeed(at.x) };
        }
        void LandingEnergy()
        {
            for(int i=0;i<landingCount;)
            {
                var site=buffer.landings[i];float elapsed=Mathf.Max(0,age-site.born);
                if(elapsed>=LandingSeconds)
                {buffer.landings[i]=buffer.landings[--landingCount];continue;}
                float t=elapsed/LandingSeconds,fade=(1-t)*(1-t),spread=1-(1-t)*(1-t)*(1-t);
                float lo=Mathf.Max(KarmaStage.ArenaLeft,site.at.x-site.width*0.5f);
                float hi=Mathf.Min(KarmaStage.ArenaRight,site.at.x+site.width*0.5f);
                if(hi<=lo){i++;continue;}
                int first=count*4;seed=site.seed;
                Vector2 socket=new Vector2(site.at.x,0.08f);
                // One brief ignition and a shallow pressure ripple, with no
                // tall spokes or duplicate sprite bursts obscuring the sword.
                float flash=1-Mathf.SmoothStep(0,1,elapsed/0.09f);
                ScarLine(socket-Vector2.right*site.width*0.12f,socket+Vector2.right*site.width*0.12f,
                    0.07f*flash,flash*0.9f);
                for(int segment=0;segment<12;segment++)
                {
                    float a=segment*Mathf.PI*2/12,b=(segment+0.82f)*Mathf.PI*2/12;
                    Vector2 radius=new Vector2(site.width*(0.10f+spread*0.37f),0.035f+spread*0.055f);
                    Vector2 center=socket+Vector2.up*0.04f;
                    ScarLine(center+Vector2.Scale(Direction(a),radius),center+Vector2.Scale(Direction(b),radius),
                        0.024f*(1-t),fade*0.7f);
                }
                // Small, fast chips retain momentum and fall back to the floor.
                // The launch leans with the incoming blade; all vertices stay
                // inside the committed landing footprint, including at walls.
                for(int chip=0;chip<6;chip++)
                {
                    float side=chip%2==0?-1:1,n=ScarNoise(chip+811);
                    Vector2 velocity=new Vector2((side*(0.65f+n*1.1f)+site.axis.x*0.35f)*site.width,1.25f+n*1.45f);
                    Vector2 point=socket+velocity*elapsed+Vector2.down*(11*elapsed*elapsed);
                    if(point.y<0.025f || point.x<lo || point.x>hi)continue;
                    float settle=Mathf.SmoothStep(0,1,(point.y-0.025f)/0.055f);
                    Vector2 tangent=(velocity+Vector2.down*(22*elapsed)).normalized;
                    Shard(point,tangent,(0.055f+n*0.075f)*(1-t*0.65f),fade*settle*0.74f);
                }
                for(int v=first;v<count*4;v++)
                {buffer.vertices[v].x=Mathf.Clamp(buffer.vertices[v].x,lo,hi);buffer.vertices[v].y=Mathf.Max(0.015f,buffer.vertices[v].y);}
                i++;
            }
        }
        public static void Eruption(Transform root, KarmaGame game, Vector2 at, float width)
        {
            var fx = Create(root,game,Kind.Eruption,0.2f);
            fx.name = "SW03 synchronized eruption flash / no damage";
            fx.at = at; fx.size = new Vector2(width,1); fx.display.sortingOrder = 34;
        }
        public static KarmaSW05Energy Scar(Transform root,KarmaGame game,Transform attack,Vector2 ground,Vector2 size,float life)
        {
            var fx=Create(root,game,Kind.Scar,life,512);fx.follow=attack;fx.at=ground;fx.size=size;
            fx.seed=KarmaSwordScarRise.EnergySeed(ground.x);
            ScarGroundImpact(root,game,ground,size.x);
            fx.name="SW03 travelling blade lightning / no damage";fx.display.sortingOrder=32;return fx;
        }
        public void SetScarBounds(Vector2 ground,Vector2 visualSize,float visibleHeight)
        {
            scarFloor=ground.y;
            // Follow the translated sprite profile, while ground sparks and
            // the electrical clipping plane remain on the actual surface.
            at=ground-Vector2.up*(visualSize.y-visibleHeight+KarmaSwordScarRise.VisualEmbedDepth(visualSize.y));
            size=visualSize;
        }
        public void SetCharge(float progress){charge=Mathf.Clamp01(progress);}
        public static KarmaSW05Energy ScarWarning(Transform root,KarmaGame game,Vector2 ground,float width,float life)
        {
            var fx=Create(root,game,Kind.ScarWarning,float.PositiveInfinity);fx.at=ground;fx.size=new Vector2(width,1);
            fx.seed=KarmaSwordScarRise.EnergySeed(ground.x);
            fx.name="SW03 electrically carved warning / no damage";fx.display.sortingOrder=11;return fx;
        }
        public static void ScarDissolve(Transform root,KarmaGame game,Vector2 ground,Vector2 size)
        {var fx=Create(root,game,Kind.ScarFade,KarmaSwordScarRise.AuraFadeSeconds);fx.SetScarBounds(ground,size,size.y);fx.seed=KarmaSwordScarRise.EnergySeed(ground.x);fx.display.sortingOrder=33;}
        public static void WardBurst(Transform root,KarmaGame game,Vector2 at,Vector2 axis,float radius,int style)
        {
            var fx=Create(root,game,style==2?Kind.WardCollapse:style==1?Kind.WardImpact:Kind.WardRelease,0.30f,512);
            fx.at=at;fx.size=axis.normalized*radius;fx.display.sortingOrder=37;
            fx.name="SW04 "+(style==2?"barrier disruption":style==1?"defensive contact shock":"directional counter release")+" / no damage";
        }
        public static KarmaSW05Energy Prepare(Transform root, KarmaGame game,
            System.Collections.Generic.List<KarmaSW05RainBlade> swords, float seconds)
        {
            // Build the approved sigil / helix / blade-fan composition from the
            // actual planted renderers. Presentation never changes damage bounds.
            var idle = FindEnergy(root,PlantedName);
            if (idle != null) { idle.gameObject.SetActive(false); Destroy(idle.gameObject); }
            // The detailed fan/sigil mesh needs 11,772 quads for 54 isolated sites.
            // Reserve once and reuse; never truncate later swords or change their shape.
            var fx = Create(root, game, Kind.Charge, seconds, 16384);
            var sorted = swords.ToArray();
            System.Array.Sort(sorted, (a, b) => a.ExplosionCenter.x.CompareTo(b.ExplosionCenter.x));
            fx.chargeSwords = sorted;
            fx.sites = new Vector3[sorted.Length];
            fx.chargeFeet = new Vector2[sorted.Length]; fx.chargeHilts = new Vector2[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                var blade = sorted[i]; var p = blade.ExplosionCenter;
                fx.sites[i] = new Vector3(blade.ElectricFoot.x, p.y, blade.PlantedBlastSize.x);
                fx.chargeFeet[i] = blade.ElectricFoot; fx.chargeHilts[i] = blade.ElectricHilt;
            }
            // Consume the committed gameplay bounds; cosmetic anchors never
            // determine damage. The normal final volley merges into one zone.
            var intervals = new Vector3[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                var blade = sorted[i]; var p = blade.ExplosionCenter; var extent = blade.ExplosionSize;
                intervals[i] = new Vector3(p.x - extent.x / 2, p.x + extent.x / 2, extent.y);
            }
            System.Array.Sort(intervals, (a, b) => a.x.CompareTo(b.x));
            var merged = new System.Collections.Generic.List<Vector3>();
            foreach (var interval in intervals)
            {
                if (merged.Count > 0 && interval.x <= merged[merged.Count - 1].y)
                {
                    var zone = merged[merged.Count - 1]; zone.y = Mathf.Max(zone.y, interval.y);
                    merged[merged.Count - 1] = zone;
                }
                else merged.Add(interval);
            }
            fx.zones = merged.ToArray();
            fx.FinalEnergy(); fx.Upload();
            return fx;
        }
        void RefreshChargeAnchors()
        {
            // The last swords still finish their brief planting settle during
            // charge. Follow those renderers without moving any damage bounds.
            if(chargeSwords==null)return;
            for(int i=0;i<chargeSwords.Length;i++)if(chargeSwords[i]!=null)
            {chargeFeet[i]=chargeSwords[i].ElectricFoot;chargeHilts[i]=chargeSwords[i].ElectricHilt;}
        }
        public void Ignite()
        {
            if (kind != Kind.Charge) return;
            kind = Kind.Detonation; age = 0; life = DetonationVisualSeconds; display.sortingOrder = 4; // Below character bodies (order 5).
            display.enabled = true;
            detonationFrame = Time.frameCount;
            // Replace the charge draw immediately on the real shared release.
            // The first render must see the peak, even on a long release frame.
            count = 0; CapacityExceeded = false;
            FinalEnergy(); Upload();
            foreach(var zone in zones)
            {
                int clusters=Mathf.Clamp(Mathf.CeilToInt((zone.y-zone.x)/1.4f),1,48);
                for(int cluster=0;cluster<clusters;cluster++)
                {
                    float x=Mathf.Lerp(zone.x,zone.y,(cluster+0.3f+Noise(cluster+500)*0.4f)/clusters);
                    EmitSplinters(transform.parent,game,new Vector2(x,0.16f),
                        zone.x,zone.y,zone.z,7,seed+cluster);
                }
            }
            // Historical fade belongs to the encounter, not a 120ms damage
            // owner. There is no delayed residual-electricity phase.
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
        float ScarNoise(int index)
        {
            unchecked
            {
                uint value=(uint)seed^((uint)index*0x9e3779b9u);
                value=(value^(value>>16))*0x7feb352du;
                value=(value^(value>>15))*0x846ca68bu;
                return ((value^(value>>16))>>8)*(1f/16777216);
            }
        }
        static void ScarGroundImpact(Transform root,KarmaGame game,Vector2 ground,float width)
        {
            const string name="SW03 grouped ground discharge / no damage";
            var node=FindEnergy(root,name);
            KarmaSW05Energy fx;
            if(node==null)
            {
                // One pooled mesh/draw for all 26 simultaneous floor impacts.
                // Do not advance the visual sequence of any other skill.
                int savedSeed=nextSeed;
                fx=Create(root,game,Kind.Eruption,0.17f,4096);nextSeed=savedSeed;
                fx.name=name;fx.sites=new Vector3[32];fx.display.sortingOrder=31;
            }
            else fx=node.GetComponent<KarmaSW05Energy>();
            if(fx.scarSiteCount<fx.sites.Length)
                fx.sites[fx.scarSiteCount++]=new Vector3(ground.x,ground.y,width);
            EmitSplinters(root,game,ground+Vector2.up*0.12f,
                ground.x-width*0.46f,ground.x+width*0.46f,2.4f,7,KarmaSwordScarRise.EnergySeed(ground.x));
        }
        // Reuse the existing two-facet shard mesh, material and mesh pool.
        // One draw and at most 384 live fragments per pattern, no physics or
        // per-fragment GameObjects. Full batches drop excess decoration only.
        static void EmitSplinters(Transform root,KarmaGame game,Vector2 at,
            float left,float right,float height,int amount,int seed)
        {
            var node=FindEnergy(root,SplinterName);
            var fx=node==null?null:node.GetComponent<KarmaSW05Energy>();
            if(fx==null)
            {
                int savedSeed=nextSeed;
                fx=Create(root,game,Kind.Splinters,float.PositiveInfinity,SplinterLimit*2);
                nextSeed=savedSeed;fx.name=SplinterName;fx.display.sortingOrder=4;
                if(fx.buffer.splinters==null)fx.buffer.splinters=new Splinter[SplinterLimit];
            }
            fx.splinterFrame=Time.frameCount;fx.display.enabled=true;
            float margin=Mathf.Min(0.12f,(right-left)*0.2f);
            at.x=Mathf.Clamp(at.x,left+margin,right-margin);
            for(int i=0;i<amount&&fx.splinterCount<SplinterLimit;i++)
            {
                float n=Mathf.Repeat(Mathf.Sin((seed%4096+i*31)*12.9898f)*43758.5453f,1);
                float duration=0.24f+n*0.24f;
                float vx=(i%2==0?-1:1)*(2+n*7);
                vx=Mathf.Clamp(vx,(left+margin-at.x)/duration,(right-margin-at.x)/duration);
                fx.buffer.splinters[fx.splinterCount++]=new Splinter {
                    origin=at,velocity=new Vector2(vx,Mathf.Min(height*0.82f/duration,3+n*10)),
                    born=fx.age,life=duration,length=0.12f+n*0.28f,
                    left=left,right=right,floor=at.y-0.12f,ceiling=at.y+height };
            }
            // LateUpdate builds and uploads once after all sites have emitted.
        }
        void SplinterEnergy()
        {
            for(int i=0;i<splinterCount;)
            {
                var s=buffer.splinters[i];float elapsed=age-s.born;
                if(elapsed>=s.life)
                {buffer.splinters[i]=buffer.splinters[--splinterCount];continue;}
                float t=Mathf.Clamp01(elapsed/s.life),fade=(1-t)*(1-t);
                Vector2 p=s.origin+s.velocity*elapsed+Vector2.down*(3*elapsed*elapsed);
                Vector2 axis=(s.velocity+Vector2.down*(6*elapsed)).normalized;
                int first=count*4;
                Shard(p,axis,s.length*(1-t*0.65f),fade*0.9f);
                // Clip the entire facet, including its bright tip, at safe gaps.
                for(int v=first;v<count*4;v++)
                {
                    buffer.vertices[v].x=Mathf.Clamp(buffer.vertices[v].x,s.left,s.right);
                    buffer.vertices[v].y=Mathf.Clamp(buffer.vertices[v].y,s.floor,s.ceiling);
                }
                i++;
            }
        }
        void ScarGroundEnergy()
        {
            float t=Mathf.Clamp01(age/life),fade=(1-t)*(1-t);
            for(int site=0;site<scarSiteCount;site++)
            {
                var origin=sites[site];seed=KarmaSwordScarRise.EnergySeed(origin.x);
                Vector2 ground=new Vector2(origin.x,origin.y+0.055f);
                float w=origin.z,gain=0.62f+ScarNoise(601)*0.14f;
                float radius=w*(0.13f+0.32f*(1-(1-t)*(1-t)));
                // The selected PNG already has a luminous base. Use two
                // outward ground impulses instead of painting a second circle.
                for(int side=-1;side<=1;side+=2)
                {
                    Vector2 p=ground+Vector2.right*side*radius;
                    ScarLine(p-Vector2.right*side*w*0.12f+Vector2.up*0.035f,p,0.030f,fade*gain);
                }
                ScarLine(ground-Vector2.right*w*0.12f,ground+Vector2.right*w*0.12f,0.085f,fade*gain);
                // A shallow, expanding pressure front stays inside this sword's
                // footprint; all 32 sites share the bounded 4096-quad mesh.
                float spread=1-(1-t)*(1-t)*(1-t);
                for(int step=0;step<10;step++)
                {
                    float a=step*Mathf.PI*2/10,b=(step+0.72f)*Mathf.PI*2/10;
                    var radius2=new Vector2(w*(0.12f+spread*0.32f),0.04f+spread*0.09f);
                    Vector2 center=ground+Vector2.up*0.09f;
                    ScarLine(center+Vector2.Scale(Direction(a),radius2),center+Vector2.Scale(Direction(b),radius2),
                        0.018f*(1-t),fade*gain*0.65f);
                }
                // A short grounded jet gives the eruption a distinct attack
                // frame. It vanishes before the quieter sustained electricity.
                float punch=1-Mathf.SmoothStep(0,1,t/0.55f);
                // Three asymmetric glass-like splinters open from the socket,
                // then burn away before the steel blade's sustained electricity.
                for(int blade=-1;blade<=1;blade++)
                {
                    float reach=w*(blade==0?1.05f:0.65f)*(0.45f+spread*0.55f);
                    Vector2 tip=ground+new Vector2(blade*w*0.32f,reach);
                    JudgementBlade(ground,tip,w*0.075f,punch*gain,site*11+blade+20);
                }
                ScarLine(ground-Vector2.right*w*0.15f,ground+Vector2.right*w*0.15f,
                    0.14f*punch,punch);
                for(int i=0;i<4;i++)
                {
                    float side=i%2==0?-1:1,launch=0.65f+ScarNoise(i+621)*0.60f;
                    float x=side*w*(0.08f+0.24f*t);
                    Vector2 p=ground+new Vector2(x,launch*t-0.45f*t*t);
                    Vector2 velocity=new Vector2(side*w*0.24f,launch-0.90f*t).normalized;
                    Shard(p,velocity,(0.18f+ScarNoise(i+631)*0.26f)*(1-t),fade*gain);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    Vector2 p=ground+Vector2.right*side*w*0.16f;
                    ScarArc(p,p+new Vector2(side*w*0.06f,0.45f+ScarNoise(side+651)*0.30f),0.055f,fade*gain,side+661);
                }
            }
        }

        void ScarEnergy()
        {
            float w=size.x,h=size.y;
            if(kind==Kind.ScarWarning)
            {
                float buildup=charge*charge*(3-2*charge);
                float imminent=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.78f,1,charge));
                float pulse=0.32f+buildup*0.38f+imminent*0.30f;
                // A fixed, low fissure shows the committed footprint throughout
                // the countdown. Its luminous center fills as the cast charges.
                Vector2 previous=at+new Vector2(-w*0.43f,0.075f);
                for(int segment=1;segment<=8;segment++)
                {
                    float u=segment/8f;
                    Vector2 next=at+new Vector2(Mathf.Lerp(-w*0.43f,w*0.43f,u),
                        0.075f+(segment==8?0:(ScarNoise(segment+71)-0.5f)*0.075f));
                    float fill=Mathf.SmoothStep(0,1,(buildup-Mathf.Abs(u-0.5f)*1.5f)*4);
                    ScarLine(previous,next,0.018f+fill*0.015f,0.25f+fill*0.50f);
                    previous=next;
                }
                for(int side=-1;side<=1;side+=2)
                {
                    float x=at.x+side*w*0.46f;
                    Line(new Vector2(x,0.05f),new Vector2(x,0.20f+buildup*0.20f),0.035f,pulse);
                    float u=Mathf.Repeat(age*1.6f+ScarNoise(side+22),1);
                    float envelope=Mathf.Sin(u*Mathf.PI);
                    Shard(at+new Vector2(side*w*(0.42f-u*0.24f),0.08f+u*u*0.38f),
                        Vector2.up,0.12f+buildup*0.24f,pulse*envelope*0.65f);
                }
                // Stable floor ellipse communicates the locked area. A second,
                // contracting ripple communicates time without rotating the footprint.
                Ring(at+Vector2.up*0.08f,new Vector2(w*0.44f,0.18f),0,pulse*0.65f,20);
                float convergence=1-buildup;
                Ring(at+Vector2.up*0.09f,new Vector2(w*(0.16f+convergence*0.25f),0.06f+convergence*0.10f),
                    0,pulse*0.45f,16);
                // The same four-point star as the judgement blade, kept low
                // inside the committed floor mark rather than a false hit column.
                Vector2 seal=at+Vector2.up*(0.10f+buildup*0.12f);
                float sealWidth=w*(0.05f+buildup*0.06f),sealHeight=0.12f+buildup*0.24f;
                for(int side=-1;side<=1;side+=2)
                {
                    ScarLine(seal+Vector2.up*sealHeight,seal+Vector2.right*side*sealWidth,
                        0.012f,pulse*0.55f);
                    ScarLine(seal+Vector2.right*side*sealWidth,seal-Vector2.up*0.08f,
                        0.012f,pulse*0.45f);
                }
                if(charge>0.75f)
                {
                    // One closing cue, driven by the real charge progress;
                    // no strobing or extra hazard outside the marked cell.
                    float ready=Mathf.SmoothStep(0,1,(charge-0.75f)/0.25f);
                    ScarLine(at+new Vector2(-w*0.10f,0.10f),at+new Vector2(w*0.10f,0.10f),
                        0.035f,ready);
                    ScarLine(at+Vector2.up*0.08f,at+Vector2.up*(0.12f+ready*0.42f),0.022f,ready*0.65f);
                }
                return;
            }
            if(kind==Kind.ScarFade)
            {
                float t=Mathf.Clamp01(age/life),fade=(1-t)*(1-t);
                Vector2 retreat=-Vector2.up*KarmaSwordScarRise.RetractionDistance(age,size.y);
                int first=count*4;
                for(int i=0;i<4;i++)
                {
                    float y=0.12f+ScarNoise(i+31)*0.74f;
                    float side=i%2==0?-1:1;
                    Vector2 p=ScarEdge(y,side)+retreat+new Vector2(side*t*0.12f,0);
                    if(p.y<=scarFloor+0.04f)continue;
                    Shard(p,new Vector2(side*0.25f,1).normalized,(0.09f+ScarNoise(i+61)*0.14f)*fade,fade*0.5f);
                }
                if(t<0.38f && ScarNoise(81)>0.45f)
                    ScarArc(ScarEdge(0.35f,1)+retreat,ScarEdge(0.41f,1)+retreat,0.014f,fade*0.35f,81);
                for(int v=first;v<count*4;v++)buffer.vertices[v].y=Mathf.Max(scarFloor,buffer.vertices[v].y);
                return;
            }
            float energyAge=age-KarmaSwordScarRise.AuraDelay;
            if(energyAge<=0)return;
            float ignition=Mathf.Clamp01(energyAge/0.035f);
            bool emerging=energyAge<0.16f;
            // Two brief streams race up the corrected blade profile, then hand
            // over to intermittent arcs. Reuse the existing mesh and local clock.
            float surge=Mathf.Clamp01(energyAge/0.16f);
            // Small white-cyan star glints connect this rising blade to SW05's
            // fuller ornaments. One travelling crest, then quiet staggered sparks.
            for(int star=0;star<3;star++)
            {
                float y=0.32f+star*0.22f;
                Vector2 center=ScarEdge(y,0);
                if(center.y<=scarFloor+0.04f)continue;
                float crest=1-Mathf.Clamp01(Mathf.Abs(surge-y)*5);
                float gain=ignition*(0.10f+crest*0.65f)*(0.8f+ScarNoise(star+701)*0.2f);
                float span=Mathf.Min(size.x*0.065f,0.10f)*(0.65f+crest*0.35f);
                ScarLine(center-Vector2.up*span*2.8f,center+Vector2.up*span*2.8f,
                    0.018f,gain);
                ScarLine(center-Vector2.right*span,center+Vector2.right*span,
                    0.012f,gain*0.75f);
            }
            if(surge<1)
            {
                float crest=Mathf.Sin(surge*Mathf.PI)*0.75f;
                for(int side=-1;side<=1;side+=2)
                {
                    float head=Mathf.Lerp(0.16f,0.94f,surge);
                    // The crest traces the actual bevel instead of cutting a
                    // straight chord across the sword's changing silhouette.
                    float tail=Mathf.Max(0.08f,head-0.28f);
                    Vector2 previous=ScarEdge(tail,side);
                    for(int segment=1;segment<=6;segment++)
                    {
                        float u=segment/6f;
                        Vector2 next=ScarEdge(Mathf.Lerp(tail,head,u),side);
                        ScarLine(previous,next,Mathf.Lerp(0.014f,0.047f,u),crest*u*ignition);
                        previous=next;
                    }
                    // A single small spark leaves the moving front; the body
                    // of the blade remains opaque and adjacent gaps stay clear.
                    Shard(previous,new Vector2(side*0.22f,1).normalized,
                        0.16f+crest*0.12f,crest*ignition*0.8f);
                }
            }
            // The ascending crest releases one restrained tip flash. Its short
            // diagonal rays follow the sword's narrow silhouette, then disappear.
            float tipAge=energyAge-0.14f;
            if(tipAge>=0 && tipAge<0.10f)
            {
                float tipT=tipAge/0.10f,gain=Mathf.Sin(tipT*Mathf.PI)*(1-tipT);
                Vector2 tip=ScarEdge(0.98f,0);
                float ray=0.10f+gain*0.13f;
                ScarLine(tip-Vector2.up*ray,tip+Vector2.up*ray,0.022f,gain*0.8f);
                for(int side=-1;side<=1;side+=2)
                    ScarLine(tip-new Vector2(side*ray*0.45f,ray*0.22f),
                        tip+new Vector2(side*ray*0.45f,ray*0.22f),0.012f,gain*0.65f);
            }
            // Two event slots share the existing mesh. Each short discharge has
            // a stable jagged shape, a dark interval and a per-sword schedule.
            // No random-state changes, extra objects or per-frame allocations.
            for(int slot=0;slot<2;slot++)
            {
                float clock=energyAge*8.5f+ScarNoise(slot+301)*7;
                int pulse=Mathf.FloorToInt(clock),id=slot*997+pulse*23+401;
                float phase=clock-pulse;
                if(ScarNoise(id)>(emerging?0.82f:0.40f))continue;
                float duration=(emerging?0.40f:0.28f)+ScarNoise(id+1)*0.28f;
                if(phase>=duration)continue;
                float side=ScarNoise(id+2)>0.5f?1:-1;
                float y=0.13f+ScarNoise(id+3)*0.67f;
                float reach=0.08f+ScarNoise(id+4)*0.065f;
                Vector2 from=ScarEdge(y,side),to=ScarEdge(y+reach,side);
                float flash=ignition*Mathf.Sqrt(1-phase/duration)*(emerging?0.95f:0.78f);
                float extension=Mathf.SmoothStep(0,1,phase/(duration*0.28f));
                ScarArc(from,to,0.044f+ScarNoise(id+5)*0.016f,flash,id,extension);
                if(ScarNoise(id+6)>0.63f)
                    Shard(to,new Vector2(side*0.4f,1).normalized,0.15f+ScarNoise(id+7)*0.18f,flash*0.65f);
            }
            // Seat the electrical accent after the visible rise completes; only
            // its secondary sparks receive a tiny per-site delay.
            float burstAge=age-KarmaSwordScarRise.RiseSeconds-ScarNoise(501)*0.018f;
            if(burstAge>=0 && burstAge<0.13f)
            {
                float t=burstAge/0.13f,fade=(1-t)*(1-t);
                for(int i=0;i<2;i++)
                {
                    float side=i==0?-1:1,y=0.27f+ScarNoise(i+511)*0.38f;
                    ScarArc(ScarEdge(y,side),ScarEdge(y+0.15f,side),0.075f,fade,521+i);
                    Vector2 p=ScarEdge(0.75f+ScarNoise(i+531)*0.12f,side);
                    Shard(p+new Vector2(side*t*0.22f,t*0.55f),new Vector2(side*0.3f,1).normalized,
                        (0.32f+ScarNoise(i+541)*0.25f)*fade,fade*0.9f);
                }
            }
        }

        // Follow the selected wide blade, independently of contact width.
        // The clipping plane stays at the real floor during emergence.
        Vector2 ScarEdge(float y,float side)
        {
            float half=KarmaSwordScarRise.BladeHalfWidth(y);
            float x=Mathf.Min(size.x*0.49f,size.x*half+0.08f);
            var point=at+new Vector2(side*x,size.y*y);
            point.y=Mathf.Max(scarFloor+0.035f,point.y);
            return point;
        }
        void ScarArc(Vector2 a,Vector2 b,float width,float alpha,int id,float progress=1)
        {
            Vector2 axis=(b-a).normalized,n=new Vector2(-axis.y,axis.x),p=a;
            float jitter=Mathf.Min(0.20f,Vector2.Distance(a,b)*0.22f);
            for(int i=1;i<=5;i++)
            {
                Vector2 q=Vector2.Lerp(a,b,i/5f)+n*(i==5?0:(ScarNoise(id*17+i)-0.5f)*jitter*2);
                float extension=Mathf.Clamp01(progress*5-(i-1));
                if(extension<=0)break;
                ScarLine(p,Vector2.Lerp(p,q,extension),width,alpha);
                if(i==3 && extension>=1)
                {
                    Vector2 elbow=q+n*(ScarNoise(id+19)>0.5f?jitter:-jitter)+axis*0.05f;
                    Vector2 tip=elbow+(elbow-q)*0.45f+axis*0.10f;
                    ScarLine(q,elbow,width*0.65f,alpha*0.65f);
                    ScarLine(elbow,tip,width*0.4f,alpha*0.40f);
                }
                p=q;
            }
        }

        void ScarLine(Vector2 a,Vector2 b,float width,float alpha)
        {
            Quad(a,b,width*1.65f,new Color(0.04f,0.42f,1.0f,alpha*0.26f));
            Quad(a,b,width*0.55f,new Color(0.70f,1.02f,1.22f,alpha));
        }

        void WardEnergy()
        {
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
        void BladeEnergy()
        {
            if (bladePicture == null) return;
            Vector2 tip = SwordPixel(bladePicture,new Vector2(255,25));
            Vector2 guard = GuardAnchor(bladePicture), axis = (guard-tip).normalized;
            Vector2 normal = new Vector2(-axis.y,axis.x);
            float width = Mathf.Abs(follow.lossyScale.x)*3.96f;
            float power = bladePicture.color.a*(kind == Kind.Fall ? 0.70f : 0.40f)*Mathf.Max(0,AuraIntensity);
            int frame = Mathf.FloorToInt(age*24);
            // Geometry and opacity come from the current renderer, including
            // the 22% formation scale. No full-sized aura before the blade exists.
            BladeSheath(bladePicture,power*0.55f);
            for (int side = -1; side <= 1; side+=2)
            {
                float u = Mathf.Repeat(age*1.9f+Noise(side+9),1);
                Vector2 point = Vector2.Lerp(tip,guard,0.12f+u*0.68f)
                    + normal*side*width*Mathf.Lerp(0.06f,0.23f,u);
                float envelope = Mathf.Sin(u*Mathf.PI);
                Line(point,point+axis*Vector2.Distance(tip,guard)*0.12f,width*0.022f,power*envelope);
                if (Noise(frame*47+side+17) < 0.42f)
                    CurrentArc(point,point+axis*Mathf.Min(0.85f,Vector2.Distance(tip,guard)*0.18f),
                        width*0.028f,power*envelope,frame*47+side+27);
                if (kind == Kind.Fall)
                {
                    Vector2 start = Vector2.Lerp(tip,guard,0.44f)+normal*width*0.27f*side;
                    Line(start,start+axis*Mathf.Min(1.8f,size.x*0.22f),width*0.018f,power*0.50f);
                }
            }
        }
        void BladeSheath(SpriteRenderer sword, float power)
        {
            if (power <= 0.001f) return;
            float width = Mathf.Abs(sword.transform.lossyScale.x)*3.96f;
            int first = count*4;
            // Two short contour segments per edge replace the baked ribbon.
            // Follow the same source-pixel profile and blade-only widening as
            // SW05BladeUV, including the current formation/plant renderer fit.
            for (int side = -1; side <= 1; side+=2)
            {
                Vector2 previous = Vector2.zero;
                for (int point = 0; point < 3; point++)
                {
                    float y = point == 0 ? 90 : point == 1 ? 420 : 650;
                    float half = point == 0 ? 14 : point == 1 ? 38 : 59;
                    float v = y/1024;
                    float mask = Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.015f,0.12f,v))
                        *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.62f,0.70f,v)));
                    // The existing shader widens around UV x=.5 (pixel 256).
                    float x = 256+(255+side*(half+2)-256)*(1+1.20f*mask);
                    Vector2 next = SwordPixel(sword,new Vector2(x,y));
                    if (point > 0) Line(previous,next,width*0.012f,power);
                    previous = next;
                }
            }
            if (kind == Kind.Planted || kind == Kind.Charge)
                for (int v = first; v < count*4; v++)
                    buffer.vertices[v].y = Mathf.Max(0.035f,buffer.vertices[v].y);
        }
        // SW05-local bounded arc. Keep the shared SW03/SW04 line routines and
        // their noise clocks untouched. Jitter scales with these shorter arcs.
        void CurrentArc(Vector2 a, Vector2 b, float width, float alpha, int id)
        {
            Vector2 axis = (b-a).normalized, normal = new Vector2(-axis.y,axis.x), previous = a;
            float jitter = Mathf.Min(0.12f,Vector2.Distance(a,b)*0.13f);
            for (int step = 1; step <= 5; step++)
            {
                Vector2 next = Vector2.Lerp(a,b,step/5f)
                    + normal*(step == 5 ? 0 : (Noise(id*31+step)-0.5f)*jitter*2);
                Line(previous,next,width,alpha);
                if (step == 3)
                {
                    Vector2 fork = next+normal*(Noise(id+19)>0.5f?jitter:-jitter)+axis*jitter;
                    Line(next,fork,width*0.55f,alpha*0.55f);
                    Line(fork,fork+axis*jitter,width*0.30f,alpha*0.25f);
                }
                previous = next;
            }
        }
        void PlantedEnergy()
        {
            bool alive = false;
            for (int i = 0; i < plantedCount; i++)
            {
                var picture = plantedPictures[i];
                if (picture == null || !picture.gameObject.activeInHierarchy) continue;
                alive = true;
                SwordCurrent(PlantedSite(picture),i,0,picture.color.a*0.58f*Mathf.Max(0,AuraIntensity),picture);
            }
            if (!alive) Destroy(gameObject);
        }
        void SwordCurrent(ElectricSite site, int index, float buildup, float power, SpriteRenderer sword)
        {
            Vector2 axis = (site.hilt-site.foot).normalized, normal = new Vector2(-axis.y,axis.x);
            float width = site.width, span = Vector2.Distance(site.hilt,site.foot);
            int frame = Mathf.FloorToInt(age*24), id = index*997;
            float gain = power*(0.75f+0.25f*Mathf.Sin(age*3.4f+Noise(id)*6.28f));
            // Eight sheath quads replace the two-quad fuller. At 54 swords the
            // idle maximum is 3780/4096 and charge is 5616/8192, without a new layer.
            if (sword != null) BladeSheath(sword,gain*(0.35f+buildup*0.30f));
            Line(site.foot-Vector2.right*width*0.13f,site.foot+Vector2.right*width*0.13f,
                width*0.035f,gain*(0.45f+buildup*0.25f));
            for (int side = -1; side <= 1; side+=2)
            {
                float u = Mathf.Repeat(age*(0.9f+buildup*1.5f)+Noise(id+side+4),1);
                Vector2 p = Vector2.Lerp(site.foot,site.hilt,0.08f+u*0.78f)
                    +normal*(side*width*Mathf.Lerp(0.10f,0.23f,u));
                float envelope = Mathf.Sin(u*Mathf.PI);
                Line(p,p+axis*span*0.10f,width*0.019f,gain*envelope);
                int flicker = id+frame*71+side*43;
                if (Noise(flicker) < 0.16f+buildup*0.58f)
                    CurrentArc(p,p+axis*Mathf.Min(span*0.16f,0.9f),width*0.025f,
                        gain*(0.65f+buildup*0.35f),flicker);
                if (Noise(flicker+11) < 0.10f+buildup*0.48f)
                    CurrentArc(site.foot,site.foot+new Vector2(side*width*(0.24f+Noise(id+8)*0.20f),0.05f),
                        width*0.020f,gain*0.75f,flicker+11);
            }
            if (buildup > 0.35f && Noise(id+frame*37) < buildup*0.55f)
                CurrentArc(site.hilt-normal*width*0.34f,site.hilt+normal*width*0.34f,
                    width*0.023f,gain*0.7f,id+frame*37);
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
        // Final presentation recovered from 036c22b, formerly in
        // SwordWave/Presentation/KarmaSW05VFX.cs. No magnetic/residual pass.
        void HazardVolumes(float t, bool detonation)
        {
            foreach (var zone in zones)
            {
                int first = count*4;
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
                ClipFinalRegion(first,zone);
            }
        }
        // A tapered strip with a transparent edge. Unlike a widened line this
        // keeps the open ends of the approved ribbons thin and pointed.
        void RibbonSegment(Vector2 a, Vector2 b, float wa, float wb, float alpha)
        {
            Vector2 d = (b-a).normalized, n = new Vector2(-d.y,d.x);
            for (int layer=0;layer<2;layer++)
            {
                if (count >= buffer.capacity) { CapacityExceeded=true; return; }
                float scale=layer==0?2.6f:1;
                int v=count++*4;
                buffer.vertices[v]=a-n*wa*scale; buffer.vertices[v+1]=a+n*wa*scale;
                buffer.vertices[v+2]=b-n*wb*scale; buffer.vertices[v+3]=b+n*wb*scale;
                Color c=layer==0?Blue:White; c.a=alpha*(layer==0?0.18f:1);
                for(int k=0;k<4;k++)buffer.colors[v+k]=c;
            }
        }
        void Facet(Vector2 a,Vector2 b,Vector2 c,Color tint,float rootAlpha,float tipAlpha)
        {
            if(count>=buffer.capacity){CapacityExceeded=true;return;}
            int v=count++*4;
            buffer.vertices[v]=a;buffer.vertices[v+1]=b;
            buffer.vertices[v+2]=buffer.vertices[v+3]=c;
            tint.a=rootAlpha;buffer.colors[v]=buffer.colors[v+1]=tint;
            tint.a=tipAlpha;buffer.colors[v+2]=buffer.colors[v+3]=tint;
        }
        // Angular, asymmetric sword-energy planes: blue outer glass, cyan bevel,
        // white split core and a hooked shoulder. No identical triangular rays.
        void JudgementBlade(Vector2 root,Vector2 tip,float breadth,float power,int id)
        {
            Vector2 d=tip-root,n=new Vector2(-d.y,d.x).normalized;
            Vector2 shoulder=root+d*(0.42f+Noise(id)*0.13f);
            Vector2 left=shoulder-n*breadth,right=shoulder+n*breadth*0.58f;
            Vector2 notch=root+d*0.67f-n*breadth*0.13f;
            Facet(root,left,tip,Blue,power*0.13f,power*0.06f);
            Facet(root,right,tip,Blue,power*0.27f,power*0.08f);
            Facet(root,left,notch,new Color(0.12f,0.83f,1.3f),power*0.40f,power*0.24f);
            Facet(root,shoulder+n*breadth*0.16f,tip,White,power*0.83f,power*0.18f);
            Facet(root,shoulder-n*breadth*0.12f,notch,White,power*0.95f,power*0.75f);
            Vector2 hook=left+d*0.15f-n*breadth*0.23f;
            Facet(left,notch,hook,White,power*0.65f,power*0.12f);
            RibbonSegment(left,notch,breadth*0.032f,0,power*0.8f);
            RibbonSegment(right,tip,breadth*0.02f,0,power*0.7f);
        }
        int FinalSite(Vector3 zone,int cluster,int clusters)
        {
            float target=zone.x+(zone.y-zone.x)*(cluster+0.5f)/clusters;
            int nearest=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<sites.Length;i++)
            {
                if(sites[i].x<zone.x || sites[i].x>zone.y)continue;
                float d=Mathf.Abs(sites[i].x-target);
                if(d<distance){distance=d;nearest=i;}
            }
            return nearest;
        }
        void GroundSigil(Vector2 foot,float radius,float power,float contraction,int steps)
        {
            for(int ring=0;ring<2;ring++)
            {
                float r=radius*(ring==0?1:0.66f)*(1-contraction*0.28f);
                for(int j=0;j<steps;j++)
                {
                    if((j+ring*2)%7==0)continue;
                    float a=j*Mathf.PI*2/steps,b=(j+0.88f)*Mathf.PI*2/steps;
                    Vector2 p=foot+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r*0.16f);
                    Vector2 q=foot+new Vector2(Mathf.Cos(b)*r,Mathf.Sin(b)*r*0.16f);
                    Line(p,q,0.014f,power*(ring==0?0.55f:0.8f));
                }
            }
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*0.5f;
                Vector2 p=foot+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*0.16f);
                Vector2 h=new Vector2(radius*0.07f,0),v=new Vector2(0,0.065f);
                Line(p-h,p+v,0.012f,power);Line(p+v,p+h,0.012f,power);
                Line(p+h,p-v,0.012f,power);Line(p-v,p-h,0.012f,power);
            }
        }
        void FinalEnergy()
        {
            if (kind == Kind.Detonation && age >= life) return;
            float t=Mathf.Clamp01(age/life);
            if(kind==Kind.Detonation){FinalEruption(t);return;}
            float compression=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.65f,1,t));
            // Ground countdown and the existing winding aura only. Eruption
            // geometry starts exclusively on the shared damage/release frame.
            float winding=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.12f,0.48f,t));
            foreach(var zone in zones)
            {
                int first=count*4,clusters=Mathf.Clamp(Mathf.CeilToInt((zone.y-zone.x)/3.8f),1,18);
                // Light boundary strokes announce the actual jump-clearance ceiling.
                for (int k = 0; k < clusters * 4; k++)
                {
                    float x0 = Mathf.Lerp(zone.x, zone.y, k / (float)(clusters * 4));
                    float x1 = Mathf.Lerp(zone.x, zone.y, (k + 1f) / (clusters * 4));
                    Line(new Vector2(x0, zone.z - 0.025f), new Vector2(x1, zone.z - 0.025f), 0.014f,
                        (0.25f + 0.18f * t) * (0.7f + 0.3f * Mathf.Sin(age * 8 + k)));
                }
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? zone.x : zone.y;
                    Line(new Vector2(x, 0.04f), new Vector2(x, zone.z - 0.025f), 0.014f, 0.28f + 0.2f * t);
                }
                int previous=-1,steps=zones.Length>20?10:24;
                for(int cluster=0;cluster<clusters;cluster++)
                {
                    int i=FinalSite(zone,cluster,clusters);if(i<0||i==previous)continue;previous=i;
                    Vector2 foot=chargeFeet[i],hilt=chargeHilts[i];
                    float span=Mathf.Min(Vector2.Distance(foot,hilt),zone.z*0.6f);
                    Vector2 axis=(hilt-foot).normalized,n=new Vector2(-axis.y,axis.x);
                    float width=Mathf.Min(sites[i].z*0.46f,(zone.y-zone.x)/clusters*0.46f);
                    float power=0.4f+t*0.6f;
                    GroundSigil(foot,width,power*(1-compression*0.8f),compression,steps);
                    // Two counter-wound open coils follow the planted sword, then
                    // collapse into its fuller rather than expanding before impact.
                    for(int coil=0;coil<2;coil++)
                        for(int k=0;k<steps;k++)
                        {
                            float u=k/(float)steps,v=(k+1f)/steps;
                            float a=u*7.8f-age*5+coil*Mathf.PI,b=v*7.8f-age*5+coil*Mathf.PI;
                            float r=width*0.34f*(1-compression*0.94f);
                            Vector2 p=foot+axis*(u*span)+n*(Mathf.Sin(a)*r*(0.5f+u*0.5f));
                            Vector2 q=foot+axis*(v*span)+n*(Mathf.Sin(b)*r*(0.5f+v*0.5f));
                            float alpha=power*winding*(1-compression*0.78f)*Mathf.Sin(u*Mathf.PI)*(0.3f+0.7f*(Mathf.Cos(a)*0.5f+0.5f));
                            RibbonSegment(p,q,0.019f*Mathf.Sin(u*Mathf.PI),0.019f*Mathf.Sin(v*Mathf.PI),alpha);
                        }
                    Line(foot,foot+axis*span,0.018f,power*(0.25f+compression*0.5f));
                    for(int side=-1;side<=1;side+=2)
                    {
                        Vector2 elbow=foot+new Vector2(side*width*0.5f,0.16f);
                        Line(foot,elbow,0.022f,power*(1-compression));
                        Line(elbow,foot+new Vector2(side*width,0),0.012f,power*(1-compression));
                        RibbonSegment(foot,foot+axis*span*0.75f+n*side*width*0.42f,
                            0.03f,0,compression*0.3f);
                    }
                }
                ClipFinalRegion(first,zone);
            }
        }
        void FinalEruption(float t)
        {
            float travel=1-Mathf.Pow(1-t,3),tail=(1-t)*(1-t);
            float peak=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.18f,0.72f,t));
            float opening=1-Mathf.Pow(1-Mathf.Clamp01(t/0.23f),3);
            float flash=1-Mathf.SmoothStep(0,1,t/0.24f);
            foreach(var zone in zones)
            {
                int first=count*4;
                float width=zone.y-zone.x,height=zone.z;
                int cells=Mathf.Clamp(Mathf.CeilToInt(width/1.1f),2,72);
                float step=width/cells;
                // Restore the broad, simultaneous burst and its original tail.
                // All ridges share one ceiling; overlapping roots fill the zone.
                for(int cell=0;cell<cells;cell++)
                {
                    int key=cell*31+Mathf.FloorToInt(zone.x*13)+1700;
                    float x=zone.x+(cell+0.5f)*step;
                    float h=height-0.04f;
                    float lean=(Noise(key+1)-0.5f)*step*0.65f;
                    Vector2 left=new Vector2(x-step*0.61f,0.04f);
                    Vector2 right=new Vector2(x+step*0.61f,0.04f);
                    Vector2 crest=new Vector2(x+lean,h);
                    if(peak>0.001f)
                    {
                        EruptionPlume(new Vector2(x,0.04f),step*0.85f,h,travel,peak,key);
                        if(cell%2==1)
                        {
                            // Slightly firmer electrical cores preserve the old
                            // silhouette without raising the broad plume glow.
                            Bolt(new Vector2(x,0.22f),crest,0.062f,peak,key+3);
                            Vector2 fork=Vector2.Lerp(new Vector2(x,0.22f),crest,0.55f);
                            Bolt(fork,fork+new Vector2(step*0.55f,height*0.17f),0.032f,peak*0.75f,key+4);
                        }
                        if(flash>0.001f)
                            EruptionPlume(new Vector2(x,0.04f),step*1.12f,h*0.48f,travel,flash,key+37);
                        Vector2 front=new Vector2(x+lean*0.4f,h*(0.10f+opening*0.44f));
                        RibbonSegment(front-Vector2.right*step*0.42f,front+Vector2.right*step*0.42f,
                            0.035f,0.012f,flash*0.8f);
                    }
                    Line(left,right,0.14f+opening*0.10f,flash*0.9f);
                    for(int j=0;j<5;j++)
                    {
                        float u=Noise(key+j+9);
                        Vector2 p=new Vector2(x+(u-0.5f)*step,
                            height*(0.08f+u*0.65f)*(0.55f+travel*0.45f));
                        Vector2 axis=new Vector2((Noise(key+j+15)-0.5f)*1.2f,1).normalized;
                        RibbonSegment(p-axis*(0.10f+travel*0.38f),p,
                            0.022f*(1-t),0,tail*0.8f);
                        Shard(p,axis,(0.16f+u*0.18f)*(1-t),tail*0.9f);
                    }
                }
                ClipFinalRegion(first,zone);
            }
        }
        void EruptionPlume(Vector2 origin,float breadth,float height,float travel,float power,int key)
        {
            // Vertex-alpha feathering gives a continuous area burst without
            // new textures, particles, materials or a rectangular light panel.
            // Four transverse lanes soften the sides; eight bands bend upward.
            const int bands=8,lanes=4;
            for(int band=0;band<bands;band++)for(int lane=0;lane<lanes;lane++)
            {
                if(count>=buffer.capacity){CapacityExceeded=true;return;}
                int v=count++*4;
                for(int corner=0;corner<4;corner++)
                {
                    float u=(band+corner/2)/(float)bands;
                    float side=(lane+corner%2)/(float)lanes*2-1;
                    float bend=Mathf.Sin(u*5+Noise(key+5)*6+travel*1.2f)*breadth*u*0.34f;
                    float spread=breadth*(1-u*0.66f)*(0.9f+Mathf.Sin(u*8+key)*0.1f);
                    buffer.vertices[v+corner]=origin+new Vector2(bend+side*spread,u*height);
                    float feather=1-side*side;
                    float fade=Mathf.Pow(1-u,0.48f);
                    float crestBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.65f,0.90f,u));
                    float ripple=0.84f+0.16f*Mathf.Lerp(Mathf.Sin(u*13-travel*4+key),
                        Mathf.Sin(u*13-travel*4),crestBlend);
                    float white=Mathf.Clamp01(1.02f-u*0.64f-travel*0.28f);
                    buffer.colors[v+corner]=new Color(
                        Mathf.Lerp(Blue.r,White.r,white),Mathf.Lerp(Blue.g,White.g,white),
                        Mathf.Lerp(Blue.b,White.b,white),power*feather*fade*ripple*0.92f);
                }
            }
        }
        void ClipFinalRegion(int first, Vector3 zone)
        {
            for (int v = first; v < count*4; v++)
            {
                buffer.vertices[v].x = Mathf.Clamp(buffer.vertices[v].x,zone.x,zone.y);
                buffer.vertices[v].y = Mathf.Clamp(buffer.vertices[v].y,0,zone.z);
            }
        }
        void Upload()
        {
            // Disabling an owner returns its buffer immediately, including when
            // teardown happens inside the current update. Never upload a released mesh.
            if (buffer == null || buffer.mesh == null) return;
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
            if ((kind != Kind.Detonation || Time.frameCount != detonationFrame)
                && (kind != Kind.Splinters || Time.frameCount != splinterFrame)) age += Time.deltaTime;
            sampledFrame=Time.frameCount;
            if (kind == Kind.Form || kind == Kind.Fall || kind == Kind.Scar)
            {
                if (follow == null || !follow.gameObject.activeInHierarchy || rain != null && rain.Landed)
                { Destroy(gameObject); return; }
            }
            else if (age >= life && kind != Kind.Charge) { Destroy(gameObject); return; }
            count = 0;CapacityExceeded=false;
            if(kind==Kind.Splinters)
            {
                SplinterEnergy();
                if(splinterCount==0){gameObject.SetActive(false);Destroy(gameObject);return;}
                // Expiry or pattern interruption returns the mesh and fragment
                // storage together; no empty effect remains after recovery.
            }
            else if(kind==Kind.Landing)
            {
                LandingEnergy();
                if(landingCount==0){gameObject.SetActive(false);Destroy(gameObject);return;}
            }
            else if(kind==Kind.GroundStrike)GroundStrikeEnergy();
            else if (kind == Kind.Form || kind == Kind.Fall) BladeEnergy();
            else if (kind == Kind.Planted) PlantedEnergy();
            else if(kind==Kind.Scar||kind==Kind.ScarWarning||kind==Kind.ScarFade)ScarEnergy();
            else if(kind==Kind.WardRelease||kind==Kind.WardImpact||kind==Kind.WardCollapse)WardEnergy();
            else if(kind==Kind.Eruption && scarSiteCount>0)ScarGroundEnergy();
            else if (kind == Kind.Eruption)
            {
                float t = Mathf.Clamp01(age/life);
                Burst(at,size.x,t,false,seed);
                if (kind == Kind.Eruption)
                    for (int side = -1; side <= 1; side+=2)
                        Shard(at+new Vector2(side*size.x*0.25f,0.1f),new Vector2(side*0.3f,1).normalized,
                            2.8f*(1-t)*(1-t),1-t);
            }
            else {if(kind==Kind.Charge)RefreshChargeAnchors();FinalEnergy();}
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
