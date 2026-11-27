using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        KarmaSwordBasicArt swordGroundArt;
        KarmaSwordGroundPose swordGroundPose;
        SpriteRenderer swordGroundBlade;
        SpriteRenderer swordGroundShield, swordGroundStrap;
        Sprite swordGroundShieldFront;
        int swordGroundDrawing = -1;

        // Strap centres on the off-hand forearm, in GroundPlunge cell pixels.
        // Slot order is setup, grip, apex, descent, plant, recovery, then three lift poses.
        static readonly Vector3[] GroundShieldStraps = {
            new(329, 194, 6), new(329, 185, 34), new(336, 154, 58),
            new(185, 169, -49), new(164, 191, -62), new(191, 178, -58),
            new(329, 187, 47), new(323, 182, 58), new(331, 168, 63)
        };

        void ConfigureSwordGroundArt()
        {
            swordGroundArt = Resources.Load<KarmaSwordBasicArt>("SwordWave/Ground/" + motionForm);
            if (swordGroundArt == null || swordGroundArt.poses.Length != 9 || swordGroundArt.blade == null)
                throw new System.InvalidOperationException("Missing reverse-grip SW03 art: " + motionForm);
            var node = new GameObject("Ground reverse rigid blade");
            node.transform.SetParent(picture.transform, false);
            swordGroundBlade = node.AddComponent<SpriteRenderer>();
            swordGroundBlade.sprite = swordGroundArt.blade; swordGroundBlade.enabled = false;
            swordGroundPose = picture.gameObject.AddComponent<KarmaSwordGroundPose>();
            float bodyScale = SwordInheritedDrawingScale(swordGroundArt.poses[0].texture);
            swordGroundPose.Configure(swordGroundArt, picture, swordGroundArt.bladeLength / bodyScale);
            if (motionForm == "WaveFinal")
            {
                if (swordGroundArt.shieldInside == null || swordGroundArt.shieldStrap == null)
                    throw new System.InvalidOperationException("Missing WaveFinal SW03 shield interior or strap");
                swordGroundShieldFront = swordGroundPose.CutIdleShield(bodyScale);
                swordGroundShield = CreateShieldMantlePart(swordGroundShieldFront, picture.transform);
                swordGroundStrap = CreateShieldMantlePart(swordGroundArt.shieldStrap, swordGroundShield.transform);
            }
        }

        bool DrawSwordGroundArt(EnemyAttack attack)
        {
            if (swordGroundArt == null || attack != EnemyAttack.SW03 || SwordBodyAction != "Summon") return false;
            float step = KarmaBootstrap.Step;
            int frame;
            if (SwordBodyReleased) frame = SwordReleaseAge < SwordGroundPlantSeconds ? 4 : 5;
            else
            {
                float remaining = (1 - SwordGroundProgress) * SwordBodyDuration;
                // Raise and lower the sword during the windup. The plant starts the ground sequence.
                int lift = Mathf.FloorToInt(SwordBodyAge / step);
                frame = remaining <= step ? 3 : lift == 0 ? 0 : lift == 1 ? 1
                    : lift < 5 ? lift + 4 : 2;
            }
            ShowSwordGroundDrawing(frame);
            return true;
        }

        void ShowSwordGroundDrawing(int frame)
        {
            swordGroundDrawing = frame;
            displayForm = motionForm; displayMotion = "SW03";
            picture.sprite = displaySprite = swordGroundPose.Drawing(frame);
            picture.transform.localScale = pictureScale;
            RegisterSwordAppearance();
        }

        KarmaGuardianGrowth riseCeremony;
        Sprite risePreviousIdle;
        KarmaGuardianGrowth.Beat riseBeat;
        int riseAttempt;
        float riseBeatStarted;

        internal void PresentSwordRiseInheritance(KarmaGuardianGrowth growth)
        {
            if (!growthActive || growth.Stage < 2) return;
            var beat = growth.CurrentBeat;
            if (riseCeremony != growth)
                risePreviousIdle = Resources.Load<KarmaSwordBasicArt>("SwordWave/Basic/" + previousMotionForm).idle;
            if (riseCeremony != growth || riseBeat != beat || riseAttempt != growth.CurrentAttempt)
            {
                riseCeremony = growth;
                riseBeat = beat;
                riseAttempt = growth.CurrentAttempt;
                riseBeatStarted = growth.Age;
            }
            if (beat == KarmaGuardianGrowth.Beat.Ready)
            {
                ShowSwordBasicDrawing(-1, 0);
                picture.transform.localPosition = pictureHome;
                return;
            }
            if (growth.Stage == 3)
            {
                if (picture.sprite != null && picture.sprite.texture.name == "GrowthMastery")
                    picture.transform.localScale = pictureScale * (SwordStandingHeight(motionForm) / 2.58f);
                return;
            }
            int tick = Mathf.FloorToInt((growth.Age - riseBeatStarted) * KarmaBootstrap.FramesPerSecond + 0.001f);
            if (beat == KarmaGuardianGrowth.Beat.Observe || (beat == KarmaGuardianGrowth.Beat.Revive && CurrentPose == "Growth/Revive/5"))
            {
                picture.sprite = displaySprite = risePreviousIdle;
                RegisterSwordAppearance();
            }
            else if (beat == KarmaGuardianGrowth.Beat.Adapt)
            {
                // Demonstrate the acquired technique with its actual body, hand sockets and rigid blade.
                int frame = tick < 2 ? tick : tick < 5 ? tick + 4 : tick < 8 ? 2 : tick == 8 ? 3 : tick < 15 ? 4 : 5;
                ShowSwordGroundDrawing(frame);
                picture.transform.localPosition = pictureHome;
            }
            else if (beat == KarmaGuardianGrowth.Beat.Imitate ||
                (beat == KarmaGuardianGrowth.Beat.Inspect && CurrentPose == "Growth/InspectWeapon"))
            {
                string motion = beat == KarmaGuardianGrowth.Beat.Inspect ? "Guard" : tick < 6 ? "Windup" : "FollowThrough";
                var frames = motionArt.Frames(previousMotionForm, motion);
                int frame = beat == KarmaGuardianGrowth.Beat.Inspect ? 0 : Mathf.Min(frames.Length - 1, tick < 6 ? tick : tick - 6);
                picture.sprite = displaySprite = frames[frame];
                RegisterSwordAppearance();
                picture.transform.localPosition = pictureHome;
            }
            else if (beat == KarmaGuardianGrowth.Beat.Stagger || beat == KarmaGuardianGrowth.Beat.Falter)
                RegisterSwordGrowthFall(beat);
        }
        void RegisterSwordGroundArt()
        {
            if (picture == null || swordGroundBlade == null) return;
            bool visible = swordGroundDrawing >= 0 && swordGroundDrawing < swordGroundArt.poses.Length
                && swordGroundPose.Matches(picture.sprite, swordGroundDrawing)
                && deathAt < 0 && Time.time >= hurtUntil && sw02Owner != null && !sw02Owner.Stunned;
            swordGroundBlade.enabled = visible && picture.enabled;
            if (swordGroundShield != null) RegisterGroundShield(swordGroundBlade.enabled);
            if (!visible) return;
            swordGroundBlade.transform.localPosition = swordGroundPose.Grip(swordGroundDrawing);
            swordGroundBlade.transform.localRotation = Quaternion.Euler(0, 0, swordGroundArt.angles[swordGroundDrawing]);
            swordGroundBlade.transform.localScale = Vector3.one * (swordGroundPose.BladeLength / swordGroundArt.blade.bounds.size.x);
            swordGroundBlade.sharedMaterial = picture.sharedMaterial; swordGroundBlade.color = picture.color;
            // On the equipped final pose the sword hand lifts in front of the shoulder and mantle.
            bool behind = swordGroundShield == null && (swordGroundDrawing == 2 || swordGroundDrawing == 8
                || swordGroundDrawing == 7 && swordGroundArt.angles[swordGroundDrawing] < -100);
            swordGroundBlade.sortingLayerID = picture.sortingLayerID;
            swordGroundBlade.sortingOrder = picture.sortingOrder + (behind ? -1 : swordGroundShield != null ? 2 : 1);
        }
        void RegisterGroundShield(bool visible)
        {
            bool armBack = swordGroundDrawing >= 3 && swordGroundDrawing <= 5;
            swordGroundShield.enabled = visible;
            swordGroundStrap.enabled = visible && armBack;
            if (!visible) return;

            var anchor = GroundShieldStraps[swordGroundDrawing];
            var body = swordGroundArt.poses[swordGroundDrawing];
            swordGroundShield.transform.localPosition = ShieldLocalPoint(body, (Vector2)anchor);
            swordGroundShield.transform.localRotation = Quaternion.Euler(0, 0, anchor.z);
            swordGroundShield.sprite = armBack ? swordGroundArt.shieldInside : swordGroundShieldFront;
            swordGroundShield.color = swordGroundStrap.color = picture.color;
            swordGroundShield.sharedMaterial = swordGroundStrap.sharedMaterial = picture.sharedMaterial;
            swordGroundShield.sortingLayerID = swordGroundStrap.sortingLayerID = picture.sortingLayerID;
            // The forearm sits against the inside of the shield and under the leather strap.
            swordGroundShield.sortingOrder = picture.sortingOrder + (armBack ? -1 : 1);
            swordGroundStrap.sortingOrder = picture.sortingOrder + 1;
        }
        internal bool SwordGroundBladePoints(out Vector2 heel, out Vector2 bend, out Vector2 tip)
        {
            heel = bend = tip = default;
            if (swordGroundBlade == null || !swordGroundBlade.enabled) return false;
            heel = swordGroundBlade.transform.position;
            tip = swordGroundBlade.transform.TransformPoint(Vector3.right * swordGroundArt.blade.bounds.size.x);
            bend = Vector2.Lerp(heel, tip, 0.5f); return true;
        }
    }
}
