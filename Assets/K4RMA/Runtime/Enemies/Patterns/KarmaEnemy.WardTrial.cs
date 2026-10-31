using System.Collections;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        internal const float TrialCenter = 20;
        internal const float TrialRadius = 10 * KarmaSpace.Scale;
        const float TrialAttackScale = 1.3f;
        const float TrialWaveSpacing = 2.6f * KarmaSpace.Scale;

        IEnumerator SurvivalTrial()
        {
            fullShield = true; state = ActionState.Telegraph;
            Intent = "GD05 노란 반원 보호막 형성 · 결계 안에서 생존";
            yield return Wait(1);
            float limit = TrialRadius - KarmaSpace.Units(0.6f);
            game.Player.ConfineArena(TrialCenter - limit, TrialCenter + limit);
            yield return Motion(new Vector2(TrialCenter, KarmaSpace.GuardianGroundY), 0.5f, Vector2.zero);
            var dome = KarmaGuardDome.Create(game, effectsRoot, new Vector2(TrialCenter, 0), TrialRadius);
            GuardDecoration("GuardSeal", "GroundRing", new Vector2(TrialCenter, KarmaSpace.Units(0.25f)),
                new Vector2(TrialRadius * 2, KarmaSpace.Units(2)), new Color(1, 1, 1, 0.5f), 3);
            trial.Begin(Mathf.Clamp(Tuning.trialAttackCount, 3, 8), Tuning.trialAllowedHits);
            for (int i = 0; i < trial.Total; i++)
            {
                var contact = new KarmaAttackContact(); // Simultaneous hazards share one contact per beat.
                state = ActionState.Telegraph;
                bool left = i % 2 == 0;
                switch (i % 5)
                {
                    case 0: yield return TrialHorizontal(left, contact); break;
                    case 1: yield return TrialRain(contact); break;
                    case 2: yield return TrialPincer(contact); break;
                    case 3: yield return TrialPillars(contact); break;
                    default: yield return TrialWeave(left, contact); break;
                }
                trial.CompleteAttack();
                yield return Wait(Mathf.Max(0.4f, Tuning.trialAttackGap * Tempo));
            }
            bool passed = trial.Passed;
            trial.Clear();
            if (!passed)
            {
                // Keep the room intact while cracks and light announce its own detonation.
                Intent = "GD05 시험 실패 · 보호막 과부하 / 균열이 터질 때 K 반격";
                dome.Charge(0.95f);
                yield return Wait(0.95f);
                dome.Explode();
                Intent = "GD05 결계 폭발 · 보호막 파열";
                var contact = new KarmaAttackContact();
                float active = 0;
                while (active < 0.18f && Active)
                {
                    if (!game.Paused)
                    {
                        var player = game.Player;
                        if (dome.Overlaps(player.Position, new Vector2(KarmaSpace.PlayerHalfWidth, KarmaSpace.PlayerHalfHeight)))
                            TryAttackHit(player.Position, player.Position, Vector2.zero, Damage * Tuning.trialExplosionMultiplier, contact, 0);
                        active += Time.deltaTime;
                    }
                    yield return null;
                }
                yield return Wait(0.57f);
                ClearPatternObjects(); game.Player.ReleaseArena();
                yield return Recovery(1.5f);
            }
            else
            {
                BreakAutoShield();
                game.Player.ReleaseArena(); dome.Dissolve();
                yield return Wait(0.4f);
                yield return Recovery(Mathf.Max(1, Tuning.trialStunSeconds),
                    "GD05 시험 성공 · GD01 방어막 해제 / 지금 공격하세요", scaleDuration: false);
            }
        }
        string TrialCue(string cue)
        {
            return "GD05 · " + cue
                + (trial.Hits > trial.Allowed ? " · 종료 후 결계 파열 예정" : "");
        }
        Vector2 TrialWaveSize { get { return WaveSize * TrialAttackScale; } }
        Vector2 TrialSide(bool left, float y)
        {
            // Keep the enlarged projectile's outer top corner below the curved roof.
            float top = y + TrialWaveSize.y / 2 + KarmaSpace.Units(0.1f);
            float edge = Mathf.Sqrt(Mathf.Max(0, TrialRadius * TrialRadius - top * top))
                - TrialWaveSize.x / 2 - KarmaSpace.Units(0.1f);
            return new Vector2(TrialCenter + (left ? -edge : edge), y);
        }
        Vector2 TrialWaveStart(bool left, float y, int leading)
        { return TrialSide(left, y) + Vector2.right * (left ? 1 : -1) * leading * TrialWaveSpacing; }
        float TrialWaveLife(float y, int leading = 0)
        { return (Vector2.Distance(TrialSide(true, y), TrialSide(false, y)) - leading * TrialWaveSpacing) / KarmaSpace.Units(10); }
        Vector2 TrialVelocity(bool left) { return Vector2.right * KarmaSpace.Units(left ? 10 : -10); }
        KarmaBossAttack TrialShot(Vector2 at, Vector2 velocity, Vector2 size, float life, KarmaAttackContact contact, string pose = "Wave")
        {
            string sheet = pose == "Ward" ? "GuardShard" : pose == "Pillar" ? "GuardPillar" : "GuardWave";
            return KarmaBossAttack.Spawn(game, this, effectsRoot, at, velocity, size, life, Damage * 0.65f,
                new Color(1, 0.75f, 0.15f), contact, 0, pose, sheet);
        }
        SpriteRenderer[] TrialWaveMarks(bool left, float y)
        {
            var marks = new SpriteRenderer[2];
            for (int i = 0; i < marks.Length; i++)
                marks[i] = PathMark(TrialWaveStart(left, y, i), TrialSide(!left, y), TrialWaveSize);
            return marks;
        }
        void LaunchTrialWaves(bool left, float y, SpriteRenderer[] marks, KarmaAttackContact contact)
        {
            for (int i = 0; i < marks.Length; i++)
            {
                marks[i].gameObject.SetActive(false); Destroy(marks[i].gameObject);
                TrialShot(TrialWaveStart(left, y, i), TrialVelocity(left), TrialWaveSize, TrialWaveLife(y, i), contact);
            }
        }
        IEnumerator TrialHorizontal(bool left, KarmaAttackContact contact)
        {
            float y = KarmaSpace.Units(0.9f);
            var marks = TrialWaveMarks(left, y);
            Intent = TrialCue("낮은 파동 2개 · 점프/반격");
            yield return Wait(Warning(0.75f)); state = ActionState.Attack;
            LaunchTrialWaves(left, y, marks, contact);
            yield return Wait(TrialWaveLife(y) + 0.05f);
        }
        IEnumerator TrialRain(KarmaAttackContact contact)
        {
            var starts = new Vector2[5]; var lives = new float[5]; var marks = new SpriteRenderer[5];
            Vector2 size = KarmaSpace.Units(new Vector2(0.65f, 0.85f)) * TrialAttackScale;
            float aim = Mathf.Clamp(game.Player.Position.x, TrialCenter - KarmaSpace.Units(3.4f), TrialCenter + KarmaSpace.Units(3.4f));
            float longest = 0;
            for (int i = 0; i < starts.Length; i++)
            {
                float x = aim + KarmaSpace.Units((i - 2) * 2.6f);
                float dx = Mathf.Abs(x - TrialCenter) + size.x / 2;
                float roof = Mathf.Sqrt(TrialRadius * TrialRadius - dx * dx);
                starts[i] = new Vector2(x, roof - size.y / 2 - KarmaSpace.Units(0.25f));
                lives[i] = (starts[i].y - size.y / 2 - KarmaSpace.Units(0.1f)) / KarmaSpace.Units(12);
                longest = Mathf.Max(longest, lives[i]);
                marks[i] = PathMark(starts[i], starts[i] + Vector2.down * KarmaSpace.Units(12) * lives[i], size);
            }
            Intent = TrialCue("봉인 낙하 5개 · 조준 고정 / 옆으로 이동");
            yield return Wait(Warning(0.9f)); state = ActionState.Attack;
            for (int i = 0; i < starts.Length; i++)
            {
                Destroy(marks[i].gameObject);
                TrialShot(starts[i], Vector2.down * KarmaSpace.Units(12), size, lives[i], contact, "Ward");
            }
            yield return Wait(longest + 0.05f);
        }
        IEnumerator TrialPincer(KarmaAttackContact contact)
        {
            float y = KarmaSpace.Units(0.9f);
            var left = TrialWaveMarks(true, y); var right = TrialWaveMarks(false, y);
            Intent = TrialCue("양쪽 협공 4개 · 파동 묶음을 넘거나 반격");
            yield return Wait(Warning(0.85f));
            state = ActionState.Attack;
            LaunchTrialWaves(true, y, left, contact);
            LaunchTrialWaves(false, y, right, contact);
            yield return Wait(TrialWaveLife(y) + 0.05f);
        }
        IEnumerator TrialPillars(KarmaAttackContact contact)
        {
            Vector2 size = KarmaSpace.Units(new Vector2(0.9f, 3.8f)) * TrialAttackScale;
            var centers = new Vector2[5]; var marks = new SpriteRenderer[5];
            for (int i = 0; i < centers.Length; i++)
            {
                centers[i] = new Vector2(TrialCenter + KarmaSpace.Units((i - 2) * 3.4f), size.y / 2);
                marks[i] = Mark("바닥 봉인 · 기둥 사이가 안전", centers[i], size);
            }
            Intent = TrialCue("바닥 봉인 5개 · 기둥 사이 빈 칸으로");
            yield return Wait(Warning(0.85f)); state = ActionState.Attack;
            for (int i = 0; i < centers.Length; i++)
            {
                Destroy(marks[i].gameObject);
                TrialShot(centers[i], Vector2.zero, size, 0.3f, contact, "Pillar");
            }
            yield return Wait(0.35f);
        }
        IEnumerator TrialWeave(bool left, KarmaAttackContact contact)
        {
            float high = KarmaSpace.Units(2.7f), low = KarmaSpace.Units(0.9f);
            var highMarks = TrialWaveMarks(left, high); var lowMarks = TrialWaveMarks(!left, low);
            Intent = TrialCue("높낮이 연속 4개 · 높은 탄 아래로 / 뒤따르는 낮은 탄은 점프");
            yield return Wait(Warning(0.85f)); state = ActionState.Attack;
            LaunchTrialWaves(left, high, highMarks, contact);
            yield return Wait(0.85f);
            LaunchTrialWaves(!left, low, lowMarks, contact);
            yield return Wait(TrialWaveLife(low) + 0.05f);
        }
    }
}
