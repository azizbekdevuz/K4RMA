using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        readonly KarmaHitFeedbackState hitFeedback = new KarmaHitFeedbackState();
        Vector3 lastShake;
        Bounds stageBackgroundBounds;
        bool hasStageBackground;
        public void FrameStageBackground(Bounds bounds)
        {
            stageBackgroundBounds = bounds;
            hasStageBackground = true;
            FitStageBackground(true);
        }
        void FitStageBackground(bool snap = false)
        {
            // Preserve the full-image zoom while following across the expanded arena.
            GameCamera.orthographicSize = Mathf.Max(stageBackgroundBounds.extents.y,
                25 / Mathf.Max(GameCamera.aspect, 0.01f));
            float halfWidth = GameCamera.orthographicSize * GameCamera.aspect;
            float left = Mathf.Max(KarmaStage.ArenaLeft, stageBackgroundBounds.min.x);
            float right = Mathf.Min(KarmaStage.ArenaRight, stageBackgroundBounds.max.x);
            float playerX = snap || Player == null ? KarmaStage.Spawn.x : Player.Position.x;
            float facing = snap || Player == null ? 1 : Player.Facing;
            float x = right - left <= halfWidth * 2 ? (left + right) / 2
                : Mathf.Clamp(playerX + facing * KarmaSpace.Units(2), left + halfWidth, right - halfWidth);
            var target = new Vector3(x, stageBackgroundBounds.center.y, -10);
            GameCamera.transform.position = snap ? target
                : Vector3.Lerp(GameCamera.transform.position - lastShake, target,
                    1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
            lastShake = Vector3.zero;
        }
        public void SwordHitFeedback()
        {
            if (!IsCombat) return;
            var tuning = Config.Techniques;
            hitFeedback.Trigger(Time.unscaledTime, tuning.hitStopSeconds, tuning.hitShakeSeconds);
        }
        void UpdateHitFeedback()
        {
            Time.timeScale = hitFeedback.TimeScale(Time.unscaledTime, IsCombat, Paused);
        }
        void ResetHitFeedback()
        {
            hitFeedback.Reset();
            if (GameCamera != null) GameCamera.transform.position -= lastShake;
            lastShake = Vector3.zero;
        }
        void LateUpdate()
        {
            if (Player == null) return;
            if (Phase == RunPhase.GuardianFallen || Phase == RunPhase.Choosing
                || Phase == RunPhase.PlayerFallen || Phase == RunPhase.Defeat)
            {
                // Hold the combat framing through the fall and selection screen.
                GameCamera.transform.position -= lastShake;
                lastShake = Vector3.zero;
                return;
            }
            if (Phase == RunPhase.Transition)
            {
                var actor = GuardianInheriting && Enemies.Count > 0 ? Enemies[0].transform : Player.transform;
                float height = GuardianInheriting ? 2.2f : 0.6f;
                var focus = actor.position + new Vector3(0, height, -10);
                float blend = 1 - Mathf.Exp(-5 * Time.deltaTime);
                GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position - lastShake, focus, blend);
                float combatSize = hasStageBackground
                    ? Mathf.Max(stageBackgroundBounds.extents.y, 25 / Mathf.Max(GameCamera.aspect, 0.01f))
                    : 6.8f / Mathf.Min(1, Mathf.Max(0.4f, GameCamera.aspect));
                GameCamera.orthographicSize = Mathf.Lerp(GameCamera.orthographicSize,
                    combatSize * (GuardianInheriting ? 0.8f : 0.75f), blend);
                lastShake = Vector3.zero;
                return;
            }
            if (hasStageBackground)
            {
                FitStageBackground();
            }
            else
            {
                float halfWidth = GameCamera.orthographicSize * GameCamera.aspect;
                float min = Mathf.Min(KarmaStage.ArenaCenter, KarmaStage.ArenaLeft + halfWidth - 1), max = Mathf.Max(KarmaStage.ArenaCenter, KarmaStage.ArenaRight + 1 - halfWidth);
                float x = Mathf.Clamp(Player.Position.x + Player.Facing * 2, min, max);
                var target = new Vector3(x, 4.4f, -10);
                GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position - lastShake, target,
                    1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
            }
            lastShake = Vector3.zero;
            if (IsCombat && hitFeedback.Shake(Time.unscaledTime, Config.Techniques.hitShakeSeconds) > 0)
            {
                float strength = Config.Techniques.hitShakeStrength * hitFeedback.Shake(Time.unscaledTime, Config.Techniques.hitShakeSeconds);
                lastShake = new Vector3(Mathf.Sin(Time.unscaledTime * 97) * strength, Mathf.Sin(Time.unscaledTime * 73) * strength * 0.55f, 0);
            }
            GameCamera.transform.position += lastShake;
        }
    }
}
