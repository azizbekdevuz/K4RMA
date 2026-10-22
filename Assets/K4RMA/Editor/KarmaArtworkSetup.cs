#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace KarmaPrototype.Editor
{
    public static class KarmaArtworkSetup
    {
        public const string SettingsPath = "Assets/K4RMA/Resources/KarmaArtworkSettings.asset";
        const string Folder = "Assets/K4RMA/Resources/K4RMAArtwork/";

        [MenuItem("K4RMA/이미지 설정 선택")]
        public static void SelectSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(SettingsPath);
            if (settings == null)
                throw new InvalidOperationException("기본 이미지 설정이 없습니다. K4RMA/기본 이미지 설정 생성 (최초 1회)을 실행하세요.");
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("K4RMA/기본 이미지 설정 생성 (최초 1회)")]
        public static void CreateDefaults()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("플레이 모드를 종료한 뒤 실행하세요.");
            // Never overwrite a team's custom assignments or re-slice their images.
            if (AssetDatabase.LoadAssetAtPath<KarmaArtworkSettings>(SettingsPath) != null)
            {
                SelectSettings();
                return;
            }
            var settings = ScriptableObject.CreateInstance<KarmaArtworkSettings>();
            float[] feet = { 397f/418, 395f/418, 395f/418, 368f/418, 369f/418, 380f/418, 341f/418, 342f/418, 340f/418 };
            var player = new SpriteRect[9];
            for (int i = 0; i < player.Length; i++)
                player[i] = Frame("Pose" + i, new Rect(i % 3 * 418, (2 - i / 3) * 418, 418, 418), new Vector2(0.5f, 1 - feet[i]));
            ImportSprite("PlayerAnimation", 418 / 2.5f, player);
            settings.playerIdle = Sprite("PlayerAnimation", "Pose0");
            settings.playerMove1 = Sprite("PlayerAnimation", "Pose1");
            settings.playerMove2 = Sprite("PlayerAnimation", "Pose2");
            settings.playerSlash1 = Sprite("PlayerAnimation", "Pose3");
            settings.playerSlash2 = Sprite("PlayerAnimation", "Pose4");
            settings.playerRising = Sprite("PlayerAnimation", "Pose5");
            settings.playerFalling = Sprite("PlayerAnimation", "Pose6");
            settings.playerHurt = Sprite("PlayerAnimation", "Pose7");
            settings.playerDeath = Sprite("PlayerAnimation", "Pose8");

            var guardians = new SpriteRect[3];
            for (int i = 0; i < guardians.Length; i++)
                guardians[i] = Frame("Guardian" + i, new Rect(i * 724, 0, 724, 724), new Vector2(0.5f, 0));
            ImportSprite("GuardianSprites", 724 / 2.05f, guardians);
            settings.swordGuardian = Sprite("GuardianSprites", "Guardian0");
            settings.risingGuardian = Sprite("GuardianSprites", "Guardian1");
            settings.wardGuardian = Sprite("GuardianSprites", "Guardian2");
            ImportSprite("DojoBackground", 100, null);
            settings.dojoBackground = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "DojoBackground.png");

            settings.worldOverview = ImportTexture("WorldOverview");
            settings.playerSheet = ImportTexture("PlayerSheet");
            settings.playerSwordWave = ImportTexture("PlayerSwordWave");
            settings.playerRisingSlash = ImportTexture("PlayerRisingSlash");
            settings.swordGuardianIllustration = ImportTexture("SwordGuardian");
            settings.risingGuardianIllustration = ImportTexture("RisingGuardian");
            settings.wardGuardianIllustration = ImportTexture("WardGuardian");
            settings.guardianEvolution = ImportTexture("GuardianEvolution");
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            SelectSettings();
        }

        internal static SpriteRect Frame(string name, Rect rect, Vector2 pivot)
        {
            return new SpriteRect { name = name, rect = rect, pivot = pivot,
                alignment = SpriteAlignment.Custom, spriteID = GUID.Generate() };
        }

        static TextureImporter Importer(string name)
        {
            var importer = AssetImporter.GetAtPath(Folder + name + ".png") as TextureImporter;
            if (importer == null) throw new InvalidOperationException("PNG가 없습니다: " + name);
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            return importer;
        }

        internal static Texture2D ImportTexture(string name)
        {
            var importer = Importer(name);
            importer.textureType = TextureImporterType.Default;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + name + ".png");
        }

        internal static void ImportSprite(string name, float pixelsPerUnit, SpriteRect[] frames)
        {
            var importer = Importer(name);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = frames == null ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            if (frames == null) return;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Sprite 데이터 제공자가 없습니다: " + name);
            provider.InitSpriteEditorDataProvider();
            var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
            var required = EEditCapability.CreateAndDeleteSprite | EEditCapability.EditSpriteName
                | EEditCapability.EditSpriteRect | EEditCapability.EditPivot;
            if (edit == null || !edit.GetEditCapability().HasCapability(required))
                throw new InvalidOperationException("Sprite 자르기 권한이 없습니다: " + name);
            var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (ids == null) throw new InvalidOperationException("Sprite 참조 ID 제공자가 없습니다: " + name);
            provider.SetSpriteRects(frames);
            ids.SetNameFileIdPairs(frames.Select(frame => new SpriteNameFileIdPair(frame.name, frame.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        internal static Sprite Sprite(string sheet, string name)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(Folder + sheet + ".png").OfType<Sprite>().FirstOrDefault(s => s.name == name);
            if (sprite == null) throw new InvalidOperationException("Sprite 자르기 실패: " + sheet + "/" + name
                + " / imported: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(Folder + sheet + ".png").Select(a => a.name + ":" + a.GetType().Name)));
            return sprite;
        }

        public static void InitializeAndValidate()
        {
            CreateDefaults();
            KarmaArtworkValidation.Run();
        }
    }
}
#endif
