using UnityEngine;

namespace KarmaPrototype
{
    [DefaultExecutionOrder(80)]
    sealed class KarmaAudioHost : MonoBehaviour
    {
        void LateUpdate() { KarmaAudio.Tick(this); }
        void OnDestroy() { KarmaAudio.Detach(this); }
    }
}
