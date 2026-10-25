using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;
using KarmaPrototype;

// Run with Unity Pipeline script run. Verifies the displayed sprite after sheet overrides.
public static class CheckPlayerRun
{
    public static string Main()
    {
        var root = new GameObject("Run animation check") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var picture = new GameObject("Picture"); picture.transform.SetParent(root.transform, false);
            var renderer = picture.AddComponent<SpriteRenderer>();
            var animator = root.AddComponent<KarmaCharacterAnimator>();
            animator.ConfigureSprites(renderer, KarmaArtwork.PlayerFrames());
            animator.ConfigureSheet("Player");
            var clock = typeof(KarmaCharacterAnimator).GetField("runAt", BindingFlags.Instance | BindingFlags.NonPublic);
            animator.Pose(1, false);
            Sprite first = renderer.sprite;
            Check(first == KarmaArtwork.Sprite("Player", "Run"), "run begins on the first movement pose");
            Vector3 anchor = picture.transform.localPosition;
            clock.SetValue(animator, Time.time - 0.13f);
            animator.Pose(1, false);
            Sprite second = renderer.sprite;
            Check(second != first && second == KarmaArtwork.Settings.playerMove2, "second movement pose survives the sheet override");
            Check(Mathf.Abs(picture.transform.localPosition.y - anchor.y) < 0.06f, "run frames preserve their shared foot anchor");
            animator.Pose(-1, false);
            Check(renderer.sprite == second, "left movement uses the same run cycle");
            clock.SetValue(animator, Time.time - 0.23f);
            animator.Pose(1, false);
            Check(renderer.sprite == first, "run cycle loops");
            animator.Pose(0, false);
            Check(animator.CurrentPose == "Idle", "stopping returns to idle");
            animator.Pose(1, false);
            Check(renderer.sprite == first, "restarting movement resets the cycle");
            animator.Pose(1, true, 3);
            Check(animator.CurrentPose == "Jump", "airborne pose retains priority over running");
            animator.Attack();
            animator.Pose(1, false);
            Check(animator.CurrentPose == "Windup", "attack pose retains priority over running");
            return "PASS: 9 player run animation checks; frames=" + first.name + "/" + second.name;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    // Exercise the real Input System -> Player.Update -> animation path in Play Mode.
    public static async Task<string> Play()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        Check(Application.isPlaying && game != null && game.CanMove, "start a run before Play check");
        var behavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var background = InputSystem.settings.backgroundBehavior;
        bool runInBackground = Application.runInBackground;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            var animator = game.Player.GetComponentInChildren<KarmaCharacterAnimator>();
            var renderer = animator.GetComponentInChildren<SpriteRenderer>();
            var shown = new HashSet<Sprite>();
            float start = game.Player.Position.x;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            double until = UnityEditor.EditorApplication.timeSinceStartup + 0.65;
            while (UnityEditor.EditorApplication.timeSinceStartup < until)
            {
                await Task.Delay(20);
                if (animator.CurrentPose == "Run") shown.Add(renderer.sprite);
            }
            Check(game.Player.Position.x > start + 1, "real movement advances the player");
            Check(shown.Count == 2, "real player updates display both run frames");
            return "PASS: actual movement displayed both run frames; distance=" + (game.Player.Position.x - start).ToString("0.00");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = behavior;
            InputSystem.settings.backgroundBehavior = background;
            Application.runInBackground = runInBackground;
        }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
