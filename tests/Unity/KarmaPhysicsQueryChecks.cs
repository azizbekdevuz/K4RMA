#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using KarmaPrototype;
using UnityEngine;

public static class KarmaPhysicsQueryChecks
{
    public static void Run(KarmaPlayer player, Action<bool, string> check)
    {
        var root = new GameObject("Physics query equivalence fixture");
        var origin = new Vector2(2000, 2000);
        var hits = new List<RaycastHit2D>(8);
        bool triggers = Physics2D.queriesHitTriggers;
        bool inside = Physics2D.queriesStartInColliders;
        try
        {
            for (int i = 0; i < 40; i++)
            {
                var go = new GameObject("Obstacle " + i);
                go.transform.SetParent(root.transform);
                go.transform.position = origin + Vector2.right * (i + 1);
                go.layer = i % 7 == 0 ? 2 : 0; // Ignore Raycast must still be excluded.
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one * 0.5f;
                collider.isTrigger = i % 3 == 0;
            }
            Physics2D.SyncTransforms();
            foreach (bool queryTriggers in new[] { false, true })
            foreach (bool queryInside in new[] { false, true })
            {
                Physics2D.queriesHitTriggers = queryTriggers;
                Physics2D.queriesStartInColliders = queryInside;
                foreach (var from in new[] { origin, origin + Vector2.right * 2 })
                {
                    var expected = Physics2D.BoxCastAll(from, Vector2.one, 0, Vector2.right, 45);
                    KarmaPhysicsQueries.BoxCast(from, Vector2.one, Vector2.right, 45, hits);
                    Equal(expected, hits, check);
                    check(hits.Count > 8, "Dense box results grow beyond initial capacity");
                    expected = Physics2D.RaycastAll(from, Vector2.right, 45);
                    KarmaPhysicsQueries.Raycast(from, Vector2.right, 45, hits);
                    Equal(expected, hits, check);
                }
            }
            KarmaPhysicsQueries.BoxCast(origin, Vector2.one, Vector2.left, 1, hits);
            check(hits.Count == 0, "Empty box query removes prior hits");
            KarmaPhysicsQueries.Raycast(origin, Vector2.left, 1, hits);
            check(hits.Count == 0, "Empty ray query removes prior hits");

            const int repeats = 512;
            // Warm both paths; exclude fixture creation, JIT and list growth.
            KarmaPhysicsQueries.BoxCast(origin, Vector2.one, Vector2.right, 45, hits);
            Physics2D.BoxCastAll(origin, Vector2.one, 0, Vector2.right, 45);
            KarmaPhysicsQueries.Raycast(origin, Vector2.right, 45, hits);
            Physics2D.RaycastAll(origin, Vector2.right, 45);
            check(KarmaAllocationChecks.Count(() => GC.KeepAlive(new byte[256])) > 0,
                "Allocation recorder detects the positive control");
            long reused = KarmaAllocationChecks.Count(() =>
            {
                for (int i = 0; i < repeats; i++)
                {
                    KarmaPhysicsQueries.BoxCast(origin, Vector2.one, Vector2.right, 45, hits);
                    KarmaPhysicsQueries.Raycast(origin, Vector2.right, 45, hits);
                }
            });
            long arrays = KarmaAllocationChecks.Count(() =>
            {
                for (int i = 0; i < repeats; i++)
                {
                    Physics2D.BoxCastAll(origin, Vector2.one, 0, Vector2.right, 45);
                    Physics2D.RaycastAll(origin, Vector2.right, 45);
                }
            });
            Debug.Log($"PHYSICS ALLOCATION: {repeats * 2} queries; before={arrays} allocations, after={reused} allocations");
            check(reused == 0 && arrays >= repeats * 2,
                $"Warmed physics queries allocate no managed arrays (before={arrays}, after={reused})");

            var body = player.GetComponent<Collider2D>();
            check(player.DamageBounds == body.bounds, "Player bounds match live collider");
            bool enabled = body.enabled;
            try
            {
                body.enabled = false;
                check(player.DamageBounds == new Bounds(player.Position, KarmaSpace.PlayerSize), "Disabled collider preserves fallback bounds");
            }
            finally { body.enabled = enabled; }
        }
        finally
        {
            Physics2D.queriesHitTriggers = triggers;
            Physics2D.queriesStartInColliders = inside;
            UnityEngine.Object.DestroyImmediate(root);
            Physics2D.SyncTransforms();
        }
    }

    static void Equal(RaycastHit2D[] expected, List<RaycastHit2D> actual, Action<bool, string> check)
    {
        check(expected.Length == actual.Count, "Same hit count");
        for (int i = 0; i < expected.Length; i++)
            check(expected[i].collider == actual[i].collider
                && Mathf.Abs(expected[i].distance - actual[i].distance) < 0.0001f
                && (expected[i].normal - actual[i].normal).sqrMagnitude < 0.0001f,
                "Same collider, ordering, distance and contact normal");
    }
}
#endif
