using UnityEngine;

namespace K4RMA
{
    /// <summary>
    /// Close cut that crosses one contacted body. Not SwordWave, not Deflect, and not a free dash.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PlayerPiercingSlash : MonoBehaviour
    {
        enum Motion
        {
            Idle,
            Seek,
            Cross
        }

        PlayerTuning tuning;
        PlayerController motor;
        Health health;
        Rigidbody body;
        BodyTint tint;
        LayerMask targetLayers;
        Motion motion;
        float cooldown;
        float activeRemaining;
        float travelSpeed;
        float goalX;
        float approachX;
        float elapsed;
        float planned = 1f;
        int travelFacing = 1;
        bool targetClaimed;
        bool exitFound;
        float struckCenter;
        float struckHalf;
        Collider playerBody;
        Collider phasedBody;
        Transform trail;
        Material trailMaterial;
        Vector3 trailFrom;

        public bool IsActive => motion != Motion.Idle;
        public float Swing01 => planned <= 0.01f ? 0f : Mathf.Clamp01(elapsed / planned);

        public void Bind(MeleeHitbox basicMelee)
        {
            if (motor == null)
                motor = GetComponent<PlayerController>();
            if (tuning == null && motor != null)
                tuning = motor.Tuning;
            if (health == null)
                health = GetComponent<Health>();
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (tint == null)
                tint = GetComponent<BodyTint>();
            if (basicMelee != null)
                targetLayers = basicMelee.TargetLayers;
        }

        void Awake()
        {
            Bind(null);
        }

        void OnDisable()
        {
            Cancel(false);
            cooldown = 0f;
        }

        void Update()
        {
            if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;
                if (cooldown < 0f)
                    cooldown = 0f;
            }

            if (motion == Motion.Idle)
                return;
            if (health != null && !health.IsAlive)
            {
                Cancel(true);
                return;
            }

            if (tuning == null || !tuning.previewPiercingSlash)
                Cancel(true);
        }

        public bool TryActivate()
        {
            if (tuning == null || motor == null || body == null || !tuning.previewPiercingSlash)
                return false;

            bool alive = health == null || health.IsAlive;
            if (!PiercingSlashRules.CanActivate(alive, motion != Motion.Idle, cooldown))
                return false;

            travelFacing = motor.Facing >= 0 ? 1 : -1;
            targetClaimed = false;
            exitFound = false;
            struckHalf = 0f;
            elapsed = 0f;
            planned = Mathf.Max(0.05f, tuning.piercingSlashSeekSeconds);
            activeRemaining = planned;
            trailFrom = transform.position;
            Arena(out float left, out float right);
            goalX = PiercingSlashRules.MissEndX(
                body.position.x,
                travelFacing,
                tuning.piercingSlashMissDistance,
                motor.BodyRadius,
                left,
                right);
            float distance = Mathf.Abs(goalX - body.position.x);
            travelSpeed = distance / planned;
            motion = Motion.Seek;
            cooldown = Mathf.Max(0f, tuning.piercingSlashCooldownSeconds);
            tint?.Flash(tuning.piercingSlashColor, planned + tuning.piercingSlashCrossSeconds);
            AudioFeedback.Play(AudioCue.PiercingSlash);
            EnsureTrail();
            return true;
        }

        void FixedUpdate()
        {
            if (motion == Motion.Idle || body == null || tuning == null)
                return;
            if (health != null && !health.IsAlive)
            {
                Cancel(true);
                return;
            }

            if (motor != null && motor.InKnockback)
            {
                Cancel(true);
                return;
            }

            float dt = Mathf.Min(Time.fixedDeltaTime, 0.05f);
            if (motion == Motion.Seek && !targetClaimed && TryFindTarget(out Collider hitBody, out Health target, out float centerX, out float halfWidth))
                BeginCross(hitBody, target, centerX, halfWidth);

            activeRemaining -= dt;
            elapsed += dt;
            float next = Mathf.MoveTowards(body.position.x, goalX, travelSpeed * dt);
            ApplyMotion(next, true);
            UpdateTrail();

            bool arrived = Mathf.Abs(body.position.x - goalX) <= 0.03f;
            if (activeRemaining > 0f && !arrived)
                return;

            if (motion == Motion.Cross)
                FinishCross();
            else
                Finish();
        }

