using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        readonly KarmaHitFeedbackState hitFeedback = new KarmaHitFeedbackState();
        float presentationShakeUntil, presentationShakeStrength;
        internal void PresentationShake(float seconds, float strength)
        {
            presentationShakeUntil = Time.time + Mathf.Max(0, seconds);
            presentationShakeStrength = Mathf.Max(0, strength);
        }
        Vector3 lastShake;
        Bounds stageBackgroundBounds, paintedBackgroundBounds;
        bool hasStageBackground;
        internal static Rect ScreenViewport(float width, float height)
        {
            float scale = Mathf.Min(width / 1280f, height / 720f);
            float viewWidth = 1280 * scale, viewHeight = 720 * scale;
            return new Rect((width - viewWidth) / 2, (height - viewHeight) / 2, viewWidth, viewHeight);
        }
        void UpdateCameraViewport()
        {
            if (GameCamera == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect view = ScreenViewport(Screen.width, Screen.height);
            GameCamera.rect = new Rect(view.x / Screen.width, view.y / Screen.height,
                view.width / Screen.width, view.height / Screen.height);
            GameCamera.aspect = 16f / 9;
        }
        public void FrameStageBackground(Bounds bounds, Bounds? paintedBounds = null)
        {
            stageBackgroundBounds = bounds;
            paintedBackgroundBounds = paintedBounds ?? bounds;
            hasStageBackground = true;
            UpdateCameraViewport();
            FitStageBackground(true);
        }
        void FitStageBackground(bool snap = false)
        {
            // Preserve the full-image zoom while following across the expanded arena.
            Vector3? encounterFocus = null;
            float targetSize = Mathf.Max(stageBackgroundBounds.extents.y, 25 / Mathf.Max(GameCamera.aspect, 0.01f));
            foreach (var enemy in Enemies)
                if (enemy != null && enemy.CameraFocus.HasValue)
                {
                    encounterFocus = enemy.CameraFocus;
                    targetSize = Mathf.Max(targetSize, enemy.CameraSize(GameCamera.aspect));
                    break;
                }
            float blend = 1 - Mathf.Exp(-6 * Time.unscaledDeltaTime);
            GameCamera.orthographicSize = snap ? targetSize : Mathf.Lerp(GameCamera.orthographicSize, targetSize, blend);
            float halfWidth = GameCamera.orthographicSize * GameCamera.aspect;
            float left = Mathf.Max(KarmaStage.ArenaLeft, stageBackgroundBounds.min.x);
            float right = Mathf.Min(KarmaStage.ArenaRight, stageBackgroundBounds.max.x);
            float playerX = snap || Player == null ? KarmaStage.Spawn.x : Player.Position.x;
            float facing = snap || Player == null ? 1 : Player.Facing;
            float x = right - left <= halfWidth * 2 ? (left + right) / 2
                : Mathf.Clamp(playerX + facing * KarmaSpace.Units(2), left + halfWidth, right - halfWidth);
            var target = encounterFocus ?? new Vector3(x, stageBackgroundBounds.center.y, -10);
            GameCamera.transform.position = snap ? target
                : Vector3.Lerp(GameCamera.transform.position - lastShake, target,
                    blend);
            lastShake = Vector3.zero;
        }
        // The combat frame is a crop of the panorama. Film edge kills against
        // the full painting, leaving room around the silhouette and its effects.
        static Vector3 InsideBackdrop(Vector3 focus, float size, float aspect, Bounds painting)
        {
            float width = size * aspect;
            focus.x = painting.extents.x <= width ? painting.center.x
                : Mathf.Clamp(focus.x, painting.min.x + width, painting.max.x - width);
            focus.y = painting.extents.y <= size ? painting.center.y
                : Mathf.Clamp(focus.y, painting.min.y + size, painting.max.y - size);
            return focus;
        }

        bool Offscreen(Vector3 focus) =>
            Mathf.Abs(focus.x - GameCamera.transform.position.x) > GameCamera.orthographicSize * GameCamera.aspect;

        void FrameCeremony(Vector3 focus, float size, float blend)
        {
            GameCamera.orthographicSize = Mathf.Lerp(GameCamera.orthographicSize, size, blend);
            Vector3 position = Vector3.Lerp(GameCamera.transform.position - lastShake, focus, blend);
            GameCamera.transform.position = hasStageBackground
                ? InsideBackdrop(position, GameCamera.orthographicSize, GameCamera.aspect, paintedBackgroundBounds) : position;
            lastShake = Vector3.zero;
        }

        public void SwordHitFeedback()
        {
            if (!IsCombat) return;
            var tuning = Config.Techniques;
            hitFeedback.Trigger(Time.unscaledTime, tuning.hitStopSeconds, tuning.hitShakeSeconds);
            UpdateHitFeedback();
        }
        void UpdateHitFeedback()
        {
            Time.timeScale = GameplayFrozen ? 0 : hitFeedback.TimeScale(Time.unscaledTime, IsCombat, Paused, KarmaBootstrap.Step);
        }
        void ResetHitFeedback()
        {
            hitFeedback.Reset();
            if (GameCamera != null) GameCamera.transform.position -= lastShake;
            lastShake = Vector3.zero;
        }
        void LateUpdate()
        {
            UpdateCameraViewport();
            if (Player == null) return;
            if (Phase == RunPhase.Ending) { FrameEnding(); return; }
            if (EndingCompleted && Phase == RunPhase.Victory) return;
            if (Tutorial != null) { Tutorial.FrameCamera(); return; }
            if (Phase == RunPhase.PlayerFallen && Enemies.Count > 0 && Enemies[0] != null)
            {
                float guardianX = Enemies[0].Position.x;
                float size = Mathf.Max(12, Mathf.Abs(Player.Position.x - guardianX) / (2 * GameCamera.aspect) + 4);
                var focus = new Vector3((Player.Position.x + guardianX) * 0.5f,
                    hasStageBackground ? stageBackgroundBounds.center.y : 4.4f, -10);
                float blend = 1 - Mathf.Exp(-3 * Time.deltaTime);
                GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position - lastShake, focus, blend);
                GameCamera.orthographicSize = Mathf.Lerp(GameCamera.orthographicSize, size, blend);
                lastShake = Vector3.zero;
                return;
            }
            if (Phase == RunPhase.GuardianFallen)
            {
                float x = fallenGuardianPosition.x;
                var focus = new Vector3(x + Mathf.Clamp((Player.Position.x - x) * 0.5f, -4, 4), 3.6f, -10);
                FrameCeremony(focus, 11.5f, Offscreen(fallenGuardianPosition) ? 1 : 1 - Mathf.Exp(-5 * Time.deltaTime));
                return;
            }
            if (Phase == RunPhase.Choosing || Phase == RunPhase.PlayerFallen || Phase == RunPhase.Defeat)
            {
                // Hold the combat framing through the fall and selection screen.
                GameCamera.transform.position -= lastShake;
                lastShake = Vector3.zero;
                return;
            }
            if (Phase == RunPhase.Transition)
            {
                if (Paused) return;
                var actor = GuardianInheriting && Enemies.Count > 0 ? Enemies[0].transform : Player.transform;
                float height = GuardianInheriting ? 2.2f : 0.6f;
                var focus = actor.position + new Vector3(GuardianInheriting ? 0 : Player.Facing * 2, height, -10);
                bool growth = GuardianInheriting && guardianGrowth != null;
                if (growth) focus = guardianGrowth.CameraFocus;
                float blend = 1 - Mathf.Exp(-3 * Time.deltaTime);
                float combatSize = hasStageBackground
                    ? Mathf.Max(stageBackgroundBounds.extents.y, 25 / Mathf.Max(GameCamera.aspect, 0.01f))
                    : 6.8f / Mathf.Min(1, Mathf.Max(0.4f, GameCamera.aspect));
                float returnBlend = Mathf.SmoothStep(0, 1, GrowthReturnProgress);
                var wideFocus = new Vector3(Player.Position.x + Player.Facing * KarmaSpace.Units(2),
                    hasStageBackground ? stageBackgroundBounds.center.y : 4.4f, -10);
                if (hasStageBackground)
                {
                    float left = Mathf.Max(KarmaStage.ArenaLeft, stageBackgroundBounds.min.x);
                    float right = Mathf.Min(KarmaStage.ArenaRight, stageBackgroundBounds.max.x);
                    float halfWidth = combatSize * GameCamera.aspect;
                    wideFocus.x = right - left <= halfWidth * 2 ? (left + right) * 0.5f
                        : Mathf.Clamp(wideFocus.x, left + halfWidth, right - halfWidth);
                }
                // Keep both fighters on screen while the next name is up, even
                // if the previous fight ended with a projectile from the other wall.
                float nameSize = combatSize;
                if (returnBlend > 0 && Enemies.Count > 0)
                {
                    float leftActor = Mathf.Min(Player.Position.x, actor.position.x);
                    float rightActor = Mathf.Max(Player.Position.x, actor.position.x);
                    nameSize = Mathf.Max(combatSize, ((rightActor - leftActor) * 0.5f + 5) / GameCamera.aspect);
                    float reach = nameSize * GameCamera.aspect - 5;
                    wideFocus.x = Mathf.Clamp(wideFocus.x, rightActor - reach, leftActor + reach);
                }
                // The return is already eased by the transition clock. Settle
                // exactly on the two-shot when the confirmation gate opens.
                blend = Mathf.Lerp(blend, 1, returnBlend);
                if (returnBlend == 0 && Offscreen(actor.position)) blend = 1;
                focus = Vector3.Lerp(focus, wideFocus, returnBlend);
                float shotSize = Mathf.Max(GuardianInheriting ? 10.5f : 8.8f,
                    combatSize * (GuardianInheriting ? 0.76f : 0.62f));
                FrameCeremony(focus, Mathf.Lerp(shotSize, nameSize, returnBlend), blend);
                if (!Paused && !GuardianInheriting && Time.time < presentationShakeUntil)
                {
                    lastShake = new Vector3(Mathf.Sin(Time.time * 83), Mathf.Sin(Time.time * 67) * 0.5f, 0)
                        * Config.Techniques.hitShakeStrength * KarmaPreferences.ScreenShake * presentationShakeStrength;
                    GameCamera.transform.position += lastShake;
                }
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
                float strength = Config.Techniques.hitShakeStrength * KarmaPreferences.ScreenShake * hitFeedback.Shake(Time.unscaledTime, Config.Techniques.hitShakeSeconds);
                lastShake = new Vector3(Mathf.Sin(Time.unscaledTime * 97) * strength, Mathf.Sin(Time.unscaledTime * 73) * strength * 0.55f, 0);
            }
            if (IsCombat && !Paused && Time.time < presentationShakeUntil)
                lastShake += new Vector3(Mathf.Sin(Time.time * 83), Mathf.Sin(Time.time * 67) * 0.5f, 0)
                    * Config.Techniques.hitShakeStrength * KarmaPreferences.ScreenShake * presentationShakeStrength;
            GameCamera.transform.position += lastShake;
        }
    }
}
