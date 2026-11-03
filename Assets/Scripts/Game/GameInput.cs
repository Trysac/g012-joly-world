using UnityEngine;
using UnityEngine.InputSystem;

namespace JolyWorld.Game
{
    public enum InputSource
    {
        KeyboardMouse,
        Gamepad,
    }

    /// <summary>
    /// The keyboard, the mouse and gamepads through the Input System, read once per frame with
    /// <see cref="Read"/>. Menus use the step, confirm and back helpers.
    /// </summary>
    public sealed class GameInput
    {
        const float StickDeadZone = 0.25f;
        const float StickStep = 0.6f;

        int stickVertical;
        int stickHorizontal;
        int lastFrame = -1;

        static Keyboard Keys => Keyboard.current;

        public float Move { get; private set; }
        public bool Jump { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool Sprint { get; private set; }
        public bool Dash { get; private set; }
        public bool Cast { get; private set; }
        public bool UseSelected { get; private set; }

        /// <summary>A hotbar slot picked with the number keys this frame, or -1.</summary>
        public int PickSlot { get; private set; } = -1;

        /// <summary>-1 or +1 to move the hotbar selection (wheel, Q/R, shoulder buttons).</summary>
        public int SlotStep { get; private set; }

        public bool Inventory { get; private set; }
        public bool Pause { get; private set; }

        public InputSource Source { get; private set; } = InputSource.KeyboardMouse;

        public void Read()
        {
            if (lastFrame == Time.frameCount)
            {
                return;
            }
            lastFrame = Time.frameCount;
            float move = 0f;
            bool jump = false, jumpHeld = false, sprint = false, dash = false, cast = false, use = false, inventory = false, pause = false;
            int pick = -1, slotStep = 0;

            Keyboard keys = Keys;
            if (keys != null)
            {
                float typed = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
                move += typed;
                jump |= keys.spaceKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame;
                jumpHeld |= keys.spaceKey.isPressed || keys.wKey.isPressed || keys.upArrowKey.isPressed;
                sprint |= keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
                dash |= keys.kKey.wasPressedThisFrame || keys.xKey.wasPressedThisFrame;
                cast |= keys.jKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame;
                use |= keys.eKey.wasPressedThisFrame;
                inventory |= keys.iKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame;
                pause |= keys.escapeKey.wasPressedThisFrame || keys.pKey.wasPressedThisFrame;
                if (keys.qKey.wasPressedThisFrame) slotStep = -1;
                if (keys.rKey.wasPressedThisFrame) slotStep = 1;
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7 };
                for (int i = 0; i < digits.Length; i++)
                {
                    if (keys[digits[i]].wasPressedThisFrame)
                    {
                        pick = i;
                    }
                }
                if (typed != 0f || jump || dash || cast || use || pick >= 0)
                {
                    Source = InputSource.KeyboardMouse;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.1f)
                {
                    slotStep = wheel > 0f ? -1 : 1;
                    Source = InputSource.KeyboardMouse;
                }
            }

            foreach (Gamepad pad in Gamepad.all)
            {
                float stick = pad.leftStick.ReadValue().x;
                if (Mathf.Abs(stick) < StickDeadZone)
                {
                    stick = 0f;
                }
                float dpad = (pad.dpad.right.isPressed ? 1f : 0f) - (pad.dpad.left.isPressed ? 1f : 0f);
                move += stick + dpad;
                bool pressed = pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
                jump |= pad.buttonSouth.wasPressedThisFrame;
                jumpHeld |= pad.buttonSouth.isPressed;
                sprint |= pad.rightTrigger.isPressed || pad.leftTrigger.isPressed;
                dash |= pad.buttonEast.wasPressedThisFrame;
                cast |= pad.buttonWest.wasPressedThisFrame;
                use |= pad.buttonNorth.wasPressedThisFrame;
                inventory |= pad.selectButton.wasPressedThisFrame;
                pause |= pad.startButton.wasPressedThisFrame;
                if (pad.leftShoulder.wasPressedThisFrame) slotStep = -1;
                if (pad.rightShoulder.wasPressedThisFrame) slotStep = 1;
                if (stick != 0f || dpad != 0f || pressed)
                {
                    Source = InputSource.Gamepad;
                }
            }

            Move = Mathf.Clamp(move, -1f, 1f);
            Jump = jump;
            JumpHeld = jumpHeld;
            Sprint = sprint;
            Dash = dash;
            Cast = cast;
            UseSelected = use;
            PickSlot = pick;
            SlotStep = slotStep;
            Inventory = inventory;
            Pause = pause;
        }

        public bool Confirm =>
            (Keys != null && (Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame || Keys.spaceKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonSouth.wasPressedThisFrame);

        public bool Back =>
            (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.backspaceKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonEast.wasPressedThisFrame);

        /// <summary>Use the item under the inventory cursor.</summary>
        public bool UseInMenu =>
            (Keys != null && (Keys.eKey.wasPressedThisFrame || Keys.uKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame);

        public bool CloseInventory =>
            (Keys != null && (Keys.iKey.wasPressedThisFrame || Keys.tabKey.wasPressedThisFrame || Keys.escapeKey.wasPressedThisFrame)) ||
            AnyPad(pad => pad.selectButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);

        /// <summary>-1 for a step up a menu, +1 for a step down, 0 otherwise.</summary>
        public int VerticalStep()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.upArrowKey.wasPressedThisFrame || Keys.wKey.wasPressedThisFrame) step = -1;
                if (Keys.downArrowKey.wasPressedThisFrame || Keys.sKey.wasPressedThisFrame) step = 1;
            }
            if (step != 0)
            {
                Source = InputSource.KeyboardMouse;
                return step;
            }
            return PadStep(true, ref stickVertical);
        }

        /// <summary>-1 for a step left, +1 for a step right, 0 otherwise.</summary>
        public int HorizontalStep()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.leftArrowKey.wasPressedThisFrame || Keys.aKey.wasPressedThisFrame) step = -1;
                if (Keys.rightArrowKey.wasPressedThisFrame || Keys.dKey.wasPressedThisFrame) step = 1;
            }
            if (step != 0)
            {
                Source = InputSource.KeyboardMouse;
                return step;
            }
            return PadStep(false, ref stickHorizontal);
        }

        int PadStep(bool vertical, ref int held)
        {
            int step = 0;
            int stickSign = 0;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (vertical)
                {
                    if (pad.dpad.up.wasPressedThisFrame) step = -1;
                    if (pad.dpad.down.wasPressedThisFrame) step = 1;
                }
                else
                {
                    if (pad.dpad.left.wasPressedThisFrame) step = -1;
                    if (pad.dpad.right.wasPressedThisFrame) step = 1;
                }
                Vector2 stick = pad.leftStick.ReadValue();
                float axis = vertical ? -stick.y : stick.x;
                if (Mathf.Abs(axis) > StickStep)
                {
                    stickSign = axis > 0f ? 1 : -1;
                }
            }
            if (step == 0 && stickSign != 0 && stickSign != held)
            {
                step = stickSign;
            }
            held = stickSign;
            if (step != 0)
            {
                Source = InputSource.Gamepad;
            }
            return step;
        }

        static bool AnyPad(System.Func<Gamepad, bool> test)
        {
            foreach (Gamepad pad in Gamepad.all)
            {
                if (test(pad))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
