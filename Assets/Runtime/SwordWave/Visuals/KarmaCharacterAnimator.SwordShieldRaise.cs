using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        internal const string ShieldRaiseAction = "ShieldRaise";
        const int ShieldSnapFrame = 7;
        Sprite[] shieldRaiseFrames;
        LineRenderer shieldRim;
        SpriteRenderer shieldMantlePlate, shieldMantleCloth;
        int shieldRaiseFrame, rimDrawing = -1;

        // Sword-side shoulder in each shared drawing (top-left pixels), and its rotation in degrees.
        static readonly Vector3[] ShieldShoulder = {
            new(210,151,0), new(222,177,-4), new(240,214,-9),
            new(223,192,-6), new(222,175,-3), new(209,159,2),
            new(207,150,3), new(203,149,0), new(199,144,0)
        };

        // Leading edge of the buckler, in top-left cell pixels, from the lift to the snap.
        static readonly Vector2[,] ShieldRimPixels = {
            { new(323,205), new(331,218), new(333,235), new(327,255), new(310,273) },
            { new(323,138), new(344,150), new(364,170), new(378,190), new(389,216) },
            { new(342,83),  new(367,97),  new(385,115), new(397,135), new(401,155) },
            { new(341,91),  new(364,103), new(388,125), new(400,145), new(407,170) },
            { new(372,105), new(384,120), new(395,145), new(401,175), new(398,208) }
        };

        void ConfigureSwordShieldRaise()
        {
            if (!SwordCarriesShield) return;
            shieldRaiseFrames = KarmaArtResource.Frames("SwordWave/WaveWardShieldRaise");
            if (shieldRaiseFrames.Length != 9)
                throw new System.InvalidOperationException("Missing shield raise drawings for " + motionForm);
            if (motionForm == "WaveFinal")
            {
                var mantle = KarmaArtResource.Frames("SwordWave/WaveFinalShieldMantle");
                if (mantle.Length != 2)
                    throw new System.InvalidOperationException("Missing WaveFinal shield raise mantle");
                shieldMantlePlate = CreateShieldMantlePart(mantle[0], picture.transform);
                shieldMantleCloth = CreateShieldMantlePart(mantle[1], shieldMantlePlate.transform);
            }
            var rim = new GameObject("Shield raise rim");
            rim.transform.SetParent(picture.transform, false);
            shieldRim = rim.AddComponent<LineRenderer>();
            shieldRim.enabled = false;
            shieldRim.useWorldSpace = false;
            shieldRim.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            shieldRim.positionCount = ShieldRimPixels.GetLength(1);
            shieldRim.numCornerVertices = 3;
            shieldRim.startWidth = 0.018f;
            shieldRim.endWidth = 0.004f;
            shieldRim.startColor = new Color(0.68f, 0.94f, 1f, 0.95f);
            shieldRim.endColor = new Color(0.12f, 0.65f, 1f, 0.25f);
        }

        internal void PrepareSwordShieldRaise(float warning)
        {
            PrepareSwordBody(shieldRaiseFrames != null ? ShieldRaiseAction : "Counter", warning);
        }

        internal Vector2? ActivateSwordShieldRaise()
        {
            if (SwordBodyAction != ShieldRaiseAction) return null;
            ReleaseSwordBody(ShieldRaiseAction);
            if (picture.sprite != shieldRaiseFrames[ShieldSnapFrame]) return null;
            return picture.transform.TransformPoint(ShieldLocalPoint(picture.sprite, ShieldRimPixels[4, 2]));
        }

        void DrawSwordShieldRaise()
        {
            if (SwordBodyReleased)
                shieldRaiseFrame = SwordReleaseAge < KarmaBootstrap.Step ? ShieldSnapFrame : ShieldSnapFrame + 1;
            else
            {
                // Sample the lift at 12 FPS within the actual warning. The last two
                // poses belong to shield activation and recovery, even at a faster tempo.
                int lastBeat = Mathf.Max(1, Mathf.CeilToInt(SwordBodyDuration * KarmaBootstrap.FramesPerSecond) - 1);
                // A short warning can end between render frames. Sample its nearest
                // 12 FPS pose so the raised shield is visible before the actual snap.
                float sampleTime = lastBeat < ShieldSnapFrame ? Time.time - swordBodyAt : SwordBodyAge;
                int beat = Mathf.RoundToInt(sampleTime * KarmaBootstrap.FramesPerSecond);
                shieldRaiseFrame = Mathf.RoundToInt((ShieldSnapFrame - 1) * Mathf.Clamp01((float)beat / lastBeat));
            }
            displayForm = motionForm;
            displayMotion = "SW04";
            picture.sprite = displaySprite = shieldRaiseFrames[shieldRaiseFrame];
            RegisterSwordAppearance();
        }

        // Contact points on the two boots (boot and knee on the collapsed drawing),
        // in top-left cell pixels. Either side can carry the weight during the lean.
        static readonly Vector4[] StumbleContacts = {
            new(360, 708, 650, 703), new(220, 708, 530, 688), new(310, 708, 656, 704)
        };
        static readonly Vector4[] FalterContacts = {
            new(325, 710, 620, 711), new(281, 709, 590, 708), new(265, 707, 405, 710)
        };

        internal void PresentSwordGuardInheritance(KarmaGuardianGrowth growth)
        {
            if (!growthActive || !SwordCarriesShield) return;
            var beat = growth.CurrentBeat;
            if (beat == KarmaGuardianGrowth.Beat.Ready)
            {
                ShowSwordBasicDrawing(-1, 0);
                picture.transform.localPosition = pictureHome;
                return;
            }
            if (growth.Stage == 3)
            {
                if (beat != KarmaGuardianGrowth.Beat.Revive)
                {
                    // The last standing mastery drawing measures 2.58 units from helmet to sole.
                    picture.transform.localScale = pictureScale * (SwordStandingHeight(motionForm) / 2.58f);
                }
                return;
            }
            if (beat == KarmaGuardianGrowth.Beat.Imitate ||
                (beat == KarmaGuardianGrowth.Beat.Inspect && CurrentPose == "Growth/InspectWeapon"))
            {
                // The rehearsal and the inspection use the previous Wave form's combat guard.
                RegisterSwordAppearance();
            }
            else if (beat == KarmaGuardianGrowth.Beat.Stagger || beat == KarmaGuardianGrowth.Beat.Falter)
                RegisterSwordGrowthFall(beat);
            else if (beat == KarmaGuardianGrowth.Beat.Adapt)
            {
                // GrowthPose starts the guard motion when the new form takes over.
                // Raise, hold, and lower the combat buckler on that same 12 FPS clock.
                int tick = Mathf.FloorToInt((Time.time - motionStarted) * KarmaBootstrap.FramesPerSecond + 0.001f);
                int last = shieldRaiseFrames.Length - 1;
                int frame = Mathf.Clamp(Mathf.Min(tick, 2 * last + 2 - tick), 0, last);
                picture.sprite = displaySprite = shieldRaiseFrames[frame];
                picture.transform.localScale = pictureScale;
                picture.transform.localPosition = pictureHome;
            }
        }

        static Vector2 ShieldLocalPoint(Sprite sprite, Vector2 pixel)
            => (new Vector2(pixel.x, sprite.rect.height - pixel.y) - sprite.pivot) / sprite.pixelsPerUnit;

        void RegisterSwordGrowthFall(KarmaGuardianGrowth.Beat beat)
        {
            bool stumble = beat == KarmaGuardianGrowth.Beat.Stagger;
            var frames = motionArt.Frames(previousMotionForm, stumble ? "GrowthStumble" : "GrowthFalter");
            int frame = System.Array.IndexOf(frames, picture.sprite);
            if (frame < 0) return;
            Vector4 contacts = (stumble ? StumbleContacts : FalterContacts)[frame];
            var drawing = picture.transform;
            float left = drawing.TransformPoint(ShieldLocalPoint(picture.sprite, new Vector2(contacts.x, contacts.y))).y;
            float right = drawing.TransformPoint(ShieldLocalPoint(picture.sprite, new Vector2(contacts.z, contacts.w))).y;
            float floor = drawing.parent.TransformPoint(pictureHome).y;
            drawing.position += Vector3.up * (floor - Mathf.Min(left, right));
        }

        SpriteRenderer CreateShieldMantlePart(Sprite sprite, Transform parent)
        {
            var part = new GameObject(sprite.name).AddComponent<SpriteRenderer>();
            part.transform.SetParent(parent, false);
            part.sprite = sprite;
            part.sharedMaterial = picture.sharedMaterial;
            part.enabled = false;
            return part;
        }

        void HideSwordShieldRaise()
        {
            if (shieldRim != null) shieldRim.enabled = false;
            if (shieldMantlePlate == null) return;
            shieldMantlePlate.enabled = shieldMantleCloth.enabled = false;
        }

        void RegisterSwordShieldRaise()
        {
            if (shieldRim == null) return;
            bool visible = SwordBodyAction == ShieldRaiseAction && picture.enabled && deathAt < 0
                && Time.time >= hurtUntil && picture.sprite == shieldRaiseFrames[shieldRaiseFrame];
            if (shieldMantlePlate != null)
            {
                shieldMantlePlate.enabled = shieldMantleCloth.enabled = visible;
                if (visible)
                {
                    var shoulder = ShieldShoulder[shieldRaiseFrame];
                    shieldMantlePlate.transform.localPosition = ShieldLocalPoint(picture.sprite, shoulder);
                    shieldMantlePlate.transform.localRotation = Quaternion.Euler(0, 0, shoulder.z);
                    shieldMantlePlate.color = shieldMantleCloth.color = picture.color;
                    shieldMantlePlate.sortingLayerID = shieldMantleCloth.sortingLayerID = picture.sortingLayerID;
                    shieldMantlePlate.sortingOrder = picture.sortingOrder + 1;
                    shieldMantleCloth.sortingOrder = picture.sortingOrder - 1;
                }
            }
            shieldRim.enabled = visible && shieldRaiseFrame >= 3 && shieldRaiseFrame <= ShieldSnapFrame;
            if (!shieldRim.enabled || rimDrawing == shieldRaiseFrame) return;
            rimDrawing = shieldRaiseFrame;
            shieldRim.sortingLayerID = picture.sortingLayerID;
            shieldRim.sortingOrder = picture.sortingOrder + 1;
            for (int i = 0; i < shieldRim.positionCount; i++)
                shieldRim.SetPosition(i, ShieldLocalPoint(picture.sprite, ShieldRimPixels[rimDrawing - 3, i]));
        }
    }
}
