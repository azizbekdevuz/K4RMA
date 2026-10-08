using UnityEngine;

namespace K4RMA
{
    /// <summary>
    /// Sweeps the player capsule through solid environment colliders.
    /// The contacted guardian is ignored for that one route. Walls are not.
    /// </summary>
    public static class PiercingSlashClearance
    {
        const float SurfaceGap = 0.05f;

        public static float ChooseExit(
            CapsuleCollider body,
            Vector3 from,
            float farX,
            bool farFits,
            float nearX,
            bool nearFits,
            Collider ignoreTarget,
            out bool found,
            out bool crossed)
        {
            if (farFits && Reaches(body, from, farX, ignoreTarget))
            {
                found = true;
                crossed = true;
                return farX;
            }

            if (nearFits && Reaches(body, from, nearX, ignoreTarget))
            {
                found = true;
                crossed = false;
                return nearX;
            }

            found = false;
            crossed = false;
            float preferred = farFits ? farX : nearFits ? nearX : from.x;
            TryMove(body, from, preferred, ignoreTarget, out float safeX);
            return safeX;
        }

        public static bool TryMove(CapsuleCollider body, Vector3 from, float toX, Collider ignoreTarget, out float safeX)
        {
            safeX = from.x;
            if (body == null)
                return false;

            float delta = toX - from.x;
            float distance = delta < 0f ? -delta : delta;
            if (distance <= 0.001f)
            {
                safeX = toX;
                return !BlockedAt(body, from, ignoreTarget);
            }

            int direction = delta >= 0f ? 1 : -1;
            WorldCapsule(body, from, out Vector3 pointA, out Vector3 pointB, out float radius);
            var hits = Physics.CapsuleCastAll(
                pointA,
                pointB,
                radius,
                Vector3.right * direction,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float feet = FeetY(body, from);
            float head = HeadY(body, from);
            float blockedAt = distance;
            bool blocked = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (!IsEnvironment(hit.collider, body, ignoreTarget, feet, head))
                    continue;
                if (hit.normal.y > 0.65f && hit.point.y <= feet + 0.12f)
                    continue;
                if (hit.distance >= blockedAt)
                    continue;
                blocked = true;
                blockedAt = hit.distance;
            }

            if (!blocked)
            {
                var destination = from;
                destination.x = toX;
                if (BlockedAt(body, destination, ignoreTarget))
                    return false;
                safeX = toX;
                return true;
            }

            float travel = blockedAt - SurfaceGap;
            if (travel < 0f)
                travel = 0f;
            safeX = from.x + direction * travel;
            return false;
        }

        static bool Reaches(CapsuleCollider body, Vector3 from, float toX, Collider ignoreTarget)
        {
            if (!TryMove(body, from, toX, ignoreTarget, out float safeX))
                return false;
            float error = safeX - toX;
            if (error < 0f)
                error = -error;
            return error <= 0.02f;
        }

        static bool BlockedAt(CapsuleCollider body, Vector3 position, Collider ignoreTarget)
        {
            WorldCapsule(body, position, out Vector3 pointA, out Vector3 pointB, out float radius);
            var overlaps = Physics.OverlapCapsule(
                pointA,
                pointB,
                radius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float feet = FeetY(body, position);
            float head = HeadY(body, position);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (IsEnvironment(overlaps[i], body, ignoreTarget, feet, head))
                    return true;
            }

            return false;
        }

        static bool IsEnvironment(Collider other, CapsuleCollider self, Collider ignoreTarget, float feet, float head)
        {
            if (other == null || other.isTrigger || self == null)
                return false;
            if (other == self || other.transform.root == self.transform.root)
                return false;
            if (ignoreTarget != null && (other == ignoreTarget || other.transform.root == ignoreTarget.transform.root))
                return false;

            var bounds = other.bounds;
            if (bounds.max.y <= feet + 0.08f)
                return false;
            if (bounds.min.y >= head - 0.02f)
                return false;
            return true;
        }

        static float FeetY(CapsuleCollider body, Vector3 position)
        {
            return position.y - body.height * 0.5f * Mathf.Abs(body.transform.lossyScale.y);
        }

        static float HeadY(CapsuleCollider body, Vector3 position)
        {
            return position.y + body.height * 0.5f * Mathf.Abs(body.transform.lossyScale.y);
        }

        static void WorldCapsule(CapsuleCollider capsule, Vector3 position, out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            Vector3 scale = capsule.transform.lossyScale;
            int direction = capsule.direction;
            float axisScale = direction == 0
                ? Mathf.Abs(scale.x)
                : direction == 2 ? Mathf.Abs(scale.z) : Mathf.Abs(scale.y);
            float radialScale = direction == 0
                ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                : direction == 2
                    ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y))
                    : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            radius = capsule.radius * radialScale;
            float height = capsule.height * axisScale;
            if (height < radius * 2f)
                height = radius * 2f;
            float half = height * 0.5f - radius;
            if (half < 0f)
                half = 0f;
            Vector3 axis = direction == 0 ? Vector3.right : direction == 2 ? Vector3.forward : Vector3.up;
            Vector3 center = position + Vector3.Scale(capsule.center, scale);
            pointA = center + axis * half;
            pointB = center - axis * half;
        }
    }
}
