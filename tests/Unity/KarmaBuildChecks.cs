#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class KarmaBuildChecks
{
    public static void RunBatch()
    {
        string verdict;
        try
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes.Any(s => !File.Exists(s))) throw new Exception("Enabled build scene is missing");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, target = BuildTarget.StandaloneOSX,
                locationPathName = "output/validation/build/K4RMA.app", options = BuildOptions.StrictMode
            });
            verdict = report.summary.result == BuildResult.Succeeded
                ? "PASS: macOS player build, " + report.summary.totalSize + " bytes, " + report.summary.totalWarnings + " warnings"
                : "FAIL: " + report.summary.result + ", " + report.summary.totalErrors + " errors";
        }
        catch (Exception error) { verdict = "FAIL: " + error; }
        Directory.CreateDirectory("output/validation"); File.WriteAllText("output/validation/build.txt", verdict);
        UnityEngine.Debug.Log(verdict); EditorApplication.Exit(verdict.StartsWith("PASS:") ? 0 : 1);
    }
}
#endif
