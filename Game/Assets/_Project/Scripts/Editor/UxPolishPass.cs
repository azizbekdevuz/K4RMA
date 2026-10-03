using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace K4RMA.EditorTools
{
    public static class UxPolishPass
    {
        const string ScenePath = "Assets/_Project/Scenes/PrototypeArena.unity";

        public static void ApplyBatch()
        {
            try
            {
                Apply();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("K4RMA/Apply Altar Guide")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var hud = GameObject.Find("HUD");
            if (hud == null)
                throw new System.InvalidOperationException("HUD is missing from PrototypeArena.");
            if (hud.GetComponentInChildren<AltarGuide>(true) == null)
                AltarGuide.Create(hud.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Altar guide saved into PrototypeArena.");
        }
    }
}
