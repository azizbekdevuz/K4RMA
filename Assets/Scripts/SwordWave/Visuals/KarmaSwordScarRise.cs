using UnityEngine;

namespace KarmaPrototype
{
    // Static SW03 sword uses a narrower visual fit and the continuous floor emergence.
    // Frame geometry is installed before the shared attack's damage Update.
    [DefaultExecutionOrder(-50)]
    public sealed class KarmaSwordScarRise : MonoBehaviour
    {
        enum ScarSequence { Warning, Blade, Impact }
        public const float RiseSeconds = 0.14f, DissipationSeconds = 0.22f;
        public const float AuraDelay = 0.045f, BladeFadeSeconds = 0.22f, AuraFadeSeconds = 0.30f;
        public const float VisualWidthScale = 0.76f;
        // SW05-family silver blade, measured alpha>=128 bounds at 1024x1536.
        // Keep the crossguard at the old ground socket and preserve display width.
        public static readonly Rect BladePixels = new Rect(156,155,713,1362);
        public static readonly Vector2 FloorPixel = new Vector2(512,256);
        public static float VisualEmbedDepth(float height)
        { return (FloorPixel.y - BladePixels.yMin) * height / BladePixels.height; }
        public static Vector2 VisualSize(Vector2 contactSize)
        { return new Vector2(contactSize.y*(704f/1354)*VisualWidthScale,contactSize.y); }
        public static float BladeHalfWidth(float y)
        {
            // Half-widths measured from the clean sword alpha, in BladePixels.
            // This profile drives only the separate aura/mesh, never contacts.
            y=Mathf.Clamp01(y);
            return y<0.05f?Mathf.Lerp(0,0.066f,y/0.05f)
                :y<0.065f?Mathf.Lerp(0.066f,0.366f,(y-0.05f)/0.015f)
                :y<0.092f?Mathf.Lerp(0.366f,0.497f,(y-0.065f)/0.027f)
                :y<0.10f?Mathf.Lerp(0.497f,0.289f,(y-0.092f)/0.008f)
                :y<0.12f?Mathf.Lerp(0.289f,0.217f,(y-0.10f)/0.02f)
                :y<0.16f?Mathf.Lerp(0.217f,0.149f,(y-0.12f)/0.04f)
                :y<0.20f?Mathf.Lerp(0.149f,0.196f,(y-0.16f)/0.04f)
                :y<0.35f?Mathf.Lerp(0.196f,0.142f,(y-0.20f)/0.15f)
                :y<0.50f?Mathf.Lerp(0.142f,0.139f,(y-0.35f)/0.15f)
                :y<0.56f?Mathf.Lerp(0.139f,0.154f,(y-0.50f)/0.06f)
                :y<0.65f?Mathf.Lerp(0.154f,0.135f,(y-0.56f)/0.09f)
                :y<0.82f?Mathf.Lerp(0.135f,0.121f,(y-0.65f)/0.17f)
                :Mathf.Lerp(0.121f,0,(y-0.82f)/0.18f);
        }
        static readonly string[] Sheets = { "SW03_GroundScar_5f", "SW03_RisingBlade_Tall_5f", "SW03_HitCut_5f" };
        static readonly Sprite[,] frames = new Sprite[3, 5];
        // Alpha >= 128 pixel rectangles, in Unity's bottom-left coordinates.
        static readonly Rect[] BladeCore = {
            new Rect(116, 48, 172, 293), new Rect(126, 48, 170, 493),
            new Rect(131, 48, 172, 581), new Rect(142, 47, 174, 627), new Rect(144, 48, 176, 628)
        };
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer picture;
        KarmaSwordBossAttack attack;
        ScarSequence sequence;
        Vector2 footprint, ground, bladeFoot, bladeVisualSize;
        Transform scope;
        float age, life;
        KarmaSwordScarClock roundClock;
        bool flipped, ended;

