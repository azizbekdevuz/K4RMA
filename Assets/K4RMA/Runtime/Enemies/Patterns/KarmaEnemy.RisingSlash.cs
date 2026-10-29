using System.Collections;
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        readonly Vector2 bodyAttackSize = KarmaSpace.Units(new Vector2(1.4f, 1.8f));
        static readonly float[] FragmentDegrees = { 8f, 36f, 68f, 98f, 128f, 158f, 186f, 352f };
        float nextStride;
        IEnumerator RisingMelee()
        {
            float gap = game.Player.Position.x - Position.x;
            direction = gap < 0 ? -1 : 1;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1);
            if (Mathf.Abs(gap) > KarmaSpace.Units(2.15f))
            {
                float step = Mathf.Clamp(Mathf.Abs(gap) - KarmaSpace.Units(1.65f), 0f, KarmaSpace.Units(2.2f));
                Vector2 close = new Vector2(Mathf.Clamp(Position.x + direction * step, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), KarmaSpace.GuardianGroundY);
                state = ActionState.Approach;
                yield return Motion(close, 0.16f, Vector2.zero);
            }
            Vector2 size = KarmaSpace.Units(new Vector2(2.55f, 1.55f));
            Vector2 center = new Vector2(Position.x + direction * KarmaSpace.Units(1.35f), KarmaSpace.GuardianGroundY + KarmaSpace.Units(0.72f));
            state = ActionState.Telegraph;
            var mark = Mark("기본 베기", center, size);
            Intent = "기본 베기 · 거리를 유지하세요";
            yield return Wait(Warning(0.32f));
            if (mark != null) Destroy(mark.gameObject);
            state = ActionState.Attack;
            var slash = Attack(center, Vector2.zero, size, 0.16f, 0.8f, null, 0f, "Cleave");
            if (slash != null) slash.Repaint(KarmaRisingArt.Get("Slash"), true);
            yield return Wait(0.18f);
            yield return Recovery(0.62f);
        }
        IEnumerator AreaLanding()
        {
            Vector2 start = Position;
            float apexY = KarmaSpace.GuardianGroundY + Mathf.Clamp(Tuning.riseHeight, KarmaSpace.Units(2.4f), KarmaSpace.Units(5.2f));
            Vector2 apex = new Vector2(Mathf.Clamp(start.x + direction * KarmaSpace.Units(1.4f), KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), apexY);
            state = ActionState.Telegraph;
            var rise = RisingPath(start, apex, bodyAttackSize, KarmaRisingCue.Charging);
            var charge = BeginRisingCharge(KarmaRisingCue.Charging);
            Intent = "UP01 상승 · 착지 범위를 읽으세요";
            yield return Wait(Warning(0.42f));
            EndRisingCharge(charge);
            if (rise != null) Destroy(rise.gameObject);
            yield return Motion(apex, 0.32f, Vector2.zero);
            Vector2 landing = new Vector2(Mathf.Clamp(game.Player.Position.x, KarmaStage.ArenaLeft + 4, KarmaStage.ArenaRight - 4), KarmaSpace.GuardianGroundY);
            landing.x = Mathf.Clamp(landing.x, apex.x - KarmaSpace.Units(4.5f), apex.x + KarmaSpace.Units(4.5f));
            Vector2 area = new Vector2(KarmaSpace.Units(5.2f), KarmaSpace.Units(1.25f));
            Vector2 impact = new Vector2(landing.x, KarmaSpace.Units(0.62f));
            var drop = RisingPath(apex, landing, bodyAttackSize, KarmaRisingCue.Locked);
            var zone = Mark("UP01 착지 범위", impact, area);
            charge = BeginRisingCharge(KarmaRisingCue.Locked);
            KarmaRisingPolish.LockPop(effectsRoot, impact);
            Intent = "UP01 착지 범위 고정 · 표시 밖으로";
            yield return Wait(Warning(0.7f));
            EndRisingCharge(charge);
            if (drop != null) Destroy(drop.gameObject);
            if (zone != null) Destroy(zone.gameObject);
            var contact = new KarmaAttackContact();
            yield return Motion(landing, 0.26f, bodyAttackSize, contact, KarmaRisingArt.Get("Slash"));
            KarmaRisingPolish.AreaImpact(game, effectsRoot, impact, area * new Vector2(1.15f, 1.8f));
            var blast = Attack(impact, Vector2.zero, area, 0.22f, 1f, contact, 0f, "GroundRing");
            if (blast != null) blast.Repaint(KarmaRisingArt.Get("Impact"), true);
            if (Enraged)
            {
                yield return Wait(0.28f);
                Vector2 echoSize = new Vector2(area.x * 0.72f, area.y);
                var echo = Mark("착지 여파", impact, echoSize);
                Intent = "착지 여파 · 같은 범위에 머물지 마세요";
                yield return Wait(Warning(0.42f));
                if (echo != null) Destroy(echo.gameObject);
                var second = Attack(impact, Vector2.zero, echoSize, 0.18f, 0.7f, null, 0f, "GroundRing");
                if (second != null) second.Repaint(KarmaRisingArt.Get("Impact"), true);
                KarmaRisingPolish.AreaImpact(game, effectsRoot, impact, echoSize);
                yield return Wait(0.22f);
            }
            else yield return Wait(0.34f);
            yield return Recovery(1.05f);
        }
        IEnumerator AirPursuit(bool recover)
        {
            Vector2 origin = Position, target = origin;
            SpriteRenderer path = null;
            state = ActionState.Telegraph;
            var charge = BeginRisingCharge(KarmaRisingCue.Tracking);
            float elapsed = 0;
            while (elapsed < 0.36f && Active)
            {
                if (game.Paused) { yield return null; continue; }
                target = game.Player.Position;
                target.x = Mathf.Clamp(target.x, Mathf.Max(KarmaStage.ArenaLeft + 2, origin.x - KarmaSpace.Units(7)), Mathf.Min(KarmaStage.ArenaRight - 2, origin.x + KarmaSpace.Units(7)));
                target.y = Mathf.Clamp(target.y, KarmaSpace.Units(2.5f), KarmaSpace.Units(7));
                if (path == null) path = RisingPath(origin, target, bodyAttackSize, KarmaRisingCue.Tracking);
                else path.GetComponent<KarmaAttackWarning>().SetPath(path, origin, target, bodyAttackSize);
                elapsed += Time.deltaTime; yield return null;
            }
            if (path != null) Destroy(path.gameObject);
            if (charge != null) charge.Lock();
            KarmaRisingPolish.LockPop(effectsRoot, target);
            path = RisingPath(origin, target, bodyAttackSize, KarmaRisingCue.Locked);
            Intent = "UP02 목표 고정 · 추적 종료";
            yield return Wait(Warning(0.36f));
            EndRisingCharge(charge);
            if (path != null) Destroy(path.gameObject);
            yield return Motion(target, 0.28f, bodyAttackSize, null, KarmaRisingArt.Get("Slash"));
            yield return Motion(new Vector2(target.x, KarmaSpace.GuardianGroundY), 0.32f, Vector2.zero);
            if (recover && Enraged)
            {
                yield return Wait(Mathf.Max(0.45f, Tuning.crossingReset * Tempo));
                yield return AirPursuit(false);
            }
            if (recover) yield return Recovery(0.9f);
        }
        IEnumerator InheritedWave()
        {
            Vector2 start = new Vector2(Position.x, KarmaSpace.GuardianGroundY);
            yield return Motion(start, 0.14f, Vector2.zero);
            float room = direction > 0 ? KarmaStage.ArenaRight - 1.2f - start.x : start.x - (KarmaStage.ArenaLeft + 1.2f);
            float height = Mathf.Clamp(Tuning.groundBladeHeight, KarmaSpace.Units(4.4f), KarmaSpace.Units(6.4f));
            float gap = KarmaSpace.Units(0.45f);
            float reach = Mathf.Max(KarmaSpace.Units(8f), room * 0.78f);
            Vector2 size = new Vector2(Mathf.Max(KarmaSpace.Units(4f), reach - gap), height);
            Vector2 center = new Vector2(start.x + direction * (gap + size.x * 0.5f), height * 0.5f);
            var mark = Mark("전방 대검기 · 등 뒤로", center, size);
            state = ActionState.Telegraph;
            Intent = "UP03 전방 대검기 · 등 뒤로 이탈";
            var preview = KarmaRisingArt.Get("WaveEcho");
            if (preview != null)
            {
                var ghost = KarmaCombatVFX.Burst(effectsRoot, new Vector2(start.x + direction * (gap + size.x * 0.22f), height * 0.45f),
                    preview, "검기 예고", new Vector2(size.x * 0.42f, height * 0.55f), 0.7f, 0f, new Color(0.72f, 0.86f, 1f));
                if (ghost != null) ghost.transform.parent.localScale = new Vector3(direction, 1, 1);
            }
            yield return Wait(Warning(0.72f));
            if (mark != null) Destroy(mark.gameObject);
            var wave = Attack(center, Vector2.zero, size, 0.42f, 1.15f, null, 0f, "Wave");
            if (wave != null)
            {
                wave.Repaint(KarmaRisingArt.Get("Wave"), true);
                if (wave.Visual != null) wave.Visual.flipX = direction < 0;
            }
            yield return Wait(0.48f);
            yield return Recovery(1.15f);
        }
        IEnumerator FragmentSlam()
        {
            Vector2 start = Position;
            fullShield = true;
            float apexY = KarmaSpace.GuardianGroundY + Mathf.Clamp(Tuning.riseHeight * 0.72f, KarmaSpace.Units(2f), KarmaSpace.Units(4.2f));
            Vector2 apex = new Vector2(Mathf.Clamp(start.x, KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), apexY);
            state = ActionState.Telegraph;
            var charge = BeginRisingCharge(KarmaRisingCue.Charging);
            KarmaRisingPolish.ShieldPlant(effectsRoot, start, KarmaSpace.Units(new Vector2(2.4f, 2.8f)));
            Intent = "UP04 방패 준비 · 파편 방향을 읽으세요";
            yield return Wait(Warning(0.4f));
            EndRisingCharge(charge);
            yield return Motion(apex, 0.28f, Vector2.zero);
            var drop = RisingPath(apex, new Vector2(Mathf.Clamp(start.x + direction * KarmaSpace.Units(1.2f), KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.GuardianGroundY), bodyAttackSize, KarmaRisingCue.Locked);
            yield return Wait(Warning(0.28f));
            if (drop != null) Destroy(drop.gameObject);
            Vector2 landing = new Vector2(Mathf.Clamp(start.x + direction * KarmaSpace.Units(1.2f), KarmaStage.ArenaLeft + 3, KarmaStage.ArenaRight - 3), KarmaSpace.GuardianGroundY);
            yield return Motion(landing, 0.24f, bodyAttackSize);
            Vector2 origin = new Vector2(landing.x, KarmaSpace.Units(2.35f));
            float life = 1.02f;
            var warnings = new SpriteRenderer[FragmentDegrees.Length];
            var ends = new Vector2[FragmentDegrees.Length];
            var velocities = new Vector2[FragmentDegrees.Length];
            Vector2 shard = KarmaSpace.Units(new Vector2(1.45f, 0.95f));
            for (int i = 0; i < FragmentDegrees.Length; i++)
            {
                AimFragment(origin, FragmentDegrees[i], life, out ends[i], out velocities[i]);
                warnings[i] = PathMark(origin, ends[i], shard);
            }
            Intent = "UP04 파편 궤적 고정 · 피하거나 K로 패링";
            yield return Wait(Warning(0.5f));
            for (int i = 0; i < warnings.Length; i++) if (warnings[i] != null) Destroy(warnings[i].gameObject);
            fullShield = false;
            KarmaRisingPolish.ShieldPlant(effectsRoot, landing, KarmaSpace.Units(new Vector2(2.8f, 2.6f)));
            for (int i = 0; i < FragmentDegrees.Length && Active; i++)
            {
                var shot = Attack(origin, velocities[i], shard, life, 0.85f, new KarmaAttackContact(), 0f, "Blade", true);
                if (shot == null) continue;
                shot.name = "방패 파편";
                shot.Repaint(KarmaRisingArt.Get("Fragment" + (i % 8)), false);
                if (shot.Visual != null) shot.Visual.color = Color.white;
            }
            yield return Wait(life);
            yield return Recovery(1.15f);
        }
        IEnumerator Crossings()
        {
            state = ActionState.Telegraph;
            var charge = BeginRisingCharge(KarmaRisingCue.Charging);
            Intent = "UP05 극의 준비 · 궤적을 읽으세요";
            yield return Wait(Warning(0.62f));
            EndRisingCharge(charge);
            float left = KarmaStage.ArenaLeft + 4f, right = KarmaStage.ArenaRight - 4f;
            float ground = KarmaSpace.GuardianGroundY, mid = KarmaSpace.Units(3.15f), high = KarmaSpace.Units(5.35f);
            Vector2[] points =
            {
                new Vector2(left, ground), new Vector2(right, mid),
                new Vector2(left, high), new Vector2(right, ground),
                new Vector2(left, mid), new Vector2(right, high),
                new Vector2(left, ground), new Vector2(right, high),
                new Vector2(left, mid)
            };
            yield return Motion(points[0], 0.24f, Vector2.zero);
            Vector2 slash = KarmaSpace.Units(new Vector2(1.85f, 1.15f));
            var trail = KarmaRisingArt.Get("Slash");
            for (int i = 0; i < 8 && Active; i++)
            {
                var mark = PathMark(points[i], points[i + 1], slash);
                Intent = "UP05 횡단 " + (i + 1) + "/8 · 예고된 궤적 밖으로";
                yield return Wait(Warning(Mathf.Min(0.46f, Tuning.crossingWarning)));
                if (mark != null) Destroy(mark.gameObject);
                float duration = Mathf.Clamp(Vector2.Distance(points[i], points[i + 1]) / KarmaSpace.Units(16f), 0.22f, 0.4f);
                yield return Motion(points[i + 1], duration, slash, null, trail);
            }
            yield return Motion(new Vector2(Mathf.Clamp(Position.x, KarmaStage.BossLeftWall, KarmaStage.BossRightWall), ground), 0.26f, Vector2.zero);
            yield return Recovery(2.5f, false, "UP05 탈진 · 지금 공격하세요");
        }
        void AimFragment(Vector2 origin, float degrees, float life, out Vector2 end, out Vector2 velocity)
        {
            var dir = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            float limit = KarmaSpace.Units(14f);
            if (Mathf.Abs(dir.x) > 0.02f)
            {
                float wall = dir.x > 0 ? KarmaStage.ArenaRight - 0.8f : KarmaStage.ArenaLeft + 0.8f;
                limit = Mathf.Min(limit, (wall - origin.x) / dir.x);
            }
            if (dir.y > 0.02f) limit = Mathf.Min(limit, (KarmaSpace.Units(7.4f) - origin.y) / dir.y);
            else if (dir.y < -0.02f) limit = Mathf.Min(limit, (KarmaSpace.Units(0.45f) - origin.y) / dir.y);
            limit = Mathf.Max(KarmaSpace.Units(3.5f), limit);
            end = origin + dir * limit;
            velocity = dir * (limit / Mathf.Max(0.35f, life));
        }
    }
}
