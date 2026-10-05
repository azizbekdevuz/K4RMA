using UnityEngine;
using UnityEngine.InputSystem;

namespace K4RMA
{
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        public float MoveX { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool AbilityPressed { get; private set; }
        public bool RisingSlashPressed { get; private set; }
        public bool GuardHeld { get; private set; }
        public bool GuardPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool RestartPressed { get; private set; }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                MoveX = 0f;
                JumpPressed = false;
                AttackPressed = false;
                AbilityPressed = false;
                RisingSlashPressed = false;
                GuardHeld = false;
                GuardPressed = false;
                InteractPressed = false;
                RestartPressed = false;
                return;
            }

            float move = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                move -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                move += 1f;

            MoveX = Mathf.Clamp(move, -1f, 1f);
            JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            AttackPressed = keyboard.jKey.wasPressedThisFrame;
            AbilityPressed = keyboard.kKey.wasPressedThisFrame;
            RisingSlashPressed = keyboard.lKey.wasPressedThisFrame;
            GuardHeld = keyboard.iKey.isPressed;
            GuardPressed = keyboard.iKey.wasPressedThisFrame;
            InteractPressed = keyboard.eKey.wasPressedThisFrame;
            RestartPressed = keyboard.rKey.wasPressedThisFrame;
        }
    }
}
