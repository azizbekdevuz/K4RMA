using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaSwordBasicArt swordBasicArt;
        SpriteRenderer swordBasicBlade;
        int swordBasicDrawing = -1;
        int swordBasicWeightPhase;
        const float BasicDowncutPlaybackRate = 2f;

        void ConfigureSwordBasicArt()
        {
            swordBasicArt = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/" + motionForm);
            if (swordBasicArt == null || swordBasicArt.idle == null || swordBasicArt.blade == null
                || swordBasicArt.poses.Length != 6 || swordBasicArt.sockets.Length != 6 || swordBasicArt.angles.Length != 6)
                throw new System.InvalidOperationException("Missing SwordWave basic artwork: " + motionForm);
            var node = new GameObject("Basic rigid blade");
            node.transform.SetParent(picture.transform, false);
            swordBasicBlade = node.AddComponent<SpriteRenderer>();
            swordBasicBlade.sprite = swordBasicArt.blade;
            swordBasicBlade.enabled = false;
        }

        void DrawSwordBasicArt(EnemyAttack attack)
        {
            int frame;
            if (!SwordBodyReleased)
                frame = SwordBodyAge < KarmaBootstrap.Step ? 0
                    : SwordBodyAge < SwordFrameTime(SwordBodyDuration - KarmaBootstrap.Step) ? 1 : 2;
            else frame = BasicReleaseDrawing(BasicReleaseAge);
            displayForm = motionForm;
            displayMotion = SwordPatternMotion(attack);
            ShowSwordBasicDrawing(frame, BasicReleaseAge);
        }

        static int BasicReleaseDrawing(float elapsed)
        {
            float downcutSeconds = 2 * KarmaBootstrap.Step / BasicDowncutPlaybackRate;
            float drawingTime = elapsed < downcutSeconds ? elapsed * BasicDowncutPlaybackRate
                : elapsed + 2 * KarmaBootstrap.Step - downcutSeconds;
            int releaseFrame = Mathf.FloorToInt(drawingTime * KarmaBootstrap.FramesPerSecond + 0.001f);
            return releaseFrame < 4 ? Mathf.Min(3 + releaseFrame, 5) : -1;
        }

        internal void PresentFirstSwordDemonstration(KarmaGuardianGrowth growth)
        {
            if (!growthActive || growth == null || growth.Stage != 1 || motionForm != "Wave") return;
            if (growth.CurrentBeat is not (KarmaGuardianGrowth.Beat.Adapt or KarmaGuardianGrowth.Beat.Ready)) return;
            // GrowthPose starts its clock at the prepare/release transition. Reuse
            // the basic attack's planted body and rigid sword on that clock.
            float elapsed = Mathf.Max(0, Time.time - motionStarted);
            int frame = growth.CurrentBeat == KarmaGuardianGrowth.Beat.Ready ? -1
                : CurrentPose == "Growth/Wave" ? BasicReleaseDrawing(elapsed)
                : Mathf.Min(2, Mathf.FloorToInt(elapsed * KarmaBootstrap.FramesPerSecond));
            ShowSwordBasicDrawing(frame, elapsed);
        }

        void ShowSwordBasicDrawing(int frame, float elapsed)
        {
            swordBasicDrawing = frame;
            // Same planted load/drive/recovery as SW01. The repeated final
            // drawing settles its body before returning to the original Idle.
            swordBasicWeightPhase = frame == 5 && elapsed >= 2 * KarmaBootstrap.Step ? 7 : frame + 1;
            picture.sprite = displaySprite = frame >= 0
                ? swordCastWeight.BasicDrawing(frame, swordBasicWeightPhase) : swordBasicArt.idle;
            picture.transform.localScale = pictureScale;
            RegisterSwordAppearance();
        }

        void RegisterSwordBasicArt()
        {
            if (swordBasicArt == null || picture == null || swordBasicBlade == null) return;
            // The common sampler still owns its original damage registration.
            // Substitute only the SwordWave render sprite after that sampling.
            if (picture.sprite == stageIdle)
                picture.sprite = displaySprite = swordBasicArt.idle;
            bool visible = swordBasicDrawing >= 0 && swordBasicDrawing < swordBasicArt.poses.Length
                && swordCastWeight != null && swordCastWeight.MatchesBasic(picture.sprite, swordBasicDrawing)
                && deathAt < 0 && Time.time >= hurtUntil && sw02Owner != null && !sw02Owner.Stunned;
            swordBasicBlade.enabled = visible && picture.enabled;
            if (!visible) return;
            var sprite = picture.sprite;
            Vector2 pixel = swordBasicArt.sockets[swordBasicDrawing];
            Vector2 local = (new Vector2(pixel.x, sprite.rect.height - pixel.y) - sprite.pivot) / sprite.pixelsPerUnit;
            swordBasicBlade.transform.localPosition = swordCastWeight.BasicPoint(local, swordBasicWeightPhase);
            swordBasicBlade.transform.localRotation = Quaternion.Euler(0, 0,
                swordBasicArt.angles[swordBasicDrawing] + swordCastWeight.BasicAngle(swordBasicWeightPhase));
            float scale = swordBasicArt.bladeLength / swordBasicArt.blade.bounds.size.x;
            swordBasicBlade.transform.localScale = Vector3.one * scale;
            swordBasicBlade.sharedMaterial = picture.sharedMaterial;
            swordBasicBlade.color = picture.color;
            swordBasicBlade.sortingLayerID = picture.sortingLayerID;
            swordBasicBlade.sortingOrder = picture.sortingOrder - 1;
        }

        internal bool SwordBasicBladePoints(out Vector2 heel, out Vector2 bend, out Vector2 tip)
        {
            heel = bend = tip = default;
            if (swordBasicBlade == null || !swordBasicBlade.enabled) return false;
            heel = swordBasicBlade.transform.position;
            tip = swordBasicBlade.transform.TransformPoint(Vector3.right * swordBasicArt.blade.bounds.size.x);
            bend = Vector2.Lerp(heel, tip, 0.5f);
            return true;
        }
    }
}
