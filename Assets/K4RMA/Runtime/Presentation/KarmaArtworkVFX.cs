using UnityEngine;
namespace KarmaPrototype
{
    // Visual children never own damage, collision or attack timing.
    public sealed class KarmaArtworkVFX:MonoBehaviour
    {
        SpriteRenderer visual;
        Vector3 fullSize;
        float age,duration;
        bool pulse;
        public static KarmaArtworkVFX Spawn(Transform parent,Vector2 position,Sprite sprite,Vector2 size,float life,float angle=0,bool pulse=false)
        {
            var go=new GameObject("Authored VFX "+sprite.name);go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,0,angle);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=18;KarmaVisuals.ApplyMaterial(sr);
            var bounds=sprite.bounds.size;var fx=go.AddComponent<KarmaArtworkVFX>();
            fx.visual=sr;fx.duration=Mathf.Max(0.05f,life);fx.pulse=pulse;
            fx.fullSize=new Vector3(size.x/bounds.x,size.y/bounds.y,1);go.transform.localScale=fx.fullSize;
            return fx;
        }
        public static void Attack(Transform root,Vector2 velocity,Vector2 size,float life,int pattern)
        {
            var fx=Spawn(root,Vector2.zero,KarmaArtAtlas.Effects(1)[Mathf.Abs(pattern)%12],size,life);
            fx.fullSize=new Vector3(fx.fullSize.x/Mathf.Max(0.001f,root.localScale.x),fx.fullSize.y/Mathf.Max(0.001f,root.localScale.y),1);
            fx.transform.localScale=fx.fullSize;fx.visual.flipX=velocity.x<0;
        }
        public static void Personalize(KarmaGame game,Vector2 point,Essence essence)
        {
            var effects=KarmaArtAtlas.Effects(2);
            for(int i=0;i<4;i++)Spawn(game.World,point+Vector2.up*KarmaSpace.Units(0.2f*i),effects[(int)essence+i*3],
                Vector2.one*KarmaSpace.Units(2.3f+i*0.45f),0.45f+i*0.2f,0,true);
        }
        public static void Player(KarmaGame game,Vector2 point,int effect,Vector2 direction,float life=0.25f)
        {
            if(!KarmaArtwork.Settings.useCompleteArtwork)return;
            var fx=Spawn(game.World,point,KarmaArtAtlas.Effects(0)[Mathf.Abs(effect)%12],KarmaSpace.Units(new Vector2(2.7f,2.1f)),life);
            fx.visual.flipX=direction.x<0;
        }
        void Update()
        {
            age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}
            float t=age/duration;transform.localScale=fullSize*(pulse?Mathf.Lerp(0.45f,1.2f,t):Mathf.Lerp(0.85f,1.05f,t));
            visual.color=new Color(1,1,1,1-Mathf.Clamp01((t-0.5f)*2));
        }
    }
}
