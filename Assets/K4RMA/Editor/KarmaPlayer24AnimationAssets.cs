using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    // Separate authored library: legacy player art/clips remain available until native validation passes.
    public static class KarmaPlayer24AnimationAssets
    {
        const string Art = "Assets/K4RMA/Arts/Common/Resources/K4RMAArtwork/Characters/Player/Native24/";
        const string Clips = "Assets/K4RMA/Resources/PlayerAnimation24";
        public static readonly string[] Names = { "Idle", "Run", "Turn", "JumpStart", "Jump", "Apex", "Fall", "Land", "Crouch",
            "Attack", "SwordWave", "Rising", "Recovery", "Guard", "Parry", "Riposte", "Hurt", "Pierce", "AirHorizontal", "AirUp", "AirDiagonal", "AirHover", "AirDown",
            "StunStart", "StunLoop", "StunRecovery", "ShockStart", "ShockLoop", "ShockRecovery", "Knockback", "AirKnockback",
            "AirborneRise", "AirborneApex", "AirborneFall", "Knockdown", "DownLoop", "GetUp", "Death", "PersonalizeWave", "PersonalizeRising", "PersonalizeGuard", "PierceUp", "PierceDiagonal", "PierceDown", "RunRecovery", "AirRecovery", "Walk", "Kneel", "StandFromKneel", "Salute" };
        static readonly int[] Counts = { 12,8,4,2,4,2,4,6,6,6,6,6,6,6,6,8,4,6,4,4,4,2,4,4,8,6,4,8,6,6,6,4,4,4,8,4,12,16,12,12,12,6,6,6,8,4,16,8,8,16 };
        static readonly HashSet<string> Loops = new HashSet<string> { "Idle", "Run", "Walk", "StunLoop", "ShockLoop" };
        static readonly HashSet<string> CloseUp = new HashSet<string> { "Idle", "Kneel", "StandFromKneel", "Salute" };
        struct Measure { public int Left, Right, Bottom, Top; public float Face, FaceX, Waist; }
        static Measure MeasureCell(Color32[] pixels, int textureWidth, int x0, int y0, int w, int h, string id)
        {
            var m = new Measure { Left=w, Right=-1, Bottom=h, Top=-1 };
            bool[] skin = new bool[w*h], visited = new bool[w*h];
            int edge = 0;
            for (int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                var c=pixels[(y0+y)*textureWidth+x0+x];
                if(c.a<180) continue;
                m.Left=Math.Min(m.Left,x);m.Right=Math.Max(m.Right,x);m.Bottom=Math.Min(m.Bottom,y);m.Top=Math.Max(m.Top,y);
                if(x<2||x>=w-2||y<2||y>=h-2) edge++;
                skin[y*w+x]=c.r>75 && c.r>c.g*1.08f && c.g>c.b*1.12f;
            }
            if(m.Right<0 || edge > Mathf.Max(8,(w+h)/30)) throw new InvalidOperationException(id+": empty or clipped silhouette (border pixels="+edge+")");
            int[] queue=new int[w*h];int best=0;
            for(int seed=0;seed<skin.Length;seed++)
            {
                if(!skin[seed]||visited[seed])continue;
                int start=0,end=1,left=w,right=0,bottom=h,top=0;queue[0]=seed;visited[seed]=true;
                while(start<end)
                {
                    int p=queue[start++],x=p%w,y=p/w;left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
                    void Add(int q){if(!visited[q]&&skin[q]){visited[q]=true;queue[end++]=q;}}
                    if(x>0)Add(p-1);if(x+1<w)Add(p+1);if(y>0)Add(p-w);if(y+1<h)Add(p+w);
                }
                float aspect=(right-left+1f)/(top-bottom+1f);
                if(end>best && aspect>0.42f&&aspect<2.1f&&end>20)
                {best=end;m.Face=Mathf.Sqrt(end);m.FaceX=(left+right)*0.5f;}
            }
            if(m.Face<=0) throw new InvalidOperationException(id+": cannot establish head-scale reference");
            int bestWhite=0; float waist=0;
            for(int y=m.Bottom+(m.Top-m.Bottom)/4;y<m.Bottom+(m.Top-m.Bottom)*3/4;y++)
            {
                int white=0;
                for(int x=Math.Max(0,(int)m.FaceX-w/5);x<Math.Min(w,(int)m.FaceX+w/5);x++)
                {
                    var c=pixels[(y0+y)*textureWidth+x0+x];
                    if(c.a>180&&c.r>130&&c.g>120&&c.b>95&&c.r>=c.g&&c.r-c.g<35&&c.g-c.b<45)white++;
                }
                if(white>bestWhite){bestWhite=white;waist=y;}
            }
            m.Waist=bestWhite>=3?waist:0;
            return m;
        }
        static float Median(IEnumerable<float> values){var a=values.OrderBy(v=>v).ToArray();return a[a.Length/2];}
        public static void Rebuild()
        {
            foreach(var name in Names) if(!File.Exists(Art+name+".png"))throw new InvalidOperationException("Missing native action: "+name);
            var collected=new List<KarmaSpriteLibrary.Sheet>();float faceWorld=0, hipWorld=0;
            if(!AssetDatabase.IsValidFolder(Clips))AssetDatabase.CreateFolder("Assets/K4RMA/Resources","PlayerAnimation24");
            for(int n=0;n<Names.Length;n++)
            {
                string name=Names[n],path=Art+name+".png";int count=Counts[n],cols=count<=4?2:count==6?3:4,rows=(count+cols-1)/cols;
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.textureShape=TextureImporterShape.Texture2D;
                importer.spriteImportMode=SpriteImportMode.Multiple;importer.npotScale=TextureImporterNPOTScale.None;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(textureSettings);
                importer.maxTextureSize=4096;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);int w=texture.width/cols,h=texture.height/rows;
                var pixels=texture.GetPixels32();var measures=new Measure[count];
                for(int i=0;i<count;i++)measures[i]=MeasureCell(pixels,texture.width,i%cols*w,(rows-1-i/cols)*h,w,h,name+"/"+i);
                var scaleMeasures = name=="GetUp" ? measures.Skip(Math.Max(0,count-3))
                    : name=="Death"||name=="Knockdown" ? measures.Take(Math.Min(3,count)) : measures.AsEnumerable();
                float face=Median(scaleMeasures.Select(m=>m.Face));
                if(n==0)
                {
                    float ppu=Median(measures.Select(m=>(float)(m.Top-m.Bottom+1)))/(263f/130f);
                    faceWorld=face/ppu;
                    hipWorld=(Median(measures.Select(m=>m.Waist))-Median(measures.Select(m=>(float)m.Bottom)))/ppu;
                }
                importer.spritePixelsPerUnit=face/faceWorld;
                float pivotX=0.5f;
                bool air = name=="Jump"||name=="Apex"||name=="Fall"||name.StartsWith("Air",StringComparison.Ordinal);
                if(air && measures.Any(m=>m.Waist<=0))throw new InvalidOperationException(name+": airborne waist anchor missing");
                float pivotY=air ? (Median(measures.Select(m=>m.Waist))-hipWorld*importer.spritePixelsPerUnit)/h
                    : Median(measures.Select(m=>(float)m.Bottom))/h;
                var cells=new SpriteMetaData[count];
                for(int i=0;i<count;i++)cells[i]=new SpriteMetaData{name=name+"_"+i.ToString("00"),rect=new Rect(i%cols*w,(rows-1-i/cols)*h,w,h),alignment=(int)SpriteAlignment.Custom,pivot=new Vector2(pivotX,air || name=="Run" ? pivotY : measures[i].Bottom/(float)h)};
                #pragma warning disable 618
                importer.spritesheet=cells;
                #pragma warning restore 618
                importer.maxTextureSize=CloseUp.Contains(name)?2048:1024;
                importer.isReadable=false;importer.SaveAndReimport();
                var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
                if(sprites.Length!=count)throw new InvalidOperationException(name+": frame count mismatch");
                if(Mathf.Abs(sprites[0].bounds.size.x-w/importer.spritePixelsPerUnit)>0.01f)
                    throw new InvalidOperationException(name+": import resolution changed sprite world geometry");
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"/"+name+".anim");bool fresh=clip==null;if(fresh)clip=new AnimationClip();
                clip.name=name;clip.frameRate=24;
                var keys=sprites.Select((sprite,i)=>new ObjectReferenceKeyframe{time=i/24f,value=sprite}).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=count/24f;settings.loopTime=Loops.Contains(name);AnimationUtility.SetAnimationClipSettings(clip,settings);
                if(fresh)AssetDatabase.CreateAsset(clip,Clips+"/"+name+".anim");else EditorUtility.SetDirty(clip);
                collected.Add(new KarmaSpriteLibrary.Sheet{id=name,sprites=sprites});
                Debug.Log("Native24 "+name+": "+count+" frames, "+(count/24f)+"s, PPU="+importer.spritePixelsPerUnit);
            }
            void Alias(string id,string source)
            {
                var entry=collected.Find(s=>s.id==source);collected.Add(new KarmaSpriteLibrary.Sheet{id=id,sprites=entry.sprites});
                var sourceClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"/"+source+".anim");
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"/"+id+".anim");bool fresh=clip==null;if(fresh)clip=new AnimationClip();EditorUtility.CopySerialized(sourceClip,clip);clip.name=id;
                if(fresh)AssetDatabase.CreateAsset(clip,Clips+"/"+id+".anim");else EditorUtility.SetDirty(clip);
            }
            Alias("AirSlash","AirHorizontal");Alias("Dash","Pierce");Alias("Transition","PersonalizeWave");
            var library=AssetDatabase.LoadAssetAtPath<KarmaSpriteLibrary>(Clips+"/Frames.asset");
            if(library==null){library=ScriptableObject.CreateInstance<KarmaSpriteLibrary>();AssetDatabase.CreateAsset(library,Clips+"/Frames.asset");}
            library.sheets=collected.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            File.WriteAllText(Clips+"/Ready.txt", "24\n");AssetDatabase.ImportAsset(Clips+"/Ready.txt");
            Debug.Log("Native24 library built: "+collected.Count+" actions; legacy assets preserved.");
        }
    }
}
