#if UNITY_EDITOR
using System;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

// Runs only in the disposable Play Mode project created by run-unity-playchecks.py.
public static class KarmaMigrationChecks
{
    public static void Run(KarmaGame game)
    {
        Check(GraphicsSettings.currentRenderPipeline != null, "URP is active");
        var sprite = KarmaVisuals.DefaultMaterial;
        Check(sprite != null && sprite.shader.name == "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "empty-scene fallback material is included in Resources");
        var saved = AssetDatabase.LoadAssetAtPath<Material>("Assets/K4RMA/Settings/KarmaSprite.mat");
        Check(saved != null && saved.shader == sprite.shader, "existing scenes use the URP sprite shader");
        Check(!ShaderUtil.ShaderHasError(sprite.shader), "sprite shader compiles");
        Check(KarmaCombatVFX.GlowMaterial != null && !ShaderUtil.ShaderHasError(KarmaCombatVFX.GlowMaterial.shader),
            "additive shader compiles");
        var box = KarmaVisuals.Box(game.transform, "URP check", Vector2.zero, Vector2.one, Color.white);
        Check(box.sharedMaterial == sprite, "procedural sprites use the fallback material");
        UnityEngine.Object.Destroy(box.gameObject);

        var previousMode = InputSystem.settings.updateMode;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            KarmaInput.Held(KarmaAction.Attack); // Resolve actions before the first event.
            Send(keyboard, Key.A, Key.LeftArrow);
            Check(KarmaInput.Horizontal == -1 && KarmaInput.DirectionPressed(1), "simultaneous left aliases count as one direction");
            Send(keyboard, Key.A);
            Send(keyboard, Key.A, Key.LeftArrow);
            Check(KarmaInput.DirectionPressed(1), "arrow edge is preserved while WASD is held");
            Send(keyboard, Key.A, Key.D, Key.W, Key.S);
            Check(KarmaInput.Horizontal == 0 && KarmaInput.Vertical == 0, "opposing axes cancel");
            Send(keyboard, Key.RightArrow, Key.UpArrow);
            Check(KarmaInput.Horizontal == 1 && KarmaInput.Vertical == 1, "diagonal attack axes are preserved");
            Send(keyboard, Key.DownArrow);
            Check(KarmaInput.Vertical == -1 && KarmaInput.DirectionPressed(4), "downward attack input");

            var keys = new[] { Key.Space, Key.J, Key.K, Key.Q, Key.LeftShift, Key.E, Key.F, Key.Escape,
                Key.Enter, Key.R, Key.T, Key.S, Key.H, Key.Digit1, Key.Digit2, Key.Digit3 };
            var actions = new[] { KarmaAction.Jump, KarmaAction.Attack, KarmaAction.Counter, KarmaAction.SwordWave,
                KarmaAction.RisingSlash, KarmaAction.Guard, KarmaAction.Interact, KarmaAction.Pause, KarmaAction.Confirm,
                KarmaAction.Restart, KarmaAction.Title, KarmaAction.Story, KarmaAction.Controls,
                KarmaAction.ChooseSwordWave, KarmaAction.ChooseRisingSlash, KarmaAction.ChooseGuard };
            for (int i = 0; i < keys.Length; i++)
            {
                Send(keyboard);
                Send(keyboard, keys[i]);
                Check(KarmaInput.Pressed(actions[i]) && KarmaInput.Held(actions[i]), "binding " + actions[i]);
            }
            Send(keyboard);
            Send(keyboard, Key.J);
            InputSystem.Update();
            Check(KarmaInput.Held(KarmaAction.Attack) && !KarmaInput.Pressed(KarmaAction.Attack), "held attack does not repeat the press edge");
            Send(keyboard);
            Send(keyboard, Key.J);
            Check(KarmaInput.Pressed(KarmaAction.Attack), "release and repress produces another attack edge");

            game.ReturnToTitle();
            Send(keyboard);
            Send(keyboard, Key.S);
            Update(game);
            Check(game.Phase == RunPhase.Story, "S opens story");
            var hud = game.GetComponent<KarmaHUD>();
            Send(keyboard, Key.RightArrow);
            Update(hud);
            var page = typeof(KarmaHUD).GetField("storyPage", BindingFlags.Instance | BindingFlags.NonPublic);
            Check((int)page.GetValue(hud) == 1, "story navigation uses Input System once per Update");
            Send(keyboard, Key.LeftArrow);
            Update(hud);
            Check((int)page.GetValue(hud) == 0, "previous story page");
            Send(keyboard, Key.Escape);
            Update(game);
            Check(game.Phase == RunPhase.Title, "Esc returns to title");
            Send(keyboard, Key.Enter);
            Update(game);
            Check(game.Phase == RunPhase.Combat, "Enter starts a run");
            Send(keyboard, Key.Escape);
            Update(game);
            Check(game.Paused && Time.timeScale == 0, "Esc pauses");
            Send(keyboard, Key.T);
            Update(game);
            Check(game.Phase == RunPhase.Title && Time.timeScale == 1, "title action works while paused");
            Send(keyboard);
            Check(!KarmaInput.Held(KarmaAction.Attack) && KarmaInput.Horizontal == 0, "release clears input state");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.updateMode = previousMode;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousBehavior;
        }
        Debug.Log("PASS: URP materials, all keyboard bindings, alias edges, story/start/pause/title input");
    }
    static void Send(Keyboard keyboard, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }
    static void Update(object target) => target.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Check(bool ok, string label) { if (!ok) throw new Exception("Migration: " + label); }
}
#endif
