using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public static class KarmaTutorialStage
    {
        public const float RoomWidth = 50;
        public const float Right = 175;
        public const float FinalGate = 171;
        public static void Build(KarmaTutorial tutorial, out KarmaTrainingTarget[] targets, out GameObject[] barriers)
        {
            var parent = tutorial.transform;
            string[] backgrounds = { "01_Dojo_NoMaster", "02_Wall_Sunset1", "03_Wall_Sunset2", "04_Boss_Gate" };
            for (int i = 0; i < backgrounds.Length; i++)
            {
                Picture(parent, backgrounds[i], new Vector2(RoomWidth * i, 9), new Vector2(RoomWidth, RoomWidth * 9 / 16), -30);
            }
            Picture(parent, "Master", new Vector2(-1, 2.4f), new Vector2(4.39f, 4.8f), 2);
            Solid(parent, "Ground", new Vector2(80, -0.7f), new Vector2(192, 1.4f)).GetComponent<SpriteRenderer>().enabled = false;
            Solid(parent, "수련길 시작", new Vector2(-14, 10), new Vector2(1, 30)).GetComponent<SpriteRenderer>().enabled = false;
            Solid(parent, "수련길 끝", new Vector2(Right, 10), new Vector2(1, 30)).GetComponent<SpriteRenderer>().enabled = false;
            targets = new KarmaTrainingTarget[4]; barriers = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                float x = KarmaTutorial.Stations[i];
                barriers[i] = Solid(parent, "다음 수련길 · " + i, new Vector2(x + 7, 8), new Vector2(0.6f, 24));
                barriers[i].GetComponent<SpriteRenderer>().enabled = false;
                barriers[i].transform.localScale = Vector3.one;
                barriers[i].GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 24);
                barriers[i].AddComponent<KarmaTutorialGate>().Initialize(tutorial);
                var post = Picture(parent, "TrainingDummy", new Vector2(x, 2.5f), new Vector2(10f / 3, 5), 2);
                targets[i] = post.gameObject.AddComponent<KarmaTrainingTarget>();
                targets[i].Initialize(tutorial, (KarmaTrainingAttack)i);
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
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, 100);
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
