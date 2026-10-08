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
            Debug.Log("Altar guidance is the world-space arrow dressed by TempleArenaPass.");
        }
    }
}
