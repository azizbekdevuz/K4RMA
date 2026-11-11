using UnityEngine;

namespace KarmaPrototype
{
    // Only SW03/SW05 use the supplied pack. All frames have a fixed authored
    // pivot and canvas scale; changing frames never resizes a damage collider.
    public sealed class KarmaSwordPackedVfx : MonoBehaviour
    {
        public enum Clip { GroundCharge, BladeSurge, FallingAura, LandingShock, Overcharge, Detonation }
        static readonly string[] Names = { "SW03_GroundCharge_6f", "SW03_BladeSurge_6f", "SW05_FallingSwordAura_6f",
            "SW05_LandingShock_6f", "SW05_FinalOvercharge_6f", "SW05_FinalDetonation_8f" };
        static readonly Sprite[][] frames = new Sprite[6][];
        // Pixel extents above the fixed ground pivot (96 px), measured from
        // the supplied sheets. Aura uses its full vertical content instead.
        static readonly Vector2[] Content = { new Vector2(381,50), new Vector2(414,342), new Vector2(220,392),
            new Vector2(477,174), new Vector2(403,60), new Vector2(457,181) };
        KarmaGame game; Transform follow; KarmaSW05RainBlade rain;
        SpriteRenderer picture; MaterialPropertyBlock properties;
        float age, seconds, opacity; bool driven;
        public Clip ClipId { get; private set; }
        public int FrameIndex { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFrames() { System.Array.Clear(frames, 0, frames.Length); }
        public static Sprite Frame(Clip clip, int index)
        {
            int c = (int)clip;
            if (c < 0 || c >= frames.Length) throw new System.ArgumentOutOfRangeException(nameof(clip));
            int count = clip == Clip.Detonation ? 8 : 6;
            if (index < 0 || index >= count) throw new System.ArgumentOutOfRangeException(nameof(index));
            if (frames[c] == null)
            {
                var loaded = new Sprite[count];
                for (int i = 0; i < loaded.Length; i++)
                {
                    loaded[i] = KarmaArtwork.Sprite("SwordBossVFX",Names[c]+"_"+i);
                    if (loaded[i] == null) throw new System.InvalidOperationException("Missing packed VFX frame: " + Names[c]+"_"+i);
                }
                frames[c] = loaded;
            }
            return frames[c][index];
        }
        public static KarmaSwordPackedVfx Spawn(Transform root, KarmaGame game, Clip clip, Vector2 at,
            Vector2 size, float seconds, int order, float opacity = 0.7f, bool driven = false, Transform follow = null)
        {
            // Validate before creating a live component: a missing pack must not
            // leave a half-initialized object retrying the failure every frame.
            Frame(clip, 0);
            var material = Resources.Load<Material>("SW05/SW05Celestial");
            if (material == null) throw new System.InvalidOperationException("Missing SW05/SW05Celestial material.");
            var node = new GameObject(Names[(int)clip]+" / animated light / no damage");
            node.transform.SetParent(root,false); node.transform.position = at;
            var fx = node.AddComponent<KarmaSwordPackedVfx>(); fx.game = game; fx.follow = follow;
            fx.rain = follow != null ? follow.GetComponent<KarmaSW05RainBlade>() : null;
            fx.picture = node.AddComponent<SpriteRenderer>(); fx.picture.sortingOrder = order;
            fx.picture.sharedMaterial = material;
            fx.properties = new MaterialPropertyBlock(); fx.properties.SetFloat("_Mode",7);
            fx.SetBounds(new Rect(-10000,clip == Clip.FallingAura ? -10000 : 0,20000,20000));
            fx.Play(clip,size,seconds,opacity,driven); return fx;
        }
        public void SetBounds(Rect rect)
        {
            properties.SetVector("_ClipRect",new Vector4(rect.xMin,rect.yMin,rect.xMax,rect.yMax));
            picture.SetPropertyBlock(properties);
        }
        public void Play(Clip clip, Vector2 size, float duration, float strength, bool manual = false)
        {
            Frame(clip, 0);
            ClipId = clip; age = 0; seconds = Mathf.Max(0.01f,duration); opacity = strength; driven = manual;
            var content = Content[(int)clip];
            transform.localScale = new Vector3(size.x*100/content.x,size.y*100/content.y,1);
            SetProgress(0);
        }
        public void SetProgress(float progress)
        {
            float t = Mathf.Clamp01(progress);
            FrameIndex = Mathf.Min(ClipId == Clip.Detonation ? 7 : 5,(int)(t*(ClipId == Clip.Detonation ? 8 : 6)));
            picture.sprite = Frame(ClipId,FrameIndex);
            bool charge = ClipId == Clip.GroundCharge || ClipId == Clip.Overcharge;
            float envelope = charge ? 0.25f+0.75f*t : 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.82f,1,t));
            picture.color = new Color(0.75f,0.94f,1,opacity*envelope);
            var rect=picture.sprite.textureRect;var tex=picture.sprite.texture;
            properties.SetVector("_FrameUV",new Vector4(rect.x/tex.width,rect.y/tex.height,rect.width/tex.width,rect.height/tex.height));
            properties.SetFloat("_PackStyle",(int)ClipId);properties.SetFloat("_Progress",t);
            // Rebind after the SpriteRenderer installs the new sprite's data.
            picture.SetPropertyBlock(properties);
        }
        void LateUpdate()
        {
            if (game == null || game.Phase != RunPhase.Combat
                || follow != null && (!follow.gameObject.activeInHierarchy || rain != null && rain.Landed))
            { Destroy(gameObject); return; }
            if (game.Paused) return;
            if (follow != null) transform.position = follow.position;
            if (driven) return;
            age += Time.deltaTime; SetProgress(age/seconds);
            if (age >= seconds) Destroy(gameObject);
        }
    }
}
