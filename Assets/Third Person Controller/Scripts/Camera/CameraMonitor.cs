﻿using UnityEngine;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Monitor classes which must be added to the Camera GameObject. This class allows a custom camera controller class to be used without requiring the Third Person Controller
    /// Camera Controller and Camera Handler components.
    /// </summary>
    public class CameraMonitor : MonoBehaviour
    {
        // An enum of possible camera view modes
        public enum CameraViewMode { ThirdPerson, TopDown, RPG, Pseudo3D }

        // Internal values
        private Ray m_TargetLookRay;
        private static RaycastHit s_RaycastHit;
        private Transform m_Crosshairs;

        // SharedFields
        private SharedProperty<float> m_Recoil = null;
        private SharedProperty<CameraViewMode> m_ViewMode = null;
        private SharedProperty<Vector3> m_CameraOffset = null;
        private Ray SharedProperty_TargetLookRay { get { return m_TargetLookRay; } }

        // Exposed properties
        public GameObject Character { get { return m_Character; } set { InitializeCharacter(value); } }
        private float Recoil { get { return m_Recoil == null ? 0 : m_Recoil.Get(); } }
        private CameraViewMode ViewMode { get { return m_ViewMode == null ? CameraViewMode.ThirdPerson : m_ViewMode.Get(); } }
        private Vector3 CameraOffset { get { return m_CameraOffset == null ? Vector3.zero : m_CameraOffset.Get(); } }
        public Transform Crosshairs { set { m_Crosshairs = value; } }
        private Vector2 CrosshairsLocation
        {
            get
            {
                var location = Vector2.zero;
                if (m_Crosshairs == null) {
                    location = Vector2.one / 2;
                } else {
                    var screenPoint = RectTransformUtility.WorldToScreenPoint(null, m_Crosshairs.position);
                    location.Set(screenPoint.x / m_Camera.pixelWidth, screenPoint.y / m_Camera.pixelHeight);
                }
                return location;
            }
        }

        // Component references
        private Camera m_Camera;
        private GameObject m_Character;
        private Transform m_CharacterTransform;
        private CapsuleCollider m_CharacterCapsuleCollider;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Camera = GetComponent<Camera>();

            SharedManager.Register(this);
        }

        /// <summary>
        /// Indicate which character the camera should monitor.
        /// </summary>
        /// <param name="character">The character to initialize. Can be null.</param>
        private void InitializeCharacter(GameObject character)
        {
            m_Character = character;

            EventHandler.ExecuteEvent<GameObject>("OnCameraAttachCharacter", character);

            // If the CameraController exists it needs to do some extra character initialization.
            CameraController cameraController;
            if ((cameraController = GetComponent<CameraController>()) != null) {
                cameraController.Character = character;
            }

            if (m_Character == null) {
                m_CharacterTransform = null;
                m_CharacterCapsuleCollider = null;
                return;
            }

            m_CharacterTransform = character.transform;
            m_CharacterCapsuleCollider = character.GetComponent<CapsuleCollider>();
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(gameObject, this);
        }

        /// <summary>
        /// Update the target look ray.
        /// </summary>
        private void LateUpdate()
        {
            UpdateTargetLookRay();
        }

        /// <summary>
        /// CharacterIK and Items will need to know the direction that the camera is looking. Determine the TargetRay ahead of time to reduce the number of times it needs to be computed.
        /// </summary>
        private void UpdateTargetLookRay()
        {
            if (ViewMode == CameraViewMode.ThirdPerson || ViewMode == CameraViewMode.RPG) {
                var screenPoint = Vector3.zero;
                screenPoint.x = Screen.width * CrosshairsLocation.x;
                screenPoint.y = Screen.height * CrosshairsLocation.y;
                m_TargetLookRay = m_Camera.ScreenPointToRay(screenPoint);
            } else if (ViewMode == CameraViewMode.TopDown) {
                // The camera is in top down mode so just return a ray with the character's position and roation.
                m_TargetLookRay.direction = m_CharacterTransform.forward;
                m_TargetLookRay.origin = m_CharacterTransform.position;
            } else { // 2.5D.
                m_TargetLookRay.direction = (Vector3)PlayerInput.GetMousePosition() - m_Camera.WorldToScreenPoint(m_CharacterTransform.position + m_CharacterCapsuleCollider.center);
                m_TargetLookRay.origin = m_CharacterTransform.position;
            }
        }

        /// <summary>
        /// Return the position that the camera is looking at. An example of where this is used include when a weapon needs to know at what point to fire. 
        /// </summary>
        /// <param name="applyRecoil">Should the target position take into account any recoil?</param>
        /// <returns>The position that the camera is looking at.</returns>
        public Vector3 SharedMethod_TargetLookPosition(bool applyRecoil)
        {
            return SharedMethod_TargetLookPositionMaxDistance(applyRecoil, -1);
        }

        /// <summary>
        /// Return the position that the camera is looking at with a specified max distance. An example of where this is used include when a weapon needs to know at what point to fire. 
        /// </summary>
        /// <param name="applyRecoil">Should the target position take into account any recoil?</param>
        /// <param name="distance">How far away from the origin should the look position be? -1 to indicate no maximum.</param>
        /// <returns>The position that the camera is looking at.</returns>
        public Vector3 SharedMethod_TargetLookPositionMaxDistance(bool applyRecoil, float distance)
        {
            return TargetLookPosition(m_TargetLookRay, applyRecoil ? Recoil : 0, distance, ViewMode);
        }

        /// <summary>
        /// Returns the direction that the camera is looking. An example of where this is used include when the GUI needs to determine if the crosshairs is looking at any enemies.
        /// </summary>
        /// <param name="applyRecoil">Should the target ray take into account any recoil?</param>
        /// <returns>A ray in the direction that the camera is looking.</returns>
        public Vector3 SharedMethod_TargetLookDirection(bool applyRecoil)
        {
            return TargetLookDirection(m_TargetLookRay, applyRecoil ? Recoil : 0);
        }

        /// <summary>
        /// Return the position that the camera is looking at. An example of where this is used include when a weapon needs to know at what point to fire. 
        /// </summary>
        /// <param name="lookRay">The look ray of the camera.</param>
        /// <param name="recoil">Any recoil that should be added to the direction.</param>
        /// <param name="recoil">How far out the camera should look. A value of -1 indicates no limit.</param>
        /// <param name="viewMode">The type of camera view.</param>
        /// <returns>The position that the camera is looking at.</returns>
        public static Vector3 TargetLookPosition(Ray lookRay, float recoil, float distance, CameraViewMode viewMode)
        {
            // Account for any recoil
            if (recoil != 0) {
                var direction = lookRay.direction;
                direction.y += recoil;
                lookRay.direction = direction;
            }
            // If the distance is equal to -1 then the maximum distance should be retrieved. Fire a raycast to determine if any objects are hit in front of the camera.
            // If no objects are hit then return a point far in the distance.
            if (distance == -1) {
                if ((viewMode == CameraViewMode.ThirdPerson || viewMode == CameraViewMode.RPG) && Physics.Raycast(lookRay, out s_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                    return s_RaycastHit.point;
                } else {
                    return lookRay.GetPoint(10000);
                }
            }
            return lookRay.GetPoint(distance);
        }

        /// <summary>
        /// Returns the direction that the camera is looking. An example of where this is used include when the GUI needs to determine if the crosshairs is looking at any enemies.
        /// </summary>
        /// <param name="lookRay">The look ray of the camera.</param>
        /// <param name="recoil">Any recoil that should be added to the direction.</param>
        /// <returns>A ray in the direction that the camera is looking with the added recoil.</returns>
        public static Vector3 TargetLookDirection(Ray lookRay, float recoil)
        {
            // Account for any recoil
            if (recoil != 0) {
                var direction = lookRay.direction;
                direction.y += recoil;
                lookRay.direction = direction;
            }

            return lookRay.direction;
        }
    }
}