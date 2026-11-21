using UnityEngine;

namespace KarmaPrototype
{
    // Responds to the shared scene's published beats after the scene poses its
    // actor. Body drawings change here; blocking, captions and effect cues stay
    // on the existing scene timeline.
    [DefaultExecutionOrder(100)]
    public sealed class KarmaRisingCinematics : MonoBehaviour
    {
        KarmaGame game;
        KarmaCharacterAnimator animator;
        KarmaGuardianGrowth rehearsal;
        KarmaGuardianGrowth.Beat beat;
        string cue;
        float beatAt, cueAt;
        float acknowledgementAt = -1;

        internal void Initialize(KarmaGame director, KarmaCharacterAnimator body)
        {
            game = director;
            animator = body;
        }

        void LateUpdate()
        {
            if (game == null || animator == null) return;
            string pose = animator.CurrentPose;
            if (game.Phase == RunPhase.Ending && (pose == "Ending/Reflect" || pose == "Ending/Acknowledge"))
            {
                float time = game.EndingTime;
                if (pose == "Ending/Reflect") acknowledgementAt = -1;
                else if (acknowledgementAt < 0) acknowledgementAt = time;
                animator.DrawRisingAcknowledgement(time, acknowledgementAt >= 0 ? time - acknowledgementAt : -1);
            }

            var growth = game.GuardianGrowth;
            if (growth == null || !growth.isActiveAndEnabled || !animator.RisingGrowthActive)
            {
                rehearsal = null;
                cue = null;
                return;
            }

            if (rehearsal != growth || beat != growth.CurrentBeat)
            {
                rehearsal = growth;
                beat = growth.CurrentBeat;
                beatAt = growth.Age;
                cue = null;
            }
            if (cue != pose)
            {
                cue = pose;
                cueAt = growth.Age;
            }
            animator.DrawRisingGrowth(growth.Stage, beat, cue, growth.Age - beatAt, growth.Age - cueAt);
        }
    }

    public sealed partial class KarmaCharacterAnimator
    {
        internal bool RisingGrowthActive => growthActive;

        internal void DrawRisingAcknowledgement(float time, float acknowledgementAge)
        {
            if (motionForm != "RiseFinal") return;
            float step = KarmaBootstrap.Step;
            float sample = acknowledgementAge >= step ? 3 * step : Mathf.Min(time, 2 * step);
            DrawMotion(motionForm, "FinalDefeat", sample);
        }

        internal void DrawRisingGrowth(int stage, KarmaGuardianGrowth.Beat beat, string cue, float beatAge, float cueAge)
        {
            float step = KarmaBootstrap.Step;
            if (beat == KarmaGuardianGrowth.Beat.Revive && cue == "Growth/Revive/0" && previousMotionForm == "Neutral")
            {
                var fallen = motionArt.Frames(previousMotionForm, "Death");
                DrawMotion(previousMotionForm, "Death", (fallen.Length - 1) * step);
            }
            else if (beat == KarmaGuardianGrowth.Beat.Adapt && stage < 3)
            {
                float sample = cue == "Growth/Windup" ? Mathf.Min(cueAge, step)
                    : Mathf.Min(3 * step, (2 + Mathf.Floor(cueAge / (3 * step))) * step);
                DrawMotion(motionForm, "GrowthTechnique", sample);
            }
            else if (stage == 3 && beat != KarmaGuardianGrowth.Beat.Revive)
            {
                if (beat == KarmaGuardianGrowth.Beat.Combine && cue == "Growth/UP04")
                    DrawMotion(motionForm, "UP04", cueAge);
                else if (beat == KarmaGuardianGrowth.Beat.Combine)
                {
                    int frame = cue == "Growth/UP03" ? 2 : cue == "Growth/UP02" ? 3 : 1;
                    if (frame != 1 && cueAge >= 3 * step) frame = 4;
                    DrawMotion(motionForm, "GrowthMastery", frame * step);
                }
                else if (beat == KarmaGuardianGrowth.Beat.Foreshadow || beat == KarmaGuardianGrowth.Beat.Ready)
                    DrawMotion(motionForm, cue == "Growth/Windup" ? "Windup" : "Dash",
                        cue == "Growth/Windup" ? cueAge : Mathf.Min(cueAge, 2 * step));
                else if (beat == KarmaGuardianGrowth.Beat.TakeStance)
                    DrawMotion(motionForm, cue == "Growth/Idle" ? "Idle" : "GrowthMastery",
                        cue == "Growth/Idle" ? 0 : Mathf.Min(cueAge, step));
            }
            else if (stage == 2 && previousMotionForm == "Rise")
            {
                if (beat == KarmaGuardianGrowth.Beat.Stagger)
                    DrawMotion(previousMotionForm, "GrowthStumble", Mathf.Min(2 * step, Mathf.Floor(beatAge / 0.23f) * step));
                else if (beat == KarmaGuardianGrowth.Beat.Falter && beatAge >= 0.42f)
                    DrawMotion(previousMotionForm, "Death", 3 * step);
                else if (beat == KarmaGuardianGrowth.Beat.Inspect && cue == "Growth/InspectWeapon" && cueAge < 4 * step)
                {
                    // The inspection artwork follows the hand raising the sword.
                    picture.transform.localScale = pictureScale;
                    DrawMotion(previousMotionForm, "GrowthInspect", cueAge);
                    CurrentPose = "Growth/Inspect";
                }
            }
        }
    }
}
