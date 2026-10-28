using System.Collections;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        const float TrialCenter = 20;
        const float TrialRadius = 8 * KarmaSpace.Scale;

        IEnumerator SurvivalTrial()
        {
            state = ActionState.Telegraph;
            Intent = "GD05 노란 반원 보호막 형성 · 결계 안에서 생존";
            yield return Wait(1);
            shieldUntil = shieldWarningUntil = 0;
            game.Player.ConfineArena(TrialCenter - KarmaSpace.Units(7.4f), TrialCenter + KarmaSpace.Units(7.4f));
            yield return Motion(new Vector2(TrialCenter, KarmaSpace.GuardianGroundY), 0.5f, Vector2.zero);
            var dome = KarmaGuardDome.Create(game, effectsRoot, new Vector2(TrialCenter, 0), TrialRadius);
            GuardDecoration("GuardSeal", "GroundRing", new Vector2(TrialCenter, KarmaSpace.Units(0.25f)),
                KarmaSpace.Units(new Vector2(16, 2)), new Color(1, 1, 1, 0.5f), 3);
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
                game.Player.ReleaseArena(); dome.Dissolve();
                yield return Wait(0.4f);
                yield return Recovery(Mathf.Max(1, Tuning.trialStunSeconds), false,
                    "GD05 시험 성공 · 무력화 / 지금 공격하세요", scaleDuration: false);
            }
        }
        string TrialCue(string cue)
        {
            return "GD05 · " + cue
                + (trial.Hits > trial.Allowed ? " · 종료 후 결계 파열 예정" : "");
        }
        Vector2 TrialSide(bool left, float y)
        { return new Vector2(TrialCenter + (left ? -1 : 1) * (TrialRadius - KarmaSpace.Units(0.8f)), y); }
        float TrialWaveLife { get { return (TrialRadius * 2 - KarmaSpace.Units(1.6f)) / KarmaSpace.Units(10); } }
        Vector2 TrialVelocity(bool left) { return Vector2.right * KarmaSpace.Units(left ? 10 : -10); }
        KarmaBossAttack TrialShot(Vector2 at, Vector2 velocity, Vector2 size, float life, KarmaAttackContact contact, string pose = "Wave")
        {
            string sheet = pose == "Ward" ? "GuardShard" : pose == "Pillar" ? "GuardPillar" : "GuardWave";
            return KarmaBossAttack.Spawn(game, this, effectsRoot, at, velocity, size, life, Damage * 0.65f,
                new Color(1, 0.75f, 0.15f), contact, 0, pose, sheet);
        }
        SpriteRenderer TrialWaveMark(bool left, float y)
        {
            var start = TrialSide(left, y);
            return PathMark(start, start + TrialVelocity(left) * TrialWaveLife, WaveSize);
        }
        IEnumerator TrialHorizontal(bool left, KarmaAttackContact contact)
        {
            float y = KarmaSpace.Units(0.9f);
            var mark = TrialWaveMark(left, y);
            Intent = TrialCue("낮은 파동 · 점프/반격");
            yield return Wait(Warning(0.75f)); Destroy(mark.gameObject); state = ActionState.Attack;
            TrialShot(TrialSide(left, y), TrialVelocity(left), WaveSize, TrialWaveLife, contact);
            yield return Wait(TrialWaveLife + 0.05f);
        }
        IEnumerator TrialRain(KarmaAttackContact contact)
        {
            var starts = new Vector2[3]; var lives = new float[3]; var marks = new SpriteRenderer[3];
            Vector2 size = KarmaSpace.Units(new Vector2(0.65f, 0.85f));
            float aim = Mathf.Clamp(game.Player.Position.x, TrialCenter - KarmaSpace.Units(4.4f), TrialCenter + KarmaSpace.Units(4.4f));
            float longest = 0;
            for (int i = 0; i < 3; i++)
            {
                float x = aim + KarmaSpace.Units((i - 1) * 2.4f);
                float dx = x - TrialCenter;
                float roof = Mathf.Sqrt(TrialRadius * TrialRadius - dx * dx);
                starts[i] = new Vector2(x, roof - KarmaSpace.Units(0.6f));
                lives[i] = (starts[i].y - KarmaSpace.Units(0.8f)) / KarmaSpace.Units(12);
                longest = Mathf.Max(longest, lives[i]);
                marks[i] = PathMark(starts[i], starts[i] + Vector2.down * KarmaSpace.Units(12) * lives[i], size);
            }
            Intent = TrialCue("봉인 낙하 · 조준 고정 / 옆으로 이동");
            yield return Wait(Warning(0.9f)); state = ActionState.Attack;
            for (int i = 0; i < 3; i++)
            {
                Destroy(marks[i].gameObject);
                TrialShot(starts[i], Vector2.down * KarmaSpace.Units(12), size, lives[i], contact, "Ward");
            }
            yield return Wait(longest + 0.05f);
        }
        IEnumerator TrialPincer(KarmaAttackContact contact)
        {
            float y = KarmaSpace.Units(0.9f);
            var left = TrialWaveMark(true, y); var right = TrialWaveMark(false, y);
            Intent = TrialCue("양쪽 협공 · 두 파동을 넘거나 반격");
            yield return Wait(Warning(0.85f));
            Destroy(left.gameObject); Destroy(right.gameObject); state = ActionState.Attack;
            TrialShot(TrialSide(true, y), TrialVelocity(true), WaveSize, TrialWaveLife, contact);
            TrialShot(TrialSide(false, y), TrialVelocity(false), WaveSize, TrialWaveLife, contact);
            yield return Wait(TrialWaveLife + 0.05f);
        }
        IEnumerator TrialPillars(KarmaAttackContact contact)
        {
            Vector2 size = KarmaSpace.Units(new Vector2(0.9f, 3.8f));
            var centers = new Vector2[3]; var marks = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                centers[i] = new Vector2(TrialCenter + KarmaSpace.Units((i - 1) * 4.8f), size.y / 2);
                marks[i] = Mark("바닥 봉인 · 기둥 사이가 안전", centers[i], size);
            }
            Intent = TrialCue("바닥 봉인 · 기둥 사이 빈 칸으로");
            yield return Wait(Warning(0.85f)); state = ActionState.Attack;
            for (int i = 0; i < 3; i++)
            {
                Destroy(marks[i].gameObject);
                TrialShot(centers[i], Vector2.zero, size, 0.3f, contact, "Pillar");
            }
            yield return Wait(0.35f);
        }
        IEnumerator TrialWeave(bool left, KarmaAttackContact contact)
        {
            float high = KarmaSpace.Units(2.7f), low = KarmaSpace.Units(0.9f);
            var highMark = TrialWaveMark(left, high); var lowMark = TrialWaveMark(!left, low);
            Intent = TrialCue("높낮이 연속 · 높은 탄은 아래로 / 뒤따르는 낮은 탄은 점프");
            yield return Wait(Warning(0.85f)); Destroy(highMark.gameObject); state = ActionState.Attack;
            TrialShot(TrialSide(left, high), TrialVelocity(left), WaveSize, TrialWaveLife, contact);
            yield return Wait(0.45f); Destroy(lowMark.gameObject);
            TrialShot(TrialSide(!left, low), TrialVelocity(!left), WaveSize, TrialWaveLife, contact);
            yield return Wait(TrialWaveLife + 0.05f);
        }
    }
}
