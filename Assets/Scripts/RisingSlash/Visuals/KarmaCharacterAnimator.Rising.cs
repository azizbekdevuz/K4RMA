using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaRisingArticulatedMotion risingMotion;
        string risingClip, risingAutomaticPose;
        float risingElapsed, risingDuration, risingFrom, risingTo, risingAutomaticElapsed, risingLastHurt;
        int risingClockFrame = -1;

        // Pattern-local presentation requests never drive attack execution.
        internal void PlayRisingMotion(string pose, float seconds, float from = 0, float to = 1)
        {
            if (deathAt >= 0) return;
            risingClip = pose; risingElapsed = 0; risingDuration = Mathf.Max(0.001f, seconds);
            risingFrom = from; risingTo = to; risingClockFrame = Time.frameCount;
            SampleRisingMotion();
        }
        void EndRisingMotion()
        {
            risingClip = null; risingAutomaticPose = null; risingAutomaticElapsed = 0;
            if (risingMotion != null) risingMotion.Hide();
        }
        string RisingForm => poses.TryGetValue("Idle", out var idle) ? idle.texture.name : "Rise";
        void SampleRisingMotion()
        {
            if (risingMotion == null || risingClip == null) return;
            risingMotion.Sample(RisingForm, risingClip, risingElapsed, risingDuration, risingFrom, risingTo);
        }
        internal void ConfigureRisingPresentation()
        {
            // Base poses live in this boss's library; inherited bodies already supplied
            // their own poses and must keep those images.
            foreach (var sprite in KarmaArtwork.Sprites("Rise"))
                if (sprite != null && !poses.ContainsKey(sprite.name)) poses[sprite.name] = sprite;
            guardianPose = RisingGuardianPose;
            risingMotion = GetComponent<KarmaRisingArticulatedMotion>();
            if (risingMotion == null) risingMotion = gameObject.AddComponent<KarmaRisingArticulatedMotion>();
            risingMotion.Configure(picture, () => deathAt < 0 && Time.time >= empowermentUntil,
                () => deathAt < 0 ? -1 : Time.time - deathAt, () => revivalFrames);
            onActionEnded -= EndRisingMotion;
            onActionEnded += EndRisingMotion;
        }
        void RisingGuardianPose(string action, EnemyAttack attack, bool airborne, bool armored)
        {
            if (risingClockFrame != Time.frameCount)
            {
                // GuardianPose is skipped by the owner while paused.
                risingElapsed += Time.deltaTime;
                risingAutomaticElapsed += Time.deltaTime;
                risingClockFrame = Time.frameCount;
            }
            bool hurt = Time.time < hurtUntil;
            if (hurt || action == "Stunned" || (action != "Attack" && action != "Telegraph"))
            {
                if (risingClip != null) EndRisingMotion();
            }
            // Keep this original sprite policy intact: shared hit detection reads
            // its bounds even while the separate cutout renderers are visible.
            string pose = hurt ? "Hurt"
                : action == "Stunned" ? "Hurt"
                : action == "Recover" ? "Landing"
                : action == "Attack" ? airborne ? "AirSlash" : attack == EnemyAttack.UP01 || attack == EnemyAttack.UP03 ? "Rising" : "Slash"
                : DefaultGuardianPose(action, airborne, armored);
            if (pose == "Move") pose = "Dash";
            ApplyGuardianPose(pose);
            if (risingClip != null) { SampleRisingMotion(); return; }
            string automatic = action == "Telegraph" ? "Slash" : pose;
            if (automatic != risingAutomaticPose || (hurt && risingLastHurt != hurtUntil))
            {
                risingAutomaticPose = automatic; risingAutomaticElapsed = 0;
                risingLastHurt = hurtUntil;
            }
            float duration = automatic == "Idle" ? 1.6f : automatic == "Dash" ? 0.36f
                : automatic == "Hurt" ? 0.16f : automatic == "Landing" ? 0.35f : 0.28f;
            bool loop = automatic == "Idle" || automatic == "Dash";
            float elapsed = loop ? Mathf.Repeat(risingAutomaticElapsed, duration) : risingAutomaticElapsed;
            float from = action == "Recover" ? 0.6f : 0;
            float to = action == "Telegraph" ? 0.3f : 1;
            risingMotion.Sample(RisingForm, automatic, elapsed, duration, from, to);
        }
    }
}
