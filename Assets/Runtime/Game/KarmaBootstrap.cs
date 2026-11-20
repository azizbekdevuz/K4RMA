using UnityEngine;
namespace KarmaPrototype
{
    public static class KarmaBootstrap
    {
        public const int FramesPerSecond = 12;
        public const float Step = 1f / FramesPerSecond;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartEmptyScene()
        {
            if (Object.FindAnyObjectByType<KarmaGame>() != null) return;
            new GameObject("K4RMA Game").AddComponent<KarmaGame>();
        }
    }
}
