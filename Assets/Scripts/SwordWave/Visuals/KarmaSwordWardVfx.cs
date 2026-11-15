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
        MeshFilter backFilter,frontFilter;MeshRenderer back,front;
        Buffer shell,detail;
        float age,absorbedAt=-10,releasedAt=-10,lockedAt=-10,endingAt=-1;
        bool countering,locked,collapsed,preparing,dispersing;
        float preparationSeconds;
        int aiming=-1;
        Vector2 aimOrigin,aimAxis,aimSize,foot;
        float height,radius;
        public bool Countering => countering;
        public int FrameIndex { get; private set; }
        static Color Blue(float alpha)=>new Color(0.18f,0.45f,1.5f,alpha);
        static Color White(float alpha)=>new Color(0.78f,1.1f,1.7f,alpha);
        static Vector2 Direction(float angle)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        Vector2 Center=>foot+Vector2.up*(height*0.55f);
        public static KarmaSwordWardVfx Create(Transform root,KarmaGame game,KarmaEnemy owner,SpriteRenderer[] blades)
        {
            var go=new GameObject("SW04 electric hex dome / no damage");go.transform.SetParent(root,false);
            var fx=go.AddComponent<KarmaSwordWardVfx>();fx.game=game;fx.owner=owner;fx.blades=blades;
            var art=owner.transform.Find("Artwork/Guardian sprite");
            fx.body=art!=null?art.GetComponent<SpriteRenderer>():null;
            fx.animator=art!=null?art.GetComponentInParent<KarmaCharacterAnimator>():null;
            fx.shell=spare.Count>0?spare.Pop():new Buffer();fx.detail=spare.Count>0?spare.Pop():new Buffer();
            fx.back=fx.Layer("SW04 transparent shell behind body",fx.shell,out fx.backFilter);
            fx.front=fx.Layer("SW04 fine hexes and electric rim",fx.detail,out fx.frontFilter);
            fx.age=0.30f;fx.Paint();return fx;
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
            radius=height*1.06f;
            int layer=body!=null?body.sortingLayerID:0,order=body!=null?body.sortingOrder:5;
            back.sortingLayerID=front.sortingLayerID=layer;back.sortingOrder=order-1;front.sortingOrder=order+1;
            // World-sized geometry remains independent of mirrored/scaled parents.
            transform.position=Vector3.zero;transform.rotation=Quaternion.identity;
            var scale=transform.parent.lossyScale;
            transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        }
        Vector2 Dome(float u,float unfold)
        { return foot+new Vector2(Mathf.Cos(u*Mathf.PI)*radius,Mathf.Sin(u*Mathf.PI)*height*1.48f*unfold); }
        void Curve(Buffer buffer,Vector2 center,float rx,float ry,float turn,float span,float width,Color color)
        {
            for(int i=0;i<48;i++)
            {
                float a=turn+span*i/48,b=turn+span*(i+1)/48;
                float taper=Mathf.Sin((i+0.5f)/48*Mathf.PI);
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
        {latitude=Mathf.Max(0,latitude);return foot+new Vector2(radius*Mathf.Sin(longitude)*Mathf.Cos(latitude),height*1.48f*Mathf.Sin(latitude)*unfold);}
        void Hexagons(float unfold,float clock,float intensity)
        {
            // A connected hex lattice in longitude/latitude bends around the
            // spherical cap, rather than floating flat hexagons behind the boss.
            const float cell=0.10f;
            for(int row=0;row<9;row++)for(int col=-8;col<=8;col++)
            {
                float longitude=(col+(row%2)*0.5f)*cell*1.73f,latitude=0.10f+row*cell*1.5f;
                if(Mathf.Abs(longitude)>1.32f||latitude>1.4f)continue;
                float edge=Mathf.Clamp01(Mathf.Abs(longitude)/1.4f+latitude*0.2f);
                float alpha=(0.018f+edge*0.16f)*(0.85f+0.15f*Mathf.Sin(clock*1.2f+row))*intensity*unfold;
                Vector2 center=Surface(longitude,latitude,unfold);
                for(int side=0;side<6;side++)
                {
                    float a=Mathf.PI/6+side*Mathf.PI/3,b=Mathf.PI/6+(side+1)*Mathf.PI/3;
                    Vector2 from=Surface(longitude+Mathf.Cos(a)*cell,latitude+Mathf.Sin(a)*cell,unfold);
                    Vector2 to=Surface(longitude+Mathf.Cos(b)*cell,latitude+Mathf.Sin(b)*cell,unfold);
                    detail.Line(from,to,height*0.0025f,Blue(alpha));
                    if((row*7+col*3+19)%7==0&&row>=3)
                        shell.Quad(center,from,to,to,Blue(0.055f*unfold));
                }
            }
        }
        void ElectricLine(Buffer buffer,Vector2 a,Vector2 b,float width,float alpha)
        {buffer.Line(a,b,width*4,Blue(alpha*0.10f));buffer.Line(a,b,width,White(alpha));}
        void SwordGlyph(Vector2 at,float length,float alpha,bool crown=false)
        {
            Vector2 tip=at+Vector2.up*length*0.65f,basePoint=at-Vector2.up*length*0.35f;
            ElectricLine(detail,tip,at+new Vector2(-length*0.07f,length*0.2f),height*0.0028f,alpha);
            ElectricLine(detail,tip,at+new Vector2(length*0.07f,length*0.2f),height*0.0028f,alpha);
            ElectricLine(detail,at+new Vector2(-length*0.07f,length*0.2f),basePoint,height*0.0028f,alpha);
            ElectricLine(detail,at+new Vector2(length*0.07f,length*0.2f),basePoint,height*0.0028f,alpha);
            ElectricLine(detail,at-new Vector2(length*0.15f,0),at+new Vector2(length*0.15f,0),height*0.0035f,alpha);
            if(crown)
            {
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                    float ra=i%2==0?length*0.35f:length*0.12f,rb=i%2==0?length*0.12f:length*0.35f;
                    ElectricLine(detail,at+Direction(a)*ra,at+Direction(b)*rb,height*0.0035f,alpha);
                }
            }
        }
        Vector2 OrbitPoint(float a)
        {return foot+new Vector2(Mathf.Cos(a)*radius*1.10f,height*(0.76f+0.27f*Mathf.Cos(a)+0.16f*Mathf.Sin(a)));}
        void Orbit(float unfold,float clock)
        {
            for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                Vector2 from=OrbitPoint(a),to=OrbitPoint(b);
                from.y=foot.y+(from.y-foot.y)*unfold;to.y=foot.y+(to.y-foot.y)*unfold;
                Buffer layer=Mathf.Sin(a)>0?shell:detail;
                float head=Mathf.Max(0,Mathf.Cos(a-clock*0.38f*owner.Facing));
                ElectricLine(layer,from,to,height*(0.0016f+head*0.0012f),(0.22f+head*0.35f)*unfold);
            }
        }
        void PaintShards()
        {
            float t=Mathf.Clamp01(age/0.28f),fade=(1-t)*(1-t);
            for(int i=0;i<28;i++)
            {
                float u=(i+0.5f)/28;Vector2 start=Dome(u,1);
                Vector2 ray=Direction(u*Mathf.PI);
                Vector2 at=start+ray*height*t*0.25f-Vector2.up*height*t*t*0.18f;
                Vector2 tangent=new Vector2(-ray.y,ray.x);
                detail.Quad(at,at+ray*height*0.07f*fade+tangent*height*0.018f*fade,
                    at+ray*height*0.11f*fade,at+ray*height*0.07f*fade-tangent*height*0.018f*fade,White(fade*0.8f));
                if(i%2==0)ElectricLine(detail,at,at-ray*height*0.045f*fade,height*0.003f,fade*0.65f);
            }
            shell.Upload(Center,height*2.7f,height*3.2f);detail.Upload(Center,height*2.7f,height*3.2f);
        }
        void Paint()
        {
            if(shell==null||detail==null)return;
            if(!dispersing)Attach();shell.Begin();detail.Begin();
            if(dispersing){PaintShards();return;}
            float clock=Mathf.Floor(age*24)/24;FrameIndex=(int)Mathf.Floor(age*24)%24;
            float unfold=Mathf.SmoothStep(0,1,age/(preparing?Mathf.Max(0.05f,preparationSeconds):0.30f));
            float absorb=1-Mathf.Clamp01((age-absorbedAt)/0.20f);
            float release=1-Mathf.Clamp01((age-releasedAt)/0.16f);
            float warning=locked?Mathf.Clamp01((age-lockedAt)/0.18f):0;
            float intensity=1+warning*0.65f+absorb*0.25f+release*0.55f;
            float fade=endingAt<0?1:1-Mathf.SmoothStep(0,1,(age-endingAt)/0.125f);
            // Filled shell is behind the body. There is no bright white center.
            for(int row=0;row<24;row++)
            {
                float a=row/24f,b=(row+1)/24f;
                float wa=radius*Mathf.Sqrt(1-a*a),wb=radius*Mathf.Sqrt(1-b*b);
                float alpha=(0.018f+0.025f*a)*unfold*(1+warning*0.3f);
                shell.Quad(foot+new Vector2(-wa,a*height*1.48f*unfold),foot+new Vector2(wa,a*height*1.48f*unfold),
                    foot+new Vector2(-wb,b*height*1.48f*unfold),foot+new Vector2(wb,b*height*1.48f*unfold),Blue(alpha));
            }
            Hexagons(unfold,clock,intensity);
            for(int i=0;i<96;i++)
            {
                Vector2 a=Dome(i/96f,unfold),b=Dome((i+1)/96f,unfold);
                detail.Line(a,b,height*0.018f,Blue(0.10f*unfold));
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
            Curve(detail,foot,radius,height*0.075f,0,Mathf.PI*2,height*0.004f,Blue(0.48f*unfold));
            for(int side=-1;side<=1;side+=2)
                for(int j=0;j<4;j++)
                {
                    float pulse=0.55f+0.20f*Mathf.Sin(clock*4+j*1.7f);
                    Vector2 a=foot+new Vector2(side*radius*(0.88f+j*0.025f),height*0.015f);
                    Vector2 b=a+new Vector2(height*(-side*0.025f+Mathf.Sin(clock*9+j*3.7f)*0.012f),height*(0.05f+j*0.018f)*unfold);
                    ElectricLine(detail,a,b,height*0.004f,pulse*unfold);
                    ElectricLine(detail,b,b+new Vector2(side*height*0.012f,height*0.025f*unfold),height*0.003f,pulse*unfold);
                }
            Orbit(unfold,clock);
            Vector2 crest=Dome(0.5f,unfold)+Vector2.up*height*0.015f;
            SwordGlyph(crest,height*0.30f,unfold*0.85f,true);
            if(!countering)
                for(int i=0;i<4;i++)
                {
                    float x=i==0?-0.80f:i==1?-0.57f:i==2?0.64f:0.94f;
                    float y=i==0?0.79f:i==1?0.35f:i==2?0.34f:0.74f;
                    Vector2 at=foot+new Vector2(x*radius,(y*1.48f+Mathf.Sin(clock*1.5f+i)*0.012f)*height*unfold);
                    SwordGlyph(at,height*0.26f,unfold*(0.60f+warning*0.15f));
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
            // Contact/release accents stay on the cap and floor, never a
            // second full-body circle behind the character.
            if(release>0||absorb>0)
                for(int i=0;i<10;i++)
                {
                    float side=i<5?-1:1,x=side*radius*(0.82f+(i%5)*0.035f);
                    Vector2 a=foot+new Vector2(x,height*0.015f);
                    ElectricLine(detail,a,a+new Vector2(-side*height*0.05f,height*(0.07f+(i%3)*0.025f)),height*0.004f,Mathf.Max(release,absorb)*0.7f);
                }
            if(fade<1)
            {
                for(int i=0;i<shell.count*4;i++){var c=shell.colors[i];c.a*=fade;shell.colors[i]=c;}
                for(int i=0;i<detail.count*4;i++){var c=detail.colors[i];c.a*=fade;detail.colors[i]=c;}
            }
            shell.Upload(Center,height*2.5f,height*2.6f);detail.Upload(Center,height*2.5f,height*2.6f);
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
            KarmaSwordSkillSurface.Apply(shot.Visual,3);
            foreach(var trail in shot.GetComponentsInChildren<TrailRenderer>())trail.enabled=false;
            KarmaSW05Energy.CounterFlight(shot.transform.parent,game,shot.transform,velocity.normalized,size);
        }

        public void Collapse()
        {
            if(collapsed)return;collapsed=true;
            // Immediate shield removal, followed by pooled surface shards.
            Attach();back.enabled=front.enabled=false;
            var shards=Create(game.World,game,owner,System.Array.Empty<SpriteRenderer>());
            shards.dispersing=true;shards.foot=foot;shards.height=height;shards.radius=radius;shards.age=0;shards.Paint();
        }
        public void Finish()
        {
            // Normal completion alone gets a three-frame cosmetic fade. The
            // canceled slot root can still disappear synchronously as before.
            if(collapsed||endingAt>=0)return;
            aiming=-1;locked=false;endingAt=age;transform.SetParent(game.World,true);
        }
        void LateUpdate()
        {
            if(game==null||owner==null||!owner.PresentationActive||owner.Stunned&&!dispersing||game.Phase!=RunPhase.Combat
                ||preparing&&(owner.CurrentPattern!=(int)EnemyAttack.SW04||age>preparationSeconds))
            {gameObject.SetActive(false);Destroy(gameObject);return;}
            if(game.Paused||collapsed)return;
            age+=Time.deltaTime;
            if(dispersing&&age>=0.28f){gameObject.SetActive(false);Destroy(gameObject);return;}
            if(endingAt>=0&&age-endingAt>=0.125f){gameObject.SetActive(false);Destroy(gameObject);return;}
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
