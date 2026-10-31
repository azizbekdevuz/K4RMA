using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        Vector2 WaveSize { get { return new Vector2(Mathf.Max(KarmaSpace.Units(0.2f), Tuning.swordWidth), Mathf.Clamp(Tuning.swordHeight, KarmaSpace.Units(0.2f), KarmaSpace.Units(0.8f))); } }
        Vector2 SwordSize(EnemyAttack attack)
        {
            if (attack == EnemyAttack.SW02)
            {
                // Bound the whole ordinary jump, including the player's real body.
                // The head at its apex stays below the top of this single slash.
                float gravity = Mathf.Max(0.01f, Mathf.Abs(Physics2D.gravity.y * game.Config.gravityScale));
                float jump = game.Config.jumpSpeed * game.Config.jumpSpeed / (2 * gravity);
                float height = jump + SwordPlayerBounds.size.y + 0.6f - AimedSlashFloor;
                return new Vector2(Mathf.Max(WaveSize.x * 1.35f * 1.7f, height * 0.6f), height);
            }
            return WaveSize * (attack == EnemyAttack.SW01 || attack == EnemyAttack.SW03 ? new Vector2(1.35f, 1.30f) : Vector2.one * 1.30f);
        }
        Vector2 ScarSize { get { return KarmaSpace.Units(new Vector2(1.8f * 1.25f, 5 * 1.35f)); } }
        Bounds SwordPlayerBounds
        {
            get
            {
                var collider = game.Player.GetComponent<Collider2D>();
                return collider != null && collider.enabled ? collider.bounds
                    : new Bounds(game.Player.Position, KarmaSpace.PlayerSize);
            }
        }
        bool PlaceSwordScars(float lowX, float highX, out Vector2 a, out Vector2 b)
        {
            var size = ScarSize;
            var player = SwordPlayerBounds;
            // Measure the corridor between damage edges, in Unity world units.
            float gap = Mathf.Max(3, player.size.x + 0.5f);
            float halfSeparation = (size.x + gap) / 2;
            float inset = halfSeparation + size.x / 2;
            bool pair = highX - lowX >= inset * 2;
            float x = pair ? Mathf.Clamp(player.center.x, lowX + inset, highX - inset) : (lowX + highX) / 2;
            a = new Vector2(x - (pair ? halfSeparation : 0), size.y / 2);
            b = new Vector2(x + (pair ? halfSeparation : 0), size.y / 2);
            return pair;
        }
        float WaveLife { get { return Mathf.Clamp(Tuning.swordLifetime, 0.5f, 2); } }
        Vector2 BasicSwordSize { get { return WaveSize * new Vector2(0.9f, 1); } }
        float BasicSwordSpeed { get { return Mathf.Max(KarmaSpace.Units(5), Tuning.swordSpeed * 0.8f); } }
        float BasicSwordLife { get { return KarmaSpace.Units(5) / BasicSwordSpeed; } }
        IEnumerator BasicSwordWave()
        {
            state = ActionState.Telegraph;
            Vector2 origin = Position;
            Vector2 aim = SwordAim(origin, SwordPlayerBounds.center, direction);
            float warning = Warning(0.4f);
            var mark = PathMark(origin, origin + aim * BasicSwordSpeed * BasicSwordLife, BasicSwordSize);
            animator.PrepareWaveAttack(warning, false);
            yield return Wait(warning);
            mark.gameObject.SetActive(false); Destroy(mark.gameObject);
            state = ActionState.Attack; animator.Attack(aim);
            Attack(origin, aim * BasicSwordSpeed, BasicSwordSize, BasicSwordLife, 0.5f);
            WaveLaunchVFX(origin, aim * BasicSwordSpeed, BasicSwordSize, false);
            yield return Wait(BasicSwordLife);
            yield return Recovery(0.65f);
        }
        IEnumerator StaggeredWaves(bool scars)
        {
            state = ActionState.Telegraph;
            // Alternating lanes force repeated jump/landing decisions.
            Vector2 first = new Vector2(Position.x + direction * KarmaSpace.Scale, KarmaSpace.Units(1.05f));
            Vector2 second = new Vector2(Position.x + direction * KarmaSpace.Scale, KarmaSpace.Units(3.3f));
            Vector2 velocity = Vector2.right * direction * Mathf.Max(KarmaSpace.Scale, Tuning.swordSpeed);
            Vector2 waveSize = SwordSize(scars ? EnemyAttack.SW03 : EnemyAttack.SW01);
            // SW01 ends after two lanes; SW03 keeps its existing volley before the scars.
            int shots = scars ? (Enraged ? 4 : 3) : 2;
            for (int i = 0; i < shots; i++)
            {
                Vector2 origin = i % 2 == 0 ? first : second;
                var mark = PathMark(origin, origin + velocity * WaveLife, waveSize);
                Intent = "교차 검기 " + (i + 1) + "/" + shots + (i % 2 == 0 ? " · 낮은 검기 / 점프" : " · 높은 검기 / 착지");
                float warning = i == 0 ? Warning(0.65f) : Mathf.Max(0.42f, 0.54f * Tempo);
                if (!scars)
                {
                    state = ActionState.Telegraph;
                    animator.PrepareWaveAttack(warning, false);
                }
                else animator.PlayWavePhase("Windup", warning);
                yield return Wait(warning);
                state = ActionState.Attack; animator.Attack(); Destroy(mark.gameObject);
                Attack(origin, velocity, waveSize, WaveLife);
                if (!scars) WaveLaunchVFX(origin, velocity, waveSize, false);
                else { animator.ReleaseWavePhase(); animator.PlayWavePhase("Guard"); }
            }
            yield return Wait(WaveLife);
            if (scars)
            {
                // Shift the whole pair inside the arena, independently of the
                // preceding volley's shorter travelled lane. Keep the gap intact.
                var size = ScarSize;
                Vector2 a, b;
                bool pair = PlaceSwordScars(KarmaStage.ArenaLeft, KarmaStage.ArenaRight, out a, out b);
                Mark("검흔 상승 범위", a, size);
                if (pair) Mark("검흔 상승 범위", b, size);
                Intent = "SW03 검흔 고정 · 상승 예고 / 검흔 사이로";
                animator.PlayWavePhase("Windup");
                yield return Wait(Warning(0.75f)); ClearPatternObjects();
                // One attack/contact per scar survives the original rise and
                // three planted seconds, so touching it cannot deal frame damage.
                Attack(a, Vector2.zero, size, 3.25f);
                if (pair) Attack(b, Vector2.zero, size, 3.25f);
                foreach (var point in pair ? new[] { a, b } : new[] { a })
                {
                    var impact = KarmaVisuals.Box(effectsRoot, "Wave ground impact / no independent damage",
                        new Vector2(point.x, size.x * 0.3f), Vector2.one, Color.white, 24);
                    KarmaWaveArtwork.Apply(impact, "SW03_GroundImpact", new Vector2(size.x, size.x * 0.6f), Vector2.zero);
                    impact.gameObject.AddComponent<KarmaEffect>().Initialize(0.25f);
                }
                animator.PlayWavePhase("Rising");
                yield return Wait(0.25f);
                Intent = "SW03 검흔 유지 · 검흔 사이에서 회피";
                yield return Wait(3);
            }
            yield return Recovery(1);
        }
        const float AimedSlashFloor = 0.05f;
        float AimedSlashSpeed { get { return Mathf.Max(0.1f, Tuning.swordSpeed * 1.25f); } }
        Vector2 AimedSlashOrigin(Vector2 from, float facing)
        {
            var size = SwordSize(EnemyAttack.SW02);
            float corridor = SwordPlayerBounds.size.x + 0.2f;
            // An outguard wall cast still needs visible travel and an Update for
            // damage. A zero lifetime would be cleared synchronously by recovery.
            float launchRoom = size.x / 2;
            return new Vector2(Mathf.Clamp(from.x + facing * size.x / 2,
                KarmaStage.ArenaLeft + size.x / 2 + (facing > 0 ? corridor : launchRoom),
                KarmaStage.ArenaRight - size.x / 2 - (facing < 0 ? corridor : launchRoom)),
                AimedSlashFloor + size.y / 2);
        }
        internal static float AimedSlashBoundaryLife(Vector2 origin, Vector2 velocity, Vector2 size, float life)
        {
            if (velocity.x > 0) life = Mathf.Min(life, (KarmaStage.ArenaRight - size.x / 2 - origin.x) / velocity.x);
            else if (velocity.x < 0) life = Mathf.Min(life, (KarmaStage.ArenaLeft + size.x / 2 - origin.x) / velocity.x);
            return Mathf.Max(0, life);
        }
        float AimedSlashLife(Vector2 origin, Vector2 velocity)
        { return AimedSlashBoundaryLife(origin, velocity, SwordSize(EnemyAttack.SW02), float.MaxValue); }
        float AimedSlashLockTime(Vector2 origin, float facing, Vector2 size)
        {
            // At an outguard-facing wall the clamped rear edge can extend behind
            // the boss. Advertise enough time to clear that actual edge on foot.
            var player = SwordPlayerBounds;
            float safeX = origin.x - facing * (size.x / 2 + player.extents.x + 0.15f);
            float travel = Mathf.Max(0, facing * (player.center.x - safeX));
            // Reserve ground-friction margin as well as reaction and fixed-step latency.
            float walkingSpeed = Mathf.Max(0.01f, game.Config.moveSpeed * 0.9f);
            return Mathf.Max(0.9f, travel / walkingSpeed + 0.35f + 2 * Time.fixedDeltaTime);
        }
        IEnumerator AimedWave()
        {
            state = ActionState.Telegraph;
            Vector2 size = SwordSize(EnemyAttack.SW02);
            float facing = game.Player.Position.x < Position.x ? -1 : 1;
            Vector2 origin = AimedSlashOrigin(Position, facing);
            Vector2 velocity = Vector2.right * facing * AimedSlashSpeed;
            var marker = PathMark(origin, origin + velocity * AimedSlashLife(origin, velocity), size, false);
            float tracking = 0.4f * Tempo, elapsed = 0;
            Intent = "SW02 수평 조준 중 · 고정 후 보스 뒤로";
            animator.PlayPose("Windup", tracking + 0.18f);
            while (elapsed < tracking && Active)
            {
                if (game.Paused) { yield return null; continue; }
                facing = game.Player.Position.x < Position.x ? -1 : 1;
                origin = AimedSlashOrigin(Position, facing);
                velocity = Vector2.right * facing * AimedSlashSpeed;
                marker.GetComponent<KarmaAttackWarning>().SetPath(marker, origin,
                    origin + velocity * AimedSlashLife(origin, velocity), size, false, true);
                elapsed += Time.deltaTime; yield return null;
            }
            if (!Active) yield break;
            marker.gameObject.SetActive(false); Destroy(marker.gameObject);
            // Freeze both the horizontal direction and origin for this one release.
            direction = facing;
            art.localScale = new Vector3(facing * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            float life = AimedSlashLife(origin, velocity);
            marker = PathMark(origin, origin + velocity * life, size);
            Intent = "SW02 " + (facing < 0 ? "왼쪽" : "오른쪽") + " 고정 · 보스 뒤로 / 점프 후 상승 기술";
            float warning = AimedSlashLockTime(origin, facing, size);
            animator.PrepareWaveAttack(warning, true);
            yield return Wait(warning);
            if (!Active) yield break;
            marker.gameObject.SetActive(false); Destroy(marker.gameObject);
            state = ActionState.Attack; animator.Attack(Vector2.right * facing);
            Attack(origin, velocity, size, life, 1.15f);
            WaveLaunchVFX(origin, velocity, size, true);
            yield return Wait(life);
            yield return Recovery(1.1f);
        }
        void WaveLaunchVFX(Vector2 origin, Vector2 velocity, Vector2 size, bool aimed)
        {
            var sprite = KarmaArtwork.Sprite("GuardianVFX", aimed ? "Beam" : "Cleave");
            if (sprite == null) return;
            Vector2 axis = velocity.normalized;
            var bounds = sprite.bounds.size;
            float width = Mathf.Abs(axis.x) * bounds.x + Mathf.Abs(axis.y) * bounds.y;
            float height = Mathf.Abs(axis.y) * bounds.x + Mathf.Abs(axis.x) * bounds.y;
            float fit = Mathf.Min(size.x / width, size.y / height) * 0.7f;
            // Fit the rotated core/echo to this shot's footprint. No collider or damage;
            // ownership keeps the brief swing and existing afterimages cancellable.
            KarmaCombatVFX.Burst(effectsRoot, origin, sprite, "Wave sword swing",
                new Vector2(bounds.x, bounds.y) * fit, 0.18f,
                Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg, new Color(1, 0.38f, 0.12f), WaveVisuals);
        }
        bool guardResponsePending, guardResponseRunning;
        Transform swordGuardRoot;
        Coroutine swordGuardRoutine;
        float nextSwordGuard;
        float GuardTrackingTime { get { return 0.35f; } }
        float GuardLockTime { get { return Mathf.Max(0.3f, Tuning.counterWarning); } }
        float GuardShotGap { get { return Mathf.Max(0.16f, Tuning.swordGuardResponseCooldown - GuardTrackingTime - GuardLockTime); } }
        IEnumerator CounterGuard()
        {
            if (swordGuardRoot != null) yield break;
            state = ActionState.Telegraph;
            animator.PlayWavePhase("Windup");
            var formation = Mark("검막 형성", Position, KarmaSpace.Units(new Vector2(3, 3)));
            yield return Wait(Warning(0.7f)); Destroy(formation.gameObject);
            state = ActionState.Attack; swordGuard = true; guardResponsePending = guardResponseRunning = false;
            animator.PlayWavePhase("Guard");
            swordGuardUntil = FightSeconds + Mathf.Max(0.6f, Tuning.swordGuardDuration);
            nextGuardResponse = FightSeconds;
            nextSwordGuard = FightSeconds + Mathf.Max(8, Tuning.swordGuardDuration + 4);
            swordGuardRoot = new GameObject("Independent sword guard").transform;
            swordGuardRoot.SetParent(effectsRoot, false);
            var blades = new SpriteRenderer[4];
            for (int i = 0; i < blades.Length; i++)
            {
                blades[i] = KarmaVisuals.Box(swordGuardRoot, "회전 검막 · 접촉 피해 없음", Position, Vector2.one, Color.white, 13);
                KarmaWaveArtwork.Apply(blades[i], "SW04_CounterWave", SwordSize(EnemyAttack.SW04), Vector2.right);
            }
            swordGuardRoutine = StartCoroutine(MaintainSwordGuard(blades));
            // Only formation occupies the action slot. The buff, aim warnings
            // and counter projectiles own a separate lifetime and object root.
            yield return Recovery(0.45f, message: "검막 유지 · 다음 공격 준비");
        }
        IEnumerator MaintainSwordGuard(SpriteRenderer[] blades)
        {
            while ((swordGuard && FightSeconds < swordGuardUntil || guardResponsePending) && Active)
            {
                if (game.Paused) { yield return null; continue; }
                UpdateSwordGuardBlades(blades);
                if (guardResponsePending)
                {
                    guardResponsePending = false; guardResponseRunning = true;
                    var shots = new KarmaBossAttack[blades.Length];
                    var size = SwordSize(EnemyAttack.SW04);
                    for (int i = 0; i < blades.Length && Active; i++)
                    {
                        Vector2 origin = blades[i].transform.position, aim = Vector2.right * direction;
                        var mark = SwordGuardPathMark(origin, origin, size, false);
                        float trackingUntil = FightSeconds + GuardTrackingTime;
                        while (Active && FightSeconds < trackingUntil)
                        {
                            if (game.Paused) { yield return null; continue; }
                            UpdateSwordGuardBlades(blades, i);
                            origin = blades[i].transform.position;
                            aim = ((Vector2)SwordPlayerBounds.center - origin).normalized;
                            if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * direction;
                            blades[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                            mark.GetComponent<KarmaAttackWarning>().SetPath(mark, origin,
                                origin + aim * Tuning.swordSpeed * SwordShotLife(origin, aim * Tuning.swordSpeed, WaveLife), size, false, true);
                            yield return null;
                        }
                        if (!Active) yield break;
                        mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                        mark = SwordGuardPathMark(origin, origin + aim * Tuning.swordSpeed * WaveLife, size);
                        float lockedUntil = FightSeconds + GuardLockTime;
                        while (Active && FightSeconds < lockedUntil)
                        {
                            if (!game.Paused) UpdateSwordGuardBlades(blades, i);
                            yield return null;
                        }
                        if (!Active) yield break;
                        mark.gameObject.SetActive(false); Destroy(mark.gameObject);
                        blades[i].gameObject.SetActive(false); Destroy(blades[i].gameObject); blades[i] = null;
                        shots[i] = Attack(origin, aim * Tuning.swordSpeed, size, WaveLife,
                            pattern: EnemyAttack.SW04, parent: swordGuardRoot);
                        if (i + 1 < blades.Length)
                        {
                            float nextAt = FightSeconds + GuardShotGap;
                            while (Active && FightSeconds < nextAt)
                            {
                                if (!game.Paused) UpdateSwordGuardBlades(blades);
                                yield return null;
                            }
                        }
                    }
                    if (!Active) yield break;
                    swordGuard = false;
                    // Committed counters finish independently without changing
                    // the current attack's animation, state or recovery window.
                    bool flying;
                    do
                    {
                        flying = false;
                        foreach (var shot in shots) flying |= shot != null && shot.gameObject.activeInHierarchy;
                        if (flying) yield return null;
                    } while (Active && flying);
                    ClearSwordGuard(false);
                    yield break;
                }
                yield return null;
            }
            ClearSwordGuard(false);
        }
        SpriteRenderer SwordGuardPathMark(Vector2 from, Vector2 to, Vector2 size, bool locked = true)
        {
            var mark = KarmaVisuals.Box(swordGuardRoot, locked ? "고정 공격 경로" : "추적 중인 경로",
                Vector2.zero, Vector2.one, locked ? new Color(1, 0.55f, 0.2f, 0.3f) : new Color(0.3f, 0.85f, 1, 0.22f), 8);
            to = from + (to - from) * SwordShotLife(from, to - from, 1);
            mark.gameObject.AddComponent<KarmaAttackWarning>().SetPath(mark, from, to, size, true, true);
            return mark;
        }
        void ClearSwordGuard(bool stopRoutine = true)
        {
            if (stopRoutine && swordGuardRoutine != null) StopCoroutine(swordGuardRoutine);
            swordGuardRoutine = null;
            if (swordGuardRoot != null)
            { swordGuardRoot.gameObject.SetActive(false); Destroy(swordGuardRoot.gameObject); swordGuardRoot = null; }
            swordGuard = guardResponsePending = guardResponseRunning = false;
            swordGuardUntil = nextGuardResponse = 0;
        }
        void UpdateSwordGuardBlades(SpriteRenderer[] blades, int aiming = -1)
        {
            // Defense expires on its original deadline, even if the committed
            // four-shot execution continues without armor after that deadline.
            if (FightSeconds >= swordGuardUntil) swordGuard = false;
            for (int i = 0; i < blades.Length; i++)
            {
                if (blades[i] == null || i == aiming) continue;
                float angle = FightSeconds * 3 + i * Mathf.PI / 2;
                Vector2 at = Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * KarmaSpace.Units(1.6f);
                float margin = SwordSize(EnemyAttack.SW04).magnitude / 2 + 0.05f;
                at.x = Mathf.Clamp(at.x, KarmaStage.ArenaLeft + margin, KarmaStage.ArenaRight - margin);
                at.y = Mathf.Clamp(at.y, margin, KarmaSpace.Units(12) - margin);
                blades[i].transform.position = at;
                blades[i].transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }
        }
        Vector2 AerialSwordCastPosition { get { return new Vector2(KarmaStage.ArenaCenter, KarmaSpace.Units(6)); } }
        IEnumerator AerialSwords()
        {
            state = ActionState.Telegraph;
            animator.PlayWavePhase("Windup");
            Mark("도약 준비 · 접촉 피해 없음", Position, KarmaSpace.Units(new Vector2(2, 2)));
            yield return Wait(0.8f); ClearPatternObjects();
            Vector2 ground = new Vector2(KarmaStage.ArenaCenter, KarmaSpace.GuardianGroundY);
            animator.PlayWavePhase("Rising");
            Intent = "SW05 중앙 천장으로 이동";
            yield return Motion(AerialSwordCastPosition, 0.5f, Vector2.zero);
            if (!Active) yield break;
            transform.position = AerialSwordCastPosition;
            fullShield = true;
            animator.PlayWavePhase("Guard");
            int seed = KarmaAerialSwordPlanner.ResolveSeed(Tuning.aerialSwordSeed);
            var planner = KarmaAerialSwordPlanner.Create(effectsRoot, game, seed);
            int groups = planner.GroupSizes.Length;
            Vector2 size = SwordSize(EnemyAttack.SW05);
            float warning = Mathf.Max(0.75f, Warning(0.75f)), interval = Mathf.Max(AerialSwordInterval(), warning + 0.15f);
            float firstReleaseAt = FightSeconds + warning;
            for (int group = 0; group < groups && Active; group++)
            {
                // Absolute release slots avoid accumulating warning/gap rounding.
                // Old swords keep moving while the next full warning is formed.
                float rhythm = group % 3 == 1 ? 0.06f : group % 3 == 2 ? -0.04f : 0;
                float formationAt = firstReleaseAt + group * interval + rhythm - warning;
                while (Active && FightSeconds < formationAt) yield return null;
                if (!Active) yield break;
                KarmaAerialSwordPlanner.Group plan = null;
                while (Active && plan == null)
                {
                    try
                    {
                        plan = planner.BuildGroup(Position, group, warning, AerialSwordReachLife(),
                            size, Tuning.swordSpeed * 1.75f, FightSeconds);
                    }
                    catch (System.InvalidOperationException error) when
                        (error.Message == "SW05 could not reserve a physical sword-rain escape route.")
                    { Intent = "SW05 검우 재정렬 · 기존 검이 지나간 뒤 낙하"; }
                    // Never launch an unsafe group. Live swords keep moving;
                    // re-plan positions/timing without omitting any promised blade.
                    if (plan == null) yield return Wait(0.15f);
                }
                if (!Active) yield break;
                var marks = new SpriteRenderer[plan.shots.Length];
                var blades = new SpriteRenderer[plan.shots.Length];
                for (int member = 0; member < plan.shots.Length; member++)
                {
                    var shot = plan.shots[member];
                    // The blade's transverse width is also its landing footprint.
                    // This fixed ground mark never tracks after the sky layout locks.
                    marks[member] = PathMark(new Vector2(shot.origin.x, 0.18f), new Vector2(shot.origin.x, 0.18f),
                        new Vector2(shot.size.y, 0.26f));
                    marks[member].name = "SW05 낙하 지면 전조";
                    blades[member] = KarmaVisuals.Box(effectsRoot, "Forming aerial sword / no damage",
                        shot.origin, Vector2.one, Color.white, 25);
                    KarmaWaveArtwork.Apply(blades[member], "SW05_AerialSword", shot.size, shot.velocity);
                }
                Intent = "SW05 천공 검우 " + (group + 1) + "/" + groups + " · 지면 전조 밖으로";
                yield return FormAerialSword(blades, warning);
                if (!Active) yield break;
                for (int member = 0; member < plan.shots.Length; member++)
                {
                    marks[member].gameObject.SetActive(false); Destroy(marks[member].gameObject);
                    blades[member].gameObject.SetActive(false); Destroy(blades[member].gameObject);
                    var shot = plan.shots[member];
                    var attack = Attack(shot.origin, shot.velocity, shot.size, shot.life);
                    planner.RegisterLaunch(shot, attack, shot.size, FightSeconds);
                }
                animator.ReleaseWavePhase();
            }
            // Each shot owns its boundary-limited lifetime. Normal completion
            // must not cancel a still-dangerous sword just to begin the landing.
            Intent = "SW05 마지막 검 낙하 · 방어막 유지";
            while (Active && planner.HasLiveShots) yield return null;
            if (!Active) yield break;
            fullShield = false;
            animator.PlayWavePhase("Rising");
            yield return Motion(ground, 0.5f, Vector2.zero);
            // Let the last harmless impact finish during descent rather than
            // clearing it in the same frame as the last blade's ground contact.
            ClearPatternObjects();
            yield return Recovery(2);
        }
        float AerialSwordReachLife()
        {
            // Upper bound for rain planning; each blade ends when its tip lands.
            return new Vector2(KarmaStage.ArenaWidth, KarmaSpace.Units(12)).magnitude
                / Mathf.Max(0.01f, Tuning.swordSpeed);
        }
        float AerialSwordInterval()
        {
            return Mathf.Max(1, Tuning.aerialSwordInterval, Warning(0.5f) + Time.fixedDeltaTime);
        }
        IEnumerator FormAerialSword(SpriteRenderer[] blades, float seconds)
        {
            // The supplied SW05 image, with no attack component, collider or glow.
            // Use the same yield/elapsed sequence as Wait so formation adds no delay.
            var readyScales = new Vector3[blades.Length];
            float elapsed = 0;
            for (int i = 0; i < blades.Length; i++)
            {
                readyScales[i] = blades[i].transform.localScale;
                blades[i].transform.localScale = readyScales[i] * 0.45f;
                blades[i].color = new Color(0.65f, 0.7f, 0.72f, 0);
            }
            while (elapsed < seconds && Active)
            {
                yield return null;
                if (game.Paused) continue;
                elapsed += Time.deltaTime;
                for (int i = 0; i < blades.Length; i++)
                {
                    // Stagger appearance, then hold every fully formed blade
                    // motionless for the last quarter of the advertised warning.
                    float delay = seconds * 0.3f * i / Mathf.Max(1, blades.Length - 1);
                    float formed = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - delay) / (seconds * 0.45f)));
                    blades[i].transform.localScale = readyScales[i] * Mathf.Lerp(0.45f, 1, formed);
                    blades[i].color = Color.Lerp(new Color(0.65f, 0.7f, 0.72f, 0), new Color(1, 1, 1, 0.95f), formed);
                }
            }
        }
    }
}
