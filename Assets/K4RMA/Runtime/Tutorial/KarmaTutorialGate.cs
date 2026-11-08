using UnityEngine;
namespace KarmaPrototype
{
    // The tall invisible collider prevents jumping over a closed lesson gate.
    public sealed class KarmaTutorialGate : MonoBehaviour
    {
        KarmaTutorial tutorial;
        Transform left, right;
        bool opening;
        float progress;
        public void Initialize(KarmaTutorial owner)
        {
            tutorial = owner;
            KarmaTutorialStage.Picture(transform, "CheckpointFrame", new Vector2(0, -4.25f), new Vector2(8.5f, 8.5f), 4);
            left = Leaf("왼 문짝", -0.88f, false);
            right = Leaf("오른 문짝", 0.88f, true);
        }
        Transform Leaf(string label, float x, bool mirror)
        {
            var hinge = new GameObject(label).transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(x, -5.75f, 0);
            var sprite = KarmaTutorialStage.Picture(hinge, "CheckpointDoor", Vector2.zero, new Vector2(2.05f, 4.5f), 3);
            sprite.flipX = mirror;
            return hinge;
        }
        public void Open() { opening = true; }
        void Update()
        {
            if (!opening || progress >= 1 || tutorial.Game.GameplayFrozen) return;
            progress = Mathf.Min(1, progress + Time.deltaTime * 1.6f);
            float t = Mathf.SmoothStep(0, 1, progress);
            left.localPosition = new Vector3(Mathf.Lerp(-0.88f, -1.65f, t), -5.75f, 0);
            right.localPosition = new Vector3(Mathf.Lerp(0.88f, 1.65f, t), -5.75f, 0);
            left.localScale = right.localScale = new Vector3(Mathf.Lerp(1, 0.08f, t), 1, 1);
            if (progress >= 1) GetComponent<BoxCollider2D>().enabled = false;
        }
    }
}
