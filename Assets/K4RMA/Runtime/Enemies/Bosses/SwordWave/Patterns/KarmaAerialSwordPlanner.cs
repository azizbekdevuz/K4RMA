using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KarmaPrototype
{
    // Only copied static terrain and a copied player body enter the local scene.
    // The real player, boss position, colliders and physics scene are untouched.
    public sealed class KarmaAerialSwordPlanner : MonoBehaviour
    {
        public const float FormationRadius = 2.2f;
        public const int VolleyCount = 7;
        public sealed class Shot
        {
            public Vector2 origin, velocity, size;
            public float life;
            public bool aimed;
        }
        public sealed class Group
        {
            public Shot[] shots;
            public int escapeInput;
            public float escapeJumpAt = -1;
            public float step;
            public bool reachableEscape;
            public Vector2[] escapeCenters;
        }
        sealed class LiveShot
        {
            public KarmaBossAttack instance;
            public Shot shot;
            public Vector2 size;
            public float launchedAt;
        }
        sealed class Route
        {
            public Vector2[] centers;
            public int input;
            public float jumpAt = -1;
        }

        KarmaGame game;
        System.Random random;
        Scene simulationScene;
        PhysicsScene2D physics;
        GameObject simulationRoot;
        Rigidbody2D probe;
        BoxCollider2D probeShape;
        readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
        readonly List<LiveShot> live = new List<LiveShot>(56);
        public int[] GroupSizes { get; private set; }
        public int Seed { get; private set; }
        public bool HasLiveShots
        {
            get
            {
                foreach (var shot in live)
                    if (shot.instance != null && shot.instance.gameObject.activeInHierarchy) return true;
                return false;
            }
        }

        public static int ResolveSeed(int configuredSeed)
        { return configuredSeed != 0 ? configuredSeed : UnityEngine.Random.Range(1, int.MaxValue); }

        public static KarmaAerialSwordPlanner Create(Transform lifetimeRoot, KarmaGame game, int seed)
        {
            var go = new GameObject("Aerial sword escape planner");
            go.transform.SetParent(lifetimeRoot, false);
            var planner = go.AddComponent<KarmaAerialSwordPlanner>();
            planner.game = game; planner.Seed = seed; planner.random = new System.Random(seed);
            planner.GroupSizes = CreateGroupSizes(game.Config.BossTuning.sword, planner.random);
            planner.CopyPhysicsWorld();
            return planner;
        }

        public static int[] CreateGroupSizes(KarmaSwordBossSettings tuning, System.Random random)
        {
            int low = Mathf.Clamp(tuning.aerialSwordVolleyMin, 1, 18);
            int high = Mathf.Clamp(tuning.aerialSwordVolleyMax, low, 18);
            var sizes = new int[VolleyCount];
            for (int i = 0; i < sizes.Length; i++) sizes[i] = random.Next(low, high + 1);
            return sizes;
        }

        public Group BuildGroup(Vector2 elevated, int index, float warning, float life,
            Vector2 size, float speed, float fightSeconds)
        {
            var player = game.Player;
            var sourceBody = player.GetComponent<Rigidbody2D>();
            var sourceShape = player.GetComponent<BoxCollider2D>();
            Vector2 target = sourceShape.bounds.center;
            Vector2 half = sourceShape.bounds.extents;
            float step = Mathf.Clamp(Time.fixedDeltaTime, 0.005f, 0.05f);
            float horizon = warning + Mathf.Max(life, KarmaSpace.Units(12) / Mathf.Max(0.01f, speed))
                + TimingAllowance(step) + step * 2;
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i].instance == null || !live[i].instance.gameObject.activeInHierarchy
                    || fightSeconds - live[i].launchedAt >= live[i].shot.life) live.RemoveAt(i);
            foreach (var active in live)
                horizon = Mathf.Max(horizon, active.shot.life - (fightSeconds - active.launchedAt) + TimingAllowance(step) + step * 2);

            int count = GroupSizes[index];
            var dimensions = new Vector2[count];
            var heights = new float[count];
            float widest = size.y;
            float ceiling = KarmaSpace.Units(12);
            if (game.GameCamera != null && game.GameCamera.orthographic)
                ceiling = Mathf.Min(ceiling, game.GameCamera.transform.position.y + game.GameCamera.orthographicSize);
            float lowerSky = Mathf.Max(0, elevated.y + 6 + size.x * 1.15f / 2 + 0.1f - ceiling);
            for (int i = 0; i < count; i++)
            {
                // Variants enlarge the real sword and its matching artwork;
                // reserving an escape never relies on shrinking damage shapes.
                dimensions[i] = size * Mathf.Lerp(1, 1.15f, (float)random.NextDouble());
                widest = Mathf.Max(widest, dimensions[i].y);
                heights[i] = Mathf.Clamp(elevated.y + Mathf.Lerp(4, 6, (float)random.NextDouble()) - lowerSky,
                    dimensions[i].x / 2 + 1, ceiling - dimensions[i].x / 2 - 0.1f);
            }
            float left = KarmaStage.ArenaLeft + widest / 2 + 0.35f;
            float right = KarmaStage.ArenaRight - widest / 2 - 0.35f;
            float spacing = count > 1 ? (right - left) / (count - 1) : right - left;
            // Adjacent slot envelopes cannot overlap, even at their nearest edges.
            float shift = count > 1 ? Mathf.Max(0, Mathf.Min(spacing * 0.35f,
                (spacing - widest - 0.45f) / 2)) : spacing / 2;
            float focus = Mathf.Clamp(target.x + (index % 2 == 0 ? 0 :
                Mathf.Sin(index * 2.4f) * spacing * 0.6f), left, right);
            int aimedSlot = count > 1 ? Mathf.Clamp(Mathf.RoundToInt((focus - left) / spacing), 0, count - 1) : 0;
            int inward = target.x < KarmaStage.ArenaCenter ? 1 : -1;
            Route selected = null;
            Shot[] shots = null;
            // A route includes the entire remaining lifetime of old and new shots.
            // Continuing the previous witness therefore remains a valid candidate
            // at the next group when the player follows an available escape.
            foreach (bool jump in new[] { false, true })
            {
                foreach (int input in new[] { inward, -inward, 0 })
                {
                    var route = SimulateRoute(sourceBody, sourceShape, input, jump, step, horizon);
                    if (!SafeFromLive(route, step, half, fightSeconds)) continue;
                    var candidate = new Shot[count];
                    bool allSafe = true;
                    for (int member = 0; member < count; member++)
                    {
                        float slot = count > 1 ? Mathf.Lerp(left, right, member / (float)(count - 1))
                            : (left + right) / 2;
                        float low = Mathf.Max(left, slot - shift), high = Mathf.Min(right, slot + shift);
                        float desired = member == aimedSlot ? Mathf.Clamp(focus, low, high) :
                            Mathf.Clamp(slot + Mathf.Sin(index * 1.7f + member * 2.1f) * shift * 0.55f, low, high);
                        candidate[member] = FindSafeShot(new Vector2(desired, heights[member]), low, high,
                            speed, dimensions[member], route, step, half, warning, member == aimedSlot);
                        if (candidate[member] == null) { allSafe = false; break; }
                    }
                    if (!allSafe) continue;
                    shots = candidate; selected = route; break;
                }
                if (selected != null) break;
            }
            if (selected == null)
                throw new InvalidOperationException("SW05 could not reserve a physical sword-rain escape route.");
            // Shuffle presentation order so the main aimed sword is not always first.
            for (int i = shots.Length - 1; i > 0; i--)
            { int j = random.Next(i + 1); var swap = shots[i]; shots[i] = shots[j]; shots[j] = swap; }
            return new Group { shots = shots, escapeInput = selected.input, escapeJumpAt = selected.jumpAt,
                step = step, reachableEscape = true, escapeCenters = selected.centers };
        }

        public void RegisterLaunch(Shot shot, KarmaBossAttack instance, Vector2 size, float fightSeconds)
        { live.Add(new LiveShot { shot = shot, instance = instance, size = shot.size, launchedAt = fightSeconds }); }

        static Shot MakeShot(Vector2 origin, float speed, Vector2 size, bool aimed)
        {
            speed = Mathf.Max(0.01f, speed);
            // Local X is the blade's long axis. With a downward velocity its
            // leading tip, rather than its center, reaches the floor at expiry.
            return new Shot { origin = origin, velocity = Vector2.down * speed, size = size,
                life = Mathf.Max(0, origin.y - size.x / 2 - 0.05f) / speed, aimed = aimed };
        }

        Shot FindSafeShot(Vector2 desired, float left, float right, float speed, Vector2 size,
            Route route, float step, Vector2 half, float warning, bool aimed)
        {
            // Search only this sword's non-overlapping slot. Every accepted
            // placement remains a visible, full-size, physically falling sword.
            for (int attempt = 0; attempt <= 24; attempt++)
            {
                float x = attempt == 0 ? desired.x : Mathf.Lerp(left, right, (attempt - 1) / 23f);
                var shot = MakeShot(new Vector2(x, desired.y), speed, size, aimed && Mathf.Abs(x - desired.x) < 0.1f);
                if (shot.life > 0.05f && Safe(route, step, half, shot, warning, size)) return shot;
            }
            return null;
        }

        bool SafeFromLive(Route route, float step, Vector2 half, float now)
        {
            foreach (var active in live)
            {
                var shot = new Shot { origin = active.instance.transform.position,
                    velocity = active.shot.velocity, life = Mathf.Max(0, active.shot.life - (now - active.launchedAt)) };
                // A live sword's observed position fixes its launch phase. Do
                // not expand that already-observed ray by another hypothetical
                // early/late launch on every volley. Retain the full real swept
                // rectangle and player-body clearance from this instant onward.
                if (!SafeAtPhase(route, step, half, shot, 0, active.size, 0))
                    return false;
            }
            return true;
        }

        static bool Safe(Route route, float step, Vector2 half, Shot shot, float begins, Vector2 size)
        {
            // Warnings finish on a rendered frame, while movement advances on
            // fixed physics ticks. Reserve the route across that phase window,
            // including the already-live swords, rather than assuming one exact
            // launch instant and a perfectly aligned first movement tick.
            float allowance = TimingAllowance(step);
            // A continuous launch-time interval translates this rectangle along
            // its own velocity axis. Its exact union adds 2 * speed * allowance
            // to the reservation length; the real attack dimensions stay intact.
            Vector2 reservation = size + Vector2.right * (shot.velocity.magnitude * allowance * 2);
            return SafeAtPhase(route, step, half, shot, begins, reservation, allowance);
        }

        static float TimingAllowance(float step)
        { return Mathf.Max(step * 2, Mathf.Min(0.05f, Time.deltaTime)); }

        static bool SafeAtPhase(Route route, float step, Vector2 half, Shot shot, float begins, Vector2 size, float allowance)
        {
            Vector2 axis = shot.velocity.normalized;
            float activeFrom = begins - allowance;
            float until = begins + KarmaBossProjectileGeometry.SwordShotLife(shot.origin, shot.velocity, shot.life) + allowance;
            for (int i = 1; i < route.centers.Length; i++)
            {
                float a = (i - 1) * step, b = i * step;
                if (b < activeFrom || a > until) continue;
                float start = Mathf.Max(a, activeFrom), end = Mathf.Min(b, until);
                if (end < start) continue;
                Vector2 p = Vector2.Lerp(route.centers[i - 1], route.centers[i], (start - a) / step);
                Vector2 q = Vector2.Lerp(route.centers[i - 1], route.centers[i], (end - a) / step);
                Vector2 from = shot.origin + shot.velocity * (start - begins) - p;
                Vector2 to = shot.origin + shot.velocity * (end - begins) - q;
                // Reserve a small allowance for render/fixed update phase offsets.
                if (KarmaBossGeometry.SweptOrientedBox(from.x, from.y, to.x, to.y, 0, 0,
                    axis.x, axis.y, size.x, size.y, half.x + 0.15f, half.y + 0.15f)) return false;
            }
            return true;
        }

        Route SimulateRoute(Rigidbody2D sourceBody, BoxCollider2D sourceShape,
            int input, bool jump, float step, float horizon)
        {
            probe.simulated = false;
            probe.transform.SetPositionAndRotation(sourceBody.position, sourceBody.transform.rotation);
            probeShape.size = sourceShape.size; probeShape.offset = sourceShape.offset;
            probeShape.edgeRadius = sourceShape.edgeRadius;
            probe.transform.localScale = sourceBody.transform.lossyScale;
            probe.simulated = true;
            // Re-enabling simulation restores the native body from its Transform.
            // Set its pose and velocity afterwards; setters while disabled can
            // otherwise leave the previous candidate's endpoint in the new route.
            probe.position = sourceBody.position; probe.rotation = sourceBody.rotation;
            probe.linearVelocity = sourceBody.linearVelocity; probe.angularVelocity = 0;
            probe.gravityScale = game.Config.gravityScale;
            Physics2D.SyncTransforms();
            probe.WakeUp();
            int steps = Mathf.CeilToInt(horizon / step);
            var route = new Route { input = input, centers = new Vector2[steps + 1] };
            route.centers[0] = ProbeCenter();
            bool issued = false, grounded = !game.Player.Airborne;
            for (int i = 0; i < steps; i++)
            {
                float vertical = probe.linearVelocity.y;
                if (jump && !issued && grounded && vertical <= 0.1f)
                { vertical = game.Config.jumpSpeed; route.jumpAt = i * step; issued = true; }
                probe.linearVelocity = new Vector2(input * game.Config.moveSpeed, vertical);
                physics.Simulate(step);
                route.centers[i + 1] = ProbeCenter();
                grounded = false;
                int touching = probeShape.GetContacts(contacts);
                for (int c = 0; c < touching; c++) grounded |= contacts[c].normal.y > 0.5f;
            }
            return route;
        }

        Vector2 ProbeCenter()
        {
            Vector2 offset = Vector2.Scale(probeShape.offset, probe.transform.lossyScale);
            float angle = probe.rotation * Mathf.Deg2Rad;
            return probe.position + new Vector2(offset.x * Mathf.Cos(angle) - offset.y * Mathf.Sin(angle),
                offset.x * Mathf.Sin(angle) + offset.y * Mathf.Cos(angle));
        }

        void CopyPhysicsWorld()
        {
            simulationScene = SceneManager.CreateScene("SW05 escape physics " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = simulationScene.GetPhysicsScene2D();
            simulationRoot = new GameObject("SW05 copied collision world");
            simulationRoot.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(simulationRoot, simulationScene);
            foreach (var surface in game.World.GetComponentsInChildren<KarmaSurface>())
                foreach (var source in surface.GetComponents<Collider2D>())
                {
                    if (!source.enabled || source.isTrigger) continue;
                    var go = new GameObject("Copied " + surface.name);
                    go.transform.SetParent(simulationRoot.transform, false);
                    go.transform.position = source.transform.position;
                    go.transform.rotation = source.transform.rotation;
                    go.transform.localScale = source.transform.lossyScale;
                    var box = source as BoxCollider2D;
                    if (box == null) { Destroy(go); continue; }
                    var copy = go.AddComponent<BoxCollider2D>(); copy.size = box.size; copy.offset = box.offset;
                    copy.edgeRadius = box.edgeRadius; copy.sharedMaterial = source.sharedMaterial;
                }
            var player = new GameObject("Copied real player collider");
            player.transform.SetParent(simulationRoot.transform, false);
            probeShape = player.AddComponent<BoxCollider2D>();
            probe = player.AddComponent<Rigidbody2D>(); probe.freezeRotation = true;
            probe.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            probe.sleepMode = RigidbodySleepMode2D.NeverSleep;
            probe.linearDamping = game.Player.GetComponent<Rigidbody2D>().linearDamping;
            probeShape.sharedMaterial = game.Player.GetComponent<BoxCollider2D>().sharedMaterial;
        }

        void OnDisable()
        {
            live.Clear();
            if (simulationRoot != null)
            { simulationRoot.SetActive(false); Destroy(simulationRoot); simulationRoot = null; }
            if (simulationScene.IsValid() && simulationScene.isLoaded) SceneManager.UnloadSceneAsync(simulationScene);
            simulationScene = default(Scene);
        }
    }
}
