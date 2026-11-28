using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightWarden.Player
{
    /// <summary>
    /// Unified input handler supporting both the new Input System package
    /// and legacy Input Manager seamlessly without exceptions.
    /// </summary>
    public static class WardenInput
    {
        public static float GetHorizontal()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                float val = 0f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) val += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) val -= 1f;
                return val;
            }
#endif
            try { return Input.GetAxisRaw("Horizontal"); } catch { return 0f; }
        }

        public static float GetVertical()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                float val = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) val += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) val -= 1f;
                return val;
            }
#endif
            try { return Input.GetAxisRaw("Vertical"); } catch { return 0f; }
        }

        public static Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue() * 0.15f;
            }
#endif
            try { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); } catch { return Vector2.zero; }
        }

        public static bool GetSprint()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
#endif
            try { return Input.GetKey(KeyCode.LeftShift); } catch { return false; }
        }

        public static bool GetCrouchDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl); } catch { return false; }
        }

        public static bool GetJumpDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.spaceKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.Space); } catch { return false; }
        }

        public static bool GetPulseDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.qKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.Q); } catch { return false; }
        }

        public static bool GetTakedownDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.fKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.F); } catch { return false; }
        }

        public static bool GetDefuseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.eKey.isPressed;
#endif
            try { return Input.GetKey(KeyCode.E); } catch { return false; }
        }

        public static bool GetMapToggleDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.mKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M); } catch { return false; }
        }

        public static bool GetEscapeDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
            try { return Input.GetKeyDown(KeyCode.Escape); } catch { return false; }
        }
    }
}
