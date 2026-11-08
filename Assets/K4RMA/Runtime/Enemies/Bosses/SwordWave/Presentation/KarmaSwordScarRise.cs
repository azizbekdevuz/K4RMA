using UnityEngine;

namespace KarmaPrototype
{
    // SW03 sheets retain their imported pivots; the visible sword foot is grounded.
    // Frame geometry is installed before the shared attack's damage Update.
    [DefaultExecutionOrder(-50)]
    public sealed class KarmaSwordScarRise : MonoBehaviour
    {
        enum ScarSequence { Warning, Blade, Impact }
        public const float RiseSeconds = 0.10f, DissipationSeconds = 0.22f;
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
        KarmaSwordPackedVfx groundEnergy;
        KarmaSW05Energy electricalBlade, electricalWarning;
        MaterialPropertyBlock bladeProperties;
        public int FrameIndex { get; private set; }
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
            for (int s = 0; s < 3; s++) for (int f = 0; f < 5; f++) Frame(s, f);
        }
        public static KarmaSwordScarRise Warning(KarmaGame game, KarmaEnemy owner, Transform root, float x, float width, float seconds, string name)
        {
            var node = new GameObject(name); node.transform.SetParent(root, false);
            node.AddComponent<SpriteRenderer>().sortingOrder = 9;
            var cue = node.AddComponent<KarmaSwordScarRise>();
            cue.Setup(game, owner, ScarSequence.Warning, new Vector2(x, 0), new Vector2(width, 1.1f), seconds);
            cue.groundEnergy = KarmaSwordPackedVfx.Spawn(root,game,KarmaSwordPackedVfx.Clip.GroundCharge,
                new Vector2(x,0),new Vector2(width,1.25f),seconds,10,0.8f,true);
            cue.groundEnergy.SetBounds(new Rect(x-width/2,0,width,1.25f));
            cue.electricalWarning = KarmaSW05Energy.ScarWarning(root,game,new Vector2(x,0),width,seconds);
            cue.SetWarningProgress(0); return cue;
        }
        public void SetWarningProgress(float progress)
        {
            FrameIndex = Mathf.Min(4, (int)(Mathf.Clamp01(progress) * 5)); Paint(FrameIndex);
            if (groundEnergy != null) groundEnergy.SetProgress(progress);
            if (electricalWarning != null) electricalWarning.SetCharge(progress);
        }
        public void Initialize(KarmaGame director, KarmaEnemy enemy, KarmaBossAttack shot, Vector2 size, float seconds, bool mirror = false)
        {
            attack = (KarmaSwordBossAttack)shot; scope = shot.transform.parent;
            Setup(director, enemy, ScarSequence.Blade, new Vector2(shot.transform.position.x, 0), size, seconds);
            electricalBlade = KarmaSW05Energy.Scar(scope,game,shot.transform,new Vector2(shot.transform.position.x,0),size,seconds);
            flipped = mirror; Paint(0);
            var surge = KarmaSwordPackedVfx.Spawn(scope,game,KarmaSwordPackedVfx.Clip.BladeSurge,ground,
                new Vector2(size.x,size.y*0.5f),0.16f,31,0.6f);
            surge.SetBounds(new Rect(ground.x-size.x/2,0,size.x,size.y));
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
            FrameIndex = Mathf.Min(4, (int)(age / (sequence == ScarSequence.Blade ? RiseSeconds : life) * 5));
            Paint(FrameIndex);
        }
        void Paint(int frame)
        {
            picture.sprite = Frame((int)sequence, frame); picture.flipX = flipped; picture.flipY = false;
            float ppu = picture.sprite.pixelsPerUnit;
            // Ignore transparent canvas padding when fitting the visible art.
            Vector2 pixels = sequence == ScarSequence.Blade ? BladeCore[4].size
                : sequence == ScarSequence.Warning ? new Vector2(262, 106) : new Vector2(276, 159);
            var scale = new Vector3(footprint.x * ppu / pixels.x, footprint.y * ppu / pixels.y, 1);
            transform.localScale = scale; transform.rotation = Quaternion.identity;
            if (sequence == ScarSequence.Blade)
            {
                var core = BladeCore[frame];
                Vector2 foot = (new Vector2(core.center.x, core.yMin) - picture.sprite.pivot) / ppu;
                foot = Vector2.Scale(foot, scale); if (flipped) foot.x = -foot.x;
                // The normal shared attack performs contacts and lifetime, using
                // this frame's visible region. At full height the paired swords
                // fill the warned cell exactly, with no hidden gaps between them.
                var damageSize = new Vector2(core.width * scale.x / ppu, core.height * scale.y / ppu);
                attack.SetSW03Frame(ground + Vector2.up * damageSize.y / 2, damageSize);
                if (electricalBlade != null) electricalBlade.SetScarBounds(ground,damageSize);
                var uv = picture.sprite.textureRect; var tex = picture.sprite.texture;
                bladeProperties.SetVector("_FrameUV",new Vector4(uv.x/tex.width,uv.y/tex.height,uv.width/tex.width,uv.height/tex.height));
                bladeProperties.SetVector("_BladeRect",new Vector4(core.x/uv.width,core.y/uv.height,core.width/uv.width,core.height/uv.height));
                bladeProperties.SetFloat("_RiseProgress",Mathf.Repeat(age*4.5f,1.25f));
                picture.SetPropertyBlock(bladeProperties);
                transform.position = ground - foot;
            }
            else transform.position = ground;
        }
        void OnDisable()
        {
            if (sequence != ScarSequence.Blade || ended || game == null || owner == null || !owner.PresentationActive || owner.Stunned
                || scope == null || !scope.gameObject.activeInHierarchy || age < life - Time.deltaTime * 1.5f) return;
            ended = true;
            KarmaSW05Energy.ScarDissolve(scope,game,ground,footprint);
            Impact(game, owner, scope, ground, new Vector2(footprint.x * 1.15f, footprint.y * 0.32f), "SW03 blade dissipation / no damage");
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
