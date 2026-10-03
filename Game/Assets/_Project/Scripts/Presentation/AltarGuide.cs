using UnityEngine;
using UnityEngine.UI;

namespace K4RMA
{
    public class AltarGuide : MonoBehaviour
    {
        [SerializeField] RunDirector director;
        [SerializeField] RectTransform marker;
        [SerializeField] Text arrow;
        [SerializeField] Text caption;
        [SerializeField] float bobPixels = 12f;
        [SerializeField] float pulseSpeed = 2.6f;

        static bool building;
        Canvas canvas;
        RectTransform canvasRect;
        SacrificeSequence sequence;

        public static AltarGuide Create(Transform canvasParent)
        {
            var existing = canvasParent.GetComponentInChildren<AltarGuide>(true);
            if (existing != null && existing.marker != null)
                return existing;

            building = true;
            var root = new GameObject("AltarGuide", typeof(RectTransform));
            root.transform.SetParent(canvasParent, false);
            var markerObject = new GameObject("Marker", typeof(RectTransform));
            markerObject.transform.SetParent(root.transform, false);
            var markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(280f, 90f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var arrowText = Label(markerObject.transform, "Arrow", font, 42, FontStyle.Bold, new Color(0.96f, 0.9f, 0.62f), new Vector2(0f, 18f));
            var captionText = Label(markerObject.transform, "Caption", font, 22, FontStyle.Normal, new Color(0.96f, 0.94f, 0.88f), new Vector2(0f, -22f));
            captionText.text = "Go to the altar";
            arrowText.text = ">";

            var guide = root.AddComponent<AltarGuide>();
            guide.marker = markerRect;
            guide.arrow = arrowText;
            guide.caption = captionText;
            markerObject.SetActive(false);
            building = false;
            guide.Cache();
            return guide;
        }

        void Awake()
        {
            if (building)
                return;
            if (marker == null)
            {
                var hud = FindAnyObjectByType<HudPresenter>();
                if (hud != null)
                    Create(hud.transform);
                enabled = false;
                return;
            }

            Cache();
        }

        void Cache()
        {
            if (director == null)
                director = FindAnyObjectByType<RunDirector>();
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            sequence = FindAnyObjectByType<SacrificeSequence>();
            if (marker != null)
                marker.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (marker == null)
                return;
            if (!ShouldShow())
            {
                if (marker.gameObject.activeSelf)
                    marker.gameObject.SetActive(false);
                return;
            }

            var view = Camera.main;
            var altar = director.Altar;
            var player = FindAnyObjectByType<PlayerController>();
            if (view == null || altar == null || player == null || canvasRect == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            if (!marker.gameObject.activeSelf)
                marker.gameObject.SetActive(true);

            Vector3 altarPoint = altar.transform.position + Vector3.up * 2.3f;
            Vector3 viewport = view.WorldToViewportPoint(altarPoint);
            bool behind = viewport.z <= 0f;
            bool onScreen = !behind && viewport.x > 0.12f && viewport.x < 0.88f && viewport.y > 0.08f && viewport.y < 0.92f;
            float bob = Mathf.Sin(Time.unscaledTime * pulseSpeed) * bobPixels;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.06f;

            if (onScreen)
            {
                Vector3 screen = view.WorldToScreenPoint(altarPoint);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 placed);
                marker.anchoredPosition = placed + new Vector2(0f, 36f + bob);
                if (arrow != null)
                    arrow.text = "v";
            }
            else
            {
                float direction = altar.transform.position.x >= player.transform.position.x ? 1f : -1f;
                if (behind)
                    direction = -direction;
                float edge = canvasRect.rect.width * 0.36f;
                marker.anchoredPosition = new Vector2(direction * edge, bob);
                if (arrow != null)
                    arrow.text = direction > 0f ? ">" : "<";
            }

            marker.localScale = new Vector3(pulse, pulse, 1f);
        }

        bool ShouldShow()
        {
            if (director == null || director.State == null || director.Altar == null)
                return false;
            if (director.State.Phase != RunPhase.Altar || director.State.StageIndex != 1)
                return false;
            if (director.Altar.PlayerInside)
                return false;
            if (sequence != null && sequence.IsPlaying)
                return false;
            return true;
        }

        static Text Label(Transform parent, string name, Font font, int size, FontStyle style, Color color, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280f, 40f);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
