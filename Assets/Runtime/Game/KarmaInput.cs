using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KarmaPrototype
{
    public enum KarmaAction
    {
        Left, Right, Up, Down, Jump, Attack, Counter, SwordWave, RisingSlash, Guard,
        Interact, Pause, Confirm, Restart, Title, ChooseSwordWave, ChooseRisingSlash, ChooseGuard,
        ToggleInvincibility, Pierce, Settings, DialogueConfirm, SkipCinematic, RestoreHealth
    }

    // Shared actions stay enabled in menus and while Time.timeScale is zero.
    public static partial class KarmaInput
    {
        static InputActionMap map;
        static InputAction[] actions;

        internal readonly struct Edge
        {
            public readonly double RealTime;
            public readonly bool Down;
            public Edge(double realTime, bool down) { RealTime = realTime; Down = down; }
        }

        sealed class ButtonHistory
        {
            public int Frame = -1;
            public readonly List<Edge> Edges = new List<Edge>(4);
            public void Record(double time, bool down)
            {
                if (Frame != Time.frameCount) { Frame = Time.frameCount; Edges.Clear(); }
                Edges.Add(new Edge(time, down));
            }
        }
        static ButtonHistory[] history;

        internal static IReadOnlyList<Edge> Edges(KarmaAction id)
        {
            Initialize();
            var button = history[(int)id];
            if (button.Frame == Time.frameCount) return button.Edges;
            return System.Array.Empty<Edge>();
        }

        internal static float GameTimeOf(double eventTime)
        {
            // Input System timestamps and realtimeSinceStartup share a clock. Map into
            // this frame's actual scaled interval (also works across slow motion).
            double age = System.Math.Max(0, Time.realtimeSinceStartupAsDouble - eventTime);
            float fraction = Time.unscaledDeltaTime > 0 ? Mathf.Clamp01((float)age / Time.unscaledDeltaTime) : 0;
            return Time.time - fraction * Time.deltaTime;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            // Also runs when entering Play with domain reload disabled.
            CancelRebinding();
            if (map != null) map.Dispose();
            map = null;
            actions = null; history = null;
            RebindMessage = null; rebindHandledFrame = -1;
            Application.quitting -= Reset;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (map != null) return;
            map = new InputActionMap("K4RMA");
            actions = new InputAction[System.Enum.GetValues(typeof(KarmaAction)).Length];
            history = new ButtonHistory[actions.Length];
            Bind(KarmaAction.Left, "a", "leftArrow");
            Bind(KarmaAction.Right, "d", "rightArrow");
            Bind(KarmaAction.Up, "w", "upArrow");
            Bind(KarmaAction.Down, "s", "downArrow");
            Bind(KarmaAction.Jump, "space");
            Bind(KarmaAction.Attack, "j");
            Bind(KarmaAction.Counter, "k");
            Bind(KarmaAction.SwordWave, "l");
            Bind(KarmaAction.RisingSlash, "leftShift");
            Bind(KarmaAction.Guard, "k");
            Bind(KarmaAction.Interact, "w");
            Bind(KarmaAction.Pause, "escape");
            Bind(KarmaAction.Confirm, "enter");
            Bind(KarmaAction.Restart, "r");
            Bind(KarmaAction.Title, "t");
            Bind(KarmaAction.ChooseSwordWave, "1");
            Bind(KarmaAction.ChooseRisingSlash, "2");
            Bind(KarmaAction.ChooseGuard, "3");
            Bind(KarmaAction.ToggleInvincibility, "p");
            Bind(KarmaAction.Pierce, "l");
            Bind(KarmaAction.Settings, "o");
            Bind(KarmaAction.DialogueConfirm, "space");
            Bind(KarmaAction.SkipCinematic, "f");
            Bind(KarmaAction.RestoreHealth, "semicolon");
            if (PlayerPrefs.HasKey(BindingsKey))
            {
                try { RestoreSavedBindings(map, PlayerPrefs.GetString(BindingsKey)); }
                catch (System.FormatException error)
                {
                    // Leave the saved value intact so malformed data remains
                    // recoverable; this fresh map still has its default keys.
                    Debug.LogWarning("저장된 키 설정을 불러올 수 없습니다: " + error.Message);
                }
            }
            map.Enable();
            Application.quitting += Reset;
        }

        static void Bind(KarmaAction id, params string[] keys)
        {
            var action = map.AddAction(id.ToString(), InputActionType.Button);
            foreach (var key in keys) action.AddBinding("<Keyboard>/" + key);
            var button = history[(int)id] = new ButtonHistory();
            action.performed += context => button.Record(context.time, true);
            action.canceled += context => button.Record(context.time, false);
            actions[(int)id] = action;
        }

        public static bool Held(KarmaAction id)
        {
            Initialize();
            return actions[(int)id].IsPressed();
        }

        public static bool Pressed(KarmaAction id)
        {
            Initialize();
            // Preserve alias edges: pressing an arrow while its WASD alias is held
            // still counts, but simultaneous aliases count as one direction.
            var buttonHistory = history[(int)id];
            if (buttonHistory.Frame == Time.frameCount)
                for (int i = 0; i < buttonHistory.Edges.Count; i++)
                    if (buttonHistory.Edges[i].Down) return true;
            foreach (var control in actions[(int)id].controls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            return false;
        }

        // Opposing directions cancel; diagonal inputs retain both cardinal axes.
        public static float Horizontal => (Held(KarmaAction.Right) ? 1 : 0) - (Held(KarmaAction.Left) ? 1 : 0);
        public static float Vertical => (Held(KarmaAction.Up) ? 1 : 0) - (Held(KarmaAction.Down) ? 1 : 0);

    }
}
