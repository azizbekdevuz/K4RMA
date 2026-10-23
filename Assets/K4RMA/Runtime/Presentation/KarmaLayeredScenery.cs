using UnityEngine;
namespace KarmaPrototype
{
    public sealed class KarmaLayeredScenery:MonoBehaviour
    {
        static readonly string[] Mountain={"sky","moon","far_mountains","far_forest","mid_forest","dojo","rocks","waterfall","water","floor","vegetation","trees","silhouettes"};
        static readonly string[] Hall={"outside_sky","moon_forest","back_wall_windows","rear_rail","pillars","banners","lanterns","floor","front_rail","front_silhouette"};
        Vector3 home;
        float factor,cameraHome;
        Camera view;
        public static bool Build(Transform parent,int stage,KarmaGame game)
        {
            bool outside=stage==0;var names=outside?Mountain:Hall;
            // Check the entire set before creating anything, so a missing layer doesn't leave a partial scene.
            var sprites=new Sprite[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                string path="05_Environments/"+(outside?"MountainDojo/ext-layer-":"TrialHall/hall-layer-")+names[i]+".png";
                sprites[i]=KarmaArtAtlas.Full(path);if(sprites[i]==null)return false;
            }
            float width=KarmaStage.BackgroundWidth,height=50f*941/1672;
            for(int i=0;i<names.Length;i++)
            {
                var sprite=sprites[i];var go=new GameObject("Scenery "+names[i]);go.transform.SetParent(parent,false);
                float shift=names[i]=="floor"?(outside?53:6)*height/941:0;
                go.transform.localPosition=new Vector3(KarmaStage.ArenaCenter,height*0.3f-shift,0);
                go.transform.localScale=new Vector3(width/sprite.bounds.size.x,height/sprite.bounds.size.y,1);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;KarmaVisuals.ApplyMaterial(sr);
                bool foreground=i>=names.Length-2;sr.sortingOrder=foreground?25+i:-30+i;
                if(foreground)sr.color=new Color(1,1,1,0.45f);
                var layer=go.AddComponent<KarmaLayeredScenery>();layer.home=go.transform.localPosition;
                layer.factor=i<3?(3-i)*0.025f:0;layer.view=game!=null?game.GameCamera:Camera.main;
                layer.cameraHome=layer.view!=null?layer.view.transform.position.x:KarmaStage.ArenaCenter;
                if(i==0&&game!=null)game.FrameStageBackground(sr.bounds);
            }
            var props=KarmaArtAtlas.Props();
            for(int i=0;i<4;i++)
            {
                int index=stage*4+i;var go=new GameObject("Scenery prop "+index);go.transform.SetParent(parent,false);
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=props[index];sr.sortingOrder=-2;KarmaVisuals.ApplyMaterial(sr);
                go.transform.localPosition=new Vector3(i<2?-12+i*5:47+(i-2)*5,index%4==0?6:0,0);
                go.transform.localScale=Vector3.one*1.15f;
            }
            return true;
        }
        void LateUpdate()
        {
            if(view!=null&&factor!=0)transform.localPosition=home+Vector3.right*(view.transform.position.x-cameraHome)*factor;
        }
    }
}
