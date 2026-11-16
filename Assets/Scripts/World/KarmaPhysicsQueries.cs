using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Same default layer/trigger policy as *CastAll, with caller-owned storage.
    // Lists grow when needed, so crowded scenes cannot silently truncate hits.
    public static class KarmaPhysicsQueries
    {
        public static ContactFilter2D DefaultFilter => new ContactFilter2D
        {
            useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers,
            useTriggers = Physics2D.queriesHitTriggers
        };

        public static int BoxCast(Vector2 origin, Vector2 size, Vector2 direction,
            float distance, List<RaycastHit2D> results)
            => Physics2D.BoxCast(origin, size, 0, direction, DefaultFilter, results, distance);

        public static int Raycast(Vector2 origin, Vector2 direction,
            float distance, List<RaycastHit2D> results)
            => Physics2D.Raycast(origin, direction, DefaultFilter, results, distance);
    }
}
