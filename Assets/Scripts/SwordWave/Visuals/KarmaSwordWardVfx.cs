using UnityEngine;

namespace KarmaPrototype
{
    // SW04 presentation only. The original four slots still own all aiming,
    // damage and cancellation; this renderer never moves those slots.
    public sealed class KarmaSwordWardVfx : MonoBehaviour
    {
        static readonly Sprite[] frames = new Sprite[16];
        KarmaGame game; KarmaEnemy owner;
        SpriteRenderer formation, absorbed;
        SpriteRenderer[] blades;
        MaterialPropertyBlock properties, pulseProperties;
        readonly LineRenderer[] arcs = new LineRenderer[4], trails = new LineRenderer[4];
        readonly Vector3[] positions = new Vector3[4], previous = new Vector3[4];
        readonly Vector3[] path = new Vector3[12];
        LineRenderer outer, inner, focus;
        float age, absorbedAt = -10;
        bool countering;
        int aiming = -1;
        Vector2 aimOrigin, aimAxis, aimSize;
        bool locked;
        public bool Countering => countering;
        public int FrameIndex { get; private set; }
        public static KarmaSwordWardVfx Create(Transform root, KarmaGame game, KarmaEnemy owner, SpriteRenderer[] blades)
        {
            var go = new GameObject("SW04 single animated sword barrier / no damage"); go.transform.SetParent(root,false);
            var fx = go.AddComponent<KarmaSwordWardVfx>(); fx.game=game;fx.owner=owner;fx.blades=blades;
            for(int i=0;i<16;i++) if(frames[i]==null)
                frames[i]=KarmaArtwork.Sprite("SwordBossVFX","SW04_OrbitingSwordGuard_16f_"+i);
            if(frames[0]==null) throw new System.InvalidOperationException("Missing SW04 guard formation");
            fx.formation=fx.Sprite("SW04 baked four-sword formation",18);
            fx.absorbed=fx.Sprite("SW04 absorption ring only",33);
            fx.absorbed.sprite=KarmaArtwork.Sprite("SwordBossVFX","SW04_OrbitingSwordGuard_Base");
            fx.properties=new MaterialPropertyBlock();fx.pulseProperties=new MaterialPropertyBlock();
            fx.outer=fx.Line("SW04 slow outer runic ring",64,0.045f,17);
            fx.inner=fx.Line("SW04 counter-rotating inner glyphs",64,0.025f,17);
            fx.focus=fx.Line("SW04 committed counter slash warning",12,0.07f,34);
            for(int i=0;i<4;i++)
            {
                fx.arcs[i]=fx.Line("SW04 inter-sword lightning "+i,12,0.045f,22);
                fx.trails[i]=fx.Line("SW04 short rotational cutting trail "+i,12,0.095f,20);
            }
            fx.Paint();return fx;
        }
        SpriteRenderer Sprite(string name,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var sr=go.AddComponent<SpriteRenderer>();sr.sharedMaterial=Resources.Load<Material>("SwordWave/SW05/SW05Celestial");sr.sortingOrder=order;
            return sr;
        }
        LineRenderer Line(string name,int points,float width,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=KarmaCombatVFX.GlowMaterial;
            line.useWorldSpace=true;line.positionCount=points;line.widthMultiplier=width;
            line.sortingOrder=order;line.numCapVertices=0;line.numCornerVertices=0;
            var gradient=new Gradient();gradient.SetKeys(new[] {new GradientColorKey(new Color(0.1f,0.65f,1.1f),0),new GradientColorKey(new Color(0.85f,1.1f,1.3f),0.5f),new GradientColorKey(new Color(0.1f,0.65f,1.1f),1)},
                new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(0.9f,0.15f),new GradientAlphaKey(0.9f,0.75f),new GradientAlphaKey(0,1)});
            line.colorGradient=gradient;return line;
        }
        static Vector2 Direction(float angle)=>new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        Vector2 Center=>owner.Position+Vector2.up*2.3f;
        void Frame(SpriteRenderer sr,Sprite sprite,MaterialPropertyBlock block,float opacity,bool ringOnly)
        {
            sr.sprite=sprite;sr.color=new Color(0.78f,0.94f,1,opacity);
            sr.transform.position=Center;sr.transform.localScale=Vector3.one*(9f/1.8f);
            var rect=sprite.textureRect;var tex=sprite.texture;
            block.SetFloat("_Mode",8);block.SetFloat("_PackStyle",ringOnly?1:0);
            block.SetVector("_FrameUV",new Vector4(rect.x/tex.width,rect.y/tex.height,rect.width/tex.width,rect.height/tex.height));
            block.SetVector("_ClipRect",new Vector4(-10000,0,10000,10000));sr.SetPropertyBlock(block);
        }
        public void Absorb()
        {
            absorbedAt=age;countering=true;formation.enabled=false;
            foreach(var blade in blades)if(blade!=null)blade.enabled=true;
            KarmaSW05Energy.WardBurst(transform,game,Center,Vector2.up,4.2f,1);
        }
        public void Aim(int index,Vector2 origin,Vector2 axis,Vector2 size,bool committed)
        {aiming=index;aimOrigin=origin;aimAxis=axis;aimSize=size;locked=committed;}
        public void Released(Vector2 origin,Vector2 axis)
        {
            aiming=-1;focus.enabled=false;
            KarmaSW05Energy.WardBurst(transform,game,origin,axis,4.5f,0);
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
            // The break survives the slot cancellation and existing stun. It
            // belongs to the world for only 0.3 s and never creates a contact.
            KarmaSW05Energy.WardBurst(game.World,game,Center,Vector2.up,4.5f,2);
        }
        void Ring(LineRenderer line,Vector2 center,float radius,float turn,float squash)
        {
            for(int i=0;i<line.positionCount;i++)
            {
                float a=turn+i*Mathf.PI*2/(line.positionCount-1);
                float teeth=1+(i%8==0?0.055f:0);
                line.SetPosition(i,center+Vector2.Scale(Direction(a),new Vector2(radius*teeth,radius*squash*teeth)));
            }
        }
        void Paint()
        {
            FrameIndex=(int)(age*28)%16;
            formation.enabled=!countering;Frame(formation,frames[FrameIndex],properties,1,false);
            float hit=1-Mathf.Clamp01((age-absorbedAt)/0.22f);
            absorbed.enabled=hit>0;Frame(absorbed,absorbed.sprite,pulseProperties,hit,true);
            Vector2 center=Center;
            // The authored strip covers one clockwise quarter turn, not a
            // complete revolution. Four-fold symmetry closes that loop.
            float orbitAngle=-Mathf.Floor(age*28)*Mathf.PI/32-0.02f;
            Ring(outer,center,4.65f,age*0.7f,1);
            Ring(inner,center,3.75f,-age*1.2f,0.85f);
            for(int i=0;i<4;i++)
            {
                var blade=blades[i];
                positions[i]=countering&&blade!=null ? blade.transform.position
                    : (Vector3)(center+Direction(orbitAngle+i*Mathf.PI/2)*3.8f);
                bool visible=!countering||blade!=null&&blade.gameObject.activeInHierarchy;
                trails[i].enabled=arcs[i].enabled=visible;
                if(!visible)continue;
                for(int p=0;p<12;p++)
                {
                    float t=p/11f;
                    if(countering)
                    {
                        Vector2 delta=Vector2.ClampMagnitude((Vector2)(previous[i]-positions[i]),1.5f);
                        path[p]=positions[i]+(Vector3)(delta*t);
                    }
                    else path[p]=center+Direction(orbitAngle+i*Mathf.PI/2+t*0.32f)*(3.8f-t*0.15f);
                }
                trails[i].SetPositions(path);
                Vector2 from=positions[i],to=positions[(i+1)%4];
                // Resolve the next endpoint explicitly; no dependence on loop order.
                var next=blades[(i+1)%4];
                to=countering&&next!=null?(Vector2)next.transform.position:center+Direction(orbitAngle+(i+1)*Mathf.PI/2)*3.8f;
                Vector2 normal=new Vector2(-(to-from).y,(to-from).x).normalized;
                for(int p=0;p<12;p++)
                {
                    float t=p/11f;
                    float jag=p==0||p==11?0:Mathf.Sin(p*4.7f+i*9.3f+Mathf.Floor(age*42)*1.9f)*0.23f;
                    path[p]=Vector2.Lerp(from,to,t)+normal*jag;
                }
                arcs[i].SetPositions(path);previous[i]=positions[i];
            }
            focus.enabled=aiming>=0;
            if(aiming>=0)
            {
                var n=new Vector2(-aimAxis.y,aimAxis.x);
                for(int p=0;p<12;p++)
                {
                    float t=p/11f;
                    path[p]=aimOrigin+aimAxis*(aimSize.x*(0.1f+t*0.5f))
                        +n*(Mathf.Sin(t*Mathf.PI*4+age*32)*(locked?0.06f:0.16f));
                }
                focus.SetPositions(path);focus.widthMultiplier=locked?0.13f:0.06f;
            }
        }
        void LateUpdate()
        {
            if(game==null||owner==null||!owner.PresentationActive||owner.Stunned){Destroy(gameObject);return;}
            if(game.Paused)return;age+=Time.deltaTime;Paint();
        }
    }
}
