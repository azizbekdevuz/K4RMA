using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KarmaPrototype
{
    // RisingSlash-only cutout playback. Source sprites remain installed for fallback,
    // revival, the final fallen pose and states without an articulated rig.
    internal sealed class KarmaRisingArticulatedMotion : MonoBehaviour
    {
        [Serializable] internal sealed class RisingCutoutPart
        {
            public string name;
            public int parent;
            public Vector2 pivot, origin;
            public RisingCutoutRect rect;
        }
        [Serializable] internal sealed class RisingCutoutRect
        {
            public float x, y, width, height;
        }
        [Serializable] internal sealed class RisingJointKey
        {
            public float time;
            public float[] angles;
        }
        [Serializable] internal sealed class RisingRigData
        {
            public string form, pose, atlas;
            public float ppu;
            public Vector2 pivot;
            public RisingCutoutPart[] parts;
            public RisingJointKey[] keys;
        }
        sealed class RisingRigInstance
        {
            public RisingRigData data;
            public GameObject root;
            public Transform[] joints;
            public SpriteRenderer[] pictures;
            public Sprite[] sprites;
            public SortingGroup sorting;
        }
        readonly Dictionary<string, RisingRigInstance> rigs = new Dictionary<string, RisingRigInstance>();
        readonly HashSet<string> unavailable = new HashSet<string>();
        SpriteRenderer source;
        Func<bool> presentationAllowed;
        Func<float> deathElapsed;
        Func<Sprite[]> deathFrames;
        SpriteRenderer collapse;
        const float CollapseSeconds = 0.65f;
        Sprite expectedSource;
        string lastForm, lastPose, lastKey;
        RisingRigInstance active;
        public const float FramesPerSecond = 24;
        internal int SampleIndex { get; private set; } = -1;
        internal string Motion { get; private set; }

        internal void Configure(SpriteRenderer renderer, Func<bool> allowed, Func<float> elapsed, Func<Sprite[]> frames)
        {
            source = renderer; presentationAllowed = allowed; deathElapsed = elapsed; deathFrames = frames;
        }

        internal bool Sample(string form, string pose, float seconds, float duration, float from = 0, float to = 1)
        {
            if (form != lastForm || pose != lastPose)
            {
                lastForm = form; lastPose = pose; lastKey = form + "_" + pose;
            }
            string key = lastKey;
            if (!rigs.TryGetValue(key, out var rig))
            {
                if (unavailable.Contains(key)) { Hide(); return false; }
                rig = Load(key);
                if (rig == null) { unavailable.Add(key); Hide(); return false; }
                rigs.Add(key, rig);
            }
            if (active != rig)
            {
                Hide();
                active = rig; active.root.SetActive(true);
            }
            expectedSource = source.sprite;
            source.forceRenderingOff = true;
            Motion = key;
            // Quantize the visual clock only. Gameplay windows are never read back
            // from these samples, and the final sample clamps to the supplied phase.
            SampleIndex = Mathf.FloorToInt(Mathf.Max(0, seconds) * FramesPerSecond);
            float sampled = SampleIndex / FramesPerSecond;
            float t = Mathf.Lerp(from, to, Mathf.Clamp01(sampled / Mathf.Max(0.001f, duration)));
            var keys = rig.data.keys;
            int right = 1;
            while (right < keys.Length - 1 && t > keys[right].time) right++;
            var a = keys[right - 1]; var b = keys[right];
            float u = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a.time, b.time, t));
            for (int i = 0; i < rig.joints.Length; i++)
                rig.joints[i].localRotation = Quaternion.Euler(0, 0, -Mathf.Lerp(a.angles[i], b.angles[i], u));
            SyncAppearance();
            return true;
        }

        RisingRigInstance Load(string key)
        {
            var text = Resources.Load<TextAsset>("RisingAnimation/" + key);
            if (text == null || source == null) return null;
            var data = JsonUtility.FromJson<RisingRigData>(text.text);
            var texture = Resources.Load<Texture2D>("RisingAnimation/" +
                (data != null && !string.IsNullOrEmpty(data.atlas) ? data.atlas : key));
            if (texture == null) return null;
            if (data == null || data.ppu <= 0 || data.parts == null || data.keys == null || data.keys.Length < 2)
                return null;
            for (int i = 0; i < data.parts.Length; i++)
                if (data.parts[i].parent >= i || data.parts[i].parent < -1) return null;
            foreach (var frame in data.keys)
                if (frame.angles == null || frame.angles.Length != data.parts.Length) return null;
            var rig = new RisingRigInstance { data = data, root = new GameObject(key),
                joints = new Transform[data.parts.Length],
                pictures = new SpriteRenderer[data.parts.Length],
                sprites = new Sprite[data.parts.Length] };
            // Keep visual parts outside the original Artwork subtree: the shared
            // damage contract enumerates that subtree's SpriteRenderers.
            rig.root.transform.SetParent(source.transform.parent.parent, false);
            rig.sorting = rig.root.AddComponent<SortingGroup>();
            for (int i = 0; i < data.parts.Length; i++)
            {
                var part = data.parts[i];
                var joint = new GameObject(part.name).transform;
                joint.SetParent(part.parent < 0 ? rig.root.transform : rig.joints[part.parent], false);
                var rest = part.pivot - (part.parent < 0 ? data.pivot : data.parts[part.parent].pivot);
                joint.localPosition = new Vector3(rest.x / data.ppu, -rest.y / data.ppu);
                var picture = new GameObject("Cutout").AddComponent<SpriteRenderer>();
                picture.transform.SetParent(joint, false);
                picture.transform.localPosition = new Vector3(
                    (part.origin.x - part.pivot.x) / data.ppu,
                    (part.pivot.y - part.origin.y - part.rect.height) / data.ppu);
                var sprite = Sprite.Create(texture, new Rect(part.rect.x, part.rect.y, part.rect.width, part.rect.height), Vector2.zero, data.ppu, 0, SpriteMeshType.FullRect);
                sprite.name = key + "/" + part.name;
                picture.sprite = sprite;
                picture.sharedMaterial = source.sharedMaterial;
                picture.sortingOrder = i;
                rig.joints[i] = joint; rig.pictures[i] = picture; rig.sprites[i] = sprite;
            }
            rig.root.SetActive(false);
            return rig;
        }

        void SyncAppearance()
        {
            if (active == null || source == null) return;
            active.root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            active.root.transform.localScale = Vector3.Scale(source.transform.parent.localScale, source.transform.localScale);
            active.sorting.sortingLayerID = source.sortingLayerID;
            active.sorting.sortingOrder = source.sortingOrder;
            foreach (var picture in active.pictures)
            {
                picture.color = source.color;
                picture.sortingLayerID = source.sortingLayerID;
                picture.sharedMaterial = source.sharedMaterial;
                picture.enabled = source.enabled;
            }
        }
        void LateUpdate()
        {
            float deathTime = deathElapsed != null ? deathElapsed() : -1;
            if (deathTime >= 0 && deathTime < CollapseSeconds && SampleCollapse(deathTime)) return;
            if (collapse != null && collapse.enabled)
            {
                collapse.enabled = false;
                if (source != null) source.forceRenderingOff = false;
            }
            // Shared revival/death replace the source sprite. Restore that exact
            // original immediately, including when no GuardianPose call follows.
            if (source == null || source.sprite != expectedSource || (presentationAllowed != null && !presentationAllowed())) Hide();
            else SyncAppearance();
        }
        bool SampleCollapse(float seconds)
        {
            var frames = deathFrames != null ? deathFrames() : null;
            if (source == null || frames == null || frames.Length != 6) return false;
            foreach (var frame in frames) if (frame == null) return false;
            Hide();
            if (collapse == null)
            {
                collapse = new GameObject("Rising collapse").AddComponent<SpriteRenderer>();
                collapse.transform.SetParent(source.transform.parent.parent, false);
            }
            float sample = Mathf.Floor(seconds * FramesPerSecond) / FramesPerSecond;
            int index = Mathf.Clamp(Mathf.FloorToInt(sample / CollapseSeconds * 6), 0, 5);
            // Reuse complete canonical poses: ready, lowering, kneeling, bracing,
            // then prone. Existing revival order and source assets are untouched.
            collapse.sprite = frames[5 - index];
            collapse.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            collapse.transform.localScale = Vector3.Scale(source.transform.parent.localScale, source.transform.localScale);
            collapse.sharedMaterial = source.sharedMaterial; collapse.color = source.color;
            collapse.sortingLayerID = source.sortingLayerID; collapse.sortingOrder = source.sortingOrder;
            collapse.flipX = source.flipX; collapse.flipY = source.flipY;
            collapse.enabled = source.enabled; source.forceRenderingOff = true;
            Motion = "Collapse"; SampleIndex = Mathf.FloorToInt(seconds * FramesPerSecond);
            return true;
        }
        internal void Hide()
        {
            if (active != null && active.root != null) active.root.SetActive(false);
            if (collapse != null) collapse.enabled = false;
            active = null;
            if (source != null) source.forceRenderingOff = false;
            SampleIndex = -1; Motion = null;
        }
        void OnDisable() { Hide(); }
        void OnDestroy()
        {
            Hide();
            foreach (var rig in rigs.Values)
            {
                foreach (var sprite in rig.sprites) if (sprite != null) Destroy(sprite);
                if (rig.root != null) Destroy(rig.root);
            }
            rigs.Clear();
            if (collapse != null) Destroy(collapse.gameObject);
        }
    }
}
