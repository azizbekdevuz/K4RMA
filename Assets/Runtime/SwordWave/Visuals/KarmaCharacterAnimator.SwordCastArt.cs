using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaSwordBasicArt swordCastArt;
        SpriteRenderer swordCastBlade;
        KarmaSwordCastWeight swordCastWeight;
        const float WaveDowncutEnd = 6 * KarmaBootstrap.Step;
        int swordCastDrawing = -1;
        Sprite[] volleyDrawings;
        float volleyDrawingScale;
        internal bool IsVolleyDrawing(Sprite sprite) => volleyDrawings != null && sprite != null
            && sprite.texture == volleyDrawings[0].texture;

        void DrawSwordVolley(string action)
        {
            // The generic windup and follow-through were sampled at frame zero on
            // each pose update. Their pull-era drawings are private to this cast.
            const int windup = 4, followThrough = 5;
            if (volleyDrawings == null)
            {
                volleyDrawings = KarmaArtResource.Frames("SwordWave/Volley/" + motionForm);
                if (volleyDrawings.Length != 6)
                    throw new System.InvalidOperationException("Missing SW01 cast drawings for " + motionForm);
            }
            int frame;
            if (action == "Recover") frame = followThrough;
            else if (action == "Telegraph" && (CurrentPose == "Windup" || CurrentPose == "Guard")) frame = windup;
            else frame = SwordBodyReleased
                ? Mathf.Min(3, 1 + Mathf.FloorToInt(SwordReleaseAge * KarmaBootstrap.FramesPerSecond)) : 0;

            string motion = frame == windup ? "Windup" : frame == followThrough ? "FollowThrough" : "SW01";
            var registration = motionArt.Frames(motionForm, motion)[frame < windup ? frame : 0];
            var sprite = volleyDrawings[frame];
            volleyDrawingScale = sprite.pixelsPerUnit / registration.pixelsPerUnit;
            displayForm = motionForm;
            displayMotion = "SW01";
            picture.sprite = displaySprite = sprite;
            RegisterSwordAppearance();
        }

        float swordRainCutAt = float.NegativeInfinity;
        const float CastDownstrokeSeconds = KarmaBootstrap.Step / BasicDowncutPlaybackRate;
        const float CastSettleSeconds = CastDownstrokeSeconds + 3 * KarmaBootstrap.Step;

        void ConfigureSwordCastArt()
        {
            swordCastArt = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/" + motionForm);
            int expectedDrawings = motionForm == "Wave" ? 16 : 8;
            if (swordCastArt == null || swordCastArt.idle == null || swordCastArt.blade == null
                || swordCastArt.poses.Length != expectedDrawings || swordCastArt.sockets.Length != expectedDrawings || swordCastArt.angles.Length != expectedDrawings)
                throw new System.InvalidOperationException("Missing SwordWave cast artwork: " + motionForm);
            var node = new GameObject("Cast rigid blade");
            node.transform.SetParent(picture.transform, false);
            swordCastBlade = node.AddComponent<SpriteRenderer>();
            swordCastBlade.sprite = swordCastArt.blade;
            swordCastBlade.enabled = false;
            swordCastWeight = picture.gameObject.AddComponent<KarmaSwordCastWeight>();
            // Measured helmet-to-sole height; transparent margins and raised
            // weapons must never change the body registration.
            swordCastWeight.Configure(swordCastArt, picture, SwordStandingHeight(motionForm));
            swordCastWeight.ConfigureBasic(swordBasicArt);
        }

        // The contact drawing starts on the existing projectile event. Only
        // the downstroke uses the requested 2x speed; warnings and recovery do not.
        static int SwordCastReleaseDrawing(float age)
        {
            if (age < CastDownstrokeSeconds) return 4;
            if (age < CastDownstrokeSeconds + KarmaBootstrap.Step) return 5;
            if (age < CastDownstrokeSeconds + 2 * KarmaBootstrap.Step) return 6;
            if (age < CastSettleSeconds) return 7;
            return -1;
        }

        static int SwordCastWindupDrawing(bool lifting, float remaining)
            => remaining <= CastDownstrokeSeconds ? 3 : lifting ? 1 : 2;

        // The warning can grow with distance. Spend that time on the overhead hold.
        int WaveDowncutFrame(float age)
        {
            const float step = KarmaBootstrap.Step;
            const float swingFrameSeconds = 1f / 30f;
            switch (sw02VisualPhase)
            {
                case SW02VisualPhase.Tracking:
                    return Mathf.Min(5, Mathf.FloorToInt(6 * age / sw02PhaseSeconds));
                case SW02VisualPhase.Locked:
                    float swingAge = age - (sw02PhaseSeconds - 4 * swingFrameSeconds);
                    if (swingAge < 0) return 6;
                    return 7 + Mathf.Min(3, Mathf.FloorToInt(swingAge / swingFrameSeconds));
                case SW02VisualPhase.Released:
                case SW02VisualPhase.Recovery:
                    if (age < swingFrameSeconds) return 11;  // Actual projectile event.
                    if (age < 3 * step) return 12;
                    if (age < 4 * step) return 13;
                    if (age < 5 * step) return 14;
                    if (age < WaveDowncutEnd) return 15;
                    return -1;
                default: return -1;
            }
        }

        internal void SampleWaveDownstroke()
        {
            if (motionForm != "Wave" || SwordBodyAction != "Heavy" || !SW02UsesIntermediateFrame
                || Time.time < hurtUntil || sw02VisualPhase is not (SW02VisualPhase.Locked or SW02VisualPhase.Released)) return;
            // The projectile runs on the render clock. Sample the short cut on that same clock.
            int frame = WaveDowncutFrame(Time.time - sw02PhaseAt);
            if (frame >= 7 && frame <= 12 && frame != swordCastDrawing)
                DrawSwordCastArt(EnemyAttack.SW02);
        }

        bool DrawSwordCastArt(EnemyAttack attack)
        {
            if (swordCastArt == null) return false;
            float step = KarmaBootstrap.Step;
            bool aimedSlash = attack == EnemyAttack.SW02 && motionForm == "Wave";
            int frame;
            if (attack == EnemyAttack.SW05 && motionForm == "WaveFinal")
            {
                switch (SwordBodyAction)
                {
                    case "UltimateWindup":
                        float age = SwordGroundProgress * SwordBodyDuration;
                        frame = SwordStrikeIndex > 0 ? SwordCastReleaseDrawing(Time.time - swordRainCutAt) : -1;
                        if (frame < 0)
                        {
                            float settleAt = SwordStrikeIndex > 0 ? Mathf.Max(0, swordRainCutAt + CastSettleSeconds - swordBodyAt) : 0;
                            frame = SwordCastWindupDrawing(age < settleAt + step, SwordBodyDuration - age);
                        }
                        break;
                    case "UltimateCommand":
                    case "UltimateCharge":
                    case "UltimateFinish":
                        // Landing can start the detonation warning while the arm is
                        // still lowering. Only an actual volley restarts the cut.
                        frame = SwordCastReleaseDrawing(Time.time - swordRainCutAt);
                        break;
                    default: return false;
                }
            }
            else if (attack == EnemyAttack.SW02 && SwordBodyAction == "Heavy")
            {
                float age = Mathf.Max(0, Time.time - sw02PhaseAt);
                if (sw02VisualPhase == SW02VisualPhase.None) return false;
                if (aimedSlash) frame = WaveDowncutFrame(age);
                else switch (sw02VisualPhase)
                {
                    case SW02VisualPhase.Tracking: frame = age < step ? 1 : 2; break;
                    case SW02VisualPhase.Locked: frame = SwordCastWindupDrawing(false, sw02PhaseSeconds - age); break;
                    case SW02VisualPhase.Released:
                    case SW02VisualPhase.Recovery: frame = SwordCastReleaseDrawing(age); break;
                    default: return false;
                }
            }
            else return false;
            displayForm = motionForm; displayMotion = attack.ToString();
            swordCastDrawing = frame;
            // Wave's new drawings already contain the hip, knee and shoulder movement.
            picture.sprite = displaySprite = frame < 0 ? swordCastArt.idle
                : aimedSlash ? swordCastArt.poses[frame] : swordCastWeight.Drawing(frame, heavy: true);
            picture.transform.localScale = pictureScale;
            RegisterSwordAppearance();
            return true;
        }

        void RegisterSwordCastArt()
        {
            if (swordCastArt == null || picture == null || swordCastBlade == null) return;
            bool visible = swordCastDrawing >= 0 && swordCastDrawing < swordCastArt.poses.Length
                && (motionForm == "Wave" ? picture.sprite == swordCastArt.poses[swordCastDrawing]
                    : swordCastWeight.Matches(picture.sprite, swordCastDrawing))
                && deathAt < 0 && Time.time >= hurtUntil && sw02Owner != null && !sw02Owner.Stunned;
            swordCastBlade.enabled = visible && picture.enabled;
            if (!visible) return;
            var sprite = picture.sprite;
            Vector2 pixel = swordCastArt.sockets[swordCastDrawing];
            Vector2 grip = (new Vector2(pixel.x, sprite.rect.height - pixel.y) - sprite.pivot) / sprite.pixelsPerUnit;
            float angle = swordCastArt.angles[swordCastDrawing];
            if (motionForm != "Wave")
            {
                grip = swordCastWeight.Point(grip, swordCastDrawing, heavy: true);
                angle += swordCastWeight.Angle(swordCastDrawing, heavy: true);
            }
            swordCastBlade.transform.localPosition = grip;
            swordCastBlade.transform.localRotation = Quaternion.Euler(0, 0, angle);
            swordCastBlade.transform.localScale = Vector3.one * (swordCastArt.bladeLength / swordCastArt.blade.bounds.size.x);
            swordCastBlade.sharedMaterial = picture.sharedMaterial;
            swordCastBlade.color = picture.color;
            swordCastBlade.sortingLayerID = picture.sortingLayerID;
            swordCastBlade.sortingOrder = picture.sortingOrder - 1;
        }

        internal bool SwordCastBladePoints(out Vector2 heel, out Vector2 bend, out Vector2 tip)
        {
            heel = bend = tip = default;
            if (swordCastBlade == null || !swordCastBlade.enabled) return false;
            heel = swordCastBlade.transform.position;
            tip = swordCastBlade.transform.TransformPoint(Vector3.right * swordCastArt.blade.bounds.size.x);
            bend = Vector2.Lerp(heel, tip, 0.5f);
            return true;
        }
    }
}
