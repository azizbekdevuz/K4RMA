using UnityEngine;

namespace KarmaPrototype
{
    // SW05-only, one draw per layer. Fixed buffers and recycled meshes replace
    // hundreds of particle/line objects; motion never touches gameplay geometry.
    public sealed class KarmaSW05Energy : MonoBehaviour
    {
        enum Kind { Summon, Form, Fall, Landing, Charge, Detonation, Eruption, Scar, ScarWarning, ScarFade, WardRelease, WardImpact, WardCollapse, WardFlight, Planted, FallWarning }
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
        SpriteRenderer bladePicture;
        MeshRenderer display; MeshFilter filter; Buffer buffer;
        Kind kind; Vector2 at, size; Vector3[] sites, zones;
        struct ElectricSite
        {
            public Vector2 foot, hilt;
            public float width;
        }
        KarmaEnemy warningOwner;
        SpriteRenderer[] fallMarks;
        KarmaAerialSwordPlanner.Shot[] fallShots;
        KarmaSW05RainBlade[] fallBlades;
        float formationWarning;
        KarmaSwordPackedVfx[] chargedLayers;
        Vector2[] chargedSizes;
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
        public static void Summon(Transform root, KarmaGame game, Transform boss)
        { var fx = Create(root, game, Kind.Summon, float.PositiveInfinity, 512); fx.follow = boss; }
        public static KarmaSW05Energy FallWarnings(Transform root,KarmaGame game,KarmaEnemy owner,
            SpriteRenderer[] marks,KarmaAerialSwordPlanner.Shot[] shots,float formationSeconds)
        {
            var fx=Create(root,game,Kind.FallWarning,float.PositiveInfinity,4096);
            fx.warningOwner=owner;fx.fallMarks=marks;fx.fallShots=shots;
            fx.fallBlades=new KarmaSW05RainBlade[marks.Length];fx.formationWarning=formationSeconds;
            fx.name="SW05 batched falling sword warnings / no damage";fx.display.sortingOrder=10;
            fx.count=0;fx.FallingWarnings();fx.Upload();return fx;
        }
        public void BindFallWarning(int index,KarmaSW05RainBlade blade) {fallBlades[index]=blade;}
        bool HasFallWarnings()
        {
            if(warningOwner==null||!warningOwner.PresentationActive||warningOwner.Stunned
                ||warningOwner.CurrentPattern!=(int)EnemyAttack.SW05)return false;
            foreach(var mark in fallMarks)if(mark!=null&&mark.gameObject.activeInHierarchy)return true;
            return false;
        }
        float FallWarningProgress(int index)
        {
            var shot=fallShots[index];
            float elapsed=fallBlades[index]!=null?formationWarning+fallBlades[index].FallWarningAge:Mathf.Min(age,formationWarning);
            return Mathf.Clamp01(elapsed/Mathf.Max(0.001f,formationWarning+shot.delay+shot.life));
        }
        float FallWarningWidth(int index)
        {return fallShots[index].size.y/Mathf.Max(0.001f,Mathf.Abs(fallShots[index].velocity.normalized.y));}
        void FallingWarnings()
        {
            float clock=Mathf.Floor(age*24)/24;
            for(int i=0;i<fallMarks.Length;i++)
            {
                var mark=fallMarks[i];if(mark==null||!mark.gameObject.activeInHierarchy)continue;
                float width=FallWarningWidth(i),x=mark.transform.position.x;
                int neighbors=0;bool duplicate=false;
                for(int j=0;j<fallMarks.Length;j++)
                {
                    if(j==i||fallMarks[j]==null||!fallMarks[j].gameObject.activeInHierarchy)continue;
                    float separation=Mathf.Abs(x-fallMarks[j].transform.position.x);
                    float otherWidth=FallWarningWidth(j);
                    if(separation<(width+otherWidth)*0.65f)neighbors++;
                    if(j<i&&separation<0.025f&&Mathf.Abs(width-otherWidth)<0.025f)duplicate=true;
                }
                if(duplicate)continue;
                DrawFallWarning(new Vector2(x,0.04f),width,FallWarningProgress(i),clock,
                    1/(1+neighbors*0.45f),i);
            }
        }
        void DrawFallWarning(Vector2 foot,float width,float progress,float clock,float crowd,int index)
        {
            int first=count;
            float h=Mathf.Clamp(width*2.4f,1.1f,2.2f),half=width*0.5f;
            float power=(0.15f+0.65f*Mathf.SmoothStep(0,1,progress))*crowd;
            float pulse=0.94f+0.06f*Mathf.Sin(clock*9+index*0.7f);
            Vector2 tip=foot+Vector2.up*h,guard=foot+Vector2.up*(h*0.72f);
            // Narrow blade silhouette: decoration above the ground footprint,
            // never a filled vertical damage column.
            Line(foot,tip,width*0.010f,power*pulse);
            Line(foot,foot+new Vector2(-width*0.045f,h*0.78f),width*0.007f,power*0.65f);
            Line(foot,foot+new Vector2(width*0.045f,h*0.78f),width*0.007f,power*0.65f);
            Line(guard-new Vector2(width*0.11f,0),guard+new Vector2(width*0.11f,0),width*0.008f,power);
            float diamond=half*Mathf.Lerp(0.83f,0.68f,Mathf.SmoothStep(0.80f,1,progress));
            Vector2 left=foot+new Vector2(-diamond,h*0.47f),right=foot+new Vector2(diamond,h*0.47f);
            Line(foot,left,width*0.008f,power*0.62f);Line(left,tip,width*0.008f,power*0.62f);
            Line(tip,right,width*0.008f,power*0.62f);Line(right,foot,width*0.008f,power*0.62f);
            // The ground ring, not the tall symbol, states the real danger width.
            for(int step=0;step<24;step++)
            {
                float a=step*Mathf.PI*2/24,b=(step+1)*Mathf.PI*2/24;
                Vector2 from=foot+new Vector2(Mathf.Cos(a)*half,Mathf.Sin(a)*0.055f);
                Vector2 to=foot+new Vector2(Mathf.Cos(b)*half,Mathf.Sin(b)*0.055f);
                Line(from,to,width*0.008f,0.28f+progress*0.30f);
            }
            if(progress>0.25f)
                for(int branch=0;branch<3;branch++)
                {
                    float y=h*(0.25f+branch*0.20f),side=(branch+index)%2==0?-1:1;
                    Vector2 from=foot+new Vector2(0,y),to=foot+new Vector2(side*diamond*0.85f,y+h*0.12f);
                    Vector2 mid=Vector2.Lerp(from,to,0.48f)+new Vector2(width*0.06f*Mathf.Sin(clock*19+branch*4),-h*0.04f);
                    Line(from,mid,width*0.009f,power*progress);Line(mid,to,width*0.007f,power*progress);
                }
            for(int arc=0;arc<2;arc++)
            {
                float side=arc==0?-1:1,u=Mathf.Repeat(clock*0.65f+arc*0.45f+index*0.13f,1);
                Vector2 from=foot+new Vector2(side*diamond*(0.5f+0.3f*Mathf.Sin(u*Mathf.PI)),h*(0.20f+u*0.50f));
                Vector2 bend=from+new Vector2(-side*width*0.05f,h*0.05f);
                Line(from,bend,width*0.009f,power*(0.4f+progress*0.5f));
                Line(bend,bend+new Vector2(side*width*0.025f,h*0.035f),width*0.006f,power*(0.4f+progress*0.5f));
            }
            // Clip every glow vertex to the true horizontal warning footprint,
            // including arena edges. Ornaments cannot imply a wider safe gap.
            float lo=Mathf.Max(KarmaStage.ArenaLeft,foot.x-half),hi=Mathf.Min(KarmaStage.ArenaRight,foot.x+half);
            for(int v=first*4;v<count*4;v++)
            {buffer.vertices[v].x=Mathf.Clamp(buffer.vertices[v].x,lo,hi);buffer.vertices[v].y=Mathf.Max(0.01f,buffer.vertices[v].y);}
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
        public static void Planted(Transform root, KarmaGame game, SpriteRenderer sword, Vector2 size)
        {
            var node = root.Find(PlantedName);
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
        public static void CounterFlight(Transform root,KarmaGame game,Transform shot,Vector2 axis,Vector2 size)
        {var fx=Create(root,game,Kind.WardFlight,float.PositiveInfinity);fx.follow=shot;fx.at=axis;fx.size=size;fx.display.sortingOrder=31;}
        public static KarmaSW05Energy Prepare(Transform root, KarmaGame game,
            System.Collections.Generic.List<KarmaSW05RainBlade> swords, float seconds)
        {
            // Restore the 036c22b warning/packed explosion composition while
            // keeping the current pooled buffer and current damage geometry.
            var idle = root.Find(PlantedName);
            if (idle != null) { idle.gameObject.SetActive(false); Destroy(idle.gameObject); }
            var fx = Create(root, game, Kind.Charge, seconds, 8192);
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
            fx.FinalEnergy(); fx.Upload();
            return fx;
        }
        public void Ignite()
        {
            if (kind != Kind.Charge) return;
            // Keep the current summoning cleanup at release; the recovered
            // final composition runs only once, without an extra aerial layer.
            for (int i = transform.parent.childCount-1; i >= 0; i--)
            {
                var previous = transform.parent.GetChild(i).GetComponent<KarmaSW05Energy>();
                if (previous != null && previous != this && previous.kind == Kind.Summon)
                { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
            }
            kind = Kind.Detonation; age = 0; life = DetonationVisualSeconds; display.sortingOrder = 4; // Below character bodies (order 5).
            display.enabled = true;
            detonationFrame = Time.frameCount;
            // Replace the charge draw immediately on the real shared release.
            // The first render must see the peak, even on a long release frame.
            count = 0; CapacityExceeded = false;
            for (int i = 0; i < chargedLayers.Length; i++)
                if (chargedLayers[i] != null)
                {
                    chargedLayers[i].Play(KarmaSwordPackedVfx.Clip.Detonation,chargedSizes[i],8f/24,0.65f);
                    chargedLayers[i].GetComponent<SpriteRenderer>().sortingOrder = 4;
                }
            FinalEnergy(); Upload();
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
            var node=root.Find(name);
            KarmaSW05Energy fx;
            if(node==null)
            {
                // One pooled mesh/draw for all 26 simultaneous floor impacts.
                // Do not advance the visual sequence of any other skill.
                int savedSeed=nextSeed;
                fx=Create(root,game,Kind.Eruption,0.17f,2048);nextSeed=savedSeed;
                fx.name=name;fx.sites=new Vector3[32];fx.display.sortingOrder=31;
            }
            else fx=node.GetComponent<KarmaSW05Energy>();
            if(fx.scarSiteCount<fx.sites.Length)
                fx.sites[fx.scarSiteCount++]=new Vector3(ground.x,ground.y,width);
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
                // footprint. 20 quads/site; even 32 sites fit the 2048 budget.
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
                ScarLine(ground,ground+Vector2.up*w*(0.35f+0.45f*spread),0.055f*punch,punch*gain);
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
                float pulse=0.32f+buildup*0.68f;
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
                for(int i=0;i<4;i++)
                {
                    float y=0.12f+ScarNoise(i+31)*0.74f;
                    float side=i%2==0?-1:1;
                    Vector2 p=ScarEdge(y,side)+new Vector2(side*t*0.12f,t*0.42f);
                    Shard(p,new Vector2(side*0.25f,1).normalized,(0.09f+ScarNoise(i+61)*0.14f)*fade,fade*0.5f);
                }
                if(t<0.38f && ScarNoise(81)>0.45f)
                    ScarArc(ScarEdge(0.35f,1),ScarEdge(0.41f,1),0.014f,fade*0.35f,81);
                return;
            }
            float energyAge=age-KarmaSwordScarRise.AuraDelay;
            if(energyAge<=0)return;
            float ignition=Mathf.Clamp01(energyAge/0.035f);
            bool emerging=energyAge<0.16f;
            // Two brief streams race up the corrected blade profile, then hand
            // over to intermittent arcs. Reuse the existing mesh and local clock.
            float surge=Mathf.Clamp01(energyAge/0.16f);
            if(surge<1)
            {
                float crest=Mathf.Sin(surge*Mathf.PI)*0.75f;
                for(int side=-1;side<=1;side+=2)
                {
                    float head=Mathf.Lerp(0.16f,0.94f,surge);
                    ScarLine(ScarEdge(Mathf.Max(0.08f,head-0.22f),side),ScarEdge(head,side),0.028f,crest);
                }
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
                float flash=ignition*Mathf.Sqrt(1-phase/duration)*(emerging?0.85f:0.70f);
                ScarArc(from,to,0.055f+ScarNoise(id+5)*0.020f,flash,id);
                if(ScarNoise(id+6)>0.63f)
                    Shard(to,new Vector2(side*0.4f,1).normalized,0.15f+ScarNoise(id+7)*0.18f,flash*0.65f);
            }
            // Actual full-height event stays at 80 ms. Only the secondary
            // electrical accents get a tiny per-site delay and distinct shape.
            float burstAge=age-KarmaSwordScarRise.RiseSeconds*0.8f-ScarNoise(501)*0.018f;
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
        void ScarArc(Vector2 a,Vector2 b,float width,float alpha,int id)
        {
            Vector2 axis=(b-a).normalized,n=new Vector2(-axis.y,axis.x),p=a;
            float jitter=Mathf.Min(0.20f,Vector2.Distance(a,b)*0.22f);
            for(int i=1;i<=5;i++)
            {
                Vector2 q=Vector2.Lerp(a,b,i/5f)+n*(i==5?0:(ScarNoise(id*17+i)-0.5f)*jitter*2);
                ScarLine(p,q,width,alpha);
                if(i==3)
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
        void FinalEnergy()
        {
            if (kind == Kind.Detonation && age >= life) return;
            float t = Mathf.Clamp01(age/life);
            // No final range overlay. A single shared release clock drives all
            // grounded eruptions; charge/formation/landing stay separate.
            if (kind == Kind.Detonation) { FinalEruption(t); return; }
            foreach (var layer in chargedLayers) if (layer != null) layer.SetProgress(t);
            for (int i = 0; i < sites.Length; i++)
            {
                Vector2 p = new Vector2(sites[i].x,0.12f); float width = sites[i].z;
                int chargeFirst=count*4;
                float pulse = 0.5f + 0.5f * Mathf.Pow(0.5f+0.5f*Mathf.Sin(t*12+t*t*35),3);
                if (i > 0 && p.x-sites[i-1].x <= (width+sites[i-1].z)/2)
                    Bolt(new Vector2(sites[i-1].x,2.4f+t*1.5f),new Vector2(p.x,2.4f+t*1.5f),0.08f+t*0.06f,pulse,i);
                Bolt(p+new Vector2(-width*0.3f,0.3f+t*0.6f),p+new Vector2(width*0.3f,0.2f),0.035f,pulse,i+100);
                float u = Mathf.Repeat(age*(2+t*4)+Noise(i),1);
                Line(p+Vector2.up*(1-u)*1.8f,p+Vector2.up*((1-u)*1.8f+0.18f),0.025f,t*0.6f);
                foreach (var zone in zones)
                    if (p.x >= zone.x && p.x <= zone.y) { ClipFinalRegion(chargeFirst,zone); break; }
            }
        }
        void FinalEruption(float t)
        {
            float travel = 1 - Mathf.Pow(1-t,3);
            float tail = (1-t)*(1-t), peak = tail*(1-t);
            foreach (var zone in zones)
            {
                int first = count*4;
                float width = zone.y-zone.x, height = zone.z;
                // Spatially budget a connected field instead of stacking 54
                // full-opacity explosions. Each fan starts at a real blade.
                int clusters = Mathf.Clamp(Mathf.CeilToInt(width/4),1,18);
                float previous = float.NegativeInfinity;
                for (int cluster = 0; cluster < clusters; cluster++)
                {
                    float target = zone.x+width*(cluster+0.5f)/clusters;
                    int nearest = -1; float distance = float.PositiveInfinity;
                    for (int i=0;i<sites.Length;i++)
                    {
                        if (sites[i].x<zone.x || sites[i].x>zone.y) continue;
                        float d = Mathf.Abs(sites[i].x-target);
                        if (d<distance) { distance=d; nearest=i; }
                    }
                    if (nearest<0 || Mathf.Abs(sites[nearest].x-previous)<0.05f) continue;
                    float x = previous = sites[nearest].x;
                    Vector2 foot = new Vector2(x,0.08f);
                    float spread = Mathf.Min(width/clusters*0.8f,3.2f);
                    float prominence = 0.74f+0.24f*(1-Mathf.Abs((cluster+0.5f)/clusters*2-1));
                    for (int ray=-3;ray<=3;ray++)
                    {
                        float variation = ray==0 ? 1 : 0.72f+Noise(nearest*43+ray+400)*0.28f;
                        float h = height*prominence*(1-Mathf.Abs(ray)*0.13f)*variation*(0.82f+travel*0.18f);
                        float skew = ray==0 ? 0 : (Noise(nearest*37+ray+500)-0.5f)*0.35f;
                        Vector2 end = foot+new Vector2((ray/3f+skew)*spread,h);
                        Vector2 axis = (end-foot).normalized;
                        // Broad blue silhouette, narrow white-hot tip/core.
                        // The shared cubic fade leaves only a short electric tail.
                        Shard(foot,axis,(end-foot).magnitude,peak*(ray==0?0.95f:0.65f));
                        if (ray%2==0) Bolt(foot,end,0.065f,peak*0.75f,nearest*31+ray+900);
                    }
                    // Short outward ground cracks do not bridge safe gaps.
                    for (int side=-1;side<=1;side+=2)
                    {
                        Vector2 elbow=foot+new Vector2(side*spread*0.45f,0.16f);
                        Vector2 end=foot+new Vector2(side*spread*(0.45f+travel*0.55f),0.02f);
                        Line(foot,elbow,0.055f,tail*0.8f); Line(elbow,end,0.035f,tail*0.7f);
                        Line(elbow,elbow+new Vector2(side*spread*0.2f,0.26f),0.025f,tail*0.5f);
                    }
                    for (int j=0;j<4;j++)
                    {
                        float u=Noise(nearest*19+j+70);
                        Vector2 axis=new Vector2((u-0.5f)*1.4f,0.7f).normalized;
                        Vector2 p=foot+new Vector2(axis.x*spread, height*(0.18f+u*0.65f))*(0.2f+travel*0.8f);
                        Shard(p,axis,(0.35f+u*0.5f)*(1-t),tail*0.6f);
                        Line(p-axis*0.35f*travel,p,0.022f,tail*0.35f);
                    }
                }
                // A low shared shockwave makes the simultaneous blades read as
                // one ultimate. No airborne circles or full-height filled box.
                int steps=Mathf.Clamp(384/zones.Length,8,48);
                Ring(new Vector2((zone.x+zone.y)/2,0.10f),
                    new Vector2(width*0.49f*(0.25f+travel*0.75f),0.08f+travel*0.32f),0,tail*0.75f,steps);
                ClipFinalRegion(first,zone);
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
            if(kind==Kind.FallWarning&&!HasFallWarnings()){gameObject.SetActive(false);Destroy(gameObject);return;}
            if (game.Paused) return;
            if (kind != Kind.Detonation || Time.frameCount != detonationFrame) age += Time.deltaTime;
            if (kind == Kind.Summon || kind == Kind.Form || kind == Kind.Fall || kind == Kind.Scar || kind == Kind.WardFlight)
            {
                if (follow == null || !follow.gameObject.activeInHierarchy || rain != null && rain.Landed)
                { Destroy(gameObject); return; }
            }
            else if (age >= life && kind != Kind.Charge) { Destroy(gameObject); return; }
            count = 0;CapacityExceeded=false;
            if(kind==Kind.FallWarning)FallingWarnings();
            else if (kind == Kind.Summon) Summoning();
            else if (kind == Kind.Form || kind == Kind.Fall) BladeEnergy();
            else if (kind == Kind.Planted) PlantedEnergy();
            else if(kind==Kind.Scar||kind==Kind.ScarWarning||kind==Kind.ScarFade)ScarEnergy();
            else if(kind==Kind.WardRelease||kind==Kind.WardImpact||kind==Kind.WardCollapse||kind==Kind.WardFlight)WardEnergy();
            else if(kind==Kind.Eruption && scarSiteCount>0)ScarGroundEnergy();
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
