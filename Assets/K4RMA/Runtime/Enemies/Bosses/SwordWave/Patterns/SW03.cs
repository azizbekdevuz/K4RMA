using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        sealed class SW03Pattern : SwordBossPattern
        {
            public SW03Pattern(KarmaEnemy owner, SwordBossBehavior sword) : base(owner, sword) { }
            // Thirteen full-width cells cover 81.25% of the arena. Three narrow
            // body-wide channels remain; the planner guarantees a reachable one.
            Vector2 ScarSize { get { return new Vector2(KarmaStage.ArenaWidth / KarmaSwordScarPlanner.CellCount,
                sword.SwordSize(EnemyAttack.SW02).y * 0.92f); } }

            bool ClearEscapePath(float targetX)
            {
                var body = sword.SwordPlayerBounds;
                float dx = targetX - body.center.x;
                if (Mathf.Abs(dx) < 0.001f) return true;
                foreach (var hit in Physics2D.BoxCastAll(body.center, (Vector2)body.size * 0.98f, 0,
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
                owner.fullShield = true;
                try { yield return ExecuteSequence(); }
                finally { owner.fullShield = false; }
            }
            IEnumerator ExecuteSequence()
            {
                const int rounds = 5;
                var random = new System.Random(Random.Range(0, int.MaxValue));
                var size = ScarSize;
                float active = Mathf.Clamp(Settings.scarActiveSeconds, 0.25f, 0.8f);
                for (int round = 0; round < rounds && owner.Active; round++)
                {
                    owner.ClearPatternObjects();
                    while (owner.Active && (owner.game.Paused || owner.game.Player.Stunned)) yield return null;
                    if (!owner.Active) yield break;
                    var body = sword.SwordPlayerBounds;
                    if (!KarmaSwordScarPlanner.TryPlan(random, KarmaStage.ArenaLeft, KarmaStage.ArenaRight, size.x,
                        body.center.x, body.extents.x, Mathf.Max(0, owner.game.Config.moveSpeed) * 0.8f,
                        Mathf.Max(0.8f, owner.Warning(Settings.scarWarningSeconds)), ClearEscapePath,
                        out var cells, out var safeX, out var warning))
                    { yield return owner.Recovery(0.8f); yield break; }
                    owner.state = ActionState.Telegraph;
                    owner.animator.PlayWavePhase("Windup", warning);
                    KarmaSwordAttackPolish.Charge(owner.game, owner, owner.art, owner.Position, warning, true);
                    var marks = new KarmaSwordScarRise[cells.Length];
                    for (int i = 0; i < cells.Length; i++)
                    {
                        float x = KarmaSwordScarPlanner.DangerCenter(KarmaStage.ArenaLeft, KarmaStage.ArenaRight, cells[i], size.x);
                        marks[i] = KarmaSwordScarRise.Warning(owner.game, owner, owner.effectsRoot, x, size.x, warning,
                            "SW03 sword-cut warning / cell " + cells[i]);
                    }
                    float elapsed = 0;
                    bool slammed = false;
                    while (elapsed < warning && owner.Active)
                    {
                        yield return null;
                        if (owner.game.Paused) continue;
                        if (owner.game.Player.Stunned) { elapsed = 0; continue; }
                        elapsed += Time.deltaTime;
                        float charge = Mathf.Clamp01(elapsed / warning);
                        if (!slammed && elapsed >= warning - 0.18f)
                        {
                            slammed = true;
                            owner.animator.PlayWavePhase("Slash");
                            var swing = new GameObject("SW03 overhead ground slam / no damage");
                            swing.transform.SetParent(owner.effectsRoot, false);
                            swing.AddComponent<KarmaSwordGroundSlam>().Initialize(owner.game, owner);
                        }
                        foreach (var mark in marks) mark.SetWarningProgress(charge);
                    }
                    if (!owner.Active) yield break;
                    owner.ClearPatternObjects();
                    owner.state = ActionState.Attack;
                    owner.animator.ReleaseWavePhase(); owner.animator.PlayWavePhase("FollowThrough");
                    KarmaSwordVfxPool.For(owner.game).Flash(owner, "Sword_Release_v6",
                        owner.Position + new Vector2(owner.direction * 1.1f, -0.8f), new Vector2(0.55f, 4.5f), 0.12f);
                    KarmaAudio.StopSlot("sw-bed");
                    var contacts = new KarmaAttackContact[cells.Length];
                    var feedbackShown = new bool[cells.Length];
                    int group = 0;
                    foreach (int cell in cells)
                    {
                        float x = KarmaSwordScarPlanner.DangerCenter(KarmaStage.ArenaLeft, KarmaStage.ArenaRight, cell, size.x);
                        var contact = new KarmaAttackContact();
                        contacts[group++] = contact;
                        for (int blade = 0; blade < 2; blade++)
                        {
                            var bladeSize = new Vector2(size.x / 2, size.y);
                            float bladeX = x + (blade == 0 ? -1 : 1) * size.x / 4;
                            var shot = owner.Attack(new Vector2(bladeX, size.y / 2), Vector2.zero, bladeSize, active, contact: contact);
                            KarmaSwordAttackPolish.Core(shot);
                            shot.Visual.gameObject.name = "SW03 rising blade / cell " + cell + " / " + blade;
                            shot.Visual.gameObject.AddComponent<KarmaSwordScarRise>().Initialize(owner.game, owner, shot, bladeSize, active, blade != 0);
                        }
                        var pool = KarmaSwordVfxPool.For(owner.game);
                        pool.Sparks(owner, new Vector2(x, 0.35f), Vector2.up, 3, 18);
                    }
                    KarmaAudio.Play(KarmaSfxId.SwScarRise);
                    if (round == rounds - 1) KarmaSwordVfxPool.For(owner.game).Flash(owner, "Sword_GuardRing_v6", owner.Position, Vector2.one * 3.5f, 0.16f);
                    // Contacts expire at active; allow their final hit flash to finish
                    // during the inter-round recovery before the next clear/warning.
                    float rest = round + 1 < rounds ? Mathf.Clamp(Settings.scarRoundGap, 0.3f, 0.8f) : 0.24f;
                    float released = 0;
                    while (released < active + rest && owner.Active)
                    {
                        yield return null;
                        if (owner.game.Paused) continue;
                        released += Time.deltaTime;
                        if (released >= active) owner.state = ActionState.Recover;
                        for (int i = 0; i < contacts.Length; i++)
                            if (!feedbackShown[i] && contacts[i].DealtDamage)
                            {
                                feedbackShown[i] = true;
                                KarmaSwordScarRise.ShowHit(owner.game, owner.effectsRoot, owner);
                            }
                    }
                }
                yield return owner.Recovery(1);
            }
            // Unity can stop a nested coroutine without disposing its iterator.
            // The host calls Interrupt for cancellation, death and disable too.
            public override void Interrupt() { owner.fullShield = false; }
            public override bool CanReach(Vector2 from, Vector2 target)
            { return target.x >= KarmaStage.ArenaLeft && target.x <= KarmaStage.ArenaRight
                && sword.SwordPlayerBounds.min.y <= ScarSize.y; }

        }
    }
}
