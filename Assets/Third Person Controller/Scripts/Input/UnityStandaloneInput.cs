using System;
using UnityEngine;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// Translates UnityInput calls into KeyCode calls.
    /// </summary>
    public class UnityStandaloneInput : UnityInputBase
    {
        // Internal variables
        private float m_MouseClickTime = -1;

        /// <summary>
        /// Returns if the button is true with the specified ButtonAction.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="action">The type of action to check.</param>
        /// <returns>The status of the action.</returns>
        public override bool GetButton(string name, ButtonAction action)
        {
#if UNITY_EDITOR
            try {
                switch (action) {
                    case ButtonAction.GetButton:
                        return UnityEngine.Input.GetButton(name);
                    case ButtonAction.GetButtonDown:
                        return UnityEngine.Input.GetButtonDown(name);
                    case ButtonAction.GetButtonUp:
                        return UnityEngine.Input.GetButtonUp(name);
                }
            }
            catch (System.Exception /*e*/) {
                Debug.LogError("Button \"" + name + "\" is not setup. Please select \"Update Input Manager\" within the Start Window");
            }
#else
            switch (action) {
                case ButtonAction.GetButton:
                    return UnityEngine.Input.GetButton(name);
                case ButtonAction.GetButtonDown:
                    return UnityEngine.Input.GetButtonDown(name);
                case ButtonAction.GetButtonUp:
                    return UnityEngine.Input.GetButtonUp(name);
            }
#endif
            return false;
        }

        /// <summary>
        /// Returns true if a double click happened.
        /// </summary>
        /// <returns>True if a double click happened.</returns>
        public override bool GetDoublePress()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0)) {
                if (m_MouseClickTime != Time.time && m_MouseClickTime + 0.2f > Time.time) {
                    return true;
                }
                m_MouseClickTime = Time.time;
            }
            return false;
        }

        /// <summary>
        /// Return the value of the axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the axis.</returns>
        public override float GetAxis(string name)
        {
#if UNITY_EDITOR
            try {
                return UnityEngine.Input.GetAxis(name);
            }
            catch (UnityException /*e*/) {
                Debug.LogError("Axis \"" + name + "\" is not setup. Please select \"Update Input Manager\" within the Start Window");
            }
            return 0;
#else
            return UnityEngine.Input.GetAxis(name);
#endif
        }

        /// <summary>
        /// Returns the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        public override Vector2 GetMousePosition()
        {
            return UnityEngine.Input.mousePosition;
        }
    }
}