using UnityEngine;
namespace KarmaPrototype
{
    // The tall invisible collider prevents skipping training by jumping.
    public sealed class KarmaTutorialGate : MonoBehaviour
    {
        KarmaTutorial tutorial;
        public void Initialize(KarmaTutorial owner) { tutorial = owner; }
        public void Open() { GetComponent<BoxCollider2D>().enabled = false; }
        void OnCollisionStay2D(Collision2D collision)
        {
            if (tutorial.Game.GameplayFrozen || !KarmaInput.Held(KarmaAction.Right)) return;
            if (collision.gameObject.GetComponent<KarmaPlayer>() != null) tutorial.WarnUnfinished();
        }
    }
}
