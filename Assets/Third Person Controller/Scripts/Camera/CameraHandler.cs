using UnityEngine;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the camera. 
    /// </summary>
    public class CameraHandler : MonoBehaviour
    {
        // Internal variables
        private float m_Yaw;
        private float m_Pitch;
        private bool m_Zoom;
        private float m_StepZoom;
        private bool m_RotateBehindCharacter;

        // Exposed properties
        public float Yaw { get { return m_Yaw; } }
        public float Pitch { get { return m_Pitch; } }
        public bool Zoom { get { return m_Zoom; } }
        public float StepZoom { get { return m_StepZoom; } }
        public bool RotateBehindCharacter { get { return m_RotateBehindCharacter; } }

        // Component references
        private CameraController m_CameraController;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_CameraController = GetComponent<CameraController>();
        }

        /// <summary>
        /// Sets the input storage variables to allow the camera to move according to the user input.
        /// </summary>
        private void Update()
        {
            if (m_CameraController.ViewMode != CameraMonitor.CameraViewMode.RPG) {
                m_Pitch = PlayerInput.GetAxis(Constants.PitchInputName);
                m_Yaw = PlayerInput.GetAxis(Constants.YawInputName);
            } else {
                if (PlayerInput.IsDisabledButtonDown(true) || PlayerInput.IsDisabledButtonDown(false)) {
                    m_Pitch = PlayerInput.GetAxis(Constants.PitchInputName);
                    m_Yaw = PlayerInput.GetAxis(Constants.YawInputName);
                    m_RotateBehindCharacter = !PlayerInput.IsDisabledButtonDown(true);
                } else {
                    m_Pitch = 0;
                    m_Yaw = PlayerInput.GetAxis(Constants.SecondaryYawInputName);
                    m_RotateBehindCharacter = false;
                }
            }
            m_Zoom = m_CameraController.AllowZoom && PlayerInput.GetButton(Constants.ZoomInputName, true);
            m_StepZoom = m_CameraController.StepZoomSensitivity > 0 ? PlayerInput.GetAxis(Constants.StepZoomName) : 0;
        }

        /// <summary>
        /// Register for any events that the handler should be aware of.
        /// </summary>
        private void Start()
        {
            EventHandler.RegisterEvent<bool>(GetComponent<CameraController>().Character, "OnAllowGameplayInput", AllowGameplayInput);
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            enabled = allow;

            // Stop the camera from moving if gameplay input is disallowed.
            if (!allow) {
                m_Yaw = 0;
                m_Pitch = 0;
                m_Zoom = false;
            }
        }
    }
}