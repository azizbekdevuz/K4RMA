using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaCharacterAnimator
    {
        bool risingFarewell, farewellTurns;
        int farewellEntry;
        float farewellEntryFloor;
        Color farewellEntryColor;

        internal bool BeginRisingFarewell(float playerX)
        {
            if (motionForm != "RiseFinal" || picture == null) return false;

            // Close-range defeats can finish the fall while the player steps clear.
            var fall = motionArt.Frames(motionForm, "Death");
            farewellEntry = Mathf.Max(0, System.Array.IndexOf(fall, picture.sprite));
            farewellEntryFloor = FarewellFloor(fall[farewellEntry], FallSole[farewellEntry]);
            farewellEntryColor = picture.color;
            farewellTurns = (playerX - transform.position.x) * picture.transform.lossyScale.x < -0.001f;
            risingFarewell = true;
            return true;
        }

        // Sole contact in top-down atlas pixels. Cloth and blade tips can extend below it.
        static readonly int[] FallSole = { 651, 651, 608, 610 };
        const int KneelSole = 622, LookSole = 628, TurnSole = 630;

        float FarewellFloor(Sprite sprite, int sole)
        {
            float localY = (sprite.rect.height * (1f - sole / 768f) - sprite.pivot.y) / sprite.pixelsPerUnit;
            return picture.transform.TransformPoint(new Vector3(0, localY, 0)).y;
        }

        void DrawRisingFarewell(float elapsed)
        {
            int tick = Mathf.FloorToInt(elapsed * KarmaBootstrap.FramesPerSecond + 0.0001f);
            string motion = "FinalDefeat";
            int frame, sole;
            if (tick < 4)
            {
                motion = "Death";
                frame = tick < 2 ? farewellEntry : Mathf.Clamp(farewellEntry, 1, 2);
                sole = FallSole[frame];
            }
            else if (farewellTurns && tick >= 8 && tick < 14)
            {
                // Lift the back knee, pivot on the feet, then lower it on the new side.
                motion = "DefeatTurn";
                frame = tick - 8;
                sole = TurnSole;
            }
            else
            {
                frame = tick < 6 || tick >= 36 ? 2 : 3;
                sole = frame == 2 ? KneelSole : LookSole;
            }
            DrawMotion(motionForm, motion, frame * KarmaBootstrap.Step);
            // The last turn drawing already faces the player. Match its facing as we
            // return to the original profile, then hold the original bowed head in the dissolve.
            picture.flipX = farewellTurns && tick >= 14;
            float contact = FarewellFloor(picture.sprite, sole);
            float handoff = farewellEntryFloor * (1f - Mathf.SmoothStep(0, 1, elapsed / (2 * KarmaBootstrap.Step)));
            picture.transform.position += Vector3.up * (handoff - contact);
        }
    }
}