        void BeginCross(Collider hitBody, Health target, float centerX, float halfWidth)
        {
            IgnoreBody(hitBody);
            Arena(out float left, out float right);
            approachX = body.position.x;
            float pad = motor != null ? motor.BodyRadius : 0.45f;
            PiercingSlashRules.Sides(
                approachX,
                travelFacing,
                centerX,
                halfWidth,
                pad,
                tuning.piercingSlashClearance,
                left,
                right,
                out float farX,
                out bool farFits,
                out float nearX,
                out bool nearFits);
            var capsule = PlayerCapsule();
            goalX = PiercingSlashClearance.ChooseExit(
                capsule,
                body.position,
                farX,
                farFits,
                nearX,
                nearFits,
                hitBody,
                out bool found,
                out _);
            exitFound = found;
            if (!found)
                goalX = Mathf.Clamp(goalX, left + pad, right - pad);
            struckCenter = centerX;
            struckHalf = halfWidth;

            if (target.ApplyDamage(tuning.piercingSlashDamage, gameObject))
            {
                float sign = Mathf.Sign(target.transform.position.x - goalX);
                if (Mathf.Abs(sign) < 0.01f)
                    sign = -travelFacing;
                target.GetComponentInParent<IKnockback>()?.ApplyKnockback(
                    sign * tuning.piercingSlashKnockback,
                    tuning.piercingSlashKnockbackSeconds);
                target.GetComponentInParent<BodyTint>()?.Flash(Color.white, 0.08f);
                ImpactFeedback.PlayHit(target.transform.position + Vector3.up * 0.4f, target.GetComponent<PlayerController>() != null);
                SparkBurst.Play(target.transform.position + Vector3.up * 0.8f, tuning.piercingSlashColor);
            }

            motion = Motion.Cross;
            float crossTime = Mathf.Max(0.05f, tuning.piercingSlashCrossSeconds);
            activeRemaining = crossTime;
            planned = Mathf.Max(planned, elapsed + crossTime);
            float distance = Mathf.Abs(goalX - body.position.x);
            travelSpeed = distance / crossTime;
        }

        bool TryFindTarget(out Collider hitBody, out Health target, out float centerX, out float halfWidth)
        {
            hitBody = null;
            target = null;
            centerX = 0f;
            halfWidth = 0f;
            Vector3 center = transform.position
                + Vector3.right * (travelFacing * tuning.piercingSlashProbeForward)
                + Vector3.up * 0.15f;
            Vector3 half = tuning.piercingSlashProbeSize * 0.5f;
            if (half.x <= 0f || half.y <= 0f || half.z <= 0f)
                return false;

            var overlaps = Physics.OverlapBox(
                center,
                half,
                Quaternion.identity,
                targetLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                var col = overlaps[i];
                if (col == null || col.transform.root == transform.root)
                    continue;

                var hp = col.GetComponentInParent<Health>();
                if (hp == null || !hp.IsAlive)
                    continue;

                if (!PiercingSlashRules.TryClaimTarget(targetClaimed))
                    continue;

                targetClaimed = true;
                hitBody = col;
                target = hp;
                centerX = col.bounds.center.x;
                halfWidth = col.bounds.extents.x;
                return true;
            }

            return false;
        }

