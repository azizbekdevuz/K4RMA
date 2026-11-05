using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
namespace KarmaPrototype
{
    public static partial class KarmaInput
    {
        const string BindingsKey = "K4RMA.InputBindings.v1";
        static InputActionRebindingExtensions.RebindingOperation rebind;
        static int rebindHandledFrame = -1;
        public static bool Rebinding => rebind != null;
        public static bool RebindingThisFrame => Rebinding || rebindHandledFrame == Time.frameCount;
        public static string RebindMessage { get; private set; }
        public static readonly KarmaAction[] ConfigurableActions = {
            KarmaAction.Left, KarmaAction.Right, KarmaAction.Up, KarmaAction.Down, KarmaAction.Jump,
            KarmaAction.Attack, KarmaAction.Pierce, KarmaAction.Counter, KarmaAction.SwordWave, KarmaAction.RisingSlash, KarmaAction.Guard
        };
        public static string BindingLabel(KarmaAction id)
        {
            Initialize();
            // Display the physical key path, not the OS's current Hangul/IME label.
            string path = actions[(int)id].bindings[0].effectivePath;
            if (string.IsNullOrEmpty(path)) return "Unbound";
            string key = path.Substring(path.LastIndexOf('/') + 1);
            if (key.Length == 1) return key.ToUpperInvariant();
            switch (key)
            {
                case "space": return "Space";
                case "escape": return "Esc";
                case "enter": return "Enter";
                case "tab": return "Tab";
                case "leftShift": return "Left Shift";
                case "rightShift": return "Right Shift";
                case "leftCtrl": return "Left Ctrl";
                case "rightCtrl": return "Right Ctrl";
                case "leftAlt": return "Left Alt";
                case "rightAlt": return "Right Alt";
                case "leftArrow": return "Left Arrow";
                case "rightArrow": return "Right Arrow";
                case "upArrow": return "Up Arrow";
                case "downArrow": return "Down Arrow";
            }
            if (key.StartsWith("f", StringComparison.Ordinal) && int.TryParse(key.Substring(1), out _)) return key.ToUpperInvariant();
            string words = System.Text.RegularExpressions.Regex.Replace(key, "([a-z])([A-Z0-9])", "$1 $2");
            return char.ToUpperInvariant(words[0]) + words.Substring(1);
        }
        public static void BeginRebinding(KarmaAction id)
        {
            Initialize(); CancelRebinding();
            var action = actions[(int)id];
            string previous = action.bindings[0].overridePath;
            action.Disable(); RebindMessage = "새 키를 누르세요. Esc로 취소합니다.";
            rebind = action.PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(op => { RestoreBinding(action, previous); FinishRebinding("키 변경을 취소했습니다."); })
                .OnComplete(op =>
                {
                    string path = "<Keyboard>/" + op.selectedControl.name;
                    if (!TrySetBinding(id, path, out var message)) RestoreBinding(action, previous);
                    FinishRebinding(message);
                });
            rebind.Start();
        }
        public static bool TrySetBinding(KarmaAction id, string path, out string message)
        {
            Initialize();
            if (Array.IndexOf(ConfigurableActions, id) < 0 || string.IsNullOrEmpty(path)
                || !path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            { message = "키보드 키를 선택하세요."; return false; }
            string key = path.Substring(path.LastIndexOf('/') + 1).ToLowerInvariant();
            if (key == "escape" || key == "o" || key == "p" || key == "anykey")
            { message = "Esc·O·P는 메뉴·테스트 조작에 사용합니다."; return false; }
            foreach (var other in ConfigurableActions)
            {
                if (other == id) continue;
                foreach (var binding in actions[(int)other].bindings)
                {
                    string used = binding.effectivePath;
                    if (!string.IsNullOrEmpty(used) && string.Equals(used.Substring(used.LastIndexOf('/') + 1), key, StringComparison.OrdinalIgnoreCase))
                    { message = "이미 사용 중인 키입니다. 먼저 해당 키의 동작을 변경하세요."; return false; }
                }
            }
            actions[(int)id].ApplyBindingOverride(0, path);
            PlayerPrefs.SetString(BindingsKey, map.SaveBindingOverridesAsJson());
            message = "변경한 키를 저장했습니다."; return true;
        }
        static void RestoreBinding(InputAction action, string previous)
        { if (previous == null) action.RemoveBindingOverride(0); else action.ApplyBindingOverride(0, previous); }
        static void FinishRebinding(string message)
        {
            rebind?.Dispose(); rebind = null; map?.Enable();
            rebindHandledFrame = Time.frameCount; RebindMessage = message;
        }
        public static void CancelRebinding() { rebind?.Cancel(); }
        public static void ResetBindings()
        {
            Initialize(); CancelRebinding(); map.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingsKey); RebindMessage = "기본 키로 복원했습니다.";
        }
    }
}
