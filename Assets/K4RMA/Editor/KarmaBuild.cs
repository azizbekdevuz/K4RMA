#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace KarmaPrototype.Editor
{
    public static class KarmaBuild
    {
        [MenuItem("K4RMA/Windows 64-bit 빌드")]
        public static void Windows()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Unity Hub에서 이 Editor 버전의 Windows Build Support (Mono)를 설치하세요.");
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("먼저 K4RMA → 플레이 씬 생성 또는 Build Profiles에 플레이 씬을 등록하세요.");
            const string destination = "Builds/Windows/K4RMA.exe";
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = destination, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("K4RMA Windows 빌드 완료: " + Path.GetFullPath(destination));
        }
    }
}
#endif
