using UnityEngine;

namespace KarmaPrototype
{
    // SW04 display only. Slots, contacts, aim and all four release events remain
    // owned by the original pattern. No body renderer is tinted or resized.
    [DefaultExecutionOrder(365)]
    public sealed class KarmaSwordWardVfx : MonoBehaviour
    {
        const int Capacity = 2048;
        sealed class Buffer
        {
            public readonly Mesh mesh;
            public readonly Vector3[] vertices = new Vector3[Capacity * 4];
            public readonly Color[] colors = new Color[Capacity * 4];
            public int count;
            public Buffer()
            {
                var indices = new int[Capacity * 6];
                for (int i = 0; i < Capacity; i++)
                { int v=i*4,n=i*6;indices[n]=v;indices[n+1]=v+1;indices[n+2]=v+2;indices[n+3]=v+2;indices[n+4]=v+1;indices[n+5]=v+3; }
                mesh = new Mesh { name="SW04 recycled transparent dome" }; mesh.MarkDynamic();
                mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=indices;
            }
            public void Begin() { count=0; }
            public void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
            {
                if(count>=Capacity)throw new System.InvalidOperationException("SW04 dome buffer exceeded");
                int v=count++*4;vertices[v]=a;vertices[v+1]=b;vertices[v+2]=c;vertices[v+3]=d;
                colors[v]=colors[v+1]=colors[v+2]=colors[v+3]=color;
            }
            public void Line(Vector2 a,Vector2 b,float width,Color color)
            {
                Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*width*0.5f;
                Quad(a-n,a+n,b-n,b+n,color);
            }
            public void Upload(Vector2 center,float width,float height)
            {
                mesh.SetVertices(vertices);mesh.SetColors(colors);
                Vector2 lo=center-new Vector2(width,height)*0.5f,hi=center+new Vector2(width,height)*0.5f;
                for(int i=0;i<count*4;i++)
                {
                    lo.x=Mathf.Min(lo.x,vertices[i].x);lo.y=Mathf.Min(lo.y,vertices[i].y);
                    hi.x=Mathf.Max(hi.x,vertices[i].x);hi.y=Mathf.Max(hi.y,vertices[i].y);
                }
                var bounds=new Bounds((lo+hi)*0.5f,new Vector3(hi.x-lo.x+0.1f,hi.y-lo.y+0.1f,2));
                mesh.SetSubMesh(0,new UnityEngine.Rendering.SubMeshDescriptor(0,count*6)
                    {firstVertex=0,vertexCount=count*4,bounds=bounds},UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                mesh.bounds=bounds;
            }
        }
        static readonly System.Collections.Generic.Stack<Buffer> spare = new System.Collections.Generic.Stack<Buffer>(4);
        KarmaGame game;KarmaEnemy owner;KarmaCharacterAnimator animator;SpriteRenderer body;
        SpriteRenderer[] blades;
        Vector3[] bladeScales;
        int[] bladeOrders;
        MeshFilter backFilter,frontFilter;MeshRenderer back,front;
        Buffer shell,detail;
        float age,absorbedAt=-10,releasedAt=-10,lockedAt=-10,endingAt=-1;
        bool countering,locked,collapsed,preparing,dispersing;
        float preparationSeconds;
        const float DissolveSeconds=0.12f;
        const float ShatterSeconds=0.58f;
        // Visual randomness is independent of combat's Unity random stream.
        static readonly System.Random fractureRandom = new System.Random();
        Vector2[] fracturePoints;
        void BuildFracture()
        {
            fracturePoints = new Vector2[32 * 7];
            for (int i=0;i<32;i++)
            {
                float phase=(float)fractureRandom.NextDouble()*Mathf.PI*2;
                for(int j=0;j<7;j++)
                {
                    float angle=phase+(j+0.15f+(float)fractureRandom.NextDouble()*0.7f)*Mathf.PI*2/7;
                    float length=j%3==1?0.028f:0.045f+(float)fractureRandom.NextDouble()*0.045f;
                    fracturePoints[i*7+j]=Direction(angle)*length;
                }
            }
        }
        float bladesBoundAt;
        int aiming=-1;
        Vector2 aimOrigin,aimAxis,aimSize,foot;
        float height,radius;
        public bool Countering => countering;
        public int FrameIndex { get; private set; }
        static Color Blue(float alpha)=>new Color(0.14f,0.70f,1.05f,alpha);
        static Color White(float alpha)=>new Color(0.78f,1.15f,1.3f,alpha);
        static Vector2 Direction(float angle)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        Vector2 Center=>foot+Vector2.up*(height*0.48f);
        float FloorAngle=>-Mathf.Asin(height*0.48f/radius);
        float GroundRadius=>Mathf.Sqrt(radius*radius-height*height*0.48f*0.48f);
        public static KarmaSwordWardVfx Create(Transform root,KarmaGame game,KarmaEnemy owner,SpriteRenderer[] blades)
        {
            var go=new GameObject("SW04 electric hex dome / no damage");go.transform.SetParent(root,false);
            var fx=go.AddComponent<KarmaSwordWardVfx>();fx.game=game;fx.owner=owner;fx.blades=blades;
            fx.BindBlades(blades);
            var art=owner.transform.Find("Artwork/Guardian sprite");
            fx.body=art!=null?art.GetComponent<SpriteRenderer>():null;
            fx.animator=art!=null?art.GetComponentInParent<KarmaCharacterAnimator>():null;
            fx.shell=spare.Count>0?spare.Pop():new Buffer();fx.detail=spare.Count>0?spare.Pop():new Buffer();
            fx.back=fx.Layer("SW04 transparent shell behind body",fx.shell,out fx.backFilter);
            fx.front=fx.Layer("SW04 fine hexes and electric rim",fx.detail,out fx.frontFilter);
            fx.age=0.30f;fx.Paint();return fx;
        }
        void BindBlades(SpriteRenderer[] pictures)
        {
            blades=pictures;bladesBoundAt=age;
            bladeScales=new Vector3[blades.Length];bladeOrders=new int[blades.Length];
            for(int i=0;i<blades.Length;i++)if(blades[i]!=null)
            {bladeScales[i]=blades[i].transform.localScale;bladeOrders[i]=blades[i].sortingOrder;}
        }
        public void Form(Transform root,SpriteRenderer[] pictures)
        {
            // Preserve the shell, phase and pooled meshes across the warning boundary.
            preparing=false;transform.SetParent(root,true);BindBlades(pictures);Paint();
        }
        MeshRenderer Layer(string name,Buffer buffer,out MeshFilter filter)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            filter=go.AddComponent<MeshFilter>();filter.sharedMesh=buffer.mesh;
            var renderer=go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=KarmaCombatVFX.GlowMaterial??KarmaVisuals.DefaultMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return renderer;
        }
        void Attach()
        {
            height=animator!=null?Mathf.Max(2,animator.SwordChargeBodyHeight):4.8f;
            // Use registered support coordinates, not weapon-inclusive bounds.
            foot=body!=null&&animator!=null
                ?(Vector2)body.transform.parent.TransformPoint(animator.SwordVisualHome+animator.SwordFoot)
                :owner.Position-Vector2.up*KarmaSpace.GuardianGroundY;
            // A true body-centred sphere, clipped by the floor. Raising its
            // centre gives headroom without stretching a narrow semicircle.
            radius=height*0.62f;
            int layer=body!=null?body.sortingLayerID:0,order=body!=null?body.sortingOrder:5;
            back.sortingLayerID=front.sortingLayerID=layer;back.sortingOrder=order-1;front.sortingOrder=order+1;
            // World-sized geometry remains independent of mirrored/scaled parents.
            transform.position=Vector3.zero;transform.rotation=Quaternion.identity;
            var scale=transform.parent.lossyScale;
            transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        }
        Vector2 Dome(float u,float unfold)
        {
            float angle=Mathf.Lerp(FloorAngle,Mathf.PI-FloorAngle,u);
            return foot+new Vector2(Mathf.Cos(angle)*radius,
                Mathf.Max(0,height*0.48f+Mathf.Sin(angle)*radius))*unfold;
        }
        static float OrbitPhase(int blade,float seconds)
        {
            int lane=blade%2;
            return seconds*1.65f*(lane==0?1:-1)+lane*Mathf.PI*0.5f+(blade/2)*Mathf.PI;
        }
        // Two projected tilted rings, each with an opposite pair. Their fixed
        // speed and opposite winding make the crossing pattern easy to read.
        Vector2 OrbitPoint(int blade,float seconds)
        {
            float phase=OrbitPhase(blade,seconds),slope=blade%2==0?1:-1;
            float x=Mathf.Cos(phase)*radius*0.78f,y=Mathf.Sin(phase)*radius*0.36f;
            return foot+new Vector2(x*0.906308f-y*0.422618f*slope,
                height*0.48f+x*0.422618f*slope+y*0.906308f);
        }
        public float BladeOrbitAlpha(int blade,float seconds)
        { return (0.985f+0.015f*Mathf.Sin(OrbitPhase(blade,seconds)))
                *Mathf.SmoothStep(0,1,(age-bladesBoundAt)/0.18f); }
        public void ApplyBladeOrbitDepth(int blade,float seconds)
        {
            if(blades[blade]==null)return;
            float depth=Mathf.Sin(OrbitPhase(blade,seconds));
            var scale=bladeScales[blade];float perspective=0.96f+0.05f*depth;
            blades[blade].transform.localScale=new Vector3(scale.x*perspective,scale.y*perspective,scale.z);
            blades[blade].sortingLayerID=front.sortingLayerID;
            blades[blade].sortingOrder=depth<0?back.sortingOrder-1:front.sortingOrder+1;
        }
        void RestoreAimingBlade(int blade)
        {
            if(blade<0||blade>=blades.Length||blades[blade]==null)return;
            blades[blade].transform.localScale=bladeScales[blade];
            blades[blade].sortingOrder=bladeOrders[blade];
        }
        public Vector2 BladeOrbitPosition(int blade,float seconds)
        { Attach();return OrbitPoint(blade,seconds); }
        void Curve(Buffer buffer,Vector2 center,float rx,float ry,float turn,float span,float width,Color color,int steps=48)
        {
            for(int i=0;i<steps;i++)
            {
                float a=turn+span*i/steps,b=turn+span*(i+1)/steps;
                float taper=Mathf.Sin((i+0.5f)/steps*Mathf.PI);
                buffer.Line(center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),
                    center+new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry),width*(0.25f+taper*0.75f),color);
            }
        }
        public static KarmaSwordWardVfx Prepare(KarmaGame game,KarmaEnemy owner,float seconds)
        {
            var fx=Create(game.World,game,owner,System.Array.Empty<SpriteRenderer>());
            fx.preparing=true;fx.preparationSeconds=seconds;fx.age=0;fx.Paint();
            if(game.IsCombat&&owner.Alive)KarmaAudio.Play(KarmaSfxId.SwEnergyCharge,seconds,fx);
            return fx;
        }
        Vector2 Surface(float longitude,float latitude,float unfold)
        {
            latitude=Mathf.Lerp(FloorAngle,Mathf.PI*0.5f,Mathf.Clamp01(latitude/(Mathf.PI*0.5f)));
            return foot+new Vector2(radius*Mathf.Sin(longitude)*Mathf.Cos(latitude),
                Mathf.Max(0,height*0.48f+radius*Mathf.Sin(latitude)))*unfold;
        }
        void Hexagons(float unfold,float clock,float intensity)
        {
            // A connected hex lattice in longitude/latitude bends around the
            // spherical cap, rather than floating flat hexagons behind the boss.
            const float cell=0.16f;
            for(int row=0;row<6;row++)for(int col=-5;col<=5;col++)
            {
                float longitude=(col+(row%2)*0.5f)*cell*1.73f,latitude=0.10f+row*cell*1.5f;
                if(Mathf.Abs(longitude)>1.32f||latitude>1.4f)continue;
                float edge=Mathf.Clamp01(Mathf.Abs(longitude)/1.4f+latitude*0.2f);
                // Quiet central cells leave the face and torso unobscured;
                // a slow travelling highlight reveals the curved outer shell.
                float sweep=Mathf.Max(0,Mathf.Sin(latitude*5-clock*2+longitude));
                float alpha=Mathf.Min(0.30f,(0.065f+edge*edge*0.16f)*(0.75f+sweep*0.25f)*intensity)*unfold;
                Vector2 center=Surface(longitude,latitude,unfold);
                for(int side=0;side<6;side++)
                {
                    float a=Mathf.PI/6+side*Mathf.PI/3,b=Mathf.PI/6+(side+1)*Mathf.PI/3;
                    Vector2 from=Surface(longitude+Mathf.Cos(a)*cell,latitude+Mathf.Sin(a)*cell,unfold);
                    Vector2 to=Surface(longitude+Mathf.Cos(b)*cell,latitude+Mathf.Sin(b)*cell,unfold);
                    detail.Line(from,to,height*0.0032f,Blue(alpha));
                    if((row*7+col*3+19)%7==0&&row>=3)
                        shell.Quad(center,from,to,to,Blue(0.055f*sweep*unfold));
                }
            }
        }
        void ElectricLine(Buffer buffer,Vector2 a,Vector2 b,float width,float alpha)
        {buffer.Line(a,b,width*4,Blue(alpha*0.10f));buffer.Line(a,b,width,White(alpha));}
        void Crackle(float unfold,float clock,float intensity)
        {
            // Deterministic 24 Hz bursts: new short forks every 2-3 frames,
            // confined to the rim and outer cells instead of flashing the body.
            int frame=(int)Mathf.Floor(clock*24);
            for(int i=0;i<10;i++)
            {
                int tick=frame+i*7,cycle=tick/3;
                if((cycle+i)%4==0)continue;
                float seed=Mathf.Repeat(Mathf.Sin(cycle*13.37f+i*7.91f)*437.3f,1);
                float u=0.04f+seed*0.88f;
                Vector2 last=Dome(u,unfold);
                float alpha=(tick%3==0?0.90f:0.48f)*unfold*intensity;
                for(int j=1;j<=6;j++)
                {
                    Vector2 next=Dome(Mathf.Min(0.98f,u+j*0.009f),unfold);
                    if(j<6)next+=Direction(seed*19+j*2.4f)*radius*0.018f;
                    ElectricLine(detail,last,next,height*0.005f,alpha);
                    if(j==3)
                    {
                        Vector2 branch=next+(foot-next)*0.06f+Direction(seed*9)*height*0.022f;
                        ElectricLine(detail,next,branch,height*0.003f,alpha*0.65f);
                    }
                    last=next;
                }
                // A short lightning seam follows the curved hex surface.
                float longitude=(i%2==0?-1:1)*(0.85f+seed*0.35f);
                last=Surface(longitude,0.25f+seed*0.75f,unfold);
                for(int j=1;j<=3;j++)
                {
                    Vector2 next=Surface(longitude+(j%2==0?0:0.08f),0.25f+seed*0.75f+j*0.04f,unfold);
                    ElectricLine(detail,last,next,height*0.003f,alpha*0.55f);last=next;
                }
            }
        }
        void Membrane(float unfold,float clock,float warning)
        {
            // A continuous blue volume, with denser edges and a soft moving
            // reflection. It stays behind the body, so opacity never tints skin.
            for(int row=0;row<24;row++)for(int col=0;col<16;col++)
            {
                float a=row/24f,b=(row+1)/24f,x=col/8f-1,z=(col+1)/8f-1;
                float ya=Mathf.Lerp(-height*0.48f,radius,a),yb=Mathf.Lerp(-height*0.48f,radius,b);
                float wa=Mathf.Sqrt(Mathf.Max(0,radius*radius-ya*ya)),wb=Mathf.Sqrt(Mathf.Max(0,radius*radius-yb*yb));
                float edge=Mathf.Abs((x+z)*0.5f);
                float reflection=Mathf.Max(0,Mathf.Cos(edge*3+a*2-clock*0.8f));
                float alpha=(0.035f+edge*edge*edge*0.10f+reflection*0.012f)*unfold*(1+warning*0.18f);
                shell.Quad(foot+new Vector2(x*wa,ya+height*0.48f)*unfold,foot+new Vector2(z*wa,ya+height*0.48f)*unfold,
                    foot+new Vector2(x*wb,yb+height*0.48f)*unfold,foot+new Vector2(z*wb,yb+height*0.48f)*unfold,
                    new Color(0.20f+reflection*0.08f,0.65f+reflection*0.08f,0.95f,alpha));
            }
            // Two curved glints describe a rounded shield instead of a wire cage.
            for(int side=0;side<2;side++)for(int i=0;i<24;i++)
            {
                float u=0.10f+side*0.62f+i*0.007f;
                Vector2 a=foot+(Dome(u,unfold)-foot)*0.94f;
                Vector2 b=foot+(Dome(u+0.007f,unfold)-foot)*0.94f;
                float taper=Mathf.Sin((i+0.5f)/24*Mathf.PI);
                ElectricLine(detail,a,b,height*0.008f*taper,0.42f*taper*unfold);
            }
        }
        void BladeTrails(float unfold)
        {
            // The pattern and trails sample the same shell orbit. Anchor the
            // trail to the actual slot, including its arena-edge safety clamp.
            for(int blade=0;blade<blades.Length;blade++)
            {
                if(blades[blade]==null||!blades[blade].enabled||blade==aiming)continue;
                Vector2 at=blades[blade].transform.position;
                float seconds=owner.FightSeconds;
                Vector2 current=OrbitPoint(blade,seconds);
                for(int j=0;j<12;j++)
                {
                    float a=j*0.015f,b=(j+1)*0.015f;
                    Vector2 from=at+OrbitPoint(blade,seconds-a)-current;
                    Vector2 to=at+OrbitPoint(blade,seconds-b)-current;
                    float fade=(1-j/12f)*unfold*BladeOrbitAlpha(blade,seconds)/0.96f;
                    Buffer layer=Mathf.Sin(OrbitPhase(blade,seconds-a))<0?shell:detail;
                    ElectricLine(layer,from,to,height*0.007f*fade,fade*0.78f);
                }
            }
        }
        void PaintShards()
        {
            float t=Mathf.Clamp01(age/ShatterSeconds),fade=1-Mathf.SmoothStep(0,1,(t-0.25f)/0.75f);
            // Brief disconnected seams preserve the dome silhouette at the break.
            // There is no intact membrane or expanding shock ring after a parry.
            float crackFade=1-Mathf.SmoothStep(0,1,(age-0.02f)/0.14f);
            for(int i=0;i<12;i++)
            {
                float u=(i+0.15f)/12;
                Vector2 edge=Dome(u,1),inner=foot+(edge-foot)*0.72f;
                Vector2 bend=(edge+inner)*0.5f+Direction(i*2.4f)*height*0.035f;
                ElectricLine(detail,edge,bend,height*0.009f,crackFade*0.85f);
                ElectricLine(detail,bend,inner,height*0.004f,crackFade*0.65f);
                Curve(detail,Center,radius,radius,Mathf.Lerp(FloorAngle,Mathf.PI-FloorAngle,u),Mathf.PI*0.045f,
                    height*0.006f,White(crackFade*0.7f));
            }
            // Consistent fragment dimensions, with a small stagger and ballistic drift.
            for(int row=0;row<4;row++)for(int col=0;col<8;col++)
            {
                float u=(col+0.5f)/8;
                Vector2 ray=(Dome(u,1)-Center).normalized;
                float delay=0.025f+(col%3)*0.012f+row*0.009f;
                float travel=Mathf.Clamp01((age-delay)/(ShatterSeconds-delay));
                float push=travel*(1-0.25f*travel),variation=0.90f+0.10f*Mathf.Sin(col*7+row*3);
                // Panels originate on the actual dome, instead of appearing as
                // unrelated rectangular confetti when the membrane disappears.
                float inner=row/4f,outer=(row+1)/4f;
                Vector2 start=foot+(Dome(u,1)-foot)*(inner+outer)*0.5f;
                Vector2 at=start+ray*height*push*(0.18f+variation*0.28f)
                    -Vector2.up*height*travel*travel*0.48f;
                float turn=travel*(col%2==0?1.8f:-1.8f)*variation;
                float panelFade=fade*(1-Mathf.SmoothStep(0,1,(foot.y-at.y)/(height*0.15f)));
                float glint=0.75f+0.25f*Mathf.Sin(travel*6+col+row);
                // Ordered radial vertices form a simple, chipped concave polygon.
                // Each triangle shares its center, avoiding rectangular panels.
                for(int j=0;j<7;j++)
                {
                    int offset=(row*8+col)*7;
                    Vector2 a=ShatterPoint(start+fracturePoints[offset+j]*height,start,at,turn,travel);
                    Vector2 b=ShatterPoint(start+fracturePoints[offset+(j+1)%7]*height,start,at,turn,travel);
                    shell.Quad(at,a,b,b,Blue(panelFade*0.30f));
                    detail.Line(a,b,height*(j%2==0?0.004f:0.006f),White(panelFade*glint));
                }
            }
            shell.Upload(Center,height*3.7f,height*3.6f);detail.Upload(Center,height*3.7f,height*3.6f);
        }
        static Vector2 ShatterPoint(Vector2 point,Vector2 center,Vector2 at,float turn,float travel)
        {
            Vector2 p=(point-center)*(1-travel*0.15f);
            float cos=Mathf.Cos(turn),sin=Mathf.Sin(turn);
            return at+new Vector2(p.x*cos-p.y*sin,p.x*sin+p.y*cos);
        }
        void Paint()
        {
            if(shell==null||detail==null)return;
            if(!dispersing&&endingAt<0)Attach();shell.Begin();detail.Begin();
            if(dispersing){PaintShards();return;}
            // Mesh motion follows render frames; only authored sprite indexing is 24 FPS.
            float clock=age;FrameIndex=(int)Mathf.Floor(age*24)%24;
            float unfold=Mathf.SmoothStep(0,1,age/(preparing?Mathf.Max(0.05f,preparationSeconds):0.30f));
            float absorb=1-Mathf.SmoothStep(0,1,(age-absorbedAt)/0.30f);
            float release=1-Mathf.Clamp01((age-releasedAt)/0.16f);
            float warning=locked?Mathf.Clamp01((age-lockedAt)/0.18f):0;
            // A dominant event avoids additive overexposure when hit/lock/release overlap.
            float intensity=1+Mathf.Max(warning*0.35f,Mathf.Max(absorb*0.45f,release*0.65f));
            float endProgress=endingAt<0?0:Mathf.Clamp01((age-endingAt)/DissolveSeconds);
            float fade=1-Mathf.SmoothStep(0,1,(endProgress-0.2f)/0.8f);
            Membrane(unfold,clock,warning);
            Hexagons(unfold,clock,intensity);
            Crackle(unfold,clock,intensity);
            for(int i=0;i<96;i++)
            {
                Vector2 a=Dome(i/96f,unfold),b=Dome((i+1)/96f,unfold);
                // A broad blue edge fades inward, outside the readable body.
                Vector2 capCenter=foot+Vector2.up*height*0.48f*unfold;
                Vector2 innerA=capCenter+(a-capCenter)*0.965f,innerB=capCenter+(b-capCenter)*0.965f;
                shell.Quad(a,innerA,b,innerB,Blue(0.16f*unfold));
                detail.Line(a,b,height*0.018f,Blue(0.045f*unfold));
                ElectricLine(detail,a,b,height*(0.0045f+release*0.003f),(0.65f+warning*0.2f+release*0.15f)*unfold);
            }
            // Current crawls around the shell, with short jagged branches.
            for(int i=0;i<8;i++)
            {
                float u=Mathf.Repeat(clock*0.12f+i/8f,0.94f)+0.03f;
                Vector2 last=Dome(u,unfold);
                for(int j=1;j<=5;j++)
                {
                    Vector2 next=Dome(Mathf.Min(1,u+j*0.008f),unfold);
                    if(j<5)next+=Direction(j*2.9f+i+clock*17)*height*0.009f;
                    detail.Line(last,next,height*(locked?0.007f:0.004f),White(0.48f*intensity*unfold));last=next;
                }
            }
            Curve(detail,foot,GroundRadius*unfold,height*0.035f*unfold,0,Mathf.PI*2,height*0.004f,Blue(0.48f*unfold));
            BladeTrails(unfold);
            // A shallow pulse travels over the existing surface after a real hit.
            // Its low opacity leaves the boss silhouette and aiming cue readable.
            if(absorb>0)
            {
                float travel=Mathf.Clamp01((age-absorbedAt)/0.30f);
                for(int i=0;i<48;i++)
                    detail.Line(foot+(Dome(i/48f,unfold)-foot)*(0.88f+travel*0.12f),
                        foot+(Dome((i+1)/48f,unfold)-foot)*(0.88f+travel*0.12f),
                        height*0.005f,White(absorb*0.28f));
            }
            if(age<0.30f)for(int i=0;i<8;i++)
            {
                float u=Mathf.Repeat(clock*2+i/8f,1);float x=(i-3.5f)*radius/4;
                detail.Line(foot+new Vector2(x,height*0.18f*(1-u)),foot+new Vector2(x*0.8f,0),height*0.004f,Blue((1-u)*0.4f));
            }
            if(aiming>=0)
            {
                Vector2 n=new Vector2(-aimAxis.y,aimAxis.x);
                for(int i=0;i<12;i++)
                {
                    float t=i/12f,u=(i+1)/12f;
                    detail.Line(aimOrigin+aimAxis*aimSize.x*(0.1f+t*0.5f)+n*Mathf.Sin(t*12+clock*20)*0.04f,
                        aimOrigin+aimAxis*aimSize.x*(0.1f+u*0.5f)+n*Mathf.Sin(u*12+clock*20)*0.04f,
                        locked?0.075f:0.035f,White(locked?0.8f:0.4f));
                }
            }
            // Brief rim flash, then a fast cut; no outward burst or shock contact.
            if(endingAt>=0)
            {
                for(int i=0;i<shell.count*4;i++){var c=shell.colors[i];c.a*=fade;shell.colors[i]=c;}
                float flash=1+0.65f*(1-Mathf.Clamp01(endProgress/0.35f));
                for(int i=0;i<detail.count*4;i++){var c=detail.colors[i];c.r*=flash;c.g*=flash;c.b*=flash;c.a*=fade;detail.colors[i]=c;}
            }
            if(endingAt>=0)for(int i=0;i<blades.Length;i++)if(blades[i]!=null)
            {
                blades[i].color=new Color(0.86f,0.95f,1,BladeOrbitAlpha(i,owner.FightSeconds)*fade);
            }
            float width=height*2.5f;
            shell.Upload(Center,width,height*3.2f);detail.Upload(Center,width,height*3.2f);
        }
        public void Pulse() { absorbedAt=age;Paint(); }
        public void Absorb()
        {
            absorbedAt=age;countering=true;
            foreach(var blade in blades)if(blade!=null)blade.enabled=true;
            Attach();Paint();
        }
        public void Aim(int index,Vector2 origin,Vector2 axis,Vector2 size,bool committed)
        {
            RestoreAimingBlade(index);
            if(committed&&!locked)lockedAt=age;
            aiming=index;aimOrigin=origin;aimAxis=axis;aimSize=size;locked=committed;
        }
        public void Released(Vector2 origin,Vector2 axis)
        {
            aiming=-1;locked=false;releasedAt=age;
            KarmaSW05Energy.WardBurst(transform,game,origin,axis,radius,0);
            Paint();
        }
        public static void CounterProjectile(KarmaGame game,KarmaEnemy owner,KarmaBossAttack shot,Vector2 velocity,Vector2 size)
        {
            KarmaSwordAttackPolish.Core(shot);
            ApplyBlade(shot.Visual,size,velocity);
            foreach(var trail in shot.GetComponentsInChildren<TrailRenderer>())trail.enabled=false;
        }

        public static void ApplyBlade(SpriteRenderer renderer,Vector2 size,Vector2 direction)
        {
            // Reuse the straight silver/cyan weapon already used by SW05.
            // Its downward source axis needs +90 degrees to face the shot.
            // A slightly shorter silhouette reduces the needle-like taper.
            // Width and the gameplay rectangle remain unchanged.
            KarmaSW05VFX.ApplySword(renderer,new Vector2(size.x*0.88f,size.y),direction);
            KarmaSwordSkillSurface.Apply(renderer,3);
        }

        public void Collapse()
        {
            if(collapsed)return;collapsed=true;
            // Immediate shield removal, followed by pooled surface shards.
            Attach();back.enabled=front.enabled=false;
            var shards=Create(game.World,game,owner,System.Array.Empty<SpriteRenderer>());
            shards.dispersing=true;shards.foot=foot;shards.height=height;shards.radius=radius;shards.age=0;shards.BuildFracture();shards.Paint();
        }
        public void Finish()
        {
            // Keep only harmless pictures alive while gameplay slots stop immediately.
            if(collapsed||endingAt>=0)return;
            Attach();
            aiming=-1;locked=false;endingAt=age;transform.SetParent(game.World,true);
            foreach(var blade in blades)if(blade!=null)blade.transform.SetParent(transform,true);
            Paint();
        }
        void LateUpdate()
        {
            if(game==null||owner==null||!owner.PresentationActive||owner.Stunned&&!dispersing||game.Phase!=RunPhase.Combat
                ||preparing&&owner.CurrentPattern!=(int)EnemyAttack.SW04)
            {gameObject.SetActive(false);Destroy(gameObject);return;}
            if(game.Paused||collapsed)return;
            age+=Time.deltaTime;
            if(dispersing&&age>=ShatterSeconds){gameObject.SetActive(false);Destroy(gameObject);return;}
            if(endingAt>=0&&age-endingAt>=DissolveSeconds){gameObject.SetActive(false);Destroy(gameObject);return;}
            Paint();
        }
        void OnDisable()
        {
            if(back!=null)back.enabled=false;if(front!=null)front.enabled=false;
            if(backFilter!=null)backFilter.sharedMesh=null;if(frontFilter!=null)frontFilter.sharedMesh=null;
            Return(ref shell);Return(ref detail);
        }
        static void Return(ref Buffer buffer)
        {if(buffer==null)return;if(spare.Count<4)spare.Push(buffer);else Object.Destroy(buffer.mesh);buffer=null;}
        void OnDestroy(){OnDisable();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearPool(){while(spare.Count>0)Object.Destroy(spare.Pop().mesh);}
    }
}