        KarmaSW05Energy electricalBlade, electricalWarning;
        KarmaSwordScarAura aura;
        MaterialPropertyBlock bladeProperties;
        public int FrameIndex { get; private set; }
        // A visual seed is tied to the fixed sword origin, never spawn order
        // or UnityEngine.Random. The same site keeps its identity across rounds.
        public static int EnergySeed(float x)
        {
            unchecked
            {
                uint value=(uint)Mathf.RoundToInt(x*1024)+0x9e3779b9u;
                value=(value^(value>>16))*0x7feb352du;
                value=(value^(value>>15))*0x846ca68bu;
                return (int)((value^(value>>16))&0x7fffffffu);
            }
        }
        public static Sprite Frame(int sheet, int frame)
        {
            if (frames[sheet, frame] == null)
                frames[sheet, frame] = KarmaArtwork.Sprite("SwordBossVFX", Sheets[sheet] + "_" + frame);
            if (frames[sheet, frame] == null) throw new System.InvalidOperationException("Missing SW03 frame " + Sheets[sheet] + "/" + frame);
            return frames[sheet, frame];
        }
        void Setup(KarmaGame director, KarmaEnemy enemy, ScarSequence clip, Vector2 at, Vector2 size, float seconds)
        {
            game = director; owner = enemy; sequence = clip; ground = at; footprint = size; life = seconds;
            picture = GetComponent<SpriteRenderer>(); KarmaVisuals.ApplyMaterial(picture); picture.color = Color.white;
            if (clip == ScarSequence.Blade)
            {
                picture.sharedMaterial = KarmaArtResource.Load<Material>("SwordWave/SW03/KarmaSwordRisePolish");
                bladeProperties = new MaterialPropertyBlock();
                picture.sprite=KarmaArtwork.Sprite("SwordBossVFX","SW03_RisingBlade_v7");
                picture.flipX=false;picture.flipY=false;
                float ppu=picture.sprite.pixelsPerUnit;
                bladeVisualSize=VisualSize(footprint);
                float fit=footprint.y*ppu/BladePixels.height,fitX=bladeVisualSize.x*ppu/BladePixels.width;
                transform.localScale=new Vector3(fitX, fit, 1);transform.rotation=Quaternion.identity;
                bladeFoot=Vector2.Scale((FloorPixel-picture.sprite.pivot)/ppu,new Vector2(fitX, fit));
                var uv=picture.sprite.textureRect;var tex=picture.sprite.texture;
                bladeProperties.SetVector("_FrameUV",new Vector4(uv.x/tex.width,uv.y/tex.height,uv.width/tex.width,uv.height/tex.height));
                bladeProperties.SetVector("_BladeRect",new Vector4(BladePixels.x/picture.sprite.rect.width,BladePixels.y/picture.sprite.rect.height,BladePixels.width/picture.sprite.rect.width,BladePixels.height/picture.sprite.rect.height));
                bladeProperties.SetFloat("_AuraSeed",(EnergySeed(ground.x)&65535)*(83.1f/65536));
                bladeProperties.SetFloat("_Fade",1);bladeProperties.SetFloat("_FloorY",ground.y);
                bladeProperties.SetFloat("_EnergyLayer",0);
            }
            if (clip != ScarSequence.Blade)
                for (int f = 0; f < 5; f++) Frame((int)clip, f);
        }
        public static KarmaSwordScarRise Warning(KarmaGame game, KarmaEnemy owner, Transform root, float x, float width, float seconds, string name)
        {
            var node = new GameObject(name); node.transform.SetParent(root, false);
            node.AddComponent<SpriteRenderer>().sortingOrder = 9;
            var cue = node.AddComponent<KarmaSwordScarRise>();
            cue.Setup(game, owner, ScarSequence.Warning, new Vector2(x, 0), new Vector2(width, 1.1f), seconds);
            cue.electricalWarning = KarmaSW05Energy.ScarWarning(root,game,new Vector2(x,0),width,seconds);
            cue.SetWarningProgress(0); return cue;
        }
        public void SetWarningProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            // Sample the charge at the artwork clock; retain the committed footprint.
            float sample = Mathf.Floor(progress * life * KarmaBootstrap.FramesPerSecond) * KarmaBootstrap.Step;
            FrameIndex = Mathf.Min(4, (int)(sample / life * 5)); Paint(FrameIndex);
            float charge = Mathf.SmoothStep(0, 1, progress);
            float imminent = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.78f, 1, progress));
            picture.color = new Color(0.72f + charge * 0.28f, 0.88f + charge * 0.12f, 1,
                0.42f + charge * 0.30f + imminent * 0.28f);
            if (electricalWarning != null) electricalWarning.SetCharge(progress);
        }
        public void Initialize(KarmaGame director, KarmaEnemy enemy, KarmaBossAttack shot, Vector2 size, float seconds, bool mirror = false, KarmaSwordScarClock clock = null)
        {
            roundClock = clock;
            attack = (KarmaSwordBossAttack)shot; scope = shot.transform.parent;
            Setup(director, enemy, ScarSequence.Blade, new Vector2(shot.transform.position.x, 0), size, seconds);
            // Narrow only cosmetic ground discharge/fragments, never the contact footprint.
            var energySize = new Vector2(size.x * VisualWidthScale, size.y);
            electricalBlade = KarmaSW05Energy.Scar(scope,game,shot.transform,new Vector2(shot.transform.position.x,0),energySize,seconds);
            flipped = mirror; Paint(0);
            aura=KarmaSwordScarAura.Spawn(scope, director, enemy, picture, EnergySeed(shot.transform.position.x), footprint.y, roundClock);

        }
        static void Impact(KarmaGame game, KarmaEnemy owner, Transform root, Vector2 at, Vector2 size, string name)
        {
            var node = new GameObject(name); node.transform.SetParent(root, false);
            node.AddComponent<SpriteRenderer>().sortingOrder = 35;
            var effect = node.AddComponent<KarmaSwordScarRise>();
            effect.Setup(game, owner, ScarSequence.Impact, at, size, DissipationSeconds); effect.Paint(0);
        }
        public static void ShowHit(KarmaGame game, Transform root, KarmaEnemy owner)
        {
            if (!game.IsCombat) return;
            Impact(game, owner, root, new Vector2(game.Player.Position.x, 0), new Vector2(3.8f, 2.8f), "SW03 confirmed cut / no damage");
        }
        void Update()
        {
            if (game == null || owner == null || !owner.PresentationActive || owner.Stunned)
            { ended = true; if (sequence != ScarSequence.Blade) Destroy(gameObject); return; }
            if (game.Paused || sequence == ScarSequence.Warning) return;
            age = roundClock != null ? roundClock.Sample(Time.frameCount, Time.deltaTime, game.Paused) : age + Time.deltaTime;
            if (sequence == ScarSequence.Impact && age >= life) { Destroy(gameObject); return; }
            // Rise and contact stages share the same short emergence clock.
            FrameIndex = sequence == ScarSequence.Blade
                ? Mathf.Min(4, (int)(age / RiseSeconds * 4)) : Mathf.Min(4, 2 * (int)(age * KarmaBootstrap.FramesPerSecond));
            Paint(FrameIndex);
            if (sequence == ScarSequence.Impact)
                picture.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, (age - 0.12f) / (life - 0.12f)));
        }
        public static float VisualRiseHeight(float seconds,float height)
        {
            // The tip starts at the floor, accelerates, then seats quickly.
            // Retain the final static fit/socket instead of scaling the sword.
            return Mathf.Lerp(VisualEmbedDepth(height),height,Mathf.SmoothStep(0,1,seconds/RiseSeconds));
        }
        public static float VisibleTipHeight(float seconds,float height)
        { return Mathf.Max(0,VisualRiseHeight(seconds,height)-VisualEmbedDepth(height)); }
        public static float RetractionDistance(float seconds,float height)
        { return height*Mathf.SmoothStep(0,1,seconds/BladeFadeSeconds); }
        void Paint(int frame)
        {
            if (sequence == ScarSequence.Blade)
            {
                var core = BladeCore[frame];
                // Retain the original width stages; damage cannot outrun the visible tip.
                // One static sprite translates through the floor; its fit never changes during the rise.
                var damageSize = new Vector2(core.width / BladeCore[4].width * footprint.x,
                    Mathf.Min(core.height / BladeCore[4].height * footprint.y,VisibleTipHeight(age,footprint.y)));
                attack.SetSW03Frame(ground + Vector2.up * damageSize.y / 2, damageSize);
                float visibleHeight=VisualRiseHeight(age,footprint.y);
                if(electricalBlade!=null)electricalBlade.SetScarBounds(ground,bladeVisualSize,visibleHeight);
                transform.position=ground-bladeFoot-Vector2.up*(footprint.y-visibleHeight);
                // A single release highlight, rather than a repeating scan bar.
                bladeProperties.SetFloat("_RiseProgress",age*4.5f-0.15f);
                bladeProperties.SetFloat("_AuraTime",age);
                picture.SetPropertyBlock(bladeProperties);
            }
            else
            {
                picture.sprite=Frame((int)sequence,frame);picture.flipX=flipped;picture.flipY=false;
                float ppu=picture.sprite.pixelsPerUnit;
                Vector2 pixels=sequence==ScarSequence.Warning?new Vector2(262,106):new Vector2(276,159);
                transform.localScale=new Vector3(footprint.x*ppu/pixels.x,footprint.y*ppu/pixels.y,1);
                transform.SetPositionAndRotation(ground,Quaternion.identity);
            }
        }
        void OnDisable()
        {
            if (sequence != ScarSequence.Blade || ended || game == null || owner == null || !owner.PresentationActive || owner.Stunned
                || scope == null || !scope.gameObject.activeInHierarchy || age < life - Time.deltaTime * 1.5f) return;
            ended = true;
            if(aura!=null)aura.Release();
            // Transfer the expired blade to a contact-free cosmetic fade.
            var tail = new GameObject("SW03 residual blade / no damage");
            tail.transform.SetParent(scope, false);
            tail.transform.SetPositionAndRotation(transform.position, transform.rotation);
            tail.transform.localScale = transform.lossyScale;
            var residual = tail.AddComponent<SpriteRenderer>();
            residual.sprite = picture.sprite; residual.sharedMaterial = picture.sharedMaterial;
            residual.sortingOrder = picture.sortingOrder;
            residual.SetPropertyBlock(bladeProperties);
            tail.AddComponent<KarmaSwordScarFade>().Initialize(game, owner, residual, footprint.y);
            KarmaSW05Energy.ScarDissolve(scope,game,ground,VisualSize(footprint));
        }
    }

    // A sibling renderer has its own clock, transform and lifetime. It never
    // inherits the blade scale or owns a collider, contact or attack component.
    public sealed class KarmaSwordScarAura : MonoBehaviour
    {
        [Range(0,2)] public float Intensity = 1;
        KarmaGame game; KarmaEnemy owner; SpriteRenderer source, picture;
        MaterialPropertyBlock properties; Vector3 anchor, scale;
        float age, tailAge, noiseSeed, glowGain, bladeHeight; bool released; int releaseFrame=-1;
        KarmaSwordScarClock roundClock;
        public static KarmaSwordScarAura Spawn(Transform root, KarmaGame game, KarmaEnemy owner, SpriteRenderer source, int seed, float height, KarmaSwordScarClock clock = null)
        {
            var node = new GameObject("SW03 independent aura / no damage");
            node.transform.SetParent(root, false);
            var fx = node.AddComponent<KarmaSwordScarAura>();
            fx.game=game; fx.owner=owner; fx.source=source; fx.bladeHeight=height;
            fx.roundClock=clock;
            fx.picture=node.AddComponent<SpriteRenderer>();
            fx.picture.sprite=source.sprite; fx.picture.sharedMaterial=source.sharedMaterial;
            fx.picture.sortingOrder=source.sortingOrder-1;
            fx.properties=new MaterialPropertyBlock(); source.GetPropertyBlock(fx.properties);
            fx.properties.SetFloat("_EnergyLayer",1); fx.properties.SetFloat("_Fade",0);
            fx.anchor=source.transform.position; fx.scale=source.transform.lossyScale;
            fx.noiseSeed=(seed&0x00ffffff)*(83.1f/16777216);
            fx.glowGain=0.92f+(seed&255)*(0.14f/255);
            fx.properties.SetFloat("_AuraTime",0); fx.properties.SetFloat("_AuraSeed",fx.noiseSeed);
            fx.picture.SetPropertyBlock(fx.properties);
            node.transform.SetPositionAndRotation(fx.anchor,Quaternion.identity); node.transform.localScale=fx.scale;
            return fx;
        }
        public void Release()
        {
            if(released)return;
            if(source!=null){anchor=source.transform.position;scale=source.transform.lossyScale;}
            released=true;releaseFrame=Time.frameCount;
        }
        void LateUpdate()
        {
            if(game==null || owner==null || !owner.PresentationActive || owner.Stunned) {Destroy(gameObject);return;}
            if(game.Paused)return;
            age=roundClock!=null?roundClock.Sample(Time.frameCount,Time.deltaTime,game.Paused):age+Time.deltaTime;
            if(!released && source!=null && source.gameObject.activeInHierarchy)
            {anchor=source.transform.position;scale=source.transform.lossyScale;}
            else if(!released)Release();
            if(released && Time.frameCount!=releaseFrame)tailAge+=Time.deltaTime;
            if(tailAge>=KarmaSwordScarRise.AuraFadeSeconds){Destroy(gameObject);return;}
            float ignition=Mathf.SmoothStep(0,1,(age-KarmaSwordScarRise.AuraDelay)/0.07f);
            float fade=1-tailAge/KarmaSwordScarRise.AuraFadeSeconds;
            // Irregular luminance only: the entire layer stays locked to the
            // sword. Upward flow and edge turbulence happen inside the shader.
            float pulse=0.82f+0.18f*Mathf.PerlinNoise(noiseSeed,age*19);
            properties.SetFloat("_Fade",ignition*fade*fade*pulse*0.8f*glowGain*Mathf.Max(0,Intensity));
            properties.SetFloat("_AuraTime",Mathf.Max(0,age-KarmaSwordScarRise.AuraDelay));
            picture.SetPropertyBlock(properties);
            transform.SetPositionAndRotation(anchor-Vector3.up*KarmaSwordScarRise.RetractionDistance(tailAge,bladeHeight),Quaternion.identity);
            transform.localScale=scale;
        }
    }

    public sealed class KarmaSwordScarFade : MonoBehaviour
    {
        KarmaGame game; KarmaEnemy owner; SpriteRenderer picture;
        MaterialPropertyBlock properties;
        float age, bladeHeight; Vector3 anchor;
        public void Initialize(KarmaGame director, KarmaEnemy enemy, SpriteRenderer source, float height)
        { game=director; owner=enemy; picture=source; bladeHeight=height; anchor=transform.position;
            properties=new MaterialPropertyBlock(); picture.GetPropertyBlock(properties); }
        void Update()
        {
            if(game==null || owner==null || !owner.PresentationActive || owner.Stunned)
            { Destroy(gameObject); return; }
            if(game.Paused)return;
            age+=Time.deltaTime;
            if(age>=KarmaSwordScarRise.BladeFadeSeconds){Destroy(gameObject);return;}
            // The spent blade sinks intact through the shader's fixed floor.
            // Dissolve only the final tip; the damage owner has already expired.
            transform.position=anchor-Vector3.up*KarmaSwordScarRise.RetractionDistance(age,bladeHeight);
            properties.SetFloat("_Fade",1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.65f,1,age/KarmaSwordScarRise.BladeFadeSeconds)));
            picture.SetPropertyBlock(properties);
        }
    }

}
