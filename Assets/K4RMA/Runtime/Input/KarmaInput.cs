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
        public static float Vertical
        {
            get { return (Held(KeyCode.W) || Held(KeyCode.UpArrow) ? 1 : 0)
                       - (Held(KeyCode.S) || Held(KeyCode.DownArrow) ? 1 : 0); }
        }
        // Treat keyboard aliases as a single direction, even if pressed together.
        public static bool DirectionPressed(int direction)
        {
            switch (direction)
            {
                case 1: return Pressed(KeyCode.A) || Pressed(KeyCode.LeftArrow);
                case 2: return Pressed(KeyCode.D) || Pressed(KeyCode.RightArrow);
                case 3: return Pressed(KeyCode.W) || Pressed(KeyCode.UpArrow);
                case 4: return Pressed(KeyCode.S) || Pressed(KeyCode.DownArrow);
                default: return false;
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
                case KeyCode.W: key = Key.W; break;
                case KeyCode.D: key = Key.D; break;
                case KeyCode.LeftArrow: key = Key.LeftArrow; break;
                case KeyCode.RightArrow: key = Key.RightArrow; break;
                case KeyCode.Space: key = Key.Space; break;
                case KeyCode.J: key = Key.J; break;
                case KeyCode.K: key = Key.K; break;
                case KeyCode.S: key = Key.S; break;
                case KeyCode.H: key = Key.H; break;
                case KeyCode.T: key = Key.T; break;
                case KeyCode.UpArrow: key = Key.UpArrow; break;
                case KeyCode.DownArrow: key = Key.DownArrow; break;
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
