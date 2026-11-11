using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaPlayer
    {
        float controlVisualAt, controlRecoveryAt = -10;
        bool hadVisualControl;
        string recoveryPose = "StunRecovery";
        Vector2 previousVisualPosition;
        LineRenderer[] shockArcs;
        // Presentation clients can discard a reaction after another hit or room reset.
        // This avoids a dependency from the player onto a boss-specific effect.
        internal int ControlRevision { get; private set; }
        void BeginControlPresentation() { ControlRevision++; controlVisualAt = Time.time; animator?.ResetPlayerStatus(); animator?.CancelPlayerActionForControl(); if (art != null) art.localScale = new Vector3(Facing, 1, 1); }
        void ResetControlPresentation()
        {
            ControlRevision++;
            hadVisualControl = false; controlRecoveryAt = -10; animator?.ResetPlayerStatus();
            previousVisualPosition = Position;
            if (shockArcs != null) foreach (var arc in shockArcs) if (arc != null) arc.enabled = false;
        }
        bool PresentControlState(bool grounded)
        {
            bool locked = ControlLocked;
            float age = Mathf.Max(0, Time.time - controlVisualAt);
            float travel = Position.x - previousVisualPosition.x;
            previousVisualPosition = Position;
            UpdateShockArcs(Shocked);
            if (locked)
            {
                hadVisualControl = true;
                recoveryPose = Shocked ? "ShockRecovery" : KnockedBack ? "Recovery" : "StunRecovery";
                string pose;
                if (KnockedDown) pose = age < 8f / 24 ? "Knockdown" : "DownLoop";
                else if (GettingUp) { pose = "GetUp"; age = Mathf.Max(0, Time.time - knockdownUntil); }
                else if (Shocked) pose = age < 4f / 24 ? "ShockStart" : "ShockLoop";
                else if (!grounded && (KnockedAirborne || Stunned))
                    pose = body.linearVelocity.y > 2 ? "AirborneRise" : body.linearVelocity.y < -2 ? "AirborneFall" : "AirborneApex";
                else if (KnockedBack || Mathf.Abs(travel) > 0.005f) pose = grounded ? "Knockback" : "AirKnockback";
                else pose = age < 4f / 24 ? "StunStart" : "StunLoop";
                if (pose == "StunLoop" || pose == "ShockLoop") age = Mathf.Max(0, age - 4f / 24);
                if (pose == "DownLoop") age = Mathf.Max(0, age - 8f / 24);
                return animator.PresentPlayerStatus(pose, age, reverse: (pose == "Knockback" || pose == "AirKnockback")
                    && (Mathf.Abs(travel) > 0.005f ? travel : body.linearVelocity.x) * Facing > 0);
            }
            if (hadVisualControl) { hadVisualControl = false; controlRecoveryAt = Time.time; }
            if (grounded && Time.time - controlRecoveryAt < 0.25f && Mathf.Abs(move) < 0.01f)
                return animator.PresentPlayerStatus(recoveryPose, Time.time - controlRecoveryAt, recovery: true);
            return false;
        }
        void UpdateShockArcs(bool visible)
        {
            if (!visible && shockArcs == null) return;
            if (shockArcs == null)
            {
                shockArcs = new LineRenderer[4];
                for (int i = 0; i < shockArcs.Length; i++)
                {
                    var go = new GameObject("Player shock arc " + i); go.transform.SetParent(transform, false);
                    var arc = go.AddComponent<LineRenderer>(); shockArcs[i] = arc;
                    arc.useWorldSpace = false; arc.positionCount = 5;
                    arc.sharedMaterial = KarmaCombatVFX.GlowMaterial;
                    arc.widthMultiplier = 0.035f; arc.sortingOrder = 24;
                    arc.startColor = new Color(0.65f, 0.88f, 1, 0.9f); arc.endColor = new Color(1, 0.94f, 0.5f, 0.2f);
                }
            }
            int frame = Mathf.FloorToInt(Time.time * 24);
            for (int i = 0; i < shockArcs.Length; i++)
            {
                var arc = shockArcs[i]; arc.enabled = visible && (frame + i) % 4 != 0;
                if (!arc.enabled) continue;
                float angle = i * Mathf.PI * 0.5f;
                for (int j = 0; j < 5; j++)
                {
                    float radius = 0.5f + j * 0.16f;
                    float jitter = Mathf.Sin((frame * 13 + i * 7 + j * 11) * 1.7f) * 0.22f;
                    arc.SetPosition(j, new Vector3(Mathf.Cos(angle) * radius + jitter,
                        0.25f + Mathf.Sin(angle) * radius * 1.4f, 0));
                }
            }
        }
    }
}
