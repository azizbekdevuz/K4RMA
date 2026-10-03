using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace K4RMA.EditorTools
{
    [InitializeOnLoad]
    static class PresentationBootstrap
    {
        static int attempts;

        static PresentationBootstrap()
        {
            EditorApplication.delayCall += Schedule;
        }

        static void Schedule()
        {
            if (attempts++ > 40 || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Schedule;
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var path = scene.path == null ? string.Empty : scene.path.Replace('\\', '/');
            if (!path.EndsWith("Assets/_Project/Scenes/PrototypeArena.unity"))
            {
                EditorApplication.delayCall += Schedule;
                return;
            }

            if (GameObject.Find("Torso") != null)
                return;

            PresentationPass.Apply();
        }
    }
}
