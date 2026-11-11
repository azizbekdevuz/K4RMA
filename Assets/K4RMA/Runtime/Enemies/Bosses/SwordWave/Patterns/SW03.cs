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
            public int Seed { get; private set; }
            public KarmaSwordScarPlanner.Plan CurrentPlan { get; private set; }
            public bool Running { get { return groundRoot != null && groundRoot.gameObject.activeSelf; } }
            // Keep the original individual 2.1875-wide contacts and artwork fit.
            Vector2 ScarSize { get { return new Vector2(KarmaStage.ArenaWidth / KarmaSwordScarPlanner.ScarCount,
                sword.SwordSize(EnemyAttack.SW02).y * 0.92f); } }

            bool ClearEscapePath(float targetX)
            {
                var body = sword.SwordPlayerBounds;
                float dx = targetX - body.center.x;
                if (Mathf.Abs(dx) < 0.001f) return true;
                foreach (var hit in Physics2D.BoxCastAll(body.center, (Vector2)body.size, 0,
                    Vector2.right * Mathf.Sign(dx), Mathf.Abs(dx)))
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
                groundRoot = new GameObject("SW03 independent ground timeline").transform;
                groundRoot.SetParent(sword.PersistentRoot, false);
                groundRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                groundRoutine = owner.StartCoroutine(RunGround());
                // Return the action slot to the existing movement AI. The
                // persistent root survives ordinary attack completion/recovery.
                yield break;
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
            Transform GroupRoot(int round, int group)
            {
                var node = new GameObject("SW03 group " + group + " / round " + (round + 1)).transform;
                node.SetParent(roundRoot, false); return node;
            }
            KarmaSwordScarRise[] MarkGroup(Transform root, int group, Vector2 size, float warning)
            {
                var marks = new KarmaSwordScarRise[KarmaSwordScarPlanner.GroupCount];
                int mark = 0;
                for (int i = 0; i < CurrentPlan.Positions.Length; i++)
                    if (CurrentPlan.Groups[i] == group)
                        marks[mark++] = KarmaSwordScarRise.Warning(owner.game, owner, root,
                            CurrentPlan.Positions[i], size.x, warning, "SW03 sword-cut warning / site " + i);
                return marks;
            }
            void ReleaseGroup(Transform root, int group, Vector2 size, float active, KarmaAttackContact[] contacts)
            {
                for (int i = 0; i < CurrentPlan.Positions.Length; i++)
                {
                    if (CurrentPlan.Groups[i] != group) continue;
                    float x = CurrentPlan.Positions[i];
                    var shot = owner.Attack(new Vector2(x, size.y / 2), Vector2.zero, size, active,
                        contact: contacts[i / 2], pattern: EnemyAttack.SW03, parent: root);
                    KarmaSwordAttackPolish.Core(shot);
                    shot.Visual.gameObject.name = "SW03 rising blade / site " + i;
                    shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(owner.game, owner, shot, size, active, i % 2 != 0);
                    KarmaSwordVfxPool.For(owner.game).Sparks(owner, new Vector2(x, 0.35f), Vector2.up, 3, 18, root);
                }
            }
            IEnumerator ExecuteSequence()
            {
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
                    { yield return owner.Wait(RecoverySeconds(0.8f)); yield break; }
                    CurrentPlan = plan; previousSafe = plan.SafeX;
                    previousPlacement = (float[])plan.Positions.Clone();
                    var chargeCue = KarmaSwordAttackPolish.Charge(owner.game, owner, owner.art, owner.Position, warning, true);
                    chargeCue.transform.SetParent(roundRoot, true);
                    var roots = new[] { GroupRoot(round, 0), GroupRoot(round, 1) };
                    var warnings = new Transform[2];
                    var marks = new KarmaSwordScarRise[2][];
                    var releaseAt = new[] { float.PositiveInfinity, float.PositiveInfinity };
                    var contacts = new KarmaAttackContact[KarmaSwordScarPlanner.GroupCount];
                    var feedbackShown = new bool[contacts.Length];
                    // Keep shared contact consumption for each adjacent pair.
                    // Each pair contributes one physical sword to each group.
                    for (int i = 0; i < contacts.Length; i++) contacts[i] = new KarmaAttackContact();
                    warnings[0] = new GameObject("SW03 warning group 0").transform;
                    warnings[0].SetParent(roots[0], false);
                    marks[0] = MarkGroup(warnings[0], 0, size, warning);
                    // Both locked layouts are visible before the player reacts.
                    // The later group has a longer countdown, not a late surprise.
                    warnings[1] = new GameObject("SW03 warning group 1").transform;
                    warnings[1].SetParent(roots[1], false);
                    marks[1] = MarkGroup(warnings[1], 1, size, warning + KarmaSwordScarPlanner.StaggerSeconds);
                    float elapsed = 0;
                    bool slammed = false;
                    float rest = round + 1 < KarmaSwordScarPlanner.Rounds ? Mathf.Clamp(Settings.scarRoundGap, 0.3f, 0.8f) : 0.24f;
                    while (owner.Active && elapsed < releaseAt[1] + active + rest)
                    {
                        yield return null;
                        if (!owner.Active) yield break;
                        if (owner.game.Paused) continue;
                        // Before any release, a player stun restarts the warning
                        // clock as before. Already-visible locations stay locked.
                        if (owner.game.Player.Stunned && float.IsPositiveInfinity(releaseAt[0]))
                        {
                            elapsed = 0;
                            foreach (var groupMarks in marks)
                                if (groupMarks != null) foreach (var mark in groupMarks) mark.SetWarningProgress(0);
                            continue;
                        }
                        elapsed += Time.deltaTime;
                        if (!slammed && elapsed >= warning - 0.18f)
                        {
                            slammed = true;
                            if (!owner.running && owner.animator.CurrentPose != "Backstep" && owner.animator.CurrentPose != "Move")
                            {
                                var swing = new GameObject("SW03 overhead ground slam / no damage");
                                swing.transform.SetParent(roundRoot, false);
                                swing.AddComponent<KarmaSwordGroundSlam>().Initialize(owner.game, owner);
                            }
                        }
                        for (int group = 0; group < 2; group++)
                        {
                            if (marks[group] == null || !float.IsPositiveInfinity(releaseAt[group])) continue;
                            float groupWarning = warning + group * KarmaSwordScarPlanner.StaggerSeconds;
                            float progress = Mathf.Clamp01(elapsed / groupWarning);
                            foreach (var mark in marks[group]) mark.SetWarningProgress(progress);
                            // A frame drop can lengthen, never shorten, a warning.
                            if (elapsed < groupWarning) continue;
                            warnings[group].gameObject.SetActive(false);
                            Destroy(warnings[group].gameObject);
                            releaseAt[group] = elapsed;
                            ReleaseGroup(roots[group], group, size, active, contacts);
                            if (group == 0)
                            {
                                KarmaSwordVfxPool.For(owner.game).Flash(owner, "Sword_Release_v6",
                                    owner.Position + new Vector2(owner.direction * 1.1f, -0.8f),
                                    new Vector2(0.55f, 4.5f), 0.12f, scope: roundRoot);
                                KarmaAudio.StopSlot("sw-bed");
                                KarmaAudio.Play(KarmaSfxId.SwElectricDischarge);
                            }
                            if (group == 1 && round == KarmaSwordScarPlanner.Rounds - 1)
                                KarmaSwordVfxPool.For(owner.game).Flash(owner, "Sword_GuardRing_v6",
                                    owner.Position, Vector2.one * 3.5f, 0.16f, scope: roundRoot);
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
