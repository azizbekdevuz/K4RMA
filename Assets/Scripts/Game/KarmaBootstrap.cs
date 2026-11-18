using UnityEngine;
namespace KarmaPrototype
{
    public static class KarmaBootstrap
    {
        public const int FramesPerSecond = 12;
        public const float Step = 1f / FramesPerSecond;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureTiming()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FramesPerSecond;
            Time.fixedDeltaTime = Step;
            UnityEngine.Rendering.OnDemandRendering.renderFrameInterval = 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartEmptyScene()
        {
            if (Object.FindAnyObjectByType<KarmaGame>() != null) return;
            new GameObject("K4RMA Game").AddComponent<KarmaGame>();
        }
    }
}
