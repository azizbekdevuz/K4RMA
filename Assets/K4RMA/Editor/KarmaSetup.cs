#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KarmaPrototype;

namespace KarmaPrototype.Editor
{
    public static class KarmaSetup
    {
        [MenuItem("K4RMA/플레이 씬 생성")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("먼저 플레이 모드를 종료하세요."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/K4RMA/Scenes/KarmaPrototype.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                RegisterScene(path);
                return;
            }
            Directory.CreateDirectory("Assets/K4RMA/Scenes");
            Directory.CreateDirectory("Assets/K4RMA/Settings");
            AssetDatabase.Refresh();
            const string configPath = "Assets/K4RMA/Settings/KarmaConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<KarmaConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<KarmaConfig>();
                AssetDatabase.CreateAsset(config, configPath);
            }
            if (config.spriteMaterial == null)
            {
                config.spriteMaterial = KarmaVisuals.DefaultMaterial; EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("K4RMA Game");
            var game = root.AddComponent<KarmaGame>();
            var serialized = new SerializedObject(game);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(scene, path);
            RegisterScene(path);
            Selection.activeObject = root;
            Debug.Log("K4RMA 준비 완료. Play 후 Enter를 누르세요. 설정: " + configPath);
        }

        static void RegisterScene(string path)
        {
            if (EditorBuildSettings.scenes.Any(scene => scene.path == path)) return;
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(new[] { new EditorBuildSettingsScene(path, true) }).ToArray();
        }

        [MenuItem("K4RMA/로직 검사")]
        public static void RunChecks() { KarmaChecks.Run(); }

        [MenuItem("K4RMA/선택한 설정의 보스 난이도 초기화")]
        public static void ResetBossTuning()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("먼저 플레이 모드를 종료하세요."); return; }
            var config = Selection.activeObject as KarmaConfig;
            if (config == null) { Debug.LogWarning("Project 창에서 KarmaConfig 설정 에셋을 먼저 선택하세요."); return; }
            Undo.RecordObject(config, "보스 난이도 초기화");
            config.bossTuning = new KarmaBossTuning();
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
            Debug.Log("보스 난이도를 기본 프로필로 초기화했습니다. 폰트와 플레이어 설정은 유지합니다.");
        }
    }
}
#endif