        void ApplyMotion(float x, bool holdHorizontal)
        {
            Arena(out float left, out float right);
            float pad = motor != null ? motor.BodyRadius : 0.45f;
            float min = left + pad;
            float max = right - pad;
            if (min <= max)
                x = Mathf.Clamp(x, min, max);
            var capsule = PlayerCapsule();
            if (capsule != null && !PiercingSlashClearance.TryMove(capsule, body.position, x, phasedBody, out float safeX))
            {
                x = min <= max ? Mathf.Clamp(safeX, min, max) : safeX;
                goalX = x;
                exitFound = false;
                activeRemaining = 0f;
            }
            var position = body.position;
            position.x = x;
            position.z = 0f;
            body.position = position;
            if (!holdHorizontal)
                return;
            var velocity = body.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        void FinishCross()
        {
            if (OverlappingStruck())
                ApplyMotion(exitFound ? goalX : approachX, false);
            Finish();
        }

        bool OverlappingStruck()
        {
            if (struckHalf <= 0f)
                return false;
            float pad = motor != null ? motor.BodyRadius : 0.45f;
            float playerX = body != null ? body.position.x : transform.position.x;
            float separation = Mathf.Abs(playerX - struckCenter);
            return separation < struckHalf + pad;
        }

        void Finish()
        {
            motion = Motion.Idle;
            activeRemaining = 0f;
            travelSpeed = 0f;
            RestoreCollision();
            ClearTrail();
        }

        void Cancel(bool separate)
        {
            if (separate && motion == Motion.Cross && OverlappingStruck())
                ApplyMotion(exitFound ? goalX : approachX, false);
            motion = Motion.Idle;
            activeRemaining = 0f;
            travelSpeed = 0f;
            RestoreCollision();
            ClearTrail();
        }

        void IgnoreBody(Collider other)
        {
            CachePlayerBody();
            if (playerBody == null || other == null || other.isTrigger)
                return;
            Physics.IgnoreCollision(playerBody, other, true);
            phasedBody = other;
        }

        void RestoreCollision()
        {
            CachePlayerBody();
            if (playerBody != null && phasedBody != null)
                Physics.IgnoreCollision(playerBody, phasedBody, false);
            phasedBody = null;
        }

        void CachePlayerBody()
        {
            if (playerBody == null)
                playerBody = GetComponent<CapsuleCollider>();
        }

        CapsuleCollider PlayerCapsule()
        {
            CachePlayerBody();
            return playerBody as CapsuleCollider;
        }

        void Arena(out float left, out float right)
        {
            var arena = motor != null ? motor.Arena : null;
            if (arena == null)
            {
                left = -1000f;
                right = 1000f;
                return;
            }

            left = arena.Left;
            right = arena.Right;
        }

        void EnsureTrail()
        {
            if (trail != null)
                return;

            var trailObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trailObject.name = "PiercingSlashTrail";
            Destroy(trailObject.GetComponent<Collider>());
            trail = trailObject.transform;
            var renderer = trailObject.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null && renderer != null)
            {
                trailMaterial = new Material(shader);
                var color = tuning != null ? tuning.piercingSlashColor : new Color(0.45f, 0.85f, 1f);
                trailMaterial.SetColor("_BaseColor", color);
                trailMaterial.EnableKeyword("_EMISSION");
                trailMaterial.SetColor("_EmissionColor", color * 1.6f);
                renderer.sharedMaterial = trailMaterial;
            }
        }

        void UpdateTrail()
        {
            if (trail == null)
                return;
            Vector3 start = trailFrom + Vector3.up * 0.85f;
            Vector3 end = transform.position + Vector3.up * 0.85f;
            Vector3 delta = end - start;
            float length = delta.magnitude;
            trail.position = (start + end) * 0.5f;
            trail.localScale = new Vector3(Mathf.Max(length, 0.05f), 0.07f, 0.07f);
            if (length > 0.001f)
                trail.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        void ClearTrail()
        {
            if (trail != null)
                Destroy(trail.gameObject);
            trail = null;
            if (trailMaterial != null)
                Destroy(trailMaterial);
            trailMaterial = null;
        }
    }
}
