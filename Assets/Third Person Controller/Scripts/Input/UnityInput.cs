using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.Input
{
    /// <summary>
    /// Acts as a common base class for any type of Unity input. Works with keyboard/mouse, controller, and mobile input.
    /// </summary>
    public class UnityInput : PlayerInput
    {
        [Tooltip("Should the mobile input be used? Useful for debugging with Unity remote")]
        [SerializeField] private bool m_ForceMobileInput;
        [Tooltip("Should the standalone input be used? This will force non-mobile input while on a mobile platform")]
        [SerializeField] private bool m_ForceStandaloneInput;
        [Tooltip("Should the cursor be disabled?")]
        [SerializeField] private bool m_DisableCursor = true;
        [Tooltip("Should the cursor be disabled with the specified button is down?")]
        [SerializeField] private bool m_DisableWhenButtonDown;

        // Internal variables
        private UnityInputBase m_Input;
        private bool m_UseMobileInput;
        private bool m_AllowGameplayInput = true;
        private Dictionary<int, Vector2> m_TouchStartPositions;
        private Vector2 m_LastTouchPosition;
        private Dictionary<string, bool> m_JoystickDownValue;
        private float m_MouseClickTime;

        /// <summary>
        /// Assign the static variables and initialize the default values.
        /// </summary>
        private void OnEnable()
        {
            s_PlayerInput = this;

            m_UseMobileInput = m_ForceMobileInput;
#if !UNITYEDITOR && (UNITY_IPHONE || UNITY_ANDROID || UNITY_WP8 || UNITY_WP8_1 || UNITY_BLACKBERRY)
            if (!m_ForceStandaloneInput) {
                m_UseMobileInput = true;
            }
#endif
            if (m_UseMobileInput) {
                m_Input = new UnityMobileInput();
                m_TouchStartPositions = new Dictionary<int, Vector2>();
                var virtualButtonManager = GameObject.FindObjectOfType<UnityVirtualButtonManager>();
                if (virtualButtonManager == null) {
                    Debug.LogError("Unable to enable mobile input - no Unity Virtual Button Manager found.");
                } else {
                    virtualButtonManager.EnableVirtualButtons(true);
                }
            } else {
                m_Input = new UnityStandaloneInput();
                m_JoystickDownValue = new Dictionary<string, bool>();

                if (m_DisableCursor) {
#if UNITY_4_6 
                    Screen.lockCursor = true;
#else
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
#endif
#if UNITY_EDITOR
                    StartCoroutine(LockCursor());
#endif
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// There is a bug in the Unity editor that prevents the cursor from always being centered when the cursor is locked. It only happens in the editor and can be fixed
        /// by toggling the lock cursor on and off between frames.
        /// </summary>
        /// <returns>Enumerator</returns>
        public IEnumerator LockCursor()
        {
            yield return new WaitForEndOfFrame();
#if UNITY_4_6
            Screen.lockCursor = false;
#else
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
#endif
            yield return new WaitForEndOfFrame();
#if UNITY_4_6
            Screen.lockCursor = true;
#else
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
#endif
        }
#endif
        
        /// <summary>
        /// Register a VirtualButton when the VirtualButton is enabled.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        /// <param name="virtualButton">A reference to the VirtualButton.</param>
        public void RegisterVirtualButton(string name, UnityVirtualButton virtualButton)
        {
            (m_Input as UnityMobileInput).AddVirtualButton(name, virtualButton);
        }

        /// <summary>
        /// Unregister a VirtualButton. This happens when the VirtualButton is disabled.
        /// </summary>
        /// <param name="name">The name of the VirtualButton.</param>
        public void UnregisterVirtualButton(string name)
        {
            (m_Input as UnityMobileInput).RemoveVirtualButton(name);
        }

        /// <summary>
        /// Register for any events that the handler should be aware of.
        /// </summary>
        private void Start()
        {
            EventHandler.RegisterEvent<bool>(gameObject, "OnAllowGameplayInput", AllowGameplayInput);
        }

        /// <summary>
        /// Unlock the cursor.
        /// </summary>
        private void OnDisable()
        {
            if (!m_UseMobileInput && m_DisableCursor) {
#if UNITY_4_6
                Screen.lockCursor = false;
#else
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
#endif
            }
        }

        /// <summary>
        /// Track the touches for use by the swipe for mobile input, otherwise keep the cursor disabled.
        /// </summary>
        private void Update()
        {
            if (m_UseMobileInput) {
                for (int i = 0; i < UnityEngine.Input.touchCount; ++i) {
                    var touch = UnityEngine.Input.touches[i];
                    if (i == 0) {
                        m_LastTouchPosition = touch.position;
                    }
                    if (touch.phase == TouchPhase.Began) {
                        m_TouchStartPositions.Add(touch.fingerId, touch.position);
                    } else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                        m_TouchStartPositions.Remove(touch.fingerId);
                    }
                }
#if UNITY_EDITOR
                // Use the mouse position within the editor if there are no touches.
                if (UnityEngine.Input.touchCount == 0) {
                    m_LastTouchPosition = UnityEngine.Input.mousePosition;
                }
#endif
            } else {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) {
#if UNITY_4_6
                    Screen.lockCursor = false;
#else
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
#endif
                } else if (m_DisableCursor && m_AllowGameplayInput) {
#if !UNITY_EDITOR
#if UNITY_4_6
                    Screen.lockCursor = true;
#else
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
#endif
#endif
                } else if (!m_DisableCursor && m_DisableWhenButtonDown) {
                    var visible = !(GetButton(Constants.PrimaryDisableButtonName, true) || GetButton(Constants.SecondaryDisableButtonName, true));
#if UNITY_4_6
                    Screen.lockCursor = !visible;
#else
                    Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = visible;
#endif
                }
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBPLAYER || UNITY_WEBGL || UNITY_WINRT
        /// <summary>
        /// Lock the cursor when the mouse is pressed down.
        /// </summary>
        private void OnMouseDown()
        {
            if (m_DisableCursor && m_AllowGameplayInput) {
#if UNITY_4_6
                Screen.lockCursor = false;
#else
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
#endif
            }
        }
#endif
        /// <summary>
        /// Internal method to determine if the disabled button is down.
        /// </summary>
        /// <param name="primaryInput">Should the primary input be checked?</param>
        /// <returns>True if the disabled button is down.</returns>
        protected override bool IsDisabledButtonDownInternal(bool primaryInput)
        {
            return GetButton(primaryInput ? Constants.PrimaryDisableButtonName : Constants.SecondaryDisableButtonName, true);
        }

        /// <summary>
        /// Returns the start position of the touch with the specified ID.
        /// </summary>
        /// <param name="touchID">The ID of the interested touch.</param>
        /// <returns>The start position of the touch.</returns>
        public Vector2 GetTouchStartPosition(int touchID)
        {
            Vector2 position;
            if (m_TouchStartPositions.TryGetValue(touchID, out position)) {
                return position;
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Returns the position of the last touch position.
        /// </summary>
        /// <returns>The last touch position.</returns>
        public Vector2 GetLastTouchPosition()
        {
            return m_LastTouchPosition;
        }

        /// <summary>
        /// Internal method to return true if the button is being pressed.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True of the button is being pressed.</returns>
        protected override bool GetButtonInternal(string name, bool positiveAxis)
        {
            if (!m_AllowGameplayInput) {
                return false;
            }

            if (m_Input.GetButton(name, UnityInputBase.ButtonAction.GetButton)) {
                return true;
            }
            if (m_Input.GetAxis(name) == (positiveAxis ? 1 : -1)) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Internal method to return true if the button was pressed this frame. Will use the joystick axis if a joystick is connected.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="positiveAxis">If using a joystick, is the joystick axis positive?</param>
        /// <returns>True if the button is pressed this frame.</returns>
        protected override bool GetButtonDownInternal(string name, bool positiveAxis)
        {
            if (!m_AllowGameplayInput) {
                return false;
            }

            if (m_Input.GetButton(name, UnityInputBase.ButtonAction.GetButtonDown)) {
                return true;
            }
            if (m_Input.GetAxis(name) == (positiveAxis ? 1 : -1)) {
                // Keep track of the previous down value so the up call will only be true immediately after the axis has been down.
                if (m_JoystickDownValue.ContainsKey(name)) {
                    m_JoystickDownValue[name] = true;
                } else {
                    m_JoystickDownValue.Add(name, true);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Internal method to return true if the button was pressed this frame.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <returns>True if the button is pressed this frame.</returns>
        protected override bool GetButtonDownInternal(string name)
        {
            return m_Input.GetButton(name, UnityInputBase.ButtonAction.GetButtonDown);
        }

        /// <summary>
        /// Internal method to return true if the button is up.
        /// </summary>
        /// <param name="name">The name of the button.</param>
        /// <param name="useJoystick">Should the input be using the joystick if connected?</param>
        /// <returns>True if the button is up.</returns>
        protected override bool GetButtonUpInternal(string name, bool useJoystickAxis)
        {
            if (m_Input.GetButton(name, UnityInputBase.ButtonAction.GetButtonUp)) {
                return true;
            }
            if (!m_UseMobileInput && useJoystickAxis) {
                var value = Mathf.Abs(m_Input.GetAxis(name));
                bool prevValue;
                m_JoystickDownValue.TryGetValue(name, out prevValue);
                if (prevValue && value <= 0.1f) {
                    m_JoystickDownValue[name] = false;
                    return true;
                }
                return false;
            }
            return false;
        }

        /// <summary>
        /// Internal method to return true if a double press occurred (double click or double tap).
        /// </summary>
        /// <returns>True if a double press occurred (double click or double tap).</returns>
        protected override bool GetDoublePressInternal()
        {
            return m_Input.GetDoublePress();
        }

        /// <summary>
        /// Internal method to return the value of the axis with the specified name.
        /// </summary>
        /// <param name="name">The name of the axis.</param>
        /// <returns>The value of the axis.</returns>
        protected override float GetAxisInternal(string name)
        {
            return m_Input.GetAxis(name);
        }

        /// <summary>
        /// Internal method to return the position of the mouse.
        /// </summary>
        /// <returns>The mouse position.</returns>
        protected override Vector2 GetMousePositionInternal()
        {
            if (GetAxisInternal(Constants.ControllerHorizontalRightThumbstick) != 0 || GetAxisInternal(Constants.ControllerVerticalRightThumbstick) != 0) {
                var position = Vector2.zero;
                position.Set(GetAxisInternal(Constants.ControllerHorizontalRightThumbstick), GetAxisInternal(Constants.ControllerVerticalRightThumbstick));
                return position;
            }
            return m_Input.GetMousePosition();
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            m_AllowGameplayInput = allow;
            if (m_DisableCursor) {
#if UNITY_4_6
                Screen.lockCursor = allow;
#else
                Cursor.lockState = (allow ? CursorLockMode.Locked : CursorLockMode.None);
                Cursor.visible = !allow;
#endif
            }
        }
    }
}