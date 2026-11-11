using UnityEngine;
using System.Collections.Generic;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator : MonoBehaviour
    {
        SpriteRenderer picture;
        Sprite[] frames;
        Sprite[] runFrames;
        float runAt;
        Vector2 slashDirection = Vector2.right;
        Vector3 pictureHome;
        Color pictureColor;
        float actionUntil, hurtUntil, deathAt = -1;
        readonly Dictionary<string, Sprite> poses = new Dictionary<string, Sprite>();
        string sheet, explicitPose;
        float poseUntil, landedUntil;
        bool wasAirborne;
        Vector3 guardianPreviousPosition;
        Sprite guardianAppearance;
        Vector3 pictureScale;
        float appearanceScale = 1;
        Sprite groundedDeathSprite;
        float deathSpriteBottom;
        float empowermentAt, empowermentUntil;
        Essence empowermentEssence;
        Sprite beforeInheritance;
        Sprite[] revivalFrames, beforeRevivalFrames;
        public string CurrentPose { get; private set; }
        public void ConfigureSheet(string name)
        {
            sheet = name; poses.Clear();
            LoadPlayerClips();
            revivalFrames = name == "Player" ? null : LoadRevivalFrames(name);
            runFrames = null;
            guardianPreviousPosition = transform.position;
            var library = KarmaArtwork.Settings.spriteLibrary;
            if (library == null) return;
            foreach (var sprite in library.All(name)) if (sprite != null) poses[sprite.name] = sprite;
            if (name == "Player" && poses.TryGetValue("Run", out var run))
            {
                // The second movement key pose is also used for jump takeoff in
                // the original sheet. Prefer a dedicated Run2 when supplied.
                if (!poses.TryGetValue("Run2", out var next)) next = KarmaArtwork.Settings.playerMove2;
                runFrames = next != null && next != run ? new[] { run, next } : new[] { run };
            }
        }
        public void PlayPose(string name, float seconds)
        {
            explicitPose = name; poseUntil = Time.time + seconds;
            onPoseRequested?.Invoke();
            if (!deferExplicitPose) { ShowPose(name); if (sheet == "Player") SamplePlayerClip(name, true); }
        }
        void ShowPose(string name)
        {
            if (picture == null || !poses.TryGetValue(name, out var sprite)) return;
            picture.sprite = sprite; CurrentPose = name;
            picture.transform.localScale = pictureScale;
        }
        public void ConfigureSprites(SpriteRenderer renderer, Sprite[] poses)
        {
            picture = renderer; frames = poses; picture.sprite = frames[0];
            pictureHome = picture.transform.localPosition; pictureColor = picture.color;
            pictureScale = picture.transform.localScale;
        }
        public void Attack() { Attack(Vector2.right); }
        public void Attack(Vector2 direction) { if (nativePlayer24) hurtUntil = 0; slashDirection = direction; actionUntil = Time.time + 0.18f;
            if (sheet == "Player") SamplePlayerClip(direction.y < -0.25f ? "AirSlash" : direction.y > 0.25f ? "Rising" : "Attack", true); }
        public void Hurt() { if (nativePlayer24) { CancelPlayerActionForControl(); playerClip = null; } hurtUntil = Time.time + 0.16f; }
        public void Die()
        {
            EndGuardianAction(); deathAt = Time.time;
            Pose(0, false);
        }
        public void Pose(float movement, bool airborne, float verticalSpeed = 0, bool crouching = false)
        {
            if (picture == null) return;
            if (deathAt >= 0)
            {
                float sinceDeath = Time.time - deathAt;
                if (sheet == "Player" && poses.ContainsKey("Death"))
                {
                    SamplePlayerClip("Death");
                    picture.transform.localPosition = pictureHome;
                    picture.transform.localScale = pictureScale;
                    picture.transform.localRotation = Quaternion.identity;
                    picture.color = pictureColor;
                    transform.localRotation = Quaternion.identity;
                }
                else if (poses.ContainsKey("Death")) ShowPose("Death");
                else if (picture != null && frames.Length >= 9) picture.sprite = frames[8];
                if (sheet != "Player" && picture != null)
                {
                    if (revivalFrames != null && revivalFrames.Length == 6) picture.sprite = revivalFrames[0];
                    float fall = Mathf.SmoothStep(0, 1, (Time.time - deathAt) / 0.65f);
                    picture.transform.localScale = pictureScale;
                    picture.transform.localPosition = pictureHome;
                    picture.transform.localRotation = Quaternion.identity;
                    transform.rotation = Quaternion.identity;
                    CurrentPose = "Death";
                    GroundFallenGuardian();
                    picture.color = Color.Lerp(pictureColor, new Color(0.55f, 0.55f, 0.6f), fall);
                }
                return;
            }
            if (EmpowermentPose()) return;
            if (picture != null)
            {
                int frame = 0;
                if (frames.Length > 1)
                {
                    if (Time.time < hurtUntil && frames.Length >= 9) frame = 7;
                    else if (Time.time < actionUntil)
                        frame = slashDirection.y < -0.25f ? 6 : slashDirection.y > 0.25f ? 5 : actionUntil - Time.time > 0.09f ? 3 : 4;
                    else if (airborne) frame = 1;
                    else if (Mathf.Abs(movement) > 0.1f) frame = (int)(Time.time * 9) % 2 == 0 ? 1 : 2;
                }
                picture.sprite = frames[frame];
                if (sheet == "Player" && poses.Count > 0)
                {
                    if (wasAirborne && !airborne) landedUntil = Time.time + 3f / 12;
                    // Landing is presentation only; never slide in a landing pose
                    // while movement or another takeoff is already underway.
                    if (airborne || Mathf.Abs(movement) > 0.1f || crouching) landedUntil = 0;
                    bool takeoff = !wasAirborne && airborne && verticalSpeed > 0;
                    wasAirborne = airborne;
                    if (takeoff) jumpVisualUntil = Time.time + 1f / 12;
                    string pose = Time.time < hurtUntil ? (nativePlayer24 && airborne ? "AirKnockback" : "Hurt")
                        : Time.time < poseUntil ? explicitPose
                        : Time.time < actionUntil ? (slashDirection.y < -0.25f ? "AirSlash"
                            : slashDirection.y > 0.25f ? "Rising" : "Attack")
                        : PlayerRecovering(movement, airborne) ? PlayerRecoveryPose(movement, airborne)
                        : !nativePlayer24 && actionUntil > 0 && Time.time < actionUntil + 0.08f ? "Recovery"
                        : airborne ? (Time.time < jumpVisualUntil ? "JumpStart" : verticalSpeed > 2 ? "Jump" : verticalSpeed < -2 ? "Fall" : "Apex")
                        : crouching ? "Crouch"
                        : Time.time < landedUntil ? "Land"
                        : PlayerTurning ? "Turn" : Mathf.Abs(movement) > 0.1f ? "Run" : "Idle";
                    SamplePlayerClip(pose);
                }
                float bob = sheet == "Player" || airborne || crouching ? 0 : Mathf.Abs(movement) > 0.1f ? Mathf.Sin(Time.time * 18) * 0.025f : Mathf.Sin(Time.time * 2) * 0.008f;
                picture.transform.localPosition = pictureHome + Vector3.up * bob;
                picture.color = Time.time < hurtUntil ? new Color(1, 0.45f, 0.45f) : pictureColor;
                transform.localRotation = Quaternion.Euler(0, 0, !nativePlayer24 && Time.time < hurtUntil ? -8 : 0);
            }
        }
        void GroundFallenGuardian()
        {
            if (picture.sprite == null) return;
            if (groundedDeathSprite != picture.sprite)
            {
                groundedDeathSprite = picture.sprite;
                deathSpriteBottom = FallenSupportHeight(picture.sprite);
            }
            float bottom = picture.transform.TransformPoint(new Vector3(0, deathSpriteBottom, 0)).y;
            transform.position -= Vector3.up * bottom;
        }
        static float FallenSupportHeight(Sprite sprite)
        {
            var vertices = sprite.vertices;
            var triangles = sprite.triangles;
            if (vertices.Length == 0) return 0;
            var outlines = new List<List<Vector2>>();
            for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
            {
                var outline = new List<Vector2>();
                sprite.GetPhysicsShape(i, outline);
                if (outline.Count >= 3) outlines.Add(outline);
            }
            // Full-rectangle rendering can still have an alpha-derived physics outline.
            // Use that silhouette rather than the transparent rectangle surrounding the pose.
            if (outlines.Count == 0)
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                    outlines.Add(new List<Vector2> { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] });
            if (outlines.Count == 0) return 0;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            foreach (var outline in outlines)
            foreach (var vertex in outline)
            {
                bottom = Mathf.Min(bottom, vertex.y); top = Mathf.Max(top, vertex.y);
                left = Mathf.Min(left, vertex.x); right = Mathf.Max(right, vertex.x);
            }
            // A blade tip is often lower than the actual body. Find the first broad supporting
            // section of the fallen silhouette, instead of grounding the character on its weapon.
            var sections = new List<Vector2>();
            var intersections = new List<float>();
            for (int sample = 1; sample <= 128; sample++)
            {
                float y = Mathf.Lerp(bottom, bottom + (top - bottom) * 0.5f, sample / 128f);
                sections.Clear();
                foreach (var outline in outlines)
                {
                    intersections.Clear();
                    for (int edge = 0; edge < outline.Count; edge++)
                    {
                        var a = outline[edge];
                        var b = outline[(edge + 1) % outline.Count];
                        if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                        {
                            float x = Mathf.Lerp(a.x, b.x, (y - a.y) / (b.y - a.y));
                            intersections.Add(x);
                        }
                    }
                    intersections.Sort();
                    for (int i = 0; i + 1 < intersections.Count; i += 2)
                        sections.Add(new Vector2(intersections[i], intersections[i + 1]));
                }
                sections.Sort((a, b) => a.x.CompareTo(b.x));
                float length = 0, end = float.NegativeInfinity;
                foreach (var section in sections)
                {
                    length += Mathf.Max(0, section.y - Mathf.Max(section.x, end));
                    end = Mathf.Max(end, section.y);
                }
                if (length >= (right - left) * 0.3f) return y;
            }
            return bottom;
        }
        void Update() { if (deathAt >= 0) Pose(0,false); }
    }
}
