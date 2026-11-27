using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class SW03Pattern : SwordBossPattern
        {
            public SW03Pattern(KarmaEnemy owner, SwordBossBehavior sword) : base(owner, sword) { }
            Coroutine groundRoutine;
            Transform groundRoot, roundRoot;
            int? seedOverride;
            float[] previousPlacement;
            bool swordPlanted;
            public int Seed { get; private set; }
            public KarmaSwordScarPlanner.Plan CurrentPlan { get; private set; }
            public bool Running { get { return groundRoot != null && groundRoot.gameObject.activeSelf; } }
            // Keep the original individual 2.1875-wide contacts and artwork fit.
            Vector2 ScarSize { get { return new Vector2(KarmaStage.ArenaWidth / 16f,
                sword.SwordSize(EnemyAttack.SW02).y * 0.92f); } }

            bool ClearEscapePath(float targetX)
            {
                var body = sword.SwordPlayerBounds;
                float dx = targetX - body.center.x;
                if (Mathf.Abs(dx) < 0.001f) return true;
                KarmaPhysicsQueries.BoxCast(body.center, body.size,
                    Vector2.right * Mathf.Sign(dx), Mathf.Abs(dx), owner.terrainHits);
                foreach (var hit in owner.terrainHits)
                {
                    if (hit.collider.GetComponent<KarmaSurface>() == null
                        || hit.collider.GetComponent<PlatformEffector2D>() != null) continue;
                    // The floor can touch the feet; walls above them block this route.
                    if (hit.collider.bounds.max.y > body.min.y + 0.1f) return false;
                }
                return true;
            }
            public override IEnumerator Execute()
            {
                if (Running || !owner.Active) yield break;
                swordPlanted = false;
                CurrentPlan = null;
                owner.state = ActionState.Telegraph;
                groundRoot = new GameObject("SW03 independent ground timeline").transform;
                groundRoot.SetParent(sword.PersistentRoot, false);
                groundRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                groundRoutine = owner.StartCoroutine(RunGround());
                // Hold the initial planted sword for one second. Ground rounds
                // retain their independent clock, then mobile evasion resumes.
                while (Running && owner.Active && !swordPlanted) yield return null;
                if (swordPlanted && owner.Active)
                    yield return owner.Wait(KarmaCharacterAnimator.SwordGroundPlantSeconds + KarmaBootstrap.Step);
            }
            IEnumerator RunGround()
            {
                try { yield return ExecuteSequence(); }
                finally { ClearGround(); }
            }
            float RecoverySeconds(float seconds)
            {
                return Mathf.Max(0.38f, seconds * Mathf.Clamp(owner.Tuning.recoveryMultiplier, 0.4f, 2)
                    * owner.Tempo * sword.Balance.recoveryScale);
            }
            void ClearRound()
            {
                if (roundRoot == null) return;
                if (owner.game.World != null) owner.game.World.GetComponent<KarmaSwordVfxPool>()?.Cancel(roundRoot);
                roundRoot.gameObject.SetActive(false); Destroy(roundRoot.gameObject); roundRoot = null;
            }
            void ClearGround()
            {
                groundRoutine = null;
                ClearRound();
                if (groundRoot == null) return;
                if (owner.game.World != null) owner.game.World.GetComponent<KarmaSwordVfxPool>()?.Cancel(groundRoot);
                groundRoot.gameObject.SetActive(false); Destroy(groundRoot.gameObject); groundRoot = null;
            }
            Transform ReleaseRoot(int round)
            {
                var node = new GameObject("SW03 synchronized release / round " + (round + 1)).transform;
                node.SetParent(roundRoot, false); return node;
            }
            KarmaSwordScarRise[] MarkRound(Transform root, Vector2 size, float warning)
            {
                var marks = new KarmaSwordScarRise[CurrentPlan.Positions.Length];
                for (int i = 0; i < CurrentPlan.Positions.Length; i++)
                    marks[i] = KarmaSwordScarRise.Warning(owner.game, owner, root,
                            CurrentPlan.Positions[i], size.x, warning, "SW03 sword-cut warning / site " + i);
                return marks;
            }
            void ReleaseRound(Transform root, Vector2 size, float active, KarmaAttackContact[] contacts)
            {
                var clock = new KarmaSwordScarClock(Time.frameCount);
                for (int i = 0; i < CurrentPlan.Positions.Length; i++)
                {
                    float x = CurrentPlan.Positions[i];
                    var shot = owner.Attack(new Vector2(x, size.y / 2), Vector2.zero, size, active,
                        contact: contacts[CurrentPlan.ContactIndices[i]], pattern: EnemyAttack.SW03, parent: root);
                    KarmaSwordAttackPolish.Core(shot);
                    shot.Visual.gameObject.name = "SW03 rising blade / site " + i;
                    shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(owner.game, owner, shot, size, active, i % 2 != 0, clock);
                    KarmaSwordVfxPool.For(owner.game).Sparks(owner, new Vector2(x, 0.35f), Vector2.up, 3, 18, root);
                }
            }
            IEnumerator ExecuteSequence()
            {
                float windup = Mathf.Max(KarmaSwordScarPlanner.MinimumWarning, owner.Warning(Settings.scarWarningSeconds));
                owner.animator.BeginSwordGroundStrike(windup);
                float raised = 0;
                while (raised < windup)
                {
                    yield return null;
                    if (!owner.Active) yield break;
                    if (owner.game.Paused || owner.game.Player.Stunned) continue;
                    raised += Time.deltaTime;
                    owner.animator.SampleSwordGroundStrike(raised / windup);
                }
                if (!owner.animator.ReleaseSwordGroundStrike()) yield break;
                swordPlanted = true;
                owner.state = ActionState.Attack;
                KarmaSW05Energy.GroundStrike(groundRoot, owner.game,
                    new Vector2(owner.animator.SwordGroundTipX, 0.04f), owner.animator.SwordChargeBodyHeight);
                // Let the contact drawing reach the screen before revealing the marked sites.
                yield return owner.Wait(KarmaBootstrap.Step);
                if (!owner.Active) yield break;
                Seed = seedOverride ?? KarmaSwordScarPlanner.NextSeed();
                // Explicit test casts replay from the same initial inputs. Normal
                // casts retain the last visible layout, even after interruption.
                if (seedOverride.HasValue) previousPlacement = null;
                var random = new System.Random(Seed);
                var size = new Vector2(ScarSize.x / 2, ScarSize.y);
                float active = Mathf.Clamp(Settings.scarActiveSeconds, 0.25f, 0.8f);
                float previousSafe = float.NaN;
                for (int round = 0; round < KarmaSwordScarPlanner.Rounds && owner.Active; round++)
                {
                    // The previous round lifetime AND rest have elapsed. Remove
                    // its scope before solving a new route; no old hazard can
                    // block the next warning's reaction/movement interval.
                    ClearRound();
                    roundRoot = new GameObject("SW03 ground round " + (round + 1)).transform;
                    roundRoot.SetParent(groundRoot, false);
                    while (owner.Active && (owner.game.Paused || owner.game.Player.Stunned)) yield return null;
                    if (!owner.Active) yield break;
                    var body = sword.SwordPlayerBounds;
                    var playerBody = owner.game.Player.GetComponent<Rigidbody2D>();
                    float velocity = playerBody != null ? playerBody.linearVelocity.x : 0;
                    float warning = Mathf.Max(KarmaSwordScarPlanner.MinimumWarning, owner.Warning(Settings.scarWarningSeconds));
                    if (!KarmaSwordScarPlanner.TryPlan(random, KarmaStage.ArenaLeft, KarmaStage.ArenaRight, size.x,
                        KarmaSwordScarRise.VisualSize(size).x, body.center.x, body.extents.x, velocity,
                        Mathf.Max(0, owner.game.Config.moveSpeed) * 0.8f, warning, previousSafe, ClearEscapePath, out var plan, previousPlacement))
                    {
                        Debug.LogWarning("SW03 cancelled before telegraph: current player state has no reachable safe corridor.");
                        yield return owner.Wait(RecoverySeconds(0.8f)); yield break;
                    }
                    CurrentPlan = plan; previousSafe = plan.SafeX;
                    previousPlacement = (float[])plan.Positions.Clone();
                    // Ground marks carry the warning; omit the rotating body ring.
                    var releaseRoot = ReleaseRoot(round);
                    float releaseAt = float.PositiveInfinity;
                    var contacts = new KarmaAttackContact[plan.ContactCount];
                    var feedbackShown = new bool[contacts.Length];
                    // One accepted encounter per spatial cluster, including
                    // overlapping blades. Other clusters keep independent ledgers.
                    for (int i = 0; i < contacts.Length; i++) contacts[i] = new KarmaAttackContact();
                    var warningRoot = new GameObject("SW03 round warnings").transform;
                    warningRoot.SetParent(releaseRoot, false);
                    // One synchronous warning pass, one countdown, one release.
                    var marks = MarkRound(warningRoot, size, warning);
                    if (owner.game.IsCombat && owner.Alive) KarmaAudio.Play(KarmaSfxId.SwEnergyCharge, warning, marks[0]);
                    float elapsed = 0;
                    float rest = round + 1 < KarmaSwordScarPlanner.Rounds ? Mathf.Clamp(Settings.scarRoundGap, 0.3f, 0.8f) : 0.24f;
                    while (owner.Active && elapsed < releaseAt + active + rest)
                    {
                        yield return null;
                        if (!owner.Active) yield break;
                        if (owner.game.Paused) continue;
                        // Before any release, a player stun restarts the warning
                        // clock as before. Already-visible locations stay locked.
                        if (owner.game.Player.Stunned && float.IsPositiveInfinity(releaseAt))
                        {
                            elapsed = 0;
                            foreach (var mark in marks) mark.SetWarningProgress(0);
                            continue;
                        }
                        elapsed += Time.deltaTime;
                        if (float.IsPositiveInfinity(releaseAt))
                        {
                            float progress = Mathf.Clamp01(elapsed / warning);
                            foreach (var mark in marks) mark.SetWarningProgress(progress);
                            // Frame drops may extend the common warning, never shorten it.
                            if (elapsed < warning) continue;
                            warningRoot.gameObject.SetActive(false);
                            Destroy(warningRoot.gameObject);
                            releaseAt = elapsed;
                            // No yields or delayed starts inside this whole-round pass.
                            ReleaseRound(releaseRoot, size, active, contacts);
                            KarmaSwordVfxPool.For(owner.game).Flash(owner, "Sword_Release_v6",
                                owner.Position + new Vector2(owner.direction * 1.1f, -0.8f),
                                new Vector2(0.55f, 4.5f), 0.12f, scope: roundRoot);
                            KarmaAudio.StopSlot("sw-bed");
                            KarmaAudio.Play(KarmaSfxId.SwElectricDischarge);
                        }
                        for (int i = 0; i < contacts.Length; i++)
                            if (!feedbackShown[i] && contacts[i].DealtDamage)
                            {
                                feedbackShown[i] = true;
                                KarmaSwordScarRise.ShowHit(owner.game, roundRoot, owner);
                            }
                    }
                }
                yield return owner.Wait(RecoverySeconds(1));
            }
            // Unity can stop a nested coroutine without disposing its iterator.
            // The host calls Interrupt for cancellation, death and disable too.
            public override void Interrupt()
            {
                var routine = groundRoutine; groundRoutine = null;
                if (routine != null) owner.StopCoroutine(routine);
                ClearGround();
            }
            public override bool CanReach(Vector2 from, Vector2 target)
            { return target.x >= KarmaStage.ArenaLeft && target.x <= KarmaStage.ArenaRight
                && sword.SwordPlayerBounds.min.y <= ScarSize.y; }

        }
    }
}
