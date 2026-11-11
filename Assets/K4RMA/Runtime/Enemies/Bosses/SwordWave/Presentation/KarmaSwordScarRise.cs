using UnityEngine;

namespace KarmaPrototype
{
    // Static SW03 sword uses a narrower visual fit and the original vertical rise.
    // Frame geometry is installed before the shared attack's damage Update.
    [DefaultExecutionOrder(-50)]
    public sealed class KarmaSwordScarRise : MonoBehaviour
    {
        enum ScarSequence { Warning, Blade, Impact }
        public const float RiseSeconds = 0.10f, DissipationSeconds = 0.22f;
        public const float AuraDelay = 0.045f, BladeFadeSeconds = 0.12f, AuraFadeSeconds = 0.30f;
        public const float VisualWidthScale = 0.76f;
        // Selected 1024x1536 artwork, Unity bottom-left pixels: y=160 is
        // the central lower point, not the crossbar. The opaque crossbar spans
        // y=247..277 at x=256/768; y=256 embeds its underside in the floor.
        // Keep the original fit/profile rectangle so this is translation only.
        public static readonly Rect BladePixels = new Rect(160,160,704,1354);
        public static readonly Vector2 FloorPixel = new Vector2(512,256);
        public static float VisualEmbedDepth(float height)
        { return (FloorPixel.y - BladePixels.yMin) * height / BladePixels.height; }
        public static Vector2 VisualSize(Vector2 contactSize)
        { return new Vector2(contactSize.y*BladePixels.width/BladePixels.height*VisualWidthScale,contactSize.y); }
        public static float BladeHalfWidth(float y)
        {
            y=Mathf.Clamp01(y);
            return y<0.05f?Mathf.Lerp(0.02f,0.50f,y/0.05f)
                :y<0.14f?Mathf.Lerp(0.50f,0.20f,(y-0.05f)/0.09f)
                :y<0.25f?Mathf.Lerp(0.20f,0.24f,(y-0.14f)/0.11f)
                :y<0.50f?Mathf.Lerp(0.24f,0.20f,(y-0.25f)/0.25f)
                :y<0.70f?Mathf.Lerp(0.20f,0.25f,(y-0.50f)/0.20f)
                :Mathf.Lerp(0.25f,0,(y-0.70f)/0.30f);
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
        Vector2 footprint, ground;
        Transform scope;
        float age, life;
        bool flipped, ended;

        KarmaSW05Energy electricalBlade, electricalWarning;
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
                picture.sharedMaterial = Resources.Load<Material>("KarmaSwordRisePolish");
                bladeProperties = new MaterialPropertyBlock();
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
            float sample = Mathf.Floor(progress * life * 24) / 24;
            FrameIndex = Mathf.Min(4, (int)(sample / life * 5)); Paint(FrameIndex);
            float charge = Mathf.SmoothStep(0, 1, progress);
            picture.color = new Color(0.72f + charge * 0.28f, 0.88f + charge * 0.12f, 1,
                Mathf.Lerp(0.42f, 1, charge));
            if (electricalWarning != null) electricalWarning.SetCharge(progress);
        }
        public void Initialize(KarmaGame director, KarmaEnemy enemy, KarmaBossAttack shot, Vector2 size, float seconds, bool mirror = false)
        {
            attack = (KarmaSwordBossAttack)shot; scope = shot.transform.parent;
            Setup(director, enemy, ScarSequence.Blade, new Vector2(shot.transform.position.x, 0), size, seconds);
            // Narrow only cosmetic ground discharge/fragments, never the contact footprint.
            var energySize = new Vector2(size.x * VisualWidthScale, size.y);
            electricalBlade = KarmaSW05Energy.Scar(scope,game,shot.transform,new Vector2(shot.transform.position.x,0),energySize,seconds);
            flipped = mirror; Paint(0);
            KarmaSwordScarAura.Spawn(scope, director, enemy, picture, EnergySeed(shot.transform.position.x));

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
            age += Time.deltaTime;
            if (sequence == ScarSequence.Impact && age >= life) { Destroy(gameObject); return; }
            // Contact stages retain their original clock. Only sprite clips run at 24 FPS.
            FrameIndex = sequence == ScarSequence.Blade
                ? Mathf.Min(4, (int)(age / RiseSeconds * 5)) : Mathf.Min(4, (int)(age * 24));
            Paint(FrameIndex);
            if (sequence == ScarSequence.Impact)
                picture.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, (age - 0.12f) / (life - 0.12f)));
        }
        public static float VisualRiseHeight(float seconds,float height)
        {
            // Interpolate only the display between the original five contact
            // stages. Contacts still use FrameIndex and the original rectangles.
            float phase=Mathf.Clamp(seconds/RiseSeconds*5,0,4);
            int lower=(int)phase,upper=Mathf.Min(lower+1,4);
            return height*Mathf.Lerp(BladeCore[lower].height,BladeCore[upper].height,phase-lower)/BladeCore[4].height;
        }
        void Paint(int frame)
        {
            picture.sprite = sequence == ScarSequence.Blade ? KarmaArtwork.Sprite("SwordBossVFX", "SW03_RisingBlade_v7") : Frame((int)sequence, frame); picture.flipX = sequence != ScarSequence.Blade && flipped; picture.flipY = false;
            float ppu = picture.sprite.pixelsPerUnit;
            // Ignore transparent canvas padding when fitting the visible art.
            Vector2 pixels = sequence == ScarSequence.Blade ? BladeCore[4].size
                : sequence == ScarSequence.Warning ? new Vector2(262, 106) : new Vector2(276, 159);
            var scale = new Vector3(footprint.x * ppu / pixels.x, footprint.y * ppu / pixels.y, 1);
            transform.localScale = scale; transform.rotation = Quaternion.identity;
            if (sequence == ScarSequence.Blade)
            {
                var core = BladeCore[frame];
                // Preserve the five original contact rectangles and their timing.
                // One static sprite translates through the floor; its fit never changes during the rise.
                var damageSize = new Vector2(core.width / BladeCore[4].width * footprint.x,
                    core.height / BladeCore[4].height * footprint.y);
                attack.SetSW03Frame(ground + Vector2.up * damageSize.y / 2, damageSize);
                float visibleHeight=VisualRiseHeight(age,footprint.y);
                if (electricalBlade != null) electricalBlade.SetScarBounds(ground,VisualSize(footprint),visibleHeight);
                float fit = footprint.y * ppu / BladePixels.height;
                // Fit this renderer only. The attack/root stays unscaled; the
                // sibling aura copies this fit, and mesh lightning uses VisualSize.
                transform.localScale = new Vector3(fit * VisualWidthScale, fit, 1);
                Vector2 foot = Vector2.Scale((FloorPixel - picture.sprite.pivot) / ppu,
                    new Vector2(fit * VisualWidthScale, fit));
                transform.position = ground - foot - Vector2.up * (footprint.y - visibleHeight);
                var uv = picture.sprite.textureRect; var tex = picture.sprite.texture;
                bladeProperties.SetVector("_FrameUV",new Vector4(uv.x/tex.width,uv.y/tex.height,uv.width/tex.width,uv.height/tex.height));
                bladeProperties.SetVector("_BladeRect",new Vector4(BladePixels.x/picture.sprite.rect.width,BladePixels.y/picture.sprite.rect.height,BladePixels.width/picture.sprite.rect.width,BladePixels.height/picture.sprite.rect.height));
                // A single release highlight, rather than a repeating scan bar.
                bladeProperties.SetFloat("_RiseProgress",age*4.5f-0.15f);
                bladeProperties.SetFloat("_AuraTime",age);
                bladeProperties.SetFloat("_AuraSeed",(EnergySeed(ground.x)&65535)*(83.1f/65536));
                bladeProperties.SetFloat("_Fade", 1);
                bladeProperties.SetFloat("_FloorY",ground.y);
                bladeProperties.SetFloat("_EnergyLayer", 0);
                picture.SetPropertyBlock(bladeProperties);
            }
            else transform.position = ground;
        }
        void OnDisable()
        {
            if (sequence != ScarSequence.Blade || ended || game == null || owner == null || !owner.PresentationActive || owner.Stunned
                || scope == null || !scope.gameObject.activeInHierarchy || age < life - Time.deltaTime * 1.5f) return;
            ended = true;
            // Transfer the expired blade to a contact-free cosmetic fade.
            var tail = new GameObject("SW03 residual blade / no damage");
            tail.transform.SetParent(scope, false);
            tail.transform.SetPositionAndRotation(transform.position, transform.rotation);
            tail.transform.localScale = transform.lossyScale;
            var residual = tail.AddComponent<SpriteRenderer>();
            residual.sprite = picture.sprite; residual.sharedMaterial = picture.sharedMaterial;
            residual.sortingOrder = picture.sortingOrder;
            residual.SetPropertyBlock(bladeProperties);
            tail.AddComponent<KarmaSwordScarFade>().Initialize(game, owner, residual);
            KarmaSW05Energy.ScarDissolve(scope,game,ground,VisualSize(footprint));
        }
    }

    // A sibling renderer has its own clock, transform and lifetime. It never
    // inherits the blade scale or owns a collider, contact or attack component.
    public sealed class KarmaSwordScarAura : MonoBehaviour
    {
        KarmaGame game; KarmaEnemy owner; SpriteRenderer source, picture;
        MaterialPropertyBlock properties; Vector3 anchor, scale;
        float age, tailAge, noiseSeed, glowGain; bool released;
        public static void Spawn(Transform root, KarmaGame game, KarmaEnemy owner, SpriteRenderer source, int seed)
        {
            var node = new GameObject("SW03 independent aura / no damage");
            node.transform.SetParent(root, false);
            var fx = node.AddComponent<KarmaSwordScarAura>();
            fx.game=game; fx.owner=owner; fx.source=source;
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
        }
        void LateUpdate()
        {
            if(game==null || owner==null || !owner.PresentationActive || owner.Stunned) {Destroy(gameObject);return;}
            if(game.Paused)return;
            age+=Time.deltaTime;
            if(source!=null && source.gameObject.activeInHierarchy)
            {anchor=source.transform.position;scale=source.transform.lossyScale;}
            else released=true;
            if(released)tailAge+=Time.deltaTime;
            if(tailAge>=KarmaSwordScarRise.AuraFadeSeconds){Destroy(gameObject);return;}
            float ignition=Mathf.SmoothStep(0,1,(age-KarmaSwordScarRise.AuraDelay)/0.07f);
            float fade=1-tailAge/KarmaSwordScarRise.AuraFadeSeconds;
            // Irregular luminance only: the entire layer stays locked to the
            // sword. Upward flow and edge turbulence happen inside the shader.
            float pulse=0.82f+0.18f*Mathf.PerlinNoise(noiseSeed,age*19);
            properties.SetFloat("_Fade",ignition*fade*fade*pulse*0.8f*glowGain);
            properties.SetFloat("_AuraTime",Mathf.Max(0,age-KarmaSwordScarRise.AuraDelay));
            picture.SetPropertyBlock(properties);
            transform.SetPositionAndRotation(anchor,Quaternion.identity);
            transform.localScale=scale;
        }
    }

    public sealed class KarmaSwordScarFade : MonoBehaviour
    {
        KarmaGame game; KarmaEnemy owner; SpriteRenderer picture;
        MaterialPropertyBlock properties;
        float age;
        public void Initialize(KarmaGame director, KarmaEnemy enemy, SpriteRenderer source)
        { game=director; owner=enemy; picture=source; properties=new MaterialPropertyBlock(); picture.GetPropertyBlock(properties); }
        void Update()
        {
            if(game==null || owner==null || !owner.PresentationActive || owner.Stunned)
            { Destroy(gameObject); return; }
            if(game.Paused)return;
            age+=Time.deltaTime;
            if(age>=KarmaSwordScarRise.BladeFadeSeconds){Destroy(gameObject);return;}
            properties.SetFloat("_Fade",1-Mathf.SmoothStep(0,1,age/KarmaSwordScarRise.BladeFadeSeconds));
            picture.SetPropertyBlock(properties);
        }
    }

    // Existing sword artwork swings from above the hand into the floor. The
    // actor image and incoming-damage silhouette are untouched.
    public sealed class KarmaSwordGroundSlam : MonoBehaviour
    {
        KarmaGame game;
        KarmaEnemy owner;
        SpriteRenderer blade;
        float age;
        public void Initialize(KarmaGame director, KarmaEnemy enemy)
        {
            game = director; owner = enemy;
            blade = KarmaVisuals.Box(transform, "SW03 downward blade", Vector2.zero, Vector2.one, Color.white, 30);
            KarmaWaveArtwork.Apply(blade, "SW04_CounterBlade_v6", new Vector2(4.4f, 0.42f), Vector2.zero);
            Paint();
        }
        void Paint()
        {
            float t = Mathf.Clamp01(age / 0.18f);
            float angle = Mathf.Lerp(78, -78, t * t);
            Vector2 axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad) * owner.Facing, Mathf.Sin(angle * Mathf.Deg2Rad));
            transform.position = owner.Position + new Vector2(owner.Facing * 0.65f, 0.1f) + axis * 1.6f;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg);
        }
        void Update()
        {
            if (owner == null || !owner.PresentationActive || owner.Stunned) { Destroy(gameObject); return; }
            if (game.Paused) return;
            age += Time.deltaTime; Paint();
            if (age >= 0.18f) Destroy(gameObject);
        }
    }
}
