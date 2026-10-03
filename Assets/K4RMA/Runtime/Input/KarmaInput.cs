using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KarmaPrototype
{
    // Both legacy Input Manager and the new Input System are supported.
    public static class KarmaInput
    {
        public static bool Held(KeyCode code) { return Read(code, false); }
        public static bool Pressed(KeyCode code) { return Read(code, true); }
        public static float Horizontal
        {
            get
            {
                return (Held(KeyCode.D) || Held(KeyCode.RightArrow) ? 1 : 0)
                     - (Held(KeyCode.A) || Held(KeyCode.LeftArrow) ? 1 : 0);
            }
        }
        static bool Read(KeyCode code, bool down)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Key key;
            switch (code)
            {
                case KeyCode.A: key = Key.A; break;
                case KeyCode.D: key = Key.D; break;
                case KeyCode.LeftArrow: key = Key.LeftArrow; break;
                case KeyCode.RightArrow: key = Key.RightArrow; break;
                case KeyCode.Space: key = Key.Space; break;
                case KeyCode.J: key = Key.J; break;
                case KeyCode.Q: key = Key.Q; break;
                case KeyCode.E: key = Key.E; break;
                case KeyCode.F: key = Key.F; break;
                case KeyCode.R: key = Key.R; break;
                case KeyCode.LeftShift: key = Key.LeftShift; break;
                case KeyCode.Escape: key = Key.Escape; break;
                case KeyCode.Return: key = Key.Enter; break;
                case KeyCode.Alpha1: key = Key.Digit1; break;
                case KeyCode.Alpha2: key = Key.Digit2; break;
                case KeyCode.Alpha3: key = Key.Digit3; break;
                default: return false;
            }
            return down ? keyboard[key].wasPressedThisFrame : keyboard[key].isPressed;
#else
            return down ? Input.GetKeyDown(code) : Input.GetKey(code);
#endif
        }
    }
}
