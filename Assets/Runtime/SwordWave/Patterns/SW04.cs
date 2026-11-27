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
                public bool Parried, Returning;
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
                float warning = owner.Warning(0.7f);
                owner.animator.PrepareSwordShieldRaise(warning);
                if (owner.game.IsCombat && owner.Alive) KarmaAudio.Play(KarmaSfxId.SwEnergyCharge, warning, owner);
                yield return owner.Wait(warning);
                KarmaAudio.StopSlot("sw-bed");
                owner.state = ActionState.Attack; Guarding = true; guardResponsePending = guardResponseRunning = false;
                owner.animator.PlayWavePhase("Guard");
                Vector2? shieldOrigin = owner.animator.ActivateSwordShieldRaise();
                swordGuardUntil = owner.FightSeconds + Mathf.Max(0.6f, Settings.swordGuardDuration) + 2f;
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
                    KarmaSwordWardVfx.ApplyBlade(blades[i], sword.SwordSize(EnemyAttack.SW04), Vector2.right);
                }
                wardVfx = KarmaSwordWardVfx.Create(swordGuardRoot,owner.game,owner,blades,shieldOrigin);
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
                            float aimingOrbitAt = owner.FightSeconds;
                            Vector2 origin = blades[i].transform.position, aim = Vector2.right * owner.direction;
                            var slot = volley.Slots[i];
                            var mark = SwordGuardPathMark(slot, origin, origin, size, false);
                            if (!owner.running || owner.selected == EnemyAttack.SW04)
                                owner.animator.PrepareSwordBody("Counter", GuardTrackingTime + GuardLockTime, i);
                            float trackingUntil = owner.FightSeconds + GuardTrackingTime;
                            while (owner.Active && volley.Allows(i) && owner.FightSeconds < trackingUntil)
                            {
                                if (owner.game.Paused) { yield return null; continue; }
                                UpdateSwordGuardBlades(blades, i, aimingOrbitAt);
                                origin = blades[i].transform.position;
                                aim = ((Vector2)sword.SwordPlayerBounds.center - origin).normalized;
                                if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * owner.direction;
                                blades[i].transform.rotation = Quaternion.RotateTowards(blades[i].transform.rotation,
                                    Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg), 420 * Time.deltaTime);
                                blades[i].color = new Color(0.42f, 0.86f, 1, 1);
                                wardVfx?.Aim(i, false);
                                UpdateSwordGuardPathMark(mark, origin,
                                    origin + aim * Settings.swordSpeed * KarmaBossProjectileGeometry.SwordShotLife(origin, aim * Settings.swordSpeed, sword.WaveLife), size, false);
                                yield return null;
                            }
                            if (!owner.Active) yield break;
                            if (!volley.Allows(i)) break;
                            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                            mark = SwordGuardPathMark(slot, origin, origin + aim * Settings.swordSpeed * sword.WaveLife, size);
                            // Commit direction, not world position. The selected surface
                            // point follows the moving boss until the projectile is released.
                            blades[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                            blades[i].color = Color.white;
                            float lockedUntil = owner.FightSeconds + GuardLockTime;
                            while (owner.Active && volley.Allows(i) && owner.FightSeconds < lockedUntil)
                            {
                                if (!owner.game.Paused)
                                {
                                    UpdateSwordGuardBlades(blades, i, aimingOrbitAt);
                                    origin = blades[i].transform.position;
                                    UpdateSwordGuardPathMark(mark, origin,
                                        origin + aim * Settings.swordSpeed * KarmaBossProjectileGeometry.SwordShotLife(origin, aim * Settings.swordSpeed, sword.WaveLife), size, true);
                                    blades[i].color = Color.white;
                                    wardVfx?.Aim(i, true);
                                }
                                yield return null;
                            }
                            if (!owner.Active) yield break;
                            if (!volley.Allows(i)) break;
                            // Release from the last advertised origin, even if the boss
                            // moves between the final warning frame and this coroutine.
                            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                            blades[i].gameObject.SetActive(false); Destroy(blades[i].gameObject); blades[i] = null;
                            shots[i] = owner.Attack(origin, aim * Settings.swordSpeed, size, sword.WaveLife,
                            contact: volley.Contacts[i], pattern: EnemyAttack.SW04, parent: slot);
                            if (!owner.running || owner.selected == EnemyAttack.SW04)
                                owner.animator.ReleaseSwordBody("Counter", i);
                            KarmaSwordWardVfx.CounterProjectile(owner.game, owner, shots[i], aim * Settings.swordSpeed, size);
                            KarmaAudio.Play(KarmaSfxId.SwWaveLaunch, lifetimeOwner: shots[i]);
                            wardVfx?.Released();
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
                        if (!volley.Returning) Guarding = false;
                        // Committed counters finish independently without changing
                        // the current attack's animation, state or recovery window.
                        bool flying;
                        do
                        {
                            flying = volley.Returning;
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
                mark.gameObject.AddComponent<KarmaAttackWarning>();
                UpdateSwordGuardPathMark(mark, from, to, size, locked);
                KarmaSwordAttackPolish.Warning(mark);
                if (locked) mark.GetComponent<KarmaAttackWarning>().Emphasize();
                return mark;
            }
            void UpdateSwordGuardPathMark(SpriteRenderer mark, Vector2 from, Vector2 to, Vector2 size, bool locked)
            {
                mark.GetComponent<KarmaAttackWarning>().SetPath(mark, from, to, size, locked, true);
            }
            void ClearSwordGuard(bool stopRoutine = true)
            {
                owner.animator.EndSwordCounterBody();
                if (stopRoutine && swordGuardRoutine != null) owner.StopCoroutine(swordGuardRoutine);
                swordGuardRoutine = null;
                if (!stopRoutine && owner.Active && owner.game.IsCombat)
                {
                    wardVfx?.Finish();
                }
                wardVfx = null;
                if (swordGuardRoot != null)
                { swordGuardRoot.gameObject.SetActive(false); Destroy(swordGuardRoot.gameObject); swordGuardRoot = null; }
                Guarding = guardResponsePending = guardResponseRunning = false;
                activeVolley = null;
                swordGuardUntil = nextGuardResponse = 0;
            }
            void UpdateSwordGuardBlades(SpriteRenderer[] blades, int aiming = -1, float aimingOrbitAt = 0)
            {
                // Defense expires on its original deadline, even if the committed
                // four-shot execution continues without armor after that deadline.
                if (owner.FightSeconds >= swordGuardUntil && !(activeVolley?.Returning ?? false)) Guarding = false;
                for (int i = 0; i < blades.Length; i++)
                {
                    if (blades[i] == null) continue;
                    // The four real counter swords must remain visible while guarding.
                    blades[i].enabled = true;
                    // Decelerate the selected orbit to a stop during tracking.
                    // The integral has continuous velocity at the selection frame.
                    float braking=Mathf.Clamp01((owner.FightSeconds-aimingOrbitAt)/GuardTrackingTime);
                    float orbitTime=i==aiming ? aimingOrbitAt+GuardTrackingTime*(braking-braking*braking+braking*braking*braking/3)
                        : owner.FightSeconds;
                    Vector2 at = wardVfx.BladeOrbitPosition(i, orbitTime);
                    Vector2 tangent = wardVfx.BladeOrbitPosition(i, owner.FightSeconds + 0.01f)
                        - wardVfx.BladeOrbitPosition(i, owner.FightSeconds - 0.01f);
                    float margin = sword.SwordSize(EnemyAttack.SW04).magnitude / 2 + 0.05f;
                    at.x = Mathf.Clamp(at.x, KarmaStage.ArenaLeft + margin, KarmaStage.ArenaRight - margin);
                    at.y = Mathf.Clamp(at.y, margin, KarmaSpace.Units(12) - margin);
                    blades[i].transform.position = at;
                    // Keep the committed aim/appearance while translating with the dome.
                    if (i == aiming) continue;
                    if (tangent.sqrMagnitude > 0.000001f)
                        blades[i].transform.rotation = Quaternion.RotateTowards(blades[i].transform.rotation,
                            Quaternion.Euler(0, 0, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg),420*Time.deltaTime);
                    // Quiet orbiting blades leave the tracking/locked blade readable.
                    wardVfx.ApplyBladeOrbitDepth(i, owner.FightSeconds);
                    blades[i].color = new Color(0.86f, 0.95f, 1, wardVfx.BladeOrbitAlpha(i, owner.FightSeconds));
                }
            }
            public Transform EffectsRoot { get { return swordGuardRoot; } }
            public bool Formed { get { return swordGuardRoot != null; } }
            public float NextReadyAt { get { return nextSwordGuard; } }
            public void Tick() { if (Guarding && owner.FightSeconds >= swordGuardUntil && !(activeVolley?.Returning ?? false)) Guarding = false; }
            public void OnDamaged()
            {
                if (Guarding && owner.FightSeconds < swordGuardUntil && owner.FightSeconds >= nextGuardResponse && !guardResponsePending && !guardResponseRunning)
                {
                    guardResponsePending = true; nextGuardResponse = owner.FightSeconds + Mathf.Max(0.5f, Settings.swordGuardResponseCooldown);
                    wardVfx?.Absorb();
                }
            }
            public void GuardImpact() { wardVfx?.Pulse(); }
            public void OnParried(KarmaAttackContact contact)
            {
                var volley = activeVolley;
                if (volley == null || !guardResponseRunning || volley.Parried) return;
                int index = System.Array.IndexOf(volley.Contacts, contact);
                if (index < 0) return;
                KarmaSwordCounterTrace.Write(Settings.sw04DebugLogging, "VolleyId=" + volley.Id + ", Projectile=" + (index + 1) + ", Result=ParryDelivered");
                volley.LastAllowed = Mathf.Min(volley.LastAllowed, index);
                volley.Parried = true;
                guardResponsePending = false;
                volley.Returning = true;
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
                var body = owner.game.Player.GetComponent<Collider2D>();
                Vector2 parryAt = body != null ? (Vector2)body.bounds.center : owner.game.Player.Position;
                KarmaSwordAttackPolish.ParrySuccess(owner.game, owner, parryAt);
                pool.Flash(owner, "Sword_GuardRing_v6", parryAt, Vector2.one * 2.8f, 0.22f);
                owner.StartCoroutine(ReturnParriedBlade(volley, parryAt));
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
            IEnumerator ReturnParriedBlade(CounterVolley volley, Vector2 origin)
            {
                var blade = KarmaVisuals.Box(swordGuardRoot, "SW04 reflected blade", origin,
                    Vector2.one, Color.white, 13);
                KarmaSwordWardVfx.ApplyBlade(blade, sword.SwordSize(EnemyAttack.SW04), Vector2.right);
                // Even point-blank returns arrive on a later frame, after the host
                // has finished resolving the player's parry contact.
                yield return null;
                while (owner.Active && !owner.Stunned && activeVolley == volley && blade != null)
                {
                    if (owner.game.Paused) { yield return null; continue; }
                    Vector2 at = blade.transform.position;
                    var body = owner.GetComponent<Collider2D>();
                    Vector2 target = body != null ? (Vector2)body.bounds.ClosestPoint(at) : owner.Position;
                    Vector2 delta = target - at;
                    float step = Mathf.Max(1, Settings.swordSpeed) * Time.deltaTime;
                    blade.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    blade.transform.position = Vector2.MoveTowards(at, target, step);
                    if (delta.magnitude <= step)
                    {
                        volley.Returning = false;
                        Guarding = false;
                        if (wardVfx != null)
                        {
                            wardVfx.Collapse();
                            wardVfx.gameObject.SetActive(false); Destroy(wardVfx.gameObject); wardVfx = null;
                        }
                        blade.gameObject.SetActive(false); Destroy(blade.gameObject);
                        sword.OnReturnedCounterHit();
                        yield break;
                    }
                    yield return null;
                }
                volley.Returning = false;
                if (blade != null) { blade.gameObject.SetActive(false); Destroy(blade.gameObject); }
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
