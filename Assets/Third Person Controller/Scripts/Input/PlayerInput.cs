using UnityEngine;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// Abstract class to expose a common interface for any input implementation.
    /// </summary>
    public abstract class PlayerInput : MonoBehaviour
    {
        // Static variables
        protected static PlayerInput s_PlayerInput;
        public static PlayerInput Instance { get { return s_PlayerInput; } }

        /// <summary>
        /// Determine if the disabled button is down.
        /// </summary>
        /// <param name="primaryInput">Should the primary input be checked?</param>
        /// <returns>True if the disabled button is down.</returns>
        public static bool IsDisabledButtonDown(bool primaryInput) { return s_PlayerInput.IsDisabledButtonDownInternal(primaryInput); }

        /// <summary>
        /// Internal method to determine if the disabled button is down.
        /// </summary>
        /// <param name="primaryInput">Should the primary input be checked?</param>
        /// <returns>True if the disabled button is down.</returns>
        protected virtual bool IsDisabledButtonDownInternal(bool primaryInput) { return false; }

        /// <summary>
        /// Return true if the button is being pressed.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True of the button is being pressed.</returns>
        public static bool GetButton(string name, bool positiveAxis) { return s_PlayerInput.GetButtonInternal(name, positiveAxis); }

        /// <summary>
        /// Internal method to return true if the button is being pressed.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True of the button is being pressed.</returns>
        protected virtual bool GetButtonInternal(string name, bool positiveAxis) { return false; }

        /// <summary>
        /// Return true if the button was pressed this frame. Will use the joystick axis if a joystick is connected.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True if the button is pressed this frame.</returns>
        public static bool GetButtonDown(string name, bool positiveAxis) { return s_PlayerInput.GetButtonDownInternal(name, positiveAxis); }

        /// <summary>
        /// Internal method to return true if the button was pressed this frame. Will use the joystick axis if a joystick is connected.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True if the button is pressed this frame.</returns>
        protected virtual bool GetButtonDownInternal(string name, bool positiveAxis) { return false; }

        /// <summary>
        /// Return true if the button was pressed this frame.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is pressed this frame.</returns>
        public static bool GetButtonDown(string name) { return s_PlayerInput.GetButtonDownInternal(name); }

        /// <summary>
        /// Internal method to return true if the button was pressed this frame.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is pressed this frame.</returns>
        protected virtual bool GetButtonDownInternal(string name) { return false; }

        /// <summary>
        /// Return true if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="useJoystick">Should the input be using the joystick axis if connected?</param>
        /// <returns>True if the button is up.</returns>
        public static bool GetButtonUp(string name, bool useJoystickAxis) { return s_PlayerInput.GetButtonUpInternal(name, useJoystickAxis); }

        /// <summary>
        /// Internal method to return true if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="useJoystick">Should the input be using the joystick axis if connected?</param>
        /// <returns>True if the button is up.</returns>
        protected virtual bool GetButtonUpInternal(string name, bool useJoystickAxis) { return false; }

        /// <summary>
        /// Return true if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is up.</returns>
        public static bool GetButtonUp(string name) { return s_PlayerInput.GetButtonUpInternal(name, false); }

        /// <summary>
        /// Internal method to return true if a double press occurred (double click or double tap).
        /// </summary>
        /// <returns>True if a double press occurred (double click or double tap).</returns>
        public static bool GetDoublePress() { return s_PlayerInput.GetDoublePressInternal(); }

        /// <summary>
        /// Internal method to return true if a double press occurred (double click or double tap).
        /// </summary>
        /// <returns>True if a double press occurred (double click or double tap).</returns>
        protected virtual bool GetDoublePressInternal() { return false; }

        /// <summary>
        /// Return the value of the axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the axis.</returns>
        public static float GetAxis(string name) { return s_PlayerInput.GetAxisInternal(name); }

        /// <summary>
        /// Internal method to return the value of the axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the axis.</returns>
        protected virtual float GetAxisInternal(string name) { return 0; }

        /// <summary>
        /// Return the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        public static Vector2 GetMousePosition() { return s_PlayerInput.GetMousePositionInternal(); }

        /// <summary>
        /// Internal method to return the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        protected virtual Vector2 GetMousePositionInternal() { return Vector2.zero; }
    }
}