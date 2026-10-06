using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaEnemy
    {
        void Update()
        {
            if (game == null || !game.IsCombat || !Alive) return;
            UpdateBurn(); if (!Alive || !game.IsCombat) return;
            if (!engaged)
            {
                if (game.Player.Position.x < 12) { Intent = "앞으로 나아가 시험을 시작하세요"; return; }
                engaged = true;
            }
            art.GetComponent<KarmaCharacterAnimator>().Pose(state == ActionState.Approach ? 1 : 0, state == ActionState.Rising);
            float dt = Time.deltaTime; FightSeconds += dt; timer -= dt;
            float dx = game.Player.Position.x - Position.x;
            if (state == ActionState.Approach || state == ActionState.Recover)
                art.localScale = new Vector3(dx < 0 ? -1.4f : 1.4f, 1.4f, 1);
            switch (state)
            {
                case ActionState.Approach:
                    float preferred = PrimaryType == Essence.Flame ? 7f : PrimaryType == Essence.Ward ? 3.8f : 2.8f;
                    if (Mathf.Abs(dx) > preferred) Move(Mathf.Sign(dx) * game.Config.enemyMoveSpeed * Tuning.moveMultiplier * dt);
                    else if (PrimaryType == Essence.Flame && Mathf.Abs(dx) < 4.5f) Move(-Mathf.Sign(dx) * game.Config.enemyMoveSpeed * dt);
                    if (timer <= 0) BeginAttack(); break;
                case ActionState.Telegraph:
                    if (timer <= 0) ExecuteAttack(); break;
                case ActionState.Rising:
                    TickRising(dt); break;
                case ActionState.Ward:
                    if (timer <= 0) EndDefense(); break;
                case ActionState.Recover:
                    if (timer <= 0)
                    {
                        state = ActionState.Approach;
                        timer = Mathf.Max(0.4f, game.Config.enemyDecisionDelay * Tuning.decisionMultiplier * (Enraged ? 0.75f : 1));
                        Intent = "접근 중";
                    }
                    break;
            }
            // Recover an externally displaced guardian without trapping the run.
            if (Position.y < -2 || Position.y > 12)
            { transform.position = new Vector3(Mathf.Clamp(Position.x, 1.5f, 38.5f), 1.15f, 0); Recover(1); }
        }
        void Move(float distance)
        {
            transform.position = new Vector3(Mathf.Clamp(Position.x + distance, 1.5f, 38.5f), Position.y, 0);
        }
    }
}
