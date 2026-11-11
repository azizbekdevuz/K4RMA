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
        // Keep Input System's existing v1 JSON format. Runtime-created maps get
        // new binding GUIDs each launch, but v1 also stores the stable map/action
        // name. This UI has always edited binding zero only; use that contract
        // when an earlier launch's GUID no longer resolves.
        [Serializable] sealed class SavedBindings { public SavedBinding[] bindings = null; }
        [Serializable] sealed class SavedBinding
        {
            public string action = null, id = null, path = null, interactions = null, processors = null;
        }
        internal static void RestoreSavedBindings(InputActionMap target, string json)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrEmpty(json)) { target.RemoveAllBindingOverrides(); return; }
            SavedBindings saved;
            try { saved = JsonUtility.FromJson<SavedBindings>(json); }
            catch (ArgumentException error) { throw new FormatException("Invalid binding JSON.", error); }
            if (saved?.bindings == null) throw new FormatException("Missing binding list.");
            var pending = new System.Collections.Generic.List<(InputAction action, int index, InputBinding binding)>();
            foreach (var entry in saved.bindings)
            {
                if (entry == null || entry.path == null && entry.interactions == null && entry.processors == null)
                    throw new FormatException("Missing binding override.");
                InputAction action = null;
                int index = -1;
                if (Guid.TryParse(entry.id, out var id))
                    index = target.FindBinding(new InputBinding { id = id }, out action);
                if (index < 0)
                {
                    string prefix = target.name + "/";
                    if (entry.action == null || !entry.action.StartsWith(prefix, StringComparison.Ordinal))
                        throw new FormatException("Unknown saved action: " + entry.action);
                    string name = entry.action.Substring(prefix.Length);
                    if (!Enum.TryParse(name, out KarmaAction key) || key.ToString() != name
                        || Array.IndexOf(ConfigurableActions, key) < 0
                        || (action = target.FindAction(name)) == null || action.bindings.Count == 0)
                        throw new FormatException("Unknown saved action: " + entry.action);
                    index = 0;
                }
                foreach (var item in pending)
                    if (item.action == action && item.index == index)
                        throw new FormatException("Duplicate saved binding: " + entry.action);
                pending.Add((action, index, new InputBinding {
                    overridePath = entry.path == "null" ? null : entry.path,
                    overrideInteractions = entry.interactions == "null" ? null : entry.interactions,
                    overrideProcessors = entry.processors == "null" ? null : entry.processors
                }));
            }
            // Validate the complete document before replacing any active keys.
            target.RemoveAllBindingOverrides();
            foreach (var item in pending) item.action.ApplyBindingOverride(item.index, item.binding);
        }
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
                if (other == id || SharesTechnique(id, other)) continue;
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
        static bool SharesTechnique(KarmaAction first, KarmaAction second)
        {
            // Original and personalized forms share defaults but remain independently rebindable.
            return first == KarmaAction.SwordWave && second == KarmaAction.Pierce
                || first == KarmaAction.Pierce && second == KarmaAction.SwordWave
                || first == KarmaAction.Guard && second == KarmaAction.Counter
                || first == KarmaAction.Counter && second == KarmaAction.Guard;
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
