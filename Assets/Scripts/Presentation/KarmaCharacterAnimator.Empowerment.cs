using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        static Sprite[] LoadRevivalFrames(string form)
        {
            var inherited = KarmaArtwork.InheritedPoses(form);
            if (inherited.Length == 16)
            {
                var names = new[] { "Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand", "Idle" };
                var result = new Sprite[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    string pose = names[i];
                    result[i] = System.Array.Find(inherited, s => s.name == pose);
                    if (result[i] == null) return System.Array.Empty<Sprite>();
                }
                return result;
            }
            var sprites = KarmaArtwork.RevivalPoses(form);
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }
        public void Empower(Essence essence, float seconds, Sprite previousAppearance = null, bool fromNeutral = false,
            string previousForm = null)
        {
            EndPersonalizationProof();
            deathAt = -1;
            empowermentAt = Time.time;
            empowermentUntil = Time.time + seconds;
            empowermentEssence = essence;
            previousMotionForm = previousForm ?? (fromNeutral ? "Neutral" : motionForm);
            EmpowermentPose();
        }
        void GuardianRevivalPose(float t)
        {
            string form = t < .48f ? previousMotionForm : motionForm;
            string pose = t < .12f ? "Death" : t < .24f ? "RevivePush" : t < .36f ? "ReviveKneel"
                : t < .62f ? "ReviveRise" : t < .76f ? "ReviveStand" : "Idle";
            if (!KarmaMotionArt.Has(form, pose)) form = previousMotionForm;
            PlayBody(pose, form);
            CurrentPose = pose;
        }
        bool EmpowermentPose()
        {
            if (picture == null || Time.time >= empowermentUntil)
            { EndPersonalizationProof(); return false; }
            float t = Mathf.InverseLerp(empowermentAt, empowermentUntil, Time.time);
            if (sheet == "Player")
            {
                float elapsed = Time.time - empowermentAt;
                if (elapsed >= KarmaPersonalizationVFX.ProofStart)
                {
                    SamplePersonalizationProof(empowermentEssence, elapsed - KarmaPersonalizationVFX.ProofStart);
                    return true;
                }
                bool ceremony = elapsed >= KarmaPersonalizationVFX.CeremonyStart
                    && elapsed < KarmaPersonalizationVFX.CeremonyStart + KarmaPersonalizationVFX.CeremonyDuration;
                string pose = !ceremony ? "Idle" : empowermentEssence == Essence.SwordWave ? "PersonalizeWave"
                    : empowermentEssence == Essence.RisingSlash ? "PersonalizeRising" : "PersonalizeGuard";
                SamplePlayerClip(pose);
            }
            else
            {
                GuardianRevivalPose(t);
            }
            Color color = empowermentEssence == Essence.Guard
                ? new Color(1, 0.9f, 0.4f) : empowermentEssence == Essence.RisingSlash
                ? new Color(1, 0.65f, 0.35f) : new Color(0.4f, 1, 1);
            // Technique colour belongs to the separate effect. Multiplying the
            // whole player sprite discoloured skin/clothes during personalization.
            picture.color = sheet == "Player" ? pictureColor
                : Color.Lerp(pictureColor, color, Mathf.Sin(t * Mathf.PI) * 0.55f);
            if (sheet != "Player")
            {
                picture.transform.localRotation = Quaternion.identity;
                picture.color = Color.Lerp(new Color(0.55f, 0.55f, 0.6f), picture.color,
                    Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.38f)));
                float flash = Mathf.Clamp01(1 - Mathf.Abs(t - 0.5f) / 0.07f);
                picture.color = Color.Lerp(picture.color, Color.white, flash);
            }
            picture.transform.localPosition = pictureHome;
            transform.localRotation = Quaternion.identity;
            return true;
        }
    }
}
