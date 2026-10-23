#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    // Copies source textures only. Existing importer Sprite rectangles and saved Inspector assignments are untouched.
    [InitializeOnLoad]
    public sealed class KarmaCompleteArtImport:IPreprocessBuildWithReport
    {
        const string Destination="Assets/K4RMA/Resources/K4RMAComplete/";
        static string Target(string path)
        { return (KarmaArtSources.ReferenceOnly(path)?"Assets/K4RMA/Editor/ArtReferences/":Destination)+path; }
        static KarmaCompleteArtImport(){EditorApplication.delayCall+=Ensure;}
        public int callbackOrder {get{return -100;}}
        public void OnPreprocessBuild(BuildReport report){Sync();}
        static void Ensure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try{Sync();}catch(Exception error){Debug.LogException(error);}
        }
        public static void Sync()
        {
            bool changed=false;
            foreach(var relative in KarmaArtSources.Files)
            {
                string source="image/"+relative,target=Target(relative);
                if(!File.Exists(source))
                {
                    if(File.Exists(target))continue;
                    throw new FileNotFoundException("원본 이미지 누락",source);
                }
                if(File.Exists(target)&&new FileInfo(source).Length==new FileInfo(target).Length
                    &&File.GetLastWriteTimeUtc(source)<=File.GetLastWriteTimeUtc(target))continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target,true);changed=true;
            }
            if(changed)AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var relative in KarmaArtSources.Files)
            {
                string target=Target(relative);var importer=AssetImporter.GetAtPath(target) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("이미지 임포트 실패: "+target);
                if(importer.textureType==TextureImporterType.Default&&!importer.mipmapEnabled
                    &&importer.npotScale==TextureImporterNPOTScale.None&&importer.maxTextureSize==4096
                    &&importer.textureCompression==TextureImporterCompression.Uncompressed)continue;
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Bilinear;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            const string catalogPath="Assets/K4RMA/Editor/CompleteArtInventory.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<KarmaArtInventory>(catalogPath);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<KarmaArtInventory>();AssetDatabase.CreateAsset(catalog,catalogPath);}
            bool update=changed||catalog.images==null||catalog.images.Length!=KarmaArtSources.Files.Length;
            if(!update)foreach(var entry in catalog.images)if(entry.image==null){update=true;break;}
            if(update)
            {
                catalog.images=new KarmaArtInventory.Entry[KarmaArtSources.Files.Length];
                for(int i=0;i<catalog.images.Length;i++)
                {
                    var relative=KarmaArtSources.Files[i];
                    catalog.images[i]=new KarmaArtInventory.Entry{source="image/"+relative,
                        usage=KarmaArtSources.ReferenceOnly(relative)?"화면 구성 / 원화 참조 (조작 UI 아님)":"게임플레이 / UI 원화",
                        image=AssetDatabase.LoadAssetAtPath<Texture2D>(Target(relative))};
                }
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            }
            if(changed)Debug.Log("K4RMA 전체 원본 이미지 연결: "+KarmaArtSources.Files.Length);
        }
        public static void Validate()
        {
            Sync();
            int sprites=KarmaArtAtlas.Player().Length;
            sprites+=KarmaArtAtlas.Guardian(null).Length;
            foreach(Essence type in Enum.GetValues(typeof(Essence)))sprites+=KarmaArtAtlas.Guardian(type).Length;
            for(int i=0;i<4;i++)sprites+=KarmaArtAtlas.Effects(i).Length;
            sprites+=KarmaArtAtlas.Props().Length;
            for(int i=0;i<16;i++)KarmaArtAtlas.Variant(i);
            for(int i=0;i<6;i++){KarmaArtAtlas.Icon(i);KarmaArtAtlas.Hud(i);}
            foreach(var path in KarmaArtSources.Files)
                if(!KarmaArtSources.ReferenceOnly(path)&&KarmaArtSources.Load(path)==null)
                    throw new InvalidOperationException("빌드 이미지 연결 누락: "+path);
            File.WriteAllText("complete-art-validation.txt","PASS: "+KarmaArtSources.Files.Length+" source images, "+sprites+" character/effect/prop sprites + variants/icons/HUD.");
            Debug.Log(File.ReadAllText("complete-art-validation.txt"));
        }
    }
}
#endif
