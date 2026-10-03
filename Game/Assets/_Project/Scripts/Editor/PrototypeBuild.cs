using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace K4RMA.EditorTools
{
    public static class PrototypeBuild
    {
        const string ScenePath = "Assets/_Project/Scenes/PrototypeArena.unity";

        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("Missing playable scene at " + ScenePath);
                EditorApplication.Exit(1);
                return;
            }

            string output = Path.GetFullPath("Builds/Windows/K4RMA-Prototype.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Windows build failed: " + report.summary.result);
                EditorApplication.Exit(1);
            }
        }
    }
}
