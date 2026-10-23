using System;
using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    // Reviewed top-left rectangles. These are key poses and independent effects, not uniform animation grids.
    public static class KarmaArtAtlas
    {
        const string Production="K4RMA_Production_Art_v1/";
        static readonly Dictionary<string,Sprite[]> cache=new Dictionary<string,Sprite[]>();
        static readonly Rect[] PlayerRects={
            new Rect(45,15,205,285),
            new Rect(330,45,265,250),
            new Rect(625,30,270,260),
            new Rect(915,0,300,290),
            new Rect(1230,30,305,260),
            new Rect(5,310,285,205),
            new Rect(320,320,290,195),
            new Rect(610,300,375,215),
            new Rect(920,295,300,225),
            new Rect(1180,295,355,225),
            new Rect(0,560,370,225),
            new Rect(355,510,245,280),
            new Rect(635,525,260,260),
            new Rect(940,550,285,240),
            new Rect(1220,545,310,240),
            new Rect(0,910,345,105),
            new Rect(335,800,285,220),
            new Rect(620,800,310,220),
            new Rect(935,825,310,195),
            new Rect(1230,790,300,230),
        };
        static readonly Rect[] NeutralRects={
            new Rect(5,90,350,420),
            new Rect(355,110,375,400),
            new Rect(745,110,350,400),
            new Rect(1100,170,435,335),
            new Rect(15,550,450,410),
            new Rect(390,585,370,375),
            new Rect(785,685,310,275),
            new Rect(1100,780,430,185),
        };
        static readonly Rect[] WaveRects={
            new Rect(5,5,350,350),
            new Rect(420,5,335,350),
            new Rect(775,5,385,350),
            new Rect(1180,25,355,330),
            new Rect(5,375,395,270),
            new Rect(400,375,445,270),
            new Rect(805,375,400,270),
            new Rect(1200,360,335,285),
            new Rect(75,650,305,340),
            new Rect(435,640,355,350),
            new Rect(750,705,370,285),
            new Rect(1120,850,410,145),
        };
        static readonly Rect[] RiseRects={
            new Rect(80,0,335,310),
            new Rect(460,0,345,310),
            new Rect(825,5,335,305),
            new Rect(1165,60,370,250),
            new Rect(25,450,420,230),
            new Rect(470,330,315,350),
            new Rect(790,415,415,265),
            new Rect(1180,335,350,345),
            new Rect(10,725,420,265),
            new Rect(440,750,445,240),
            new Rect(900,700,245,290),
            new Rect(1150,825,380,160),
        };
        static readonly Rect[] WardRects={
            new Rect(10,45,395,305),
            new Rect(470,40,250,310),
            new Rect(785,30,340,320),
            new Rect(1190,20,340,330),
            new Rect(15,400,400,250),
            new Rect(440,375,300,275),
            new Rect(755,400,350,250),
            new Rect(1140,410,390,240),
            new Rect(5,725,375,255),
            new Rect(420,660,315,320),
            new Rect(770,685,340,295),
            new Rect(1140,825,390,145),
        };
        static readonly Rect[] PlayerFXRects={
            new Rect(5,120,365,175),
            new Rect(400,80,395,230),
            new Rect(810,100,405,220),
            new Rect(1230,10,300,330),
            new Rect(5,365,385,300),
            new Rect(490,390,245,265),
            new Rect(850,345,245,330),
            new Rect(1210,360,310,310),
            new Rect(20,690,370,295),
            new Rect(405,720,415,290),
            new Rect(810,785,420,170),
            new Rect(1235,710,295,280),
        };
        static readonly Rect[] GuardianFXRects={
            new Rect(5,90,395,200),
            new Rect(415,70,425,250),
            new Rect(850,0,300,345),
            new Rect(1155,15,375,330),
            new Rect(15,365,350,330),
            new Rect(405,365,330,320),
            new Rect(745,435,475,200),
            new Rect(1235,355,295,320),
            new Rect(5,690,410,330),
            new Rect(425,750,340,255),
            new Rect(780,675,370,345),
            new Rect(1160,820,370,165),
        };
        static readonly Rect[] PersonalFXRects={
            new Rect(5,105,540,215),
            new Rect(550,0,255,345),
            new Rect(820,145,410,175),
            new Rect(1240,15,290,320),
            new Rect(0,405,460,265),
            new Rect(475,420,350,260),
            new Rect(830,380,375,290),
            new Rect(1215,375,315,310),
            new Rect(0,760,490,235),
            new Rect(485,690,350,330),
            new Rect(835,715,385,305),
            new Rect(1225,735,305,285),
        };
        static readonly Rect[] HitRects={
            new Rect(15,70,390,350),
            new Rect(445,65,420,355),
            new Rect(900,55,420,360),
            new Rect(1340,60,425,350),
            new Rect(15,460,400,420),
            new Rect(445,460,420,420),
            new Rect(900,460,420,420),
            new Rect(1340,460,425,420),
        };
        static readonly Rect[] PropsRects={
            new Rect(45,0,210,310),
            new Rect(450,5,245,310),
            new Rect(735,10,490,270),
            new Rect(1260,0,260,360),
            new Rect(40,325,220,245),
            new Rect(355,400,425,170),
            new Rect(810,305,405,265),
            new Rect(1235,390,295,180),
            new Rect(20,585,410,250),
            new Rect(450,610,330,230),
            new Rect(805,575,295,260),
            new Rect(1230,580,300,255),
            new Rect(30,835,445,185),
            new Rect(480,840,300,180),
            new Rect(800,845,405,175),
            new Rect(1220,840,310,180),
        };
        static readonly Rect[] HudRects={
            new Rect(25,625,690,80),
            new Rect(80,710,600,45),
            new Rect(750,625,765,80),
            new Rect(825,710,680,45),
            new Rect(25,785,790,200),
            new Rect(1340,805,150,160),
        };
        static readonly Vector2[] PlayerFeet={
            new Vector2(153,290),new Vector2(477,282),new Vector2(765,278),new Vector2(1090,280),new Vector2(1380,275),
            new Vector2(140,500),new Vector2(475,503),new Vector2(745,503),new Vector2(1070,503),new Vector2(1320,507),
            new Vector2(170,775),new Vector2(480,776),new Vector2(765,775),new Vector2(1080,778),new Vector2(1370,758),
            new Vector2(155,992),new Vector2(480,995),new Vector2(780,995),new Vector2(1090,995),new Vector2(1375,995)
        };
        static Vector2 Pivot(string path,int index,Rect r,bool feet)
        {
            if(!feet)return Vector2.one*0.5f;
            if(path.Contains("player-keyposes"))return new Vector2((PlayerFeet[index].x-r.x)/r.width,1-(PlayerFeet[index].y-r.y)/r.height);
            float[] anchors=path.Contains("neutral-keyposes")?new float[]{242,600,935,1290,200,600,960,1300}
                :path.Contains("wave-keyposes")?new float[]{246,606,951,1375,240,575,970,1320,220,600,960,1310}
                :path.Contains("rise-keyposes")?new float[]{255,635,990,1380,280,635,1000,1380,235,635,1010,1330}
                :path.Contains("ward-keyposes")?new float[]{222,610,955,1375,220,600,950,1350,200,590,950,1340}:null;
            return new Vector2(anchors!=null?(anchors[index]-r.x)/r.width:0.5f,0.035f);
        }
        static Sprite[] Cut(string path,Rect[] rects,float ppu,bool feet)
        {
            Sprite[] result;
            if(cache.TryGetValue(path,out result))return result;
            var texture=KarmaArtSources.Load(Production+path);
            if(texture==null)throw new InvalidOperationException("원본 이미지 연결 누락: "+path+" / Unity 이미지 임포트를 완료하세요.");
            result=new Sprite[rects.Length];
            for(int i=0;i<rects.Length;i++)
            {
                var r=rects[i];
                if(r.x<0||r.y<0||r.xMax>texture.width||r.yMax>texture.height)throw new InvalidOperationException("Sprite 영역 오류: "+path+"/"+i);
                result[i]=Sprite.Create(texture,new Rect(r.x,texture.height-r.yMax,r.width,r.height),Pivot(path,i,r,feet),ppu,0,SpriteMeshType.FullRect);
                result[i].name=path+"/"+i;
            }
            cache.Add(path,result);return result;
        }
        public static Sprite[] Player(){return Cut("01_Player/player-keyposes.png",PlayerRects,130,true);}
        public static Sprite[] Guardian(Essence? type)
        {
            return !type.HasValue?Cut("02_Guardians/guardian-neutral-keyposes.png",NeutralRects,145,true)
                :type==Essence.Flame?Cut("02_Guardians/guardian-wave-keyposes.png",WaveRects,125,true)
                :type==Essence.Dash?Cut("02_Guardians/guardian-rise-keyposes.png",RiseRects,125,true)
                :Cut("02_Guardians/guardian-ward-keyposes.png",WardRects,125,true);
        }
        public static Sprite[] Effects(int kind)
        {
            return kind==0?Cut("04_VFX/player-attack-vfx.png",PlayerFXRects,350,false)
                :kind==1?Cut("04_VFX/guardian-attack-vfx.png",GuardianFXRects,350,false)
                :kind==2?Cut("04_VFX/personalization-vfx.png",PersonalFXRects,350,false)
                :Cut("04_VFX/hit-vfx.png",HitRects,350,false);
        }
        public static Sprite[] Props(){return Cut("06_Props/props-foreground-atlas.png",PropsRects,150,true);}
        public static Sprite Hud(int index){return Cut("07_UI/ui-hud-atlas.png",HudRects,100,false)[Mathf.Clamp(index,0,5)];}
        public static Sprite Icon(int index)
        {
            var rects=new Rect[6];for(int i=0;i<6;i++)rects[i]=new Rect(i%3*512,i/3*512,512,512);
            return Cut("07_UI/skill-icons-lineart.png",rects,100,false)[Mathf.Clamp(index,0,5)];
        }
        public static Sprite Variant(int index)
        {
            var rects=new Rect[16];for(int i=0;i<16;i++)rects[i]=new Rect(i%4*384+3,i/4*256+3,378,250);
            return Cut("03_Variants/guardian-inheritance-atlas.png",rects,125,true)[Mathf.Clamp(index,0,15)];
        }
        public static Sprite Inheritance(IReadOnlyList<Essence> order)
        {
            if(order.Count==0)return Variant(12);
            Essence primary=order[0];int index=primary==Essence.Flame?0:primary==Essence.Dash?4:8;
            bool wave=false,rise=false;foreach(var e in order){wave|=e==Essence.Flame;rise|=e==Essence.Dash;}
            if(order.Count==3)index+=3;
            else if(order.Count==2)index+=primary==Essence.Flame?(rise?1:2):primary==Essence.Dash?(wave?1:2):(wave?1:2);
            return Variant(index);
        }
        public static Sprite Full(string path)
        {
            Sprite[] result;string key="full:"+path;if(cache.TryGetValue(key,out result))return result[0];
            var texture=KarmaArtSources.Load(Production+path);if(texture==null)return null;
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*0.5f,100,0,SpriteMeshType.FullRect);
            sprite.name=path;cache[key]=new[]{sprite};return sprite;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach(var sprites in cache.Values)foreach(var sprite in sprites)if(sprite!=null)UnityEngine.Object.Destroy(sprite);
            cache.Clear();
        }
    }
}
