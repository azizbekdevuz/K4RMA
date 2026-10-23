using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Fusion artwork already contains its shield, blades and crest.
    // Fixed-position duplicate parts cannot follow these hand-drawn action poses.
    public sealed class KarmaGuardianAdornment : MonoBehaviour
    {
        public void Initialize(IReadOnlyList<Essence> order)
        {
            if (order.Count < 2) return;
            GetComponent<KarmaCharacterAnimator>().SetGuardianAppearance(KarmaArtwork.GuardianPreview(order));
        }
    }
}
