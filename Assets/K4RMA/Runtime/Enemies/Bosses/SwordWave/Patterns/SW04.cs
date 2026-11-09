using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        // Pattern execution and pattern-local state. Shared damage and cancellation stay in the host.
        sealed class SW04Pattern : SwordBossPattern
        {
            public SW04Pattern(KarmaEnemy owner, SwordBossBehavior sword) : base(owner, sword) { }
            public bool Guarding;
            float swordGuardUntil, nextGuardResponse;
            bool guardResponsePending, guardResponseRunning;
            Transform swordGuardRoot;
            Coroutine swordGuardRoutine;
            KarmaSwordWardVfx wardVfx;
            float nextSwordGuard;
            int nextVolleyId;
            CounterVolley activeVolley;
            sealed class CounterVolley
            {
                public readonly int Id;
                public readonly KarmaAttackContact[] Contacts = new KarmaAttackContact[4];
                public readonly KarmaBossAttack[] Shots = new KarmaBossAttack[4];
                public readonly Transform[] Slots = new Transform[4];
                public readonly SpriteRenderer[] Blades = new SpriteRenderer[4];
                public readonly KarmaSwordCounterTrace.State[] Trace = new KarmaSwordCounterTrace.State[4];
                public int LastAllowed = 3;
                public bool Parried;
                public CounterVolley(int id) { Id = id; for (int i = 0; i < 4; i++) Contacts[i] = new KarmaAttackContact(); }
                public bool Allows(int index) { return !Parried && index <= LastAllowed; }
            }
            float GuardTrackingTime { get { return 0.35f; } }
            float GuardLockTime { get { return Mathf.Max(0.3f, Settings.counterWarning); } }
            float GuardShotGap { get { return Mathf.Max(0.16f, Settings.swordGuardResponseCooldown - GuardTrackingTime - GuardLockTime); } }
            public override IEnumerator Execute()
            {
                if (swordGuardRoot != null) yield break;
                owner.state = ActionState.Telegraph;
                owner.animator.PlayWavePhase("Windup");
                // A non-damaging formation uses the native charge circle. It
                // needs no swept hit-area sprite or duplicate square warning.
                var formation = KarmaSwordAttackPolish.Charge(owner.game, owner, owner.art,
                    owner.Position, owner.Warning(0.7f), true);
                yield return owner.Wait(owner.Warning(0.7f));
                if (formation != null) Destroy(formation.gameObject);
                KarmaAudio.StopSlot("sw-bed");
                owner.state = ActionState.Attack; Guarding = true; guardResponsePending = guardResponseRunning = false;
                owner.animator.PlayWavePhase("Guard");
                swordGuardUntil = owner.FightSeconds + Mathf.Max(0.6f, Settings.swordGuardDuration);
                nextGuardResponse = owner.FightSeconds;
                nextSwordGuard = owner.FightSeconds + Mathf.Max(8, Settings.swordGuardDuration + 4);
                swordGuardRoot = new GameObject("Independent sword guard").transform;
                swordGuardRoot.SetParent(sword.PersistentRoot, false);
                activeVolley = new CounterVolley(++nextVolleyId);
                var blades = new SpriteRenderer[4];
                for (int i = 0; i < blades.Length; i++)
                {
                    var slot = new GameObject("SW04 volley " + activeVolley.Id + " / shot " + (i + 1)).transform;
                    slot.SetParent(swordGuardRoot, false); activeVolley.Slots[i] = slot;
                    blades[i] = KarmaVisuals.Box(slot, "회전 검막 · 접촉 피해 없음", owner.Position, Vector2.one, Color.white, 13);
                    activeVolley.Blades[i] = blades[i];
                    KarmaWaveArtwork.Apply(blades[i], "SW04_CounterBlade_v2", sword.SwordSize(EnemyAttack.SW04), Vector2.right);
                }
                wardVfx = KarmaSwordWardVfx.Create(swordGuardRoot,owner.game,owner,blades);
                if (owner.Active) KarmaAudio.Play(KarmaSfxId.SwCrossingBlades);
                swordGuardRoutine = owner.StartCoroutine(MaintainSwordGuard(blades));
                // Only formation occupies the action slot. The buff, aim warnings
                // and counter projectiles own a separate lifetime and object root.
                yield return owner.Recovery(0.45f);
            }
            IEnumerator MaintainSwordGuard(SpriteRenderer[] blades)
            {
                while ((Guarding && owner.FightSeconds < swordGuardUntil || guardResponsePending) && owner.Active)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    UpdateSwordGuardBlades(blades);
                    if (guardResponsePending)
                    {
                        guardResponsePending = false; guardResponseRunning = true;
                        var volley = activeVolley;
                        var shots = volley.Shots;
                        var size = sword.SwordSize(EnemyAttack.SW04);
                        for (int i = 0; i < blades.Length && owner.Active && volley.Allows(i); i++)
                        {
                            Vector2 origin = blades[i].transform.position, aim = Vector2.right * owner.direction;
                            var slot = volley.Slots[i];
                            var mark = SwordGuardPathMark(slot, origin, origin, size, false);
                            float trackingUntil = owner.FightSeconds + GuardTrackingTime;
                            while (owner.Active && volley.Allows(i) && owner.FightSeconds < trackingUntil)
                            {
                                if (owner.game.Paused) { yield return null; continue; }
                                UpdateSwordGuardBlades(blades, i);
                                origin = blades[i].transform.position;
                                aim = ((Vector2)sword.SwordPlayerBounds.center - origin).normalized;
                                if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * owner.direction;
                                blades[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                                blades[i].color = new Color(0.42f, 0.86f, 1, 1);
                                wardVfx?.Aim(i,origin,aim,size,false);
                                mark.GetComponent<KarmaAttackWarning>().SetPath(mark, origin,
                                origin + aim * Settings.swordSpeed * KarmaBossProjectileGeometry.SwordShotLife(origin, aim * Settings.swordSpeed, sword.WaveLife), size, false, true);
                                yield return null;
                            }
                            if (!owner.Active) yield break;
                            if (!volley.Allows(i)) break;
                            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                            mark = SwordGuardPathMark(slot, origin, origin + aim * Settings.swordSpeed * sword.WaveLife, size);
                            // Direction is committed here. A cool white pulse distinguishes
                            // the locked blade from the other three still orbiting.
                            blades[i].color = Color.white;
                            float lockedUntil = owner.FightSeconds + GuardLockTime;
                            while (owner.Active && volley.Allows(i) && owner.FightSeconds < lockedUntil)
                            {
                                if (!owner.game.Paused)
                                {
                                    UpdateSwordGuardBlades(blades, i);
                                    float pulse = 0.85f + 0.15f * Mathf.Sin(owner.FightSeconds * 32);
                                    blades[i].color = new Color(pulse, 0.95f, 1, 1);
                                    wardVfx?.Aim(i,origin,aim,size,true);
                                }
                                yield return null;
                            }
                            if (!owner.Active) yield break;
                            if (!volley.Allows(i)) break;
                            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                            blades[i].gameObject.SetActive(false); Destroy(blades[i].gameObject); blades[i] = null;
                            shots[i] = owner.Attack(origin, aim * Settings.swordSpeed, size, sword.WaveLife,
                            contact: volley.Contacts[i], pattern: EnemyAttack.SW04, parent: slot);
                            KarmaSwordWardVfx.CounterProjectile(owner.game, owner, shots[i], aim * Settings.swordSpeed, size);
                            wardVfx?.Released(origin,aim);
                            KarmaSwordCounterTrace.Write(Settings.sw04DebugLogging, "VolleyId=" + volley.Id + ", Projectile=" + (i + 1) + ", Result=Released, Time=" + Time.time.ToString("F4"));
                            if (i + 1 < blades.Length)
                            {
                                float nextAt = owner.FightSeconds + GuardShotGap;
                                while (owner.Active && volley.Allows(i + 1) && owner.FightSeconds < nextAt)
                                {
                                    if (!owner.game.Paused) UpdateSwordGuardBlades(blades);
                                    yield return null;
                                }
                            }
                        }
                        if (!owner.Active) yield break;
                        Guarding = false;
                        // Committed counters finish independently without changing
                        // the current attack's animation, state or recovery window.
                        bool flying;
                        do
                        {
                            flying = false;
                            foreach (var shot in shots) flying |= shot != null && shot.gameObject.activeInHierarchy;
                            if (flying) yield return null;
                        } while (owner.Active && flying);
                        ClearSwordGuard(false);
                        yield break;
                    }
                    yield return null;
                }
                ClearSwordGuard(false);
            }
            SpriteRenderer SwordGuardPathMark(Transform slot, Vector2 from, Vector2 to, Vector2 size, bool locked = true)
            {
                var mark = KarmaVisuals.Box(slot, locked ? "고정 공격 경로" : "추적 중인 경로",
                Vector2.zero, Vector2.one, locked ? new Color(0.65f, 0.94f, 1, 0.42f) : new Color(0.3f, 0.85f, 1, 0.22f), 8);
                to = from + (to - from) * KarmaBossProjectileGeometry.SwordShotLife(from, to - from, 1);
                mark.gameObject.AddComponent<KarmaAttackWarning>().SetPath(mark, from, to, size, true, true);
                KarmaSwordAttackPolish.Warning(mark);
                if (locked) mark.GetComponent<KarmaAttackWarning>().Emphasize();
                return mark;
            }
            void ClearSwordGuard(bool stopRoutine = true)
            {
                if (stopRoutine && swordGuardRoutine != null) owner.StopCoroutine(swordGuardRoutine);
                swordGuardRoutine = null;
                wardVfx = null;
                if (swordGuardRoot != null)
                { swordGuardRoot.gameObject.SetActive(false); Destroy(swordGuardRoot.gameObject); swordGuardRoot = null; }
                Guarding = guardResponsePending = guardResponseRunning = false;
                activeVolley = null;
                swordGuardUntil = nextGuardResponse = 0;
            }
            void UpdateSwordGuardBlades(SpriteRenderer[] blades, int aiming = -1)
            {
                // Defense expires on its original deadline, even if the committed
                // four-shot execution continues without armor after that deadline.
                if (owner.FightSeconds >= swordGuardUntil) Guarding = false;
                for (int i = 0; i < blades.Length; i++)
                {
                    if (blades[i] == null || i == aiming) continue;
                    blades[i].enabled = wardVfx == null || wardVfx.Countering;
                    float angle = owner.FightSeconds * 3 + i * Mathf.PI / 2;
                    Vector2 at = owner.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * KarmaSpace.Units(1.6f);
                    float margin = sword.SwordSize(EnemyAttack.SW04).magnitude / 2 + 0.05f;
                    at.x = Mathf.Clamp(at.x, KarmaStage.ArenaLeft + margin, KarmaStage.ArenaRight - margin);
                    at.y = Mathf.Clamp(at.y, margin, KarmaSpace.Units(12) - margin);
                    blades[i].transform.position = at;
                    blades[i].transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                    // Quiet orbiting blades leave the tracking/locked blade readable.
                    blades[i].color = new Color(0.62f, 0.84f, 1, 0.88f);
                }
            }
            public Transform EffectsRoot { get { return swordGuardRoot; } }
            public bool Formed { get { return swordGuardRoot != null; } }
            public float NextReadyAt { get { return nextSwordGuard; } }
            public void Tick() { if (Guarding && owner.FightSeconds >= swordGuardUntil) Guarding = false; }
            public void OnDamaged()
            {
                if (Guarding && owner.FightSeconds < swordGuardUntil && owner.FightSeconds >= nextGuardResponse && !guardResponsePending && !guardResponseRunning)
                {
                    guardResponsePending = true; nextGuardResponse = owner.FightSeconds + Mathf.Max(0.5f, Settings.swordGuardResponseCooldown);
                    wardVfx?.Absorb();
                }
            }
            public void OnParried(KarmaAttackContact contact)
            {
                var volley = activeVolley;
                if (volley == null || !guardResponseRunning || volley.Parried) return;
                int index = System.Array.IndexOf(volley.Contacts, contact);
                if (index < 0) return;
                KarmaSwordCounterTrace.Write(Settings.sw04DebugLogging, "VolleyId=" + volley.Id + ", Projectile=" + (index + 1) + ", Result=ParryDelivered");
                volley.LastAllowed = Mathf.Min(volley.LastAllowed, index);
                volley.Parried = true;
                Guarding = guardResponsePending = false;
                var pool = KarmaSwordVfxPool.For(owner.game);
                var accent = owner.art.GetComponent<KarmaSwordAttackAccent>();
                // Stop the contact blade too: a consumed projectile continuing
                // across the player looked like another shot after success.
                for (int later = index; later < volley.Slots.Length; later++)
                {
                    if (later > index)
                        KarmaSwordCounterTrace.Write(Settings.sw04DebugLogging, "VolleyId=" + volley.Id + ", Cancelled=" + (later + 1) + ", State=" + (volley.Shots[later] != null ? "Flying" : "Queued"));
                    // Contact suppression and deactivation are synchronous, even
                    // if another shot's Update is scheduled in this same frame.
                    volley.Contacts[later].TryConsume();
                    var slot = volley.Slots[later];
                    if (slot != null)
                    {
                        pool.Cancel(slot); accent?.Cancel(slot);
                        slot.gameObject.SetActive(false);
                    }
                }
                // Cosmetics run only after every later contact/slot is suppressed.
                if (wardVfx != null)
                { wardVfx.Collapse(); wardVfx.gameObject.SetActive(false); Destroy(wardVfx.gameObject); wardVfx = null; }
                var body = owner.game.Player.GetComponent<Collider2D>();
                Vector2 parryAt = body != null ? (Vector2)body.bounds.center : owner.game.Player.Position;
                KarmaSwordAttackPolish.ParrySuccess(owner.game, owner, parryAt);
                pool.Flash(owner, "Sword_GuardRing_v6", parryAt, Vector2.one * 2.8f, 0.22f);
                for (int later = index; later < volley.Slots.Length; later++)
                {
                    // Only the unspent successors dissolve. The parried blade
                    // disappears into the single success cue at the player.
                    if (later > index)
                        pool.Dissolve(owner, volley.Shots[later] != null ? volley.Shots[later].Visual : volley.Blades[later]);
                    if (volley.Slots[later] != null) Destroy(volley.Slots[later].gameObject);
                }
                int remaining = 0;
                for (int i = 0; i < volley.Shots.Length; i++)
                    if (volley.Shots[i] != null && volley.Shots[i].gameObject.activeInHierarchy && !volley.Contacts[i].Consumed) remaining++;
                KarmaSwordCounterTrace.Write(Settings.sw04DebugLogging, "VolleyId=" + volley.Id + ", RemainingActive=" + remaining);
                // Earlier shots keep their own lifetime. The routine drains
                // those shots, then performs its normal independent cleanup.
            }
            public void TraceStarting(KarmaAttackContact contact)
            {
                if (!Settings.sw04DebugLogging || activeVolley == null) return;
                int index = System.Array.IndexOf(activeVolley.Contacts, contact);
                if (index >= 0) activeVolley.Trace[index] = KarmaSwordCounterTrace.Capture(owner.game.Player);
            }
            public void TraceResolved(KarmaAttackContact contact)
            {
                if (!Settings.sw04DebugLogging || activeVolley == null) return;
                int index = System.Array.IndexOf(activeVolley.Contacts, contact);
                if (index >= 0) KarmaSwordCounterTrace.Resolved(activeVolley.Id, index + 1, activeVolley.Trace[index], owner.game.Player);
            }
            public override void Interrupt() { ClearSwordGuard(); }
            public override bool CanReach(Vector2 from, Vector2 target)
            {
                float facing = target.x < from.x ? -1 : 1;
                return sword.SwordShotCanReach(from, SwordBossBehavior.SwordAim(from, target, facing) * Settings.swordSpeed,
                    sword.SwordSize(EnemyAttack.SW04), sword.WaveLife, target);
            }

        }
    }

    // Diagnostics never arm counters, change player state, or synthesize success.
    internal static class KarmaSwordCounterTrace
    {
        internal struct State
        {
            public float health, time, invulnerable, cooldown;
            public int parries;
            public bool ready, stunned, guarded;
        }
        internal static State Capture(KarmaPlayer player)
        {
            if (!Debug.isDebugBuild) return default;
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(KarmaPlayer);
            var techniques = (KarmaTechniqueState)type.GetField("techniques", flags).GetValue(player);
            return new State { health = player.Health, time = Time.time, parries = player.ParryCount,
                invulnerable = (float)type.GetField("invulnerableUntil", flags).GetValue(player),
                cooldown = player.Essences.Cooldown(Essence.Guard), ready = techniques.CounterReady(Time.time),
                stunned = player.Stunned, guarded = player.Shielded };
        }
        internal static void Write(bool enabled, string message) { if (enabled && Debug.isDebugBuild) Debug.Log("[SW04] " + message); }
        internal static void Resolved(int volley, int index, State before, KarmaPlayer player)
        {
            string result = player.ParryCount > before.parries ? "ParrySuccess" : player.Health < before.health ? "PlayerHit"
                : before.time < before.invulnerable ? "IgnoredInvulnerability" : before.guarded ? "Guard" : "NoDamage";
            Write(true, "VolleyId=" + volley + ", Projectile=" + index + ", Result=" + result
                + ", Time=" + before.time.ToString("F4") + ", ParryBefore=" + before.parries + ", ParryAfter=" + player.ParryCount
                + ", CounterReady=" + before.ready + ", InvulRemaining=" + Mathf.Max(0, before.invulnerable - before.time).ToString("F4")
                + ", Stunned=" + before.stunned + ", Cooldown=" + before.cooldown.ToString("F4"));
        }
    }
}
