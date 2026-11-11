using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public static class KarmaTutorialStage
    {
        internal static System.Collections.IEnumerator PreloadArtwork()
        {
            var names = new[] { "01_Dojo_NoMaster", "04_Boss_Gate", "Master", "TrainingDummy" };
            var requests = new ResourceRequest[names.Length];
            for (int i = 0; i < names.Length; i++) requests[i] = Resources.LoadAsync<Texture2D>("KarmaTutorial/" + names[i]);
            foreach (var request in requests) yield return request;
        }
        // Original Master.png: last broad sandal support is row 1294 of 1312 (17 px transparent inset).
        internal static float MasterGroundCenter(float height) => height * (0.5f - 17f / 1312f);
        public const float RoomWidth = 50;
        public const float Right = 75;
        public const float FinalGate = 59;
        public static void Build(KarmaTutorial tutorial, out KarmaTrainingTarget[] targets, out GameObject[] barriers)
        {
            var parent = tutorial.transform;
            Picture(parent, "01_Dojo_NoMaster", new Vector2(0, 9), new Vector2(RoomWidth, RoomWidth * 9 / 16), -30);
            Picture(parent, "04_Boss_Gate", new Vector2(RoomWidth, 9), new Vector2(RoomWidth, RoomWidth * 9 / 16), -30);
            // Place the master on the ground in front of the dojo steps.
            Picture(parent, "Master", new Vector2(-12, MasterGroundCenter(4.8f)), new Vector2(4.39f, 4.8f), 2);
            BuildGround(parent);
            Solid(parent, "수련길 시작", new Vector2(-25, 10), new Vector2(1, 30)).GetComponent<SpriteRenderer>().enabled = false;
            Solid(parent, "수련길 끝", new Vector2(Right, 10), new Vector2(1, 30)).GetComponent<SpriteRenderer>().enabled = false;
            targets = new KarmaTrainingTarget[4]; barriers = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                float x = KarmaTutorial.Stations[i];
                float roomEnd = RoomWidth * 0.5f;
                barriers[i] = Solid(parent, "다음 수련길 · " + i, new Vector2(roomEnd - 0.3f, 8), new Vector2(0.6f, 24));
                barriers[i].GetComponent<SpriteRenderer>().enabled = false;
                barriers[i].transform.localScale = Vector3.one;
                barriers[i].GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 24);
                barriers[i].AddComponent<KarmaTutorialGate>().Initialize(tutorial);
                if (i == 0)
                {
                    var post = Picture(parent, "TrainingDummy", new Vector2(x + 5, 2.5f), new Vector2(10f / 3, 5), 2);
                    targets[i] = post.gameObject.AddComponent<KarmaTrainingTarget>();
                }
                else targets[i] = targets[0].gameObject.AddComponent<KarmaTrainingTarget>();
                targets[i].Initialize(tutorial, (KarmaTrainingAttack)i);
                targets[i].enabled = false;
            }
        }
        static void BuildGround(Transform parent)
        {
            float left = -26;
            for (int i = 0; i < 2; i++)
            {
                float right = i == 1 ? Right : (i + 0.5f) * RoomWidth;
                float width = right - left + 0.2f;
                Solid(parent, "Ground · " + i, new Vector2((left + right) / 2, -0.7f), new Vector2(width, 1.4f))
                    .GetComponent<SpriteRenderer>().enabled = false;
                left = right;
            }
        }
        static GameObject Solid(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var sr = KarmaVisuals.Box(parent, name, at, size, new Color(0.2f, 0.23f, 0.28f), 0);
            sr.gameObject.AddComponent<KarmaSurface>(); sr.gameObject.AddComponent<BoxCollider2D>(); return sr.gameObject;
        }
        internal static SpriteRenderer Picture(Transform parent, string name, Vector2 at, Vector2 size, int order)
        {
            var texture = Resources.Load<Texture2D>("KarmaTutorial/" + name);
            if (texture == null) throw new System.InvalidOperationException("Missing tutorial artwork: " + name);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
            var sr = KarmaVisuals.Box(parent, name, at, Vector2.one, Color.white, order);
            sr.sprite = sprite; sr.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            parent.gameObject.AddComponent<KarmaTutorialResources>().Own(sprite);
            return sr;
        }
    }
    public sealed class KarmaTutorialResources : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        public void Own(params Object[] objects) { owned.AddRange(objects); }
        void OnDestroy() { foreach (var item in owned) if (item != null) Destroy(item); }
    }
}
