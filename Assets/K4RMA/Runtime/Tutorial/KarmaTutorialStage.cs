using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public static class KarmaTutorialStage
    {
        public const float Right = 150;
        public const float FinalGate = 130;
        public static void Build(KarmaTutorial tutorial, out KarmaTrainingTarget[] targets, out GameObject[] barriers)
        {
            var parent = tutorial.transform;
            // Use the panorama as a distant continuous scene, rather than enlarging
            // every building to cover the entire playable distance.
            var backdrop = Picture(parent, "TrainingPanorama", new Vector2(41, 13.64f), new Vector2(132, 44), -30);
            var scenery = parent.gameObject.AddComponent<KarmaTutorialBackdrop>();
            scenery.Initialize(backdrop.transform);
            var material = new Material(Resources.Load<Shader>("KarmaTutorial/Backdrop"));
            backdrop.sharedMaterial = material;
            parent.gameObject.AddComponent<KarmaTutorialResources>().Own(material);
            Solid(parent, "Ground", new Vector2(91, -0.7f), new Vector2(212, 1.4f)).GetComponent<SpriteRenderer>().enabled = false;
            Solid(parent, "수련길 시작", new Vector2(-14, 10), new Vector2(1, 30)).GetComponent<SpriteRenderer>().enabled = false;
            Solid(parent, "수련길 끝", new Vector2(Right, 10), new Vector2(1, 30)).GetComponent<SpriteRenderer>().enabled = false;
            targets = new KarmaTrainingTarget[3]; barriers = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                float x = KarmaTutorial.Stations[i];
                barriers[i] = Solid(parent, "다음 수련길 · " + i, new Vector2(x + 7, 8), new Vector2(0.6f, 24));
                barriers[i].GetComponent<SpriteRenderer>().enabled = false;
                barriers[i].transform.localScale = Vector3.one;
                barriers[i].GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 24);
                barriers[i].AddComponent<KarmaTutorialGate>().Initialize(tutorial);
                if (i < targets.Length)
                {
                    var post = Prop(parent, "TrainingPost", new Vector2(x, i == 2 ? 5 : 2), new Vector2(2.7f, 4));
                    targets[i] = post.gameObject.AddComponent<KarmaTrainingTarget>();
                    targets[i].Initialize(tutorial, (KarmaTrainingAttack)i);
                }
            }
        }
        static GameObject Solid(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var sr = KarmaVisuals.Box(parent, name, at, size, new Color(0.2f, 0.23f, 0.28f), 0);
            sr.gameObject.AddComponent<KarmaSurface>(); sr.gameObject.AddComponent<BoxCollider2D>(); return sr.gameObject;
        }
        static SpriteRenderer Prop(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var sr = KarmaVisuals.Box(parent, name, at, Vector2.one, Color.white, 2);
            var sprite = KarmaArtwork.Sprite("Props", name);
            if (sprite != null)
            {
                sr.sprite = sprite;
                Vector3 scale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
                sr.transform.localScale = scale;
                sr.transform.localPosition = (Vector3)at - Vector3.Scale(sprite.bounds.center, scale);
            }
            return sr;
        }
        internal static SpriteRenderer Picture(Transform parent, string name, Vector2 at, Vector2 size, int order)
        {
            var texture = Resources.Load<Texture2D>("KarmaTutorial/" + name);
            if (texture == null) throw new System.InvalidOperationException("Missing tutorial artwork: " + name);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, 100);
            var sr = KarmaVisuals.Box(parent, name, at, Vector2.one, Color.white, order);
            sr.sprite = sprite; sr.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            parent.gameObject.AddComponent<KarmaTutorialResources>().Own(sprite);
            return sr;
        }
    }
    public sealed class KarmaTutorialBackdrop : MonoBehaviour
    {
        Transform picture;
        public void Initialize(Transform value) { picture = value; }
        public void Frame(Camera camera)
        {
            var p = picture.localPosition;
            float halfWidth = camera.orthographicSize * camera.aspect;
            float travel = Mathf.InverseLerp(-25 + halfWidth, 155 - halfWidth, camera.transform.position.x);
            p.x = 41 + 48 * travel;
            // Keep painted pixels past both viewport edges, including at the
            // very first frame and the end stops. Never expose the clear color.
            var bounds = picture.GetComponent<SpriteRenderer>().bounds;
            const float padding = 1;
            float left = camera.transform.position.x - halfWidth - padding;
            float right = camera.transform.position.x + halfWidth + padding;
            p.x = Mathf.Clamp(p.x, right - bounds.extents.x, left + bounds.extents.x);
            picture.localPosition = p;
        }
    }
    public sealed class KarmaTutorialResources : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        public void Own(params Object[] objects) { owned.AddRange(objects); }
        void OnDestroy() { foreach (var item in owned) if (item != null) Destroy(item); }
    }
}
