using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        readonly KarmaHitFeedbackState hitFeedback = new KarmaHitFeedbackState();
        Vector3 lastShake;
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
            float halfWidth = GameCamera.orthographicSize * GameCamera.aspect;
            float min = Mathf.Min(20, halfWidth - 1), max = Mathf.Max(20, 41 - halfWidth);
            float x = Mathf.Clamp(Player.Position.x + Player.Facing * 2, min, max);
            var target = new Vector3(x, 4.4f, -10);
            GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position - lastShake, target,
                1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
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
