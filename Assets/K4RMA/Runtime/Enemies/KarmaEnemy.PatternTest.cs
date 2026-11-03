#if UNITY_EDITOR
using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        bool patternTestHold;
        public bool PatternTestRunning { get { return patternTestHold && running; } }
        public bool PracticeBusy { get { return running || Stunned || pendingParry; } }

        public void ArmPatternIsolation()
        {
            patternTestHold = true;
            stopped = false;
            engaged = true;
        }

        public void ReleasePatternIsolation()
        {
            if (running) InterruptPattern();
            patternTestHold = false;
            timer = 0.8f;
        }

        public void ReplayIsolatedPattern(EnemyAttack attack)
        {
            if (game == null || game.Player == null || running) return;
            patternTestHold = true;
            stopped = false;
            engaged = true;
            if (!Alive) Health = MaxHealth;
            selected = attack;
            previous = (EnemyAttack)(-1);
            direction = game.Player.Position.x < Position.x ? -1f : 1f;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, Mathf.Abs(art.localScale.y), 1f);
            state = ActionState.Approach;
            running = true;
            patternStarted = FightSeconds;
            StartCoroutine(RunPattern());
        }

        public void BeginRisingPatternTest(EnemyAttack attack)
        {
            if (game == null || game.Player == null) return;
            patternTestHold = true;
            stopped = false;
            engaged = true;
            Health = MaxHealth;
            if (running) InterruptPattern();
            float x = Mathf.Clamp(game.Player.Position.x + 6f, KarmaStage.BossLeftWall, KarmaStage.BossRightWall);
            transform.position = new Vector3(x, KarmaSpace.GuardianGroundY, 0f);
            selected = attack;
            previous = (EnemyAttack)(-1);
            direction = game.Player.Position.x < Position.x ? -1f : 1f;
            art.localScale = new Vector3(direction * KarmaSpace.GuardianArtScale, KarmaSpace.GuardianArtScale, 1f);
            state = ActionState.Approach;
            running = true;
            patternStarted = FightSeconds;
            StartCoroutine(RunPattern());
        }

        public void CancelRisingPatternTest(bool releaseAi)
        {
            InterruptPattern();
            if (releaseAi)
            {
                patternTestHold = false;
                timer = 0.8f;
            }
            else patternTestHold = true;
        }
    }
}
#endif
