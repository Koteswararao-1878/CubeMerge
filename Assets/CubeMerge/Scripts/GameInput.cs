using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace CubeMerge
{
    /// <summary>
    /// Keyboard input that works with both the old Input Manager and the new Input System package.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        public static bool Up()      { var k = Keyboard.current; return k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame); }
        public static bool Down()    { var k = Keyboard.current; return k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame); }
        public static bool Left()    { var k = Keyboard.current; return k != null && (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame); }
        public static bool Right()   { var k = Keyboard.current; return k != null && (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame); }
        public static bool Undo()    { var k = Keyboard.current; return k != null && (k.zKey.wasPressedThisFrame || k.uKey.wasPressedThisFrame); }
        public static bool Restart() { var k = Keyboard.current; return k != null && k.rKey.wasPressedThisFrame; }
        public static bool Confirm() { var k = Keyboard.current; return k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame); }
#else
        public static bool Up()      { return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W); }
        public static bool Down()    { return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S); }
        public static bool Left()    { return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A); }
        public static bool Right()   { return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D); }
        public static bool Undo()    { return Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.U); }
        public static bool Restart() { return Input.GetKeyDown(KeyCode.R); }
        public static bool Confirm() { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space); }
#endif
    }
}
