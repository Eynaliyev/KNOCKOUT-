using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The controller for a third person or top down camera. The camera will smoothly follow the character as the character moves. While in third person mode, 
    /// the camera will rotate around the specified character and reduce the amount of clipping and can also zoom when the character is zooming. Top down mode will
    /// follow the character with a birds eye view.
    /// </summary>
    [RequireComponent(typeof(CameraHandler))]
    [RequireComponent(typeof(CameraMonitor))]
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Tooltip("The view mode to use")]
        [SerializeField] private CameraMonitor.CameraViewMode m_ViewMode;

        [Tooltip("Should the character be initialized on start?")]
        [SerializeField] private bool m_InitCharacterOnStart = true;
        [Tooltip("The character that the camera is following")]
        [SerializeField] private GameObject m_Character;
        [Tooltip("The transform of the object to look at")]
        [SerializeField] private Transform m_Anchor;
        [Tooltip("Should the anchor be assigned automatically based on the bone?")]
        [SerializeField] private bool m_AutoAnchor;
        [Tooltip("The bone in which the anchor will be assigned to if automatically assigned")]
        [SerializeField] private HumanBodyBones m_AutoAnchorBone = HumanBodyBones.Head;
        
        [Tooltip("The minimum pitch angle (in degrees)")]
        [SerializeField] private float m_MinPitchLimit = -85;
        [Tooltip("The maximum pitch angle (in degrees)")]
        [SerializeField] private float m_MaxPitchLimit = 85;
        [Tooltip("The minimum yaw angle while in cover (in degrees)")]
        [SerializeField] private float m_MinYawLimit = -1;
        [Tooltip("The maximum yaw angle while in cover (in degrees)")]
        [SerializeField] private float m_MaxYawLimit = -1;
        
        [Tooltip("The amount of smoothing to apply to the movement. Can be zero")]
        [SerializeField] private float m_MoveSmoothing = 0.1f;
        [Tooltip("The amount of smoothing to apply to the pitch and yaw. Can be zero")]
        [SerializeField] private float m_TurnSmoothing = 0.05f;
        [Tooltip("The speed at which the camera turns")]
        [SerializeField] private float m_TurnSpeed = 1.5f;
        [Tooltip("Can the camera turn while the character is in the air?")]
        [SerializeField] private bool m_CanTurnInAir = true;
        [Tooltip("The offset between the anchor and the location of the camera")]
        [SerializeField] private Vector3 m_CameraOffset = new Vector3(0.3f, 0.5f, -2f);
        [Tooltip("The normal field of view")]
        [SerializeField] private float m_NormalFOV = 60;

        [Tooltip("The sensitivity of the step zoom")]
        [SerializeField] private float m_StepZoomSensitivity;
        [Tooltip("The minimum amount that the camera can step zoom")]
        [SerializeField] private float m_MinStepZoom;
        [Tooltip("The maximum amount that the camera can step zoom")]
        [SerializeField] private float m_MaxStepZoom;

        [Tooltip("The rotation speed when not using the third person view")]
        [SerializeField] private float m_RotationSpeed = 1.5f;
        [Tooltip("The distance to position the camera away from the anchor when not in third person view")]
        [SerializeField] private float m_ViewDistance = 10;
        [Tooltip("The number of degrees to adjust if the anchor is obstructed by an object when not in third person view")]
        [SerializeField] private float m_ViewStep = 5;
        [Tooltip("The 2.5D target look direction")]
        [SerializeField] private Vector3 m_LookDirection = Vector3.forward;

        [Tooltip("Can the camera zoom?")]
        [SerializeField] private bool m_AllowZoom;
        [Tooltip("The amount of smoothing to apply to the pitch and yaw when zoomed. Can be zero")]
        [SerializeField] private float m_ZoomTurnSmoothing = 0.01f;
        [Tooltip("The offset between the anchor and the location of the zoomed in camera")]
        [SerializeField] private Vector3 m_ZoomCameraOffset = new Vector3(0.4f, 0f, -3.66f);
        [Tooltip("The zoomed field of view")]
        [SerializeField] private float m_ZoomFOV = 20;
        [Tooltip("The speed at which the FOV transitions between normal and zoom")]
        [SerializeField] private float m_FOVSpeed = 5;

        [Tooltip("The amount of smoothing to apply to the pitch and yaw when in scope. Can be zero")]
        [SerializeField] private float m_ScopeTurnSmoothing = 0.01f;
        [Tooltip("The offset between the anchor and the location of the scoped camera")]
        [SerializeField] private Vector3 m_ScopeCameraOffset = new Vector3(0.4f, 0f, -3.66f);
        [Tooltip("The scoped field of view")]
        [SerializeField] private float m_ScopeFOV = 10;

        [Tooltip("Disable the character's renderer when the camera gets too close to the character. This will prevent the camera from clipping with the character")]
        [SerializeField] private float m_DisableRendererDistance = 0.2f;
        [Tooltip("The radius of the camera's collision sphere to prevent it from clipping with other objects")]
        [SerializeField] private float m_CollisionRadius = 0.01f;

        [Tooltip("The speed at which the recoil increases when the weapon is initially fired")]
        [SerializeField] private float m_RecoilSpring = 0.01f;
        [Tooltip("The speed at which the recoil decreases after the recoil has hit its peak and is settling back to its original value")]
        [SerializeField] private float m_RecoilDampening = 0.05f;
        
        [Tooltip("Optionally specify an anchor point to look at when the character dies. If no anchor is specified the character's position will be used")]
        [SerializeField] private Transform m_DeathAnchor;
        [Tooltip("When the character dies should the camera start rotating around the character? If false the camera will just look at the player")]
        [SerializeField] private bool m_UseDeathOrbit = true;
        [Tooltip("The speed at which the camera rotates when the character dies. Used by both the death orbit and regular look at")]
        [SerializeField] private float m_DeathRotationSpeed = 5;
        [Tooltip("The speed at which the death orbit moves")]
        [SerializeField] private float m_DeathOrbitMoveSpeed = 5;
        [Tooltip("How far away the camera should be orbiting the character when the character dies")]
        [SerializeField] private float m_DeathOrbitDistance = 5;

        // Internal variables
        private float m_Pitch;
        private float m_Yaw;
        private float m_StartPitch;
        private bool m_IsScoped;
        private bool m_IsZoomed;
        private bool m_LimitYaw;
        private float m_StepZoom;

        private float m_SmoothX;
        private float m_SmoothY;
        private float m_SmoothXVelocity;
        private float m_SmoothYVelocity;
        private float m_SmoothPitchVelocity;
        private float m_SmoothYawVelocity;

        private Vector3 m_SmoothPositionVelocity;
        private float m_DisableRendererDistanceSqr;
        private bool m_ApplyColliderOffset;
        private Vector3 m_AnchorStartOffset;
        private float m_VerticalOffset;
        private float m_StaticYDifference = -1;
        private bool m_ObstructionCheck = true;
        private bool m_PreventZoom;
        private RaycastHit m_RaycastHit;

        private bool m_CharacterHasDied;
        private Vector3 m_PrevTargetPosition;
        private bool m_PrevRenderersEnabled;
        private bool m_RestrictRotation;

        private float m_Recoil = 0;
        private float m_TargetRecoil = 0;

        // SharedFields
        private float SharedProperty_Recoil { get { return m_Recoil; } set { m_TargetRecoil = value; } }
        private CameraMonitor.CameraViewMode SharedProperty_ViewMode { get { return ViewMode; } }
        private Vector3 SharedProperty_CameraOffset { get { return m_IsZoomed ? m_ZoomCameraOffset : m_CameraOffset; } }

        // Exposed properties
        public GameObject Character { get { return m_Character; } set { InitializeCharacter(value); } }
        public Transform Anchor { set { m_Anchor = value; InitializeAnchor(); ImmediatePosition(); } }
        public Vector3 CameraOffset { set { m_CameraOffset = value; } }
        public float ViewDistance { set { m_ViewDistance = value; } }
        public CameraMonitor.CameraViewMode ViewMode { get { return m_ViewMode; } set { m_ViewMode = value; } }
        public float MinPitchLimit { set { m_MinPitchLimit = value; } }
        public bool AllowZoom { get { return m_AllowZoom; } }
        public float StepZoomSensitivity { get { return m_StepZoomSensitivity; } }

        // Component references
        private static Camera m_Camera;
        private CameraHandler m_CameraHandler;
        private Transform m_Transform;
        private Transform m_CharacterTransform;
        private CapsuleCollider m_CharacterCapsuleCollider;
        private RigidbodyCharacterController m_CharacterController;
        private List<Renderer> m_Renderers = new List<Renderer>();
        private Renderer m_BlankRenderer;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_Camera = GetComponent<Camera>();
            m_CameraHandler = GetComponent<CameraHandler>();

#if UNITY_EDITOR
            if (GetComponent<CameraMonitor>() == null) {
                Debug.LogWarning("Warning: the CameraMonitor component is required on cameras with the CameraController. Please add this component.");
                gameObject.AddComponent<CameraMonitor>();
            }
#endif

            SharedManager.Register(this);

            m_StartPitch = m_Pitch = m_Transform.eulerAngles.x;
            m_DisableRendererDistanceSqr = m_DisableRendererDistance * m_DisableRendererDistance;
        }

        /// <summary>
        /// Register for any events that the camera should be aware of.
        /// </summary>
        private void OnEnable()
        {
            if (m_Character == null) {
                return;
            }

            EventHandler.RegisterEvent<bool>(m_Character, "OnItemShowScope", OnStartScopeFocus);
            EventHandler.RegisterEvent<bool>(m_Character, "OnControllerGrounded", OnCharacterGrounded);
            EventHandler.RegisterEvent(m_Character, "OnControllerLeaveCover", OnCharacterLeaveCover);
            EventHandler.RegisterEvent<bool>(m_Character, "OnAnimatorPopFromCover", OnCharacterPopFromCover);
            EventHandler.RegisterEvent<Item>(m_Character, "OnInventoryDualWieldItemChange", OnDualWieldItemChange);
            EventHandler.RegisterEvent<bool>(m_Character, "OnCameraStaticHeight", OnStaticHeight);
            EventHandler.RegisterEvent<float>(m_Character, "OnCameraHeightOffset", OnHeightOffset);
            EventHandler.RegisterEvent<bool>(m_Character, "OnCameraCheckObjectObstruction", OnCheckObjectObstruction);
            EventHandler.RegisterEvent(m_Character, "OnDeath", OnCharacterDeath);
            EventHandler.RegisterEvent(m_Character, "OnRespawn", OnCharacterSpawn);
        }

        /// <summary>
        /// Unregister for any events that the camera was aware of.
        /// </summary>
        private void OnDisable()
        {
            UnregisterEvents();
        }

        /// <summary>
        /// Unregister for any events that the camera was aware of.
        /// </summary>
        private void UnregisterEvents()
        {
            if (m_Character == null) {
                return;
            }

            EventHandler.UnregisterEvent<bool>(m_Character, "OnItemShowScope", OnStartScopeFocus);
            EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerGrounded", OnCharacterGrounded);
            EventHandler.UnregisterEvent(m_Character, "OnControllerLeaveCover", OnCharacterLeaveCover);
            EventHandler.UnregisterEvent<bool>(m_Character, "OnAnimatorPopFromCover", OnCharacterPopFromCover);
            EventHandler.UnregisterEvent<bool>(m_Character, "OnCameraStaticHeight", OnStaticHeight);
            EventHandler.UnregisterEvent<float>(m_Character, "OnCameraHeightOffset", OnHeightOffset);
            EventHandler.UnregisterEvent<bool>(m_Character, "OnCameraCheckObjectObstruction", OnCheckObjectObstruction);
            EventHandler.UnregisterEvent(m_Character, "OnDeath", OnCharacterDeath);
            EventHandler.UnregisterEvent(m_Character, "OnRespawn", OnCharacterSpawn);
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            // If the character is not initialized on start then disable the controller - the controller won't function without a character.
            if (m_InitCharacterOnStart) {
                if (m_Character == null) {
                    Debug.LogWarning("Warning: No character has been assigned to the Camera Controller. It will automatically be assigned to the GameObject with the Player tag.");
                    m_Character = GameObject.FindGameObjectWithTag("Player");
                    if (m_Character == null) {
                        Debug.LogWarning("Error: Unable to find character with the Player tag. Disabling the Camera Controller.");
                        m_CameraHandler.enabled = enabled = false;
                        return;
                    }
                }
                InitializeCharacter(m_Character);
            } else {
                m_CameraHandler.enabled = enabled = m_Character != null;
            }
        }

        /// <summary>
        /// Initialize the camera to follow the character.
        /// </summary>
        /// <param name="character">The character to initialize. Can be null.</param>
        private void InitializeCharacter(GameObject character)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                return;
            }
#endif

            // Reset the variables back to their values before the character spawned.
            if (character == null) {
                UnregisterEvents();
                m_CameraHandler.enabled = enabled = false;
                m_Character = character;
                return;
            }

            m_Character = character;

            m_CharacterTransform = m_Character.transform;
            m_CharacterCapsuleCollider = m_Character.GetComponent<CapsuleCollider>();
            m_CharacterController = m_Character.GetComponent<RigidbodyCharacterController>();

            var renderers = m_Character.GetComponentsInChildren<Renderer>(true);
            // The transform positions start to go crazy when there isn't at least one renderer enabled. An empty renderer has been placed on the head GameObject to prevent this
            // from happening.
            m_Renderers.Clear();
            for (int i = 0; i < renderers.Length; ++i) {
                if (renderers[i].sharedMaterials.Length > 0) {
                    m_Renderers.Add(renderers[i]);
                } else {
                    m_BlankRenderer = renderers[i];
                }
            }

            m_Yaw = m_CharacterTransform.eulerAngles.y;
            m_Transform.rotation = m_Transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0);

            InitializeAnchor();

            // All of the variables have initialized so position the camera now.
            ImmediatePosition();

            m_CameraHandler.enabled = enabled = true;
        }

        /// <summary>
        /// Initialize the anchor transform and related variables.
        /// </summary>
        private void InitializeAnchor()
        {
            // Assign the anchor to the bone transform if auto anchor is enabled. Otherwise use the character's transform.
            if (m_AutoAnchor) {
                m_Anchor = m_Character.GetComponent<Animator>().GetBoneTransform(m_AutoAnchorBone);
            } else {
                m_Anchor = m_CharacterTransform;
            }

            m_ApplyColliderOffset = m_Anchor == m_CharacterTransform;
            m_AnchorStartOffset = m_Anchor.position - m_CharacterTransform.position;
        }

        /// <summary>
        /// Update the camera's position and rotation while the character is alive. Use FixedUpdate because the character operates on a fixed timestep.
        /// </summary>
        private void FixedUpdate()
        {
            if (m_CharacterHasDied) {
                return;
            }

            // The camera can only directly be controlled in third person view.
            if (m_ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ViewMode == CameraMonitor.CameraViewMode.RPG) {
                UpdateInput();
            }

            m_IsZoomed = !m_PreventZoom && m_AllowZoom && m_CameraHandler.Zoom;

            if (m_ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ViewMode == CameraMonitor.CameraViewMode.RPG) {
                Rotate();
            }
            Move(Time.fixedDeltaTime);
            // No need to update the recoil or check for clipping if not in third person view.
            if (m_ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ViewMode == CameraMonitor.CameraViewMode.RPG) {
                UpdateRecoil();
                CheckForCharacterClipping();
            } else if (m_ViewMode == CameraMonitor.CameraViewMode.TopDown) {
                LookAtCharacter(m_Anchor, m_RotationSpeed);
            }
        }

        /// <summary>
        /// Update the camera's position and rotation within LateUpdate if the character has died.
        /// </summary>
        public void LateUpdate()
        {
            if (m_CharacterHasDied) {
                if (m_UseDeathOrbit) {
                    DeathOrbitMovement();
                } else {
                    LookAtCharacter((m_DeathAnchor != null ? m_DeathAnchor : m_CharacterTransform), m_DeathRotationSpeed);
                }
            }
        }

        /// <summary>
        /// Update the pitch and yaw according to the user input.
        /// </summary>
        private void UpdateInput()
        {
            var x = m_CameraHandler.Yaw;
            var y = m_CameraHandler.Pitch;
            // Only use smoothing if turn smoothing is greater than zero. Otherwise directly set the pitch and yaw.
            float turnSmoothing;
            if (m_IsScoped) {
                turnSmoothing = m_ScopeTurnSmoothing;
            } else if (m_IsZoomed) {
                turnSmoothing = m_ZoomTurnSmoothing;
            } else {
                turnSmoothing = m_TurnSmoothing;
            }
            if (turnSmoothing > 0) {
                m_SmoothX = Mathf.SmoothDamp(m_SmoothX, x, ref m_SmoothXVelocity, turnSmoothing);
                m_SmoothY = Mathf.SmoothDamp(m_SmoothY, y, ref m_SmoothYVelocity, turnSmoothing);
            } else {
                m_SmoothX = x;
                m_SmoothY = y;
            }

            // Allow the camera to zoom by stepping.
            if (m_StepZoomSensitivity > 0) {
                m_StepZoom = Mathf.Clamp(m_StepZoom + m_CameraHandler.StepZoom * m_StepZoomSensitivity * Time.deltaTime, m_MinStepZoom, m_MaxStepZoom);
            }
        }

        /// <summary>
        /// Use the smoothed X and Y to adjust the yaw and pitch.
        /// </summary>
        private void Rotate()
        {
            if (m_RestrictRotation) {
                return;
            }

            // The rotation can only happen so fast.
            m_Yaw += m_SmoothX * m_TurnSpeed;
            m_Pitch += m_SmoothY * m_TurnSpeed * -1;
            m_Pitch = Utility.ClampAngle(m_Pitch, m_MinPitchLimit, m_MaxPitchLimit);

            float turnSmoothing;
            if (m_IsScoped) {
                turnSmoothing = m_ScopeTurnSmoothing;
            } else if (m_IsZoomed) {
                turnSmoothing = m_ZoomTurnSmoothing;
            } else {
                turnSmoothing = m_TurnSmoothing;
            }
            if (m_LimitYaw) {
                // The yaw limit is relative to the character.
                var lowerAngle = Utility.RestrictInnerAngle(m_CharacterTransform.eulerAngles.y + m_MinYawLimit);
                var upperAngle = Utility.RestrictInnerAngle(m_CharacterTransform.eulerAngles.y + m_MaxYawLimit);
                if (upperAngle < lowerAngle) {
                    upperAngle += 360;
                }
                m_Yaw = Mathf.SmoothDamp(m_Yaw, Mathf.Clamp(m_Yaw, lowerAngle, upperAngle), ref m_SmoothPitchVelocity, turnSmoothing);
            }

            // In most cases the character follows the camera. However, with the RPG view mode there are times when the camera should instead follow the character.
            if (m_ViewMode == CameraMonitor.CameraViewMode.RPG && m_CameraHandler.RotateBehindCharacter) {
                if (m_CharacterController.InputVector.sqrMagnitude > 0.01f) {
                    m_Yaw = Mathf.SmoothDamp(m_Yaw, m_Yaw + Mathf.DeltaAngle(m_Yaw, m_CharacterTransform.eulerAngles.y), ref m_SmoothYawVelocity, turnSmoothing);
                }
            }

            m_Transform.rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0);
        }

        /// <summary>
        /// Move between the current position and a new position specified by the new pitch, yaw, and zoom.
        /// </summary>
        /// <param name="deltaTime">The time since the last frame.</param>
        private void Move(float deltaTime)
        {
            // The field of view and look point differs if the camera is zoomed or not. The camera is closer to the anchor when it is zoomed and has a smaller field of view.
            var lookPoint = m_Anchor.position + (m_ApplyColliderOffset ? m_CharacterCapsuleCollider.center : Vector3.zero) + Vector3.up * m_VerticalOffset;

            if (m_IsScoped) {
                m_Camera.fieldOfView = m_ScopeFOV;
                lookPoint += (m_ScopeCameraOffset.x * m_Transform.right) + (m_ScopeCameraOffset.y * m_CharacterTransform.up) + (m_ScopeCameraOffset.z * m_Transform.forward);
            } else if (m_IsZoomed) {
                m_Camera.fieldOfView = Mathf.Lerp(m_Camera.fieldOfView, m_ZoomFOV, m_FOVSpeed * deltaTime);
                lookPoint += (m_ZoomCameraOffset.x * m_Transform.right) + (m_ZoomCameraOffset.y * m_CharacterTransform.up) + ((m_ZoomCameraOffset.z + m_StepZoom) * m_Transform.forward);
            } else {
                m_Camera.fieldOfView = Mathf.Lerp(m_Camera.fieldOfView, m_NormalFOV, m_FOVSpeed * deltaTime);
                lookPoint += (m_CameraOffset.x * m_Transform.right) + (m_CameraOffset.y * m_CharacterTransform.up) + ((m_CameraOffset.z + m_StepZoom) * m_Transform.forward);
            }

            // Prevent obstruction from other objects. Check for obstruction against the player position rather than the look position because the character should always be visible. It doesn't
            // matter as much if the look position isn't directly visible.
            var targetPosition = lookPoint;
            if (m_ObstructionCheck) {
                if (m_ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ViewMode == CameraMonitor.CameraViewMode.RPG) {
                    var anchorPosition = m_Anchor.position + (m_ApplyColliderOffset ? m_CharacterCapsuleCollider.center : Vector3.zero);
                    var direction = targetPosition - anchorPosition;
                    var start = anchorPosition - direction.normalized * m_CollisionRadius;
                    // Fire a sphere to prevent the camera from colliding with other objects.
                    if (Physics.SphereCast(start, m_CollisionRadius, direction.normalized, out m_RaycastHit, direction.magnitude, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                        // Move the camera in if the character isn't in view.
                        targetPosition = m_RaycastHit.point + m_RaycastHit.normal * 0.1f;

                        // Keep a constant height if there is nothing getting in the way of that position.
                        if (direction.y > 0) {
                            var constantHeightPosition = targetPosition;
                            constantHeightPosition.y = lookPoint.y;
                            direction = constantHeightPosition - anchorPosition;
                            start = anchorPosition - direction.normalized * m_CollisionRadius;
                            if (!Physics.SphereCast(start, m_CollisionRadius, direction.normalized, out m_RaycastHit, direction.magnitude, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                                targetPosition = constantHeightPosition;
                            }
                        }
                    }

                    // Prevent the camera from clipping with the character.
                    if (m_CharacterCapsuleCollider.bounds.Contains(targetPosition)) {
                        targetPosition = m_CharacterCapsuleCollider.ClosestPointOnBounds(targetPosition);
                    }
                } else if (m_ViewMode == CameraMonitor.CameraViewMode.TopDown) {
                    var direction = Quaternion.Euler(m_MinPitchLimit, 0, 0) * -Vector3.forward;
                    var step = 0f;
                    while (Physics.SphereCast(lookPoint, m_CollisionRadius, direction.normalized, out m_RaycastHit, m_ViewDistance, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                        if (m_MinPitchLimit + step >= m_MaxPitchLimit) {
                            direction = Quaternion.Euler(m_MaxPitchLimit, 0, 0) * -Vector3.forward;
                            break;
                        }
                        step += m_ViewStep;
                        direction = Quaternion.Euler(m_MinPitchLimit + step, 0, 0) * -Vector3.forward;
                    }
                    targetPosition = lookPoint + direction * m_ViewDistance;
                } else { // 2.5D.
                    targetPosition = lookPoint - m_LookDirection * m_ViewDistance;
                }
            }

            // Keep the y position the same when requested.
            if (m_StaticYDifference != -1) {
                targetPosition.y = m_CharacterTransform.position.y + m_StaticYDifference;
            }

            // Set the new position.
            m_Transform.position = Vector3.SmoothDamp(m_Transform.position, targetPosition, ref m_SmoothPositionVelocity, (m_IsZoomed ? 0 : m_MoveSmoothing));
        }

        /// <summary>
        /// A weapon has been fired. Update the recoil.
        /// </summary>
        private void UpdateRecoil()
        {
            // Use the recoil spring amount when the weapon is initially fired and the recoil is increasing in magnitude. Recoil dampening is then used
            // after the recoil has hit its peak and is settling back down to its original value.
            if (Mathf.Abs(m_TargetRecoil - m_Recoil) > 0.001f) {
                var currentVelocity = 0f;
                m_Recoil = Mathf.SmoothDamp(m_Recoil, m_TargetRecoil, ref currentVelocity, m_RecoilSpring);
                EventHandler.ExecuteEvent<float>(m_Character, "OnCameraUpdateRecoil", m_Recoil);
            } else if (m_Recoil != 0) {
                var currentVelocity = 0f;
                m_TargetRecoil = m_Recoil = Mathf.SmoothDamp(m_Recoil, 0, ref currentVelocity, m_RecoilDampening);
                EventHandler.ExecuteEvent<float>(m_Character, "OnCameraUpdateRecoil", m_Recoil);
                if (m_Recoil < 0.001f) {
                    m_Recoil = 0;
                }
            }
        }

        /// <summary>
        /// If the camera gets too close to the character then disable the character's renderer to prevent the camera from seeing inside the character.
        /// </summary>
        private void CheckForCharacterClipping()
        {
            // Disable the renderer of the character and children if the camera gets too close to the player to prevent clipping.
            var enabled = m_Anchor.InverseTransformPoint(m_Transform.position).sqrMagnitude > m_DisableRendererDistanceSqr;
            if (enabled != m_PrevRenderersEnabled) {
                for (int i = 0; i < m_Renderers.Count; ++i) {
                    m_Renderers[i].enabled = enabled;
                }
                // The the blank renderer should be enabled when the rest of the renderers are disabled to allow the Transforms to update.
                m_BlankRenderer.enabled = !enabled;
                m_PrevRenderersEnabled = enabled;
            }
        }

        /// <summary>
        /// When the character dies the camera should orbit around the character.
        /// </summary>
        private void DeathOrbitMovement()
        {
            // If no death anchor point is specified then use the regular anchor.
            var anchor = (m_DeathAnchor != null ? m_DeathAnchor : m_Anchor);
            var rotation = Quaternion.identity;

            // Start rotating once the anchor position has stopped moving. This prevents the camera from jittering when both the camera and anchor are changing positions.
            if ((m_PrevTargetPosition - anchor.position).sqrMagnitude < .01) {
                // Keep rotating around the target transform until OnCharacterSpawn is called.
                rotation = Quaternion.AngleAxis(m_DeathRotationSpeed * Time.fixedDeltaTime, Vector3.up);
            }

            var direction = (m_Transform.position - anchor.position).normalized;
            var distance = m_DeathOrbitDistance;
            // Prevent clipping with other objects.
            if (Physics.SphereCast(anchor.position, m_CollisionRadius, direction.normalized, out m_RaycastHit, distance, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                distance = m_RaycastHit.distance;
            }
            
            // Set the rotation and position.
            var targetPosition = anchor.position + (rotation * direction * distance);
            m_Transform.position = Vector3.MoveTowards(m_Transform.position, targetPosition, m_DeathOrbitMoveSpeed);
            m_Transform.rotation = Quaternion.LookRotation(-direction);
            m_PrevTargetPosition = anchor.position;
        }

        /// <summary>
        /// When the character dies the camera should look at the character instead of orbiting around the character.
        /// </summary>
        /// <param name="anchor">The point to look at.</param>
        /// <param name="roationSpeed">The speed at which the rotation occurs.</param>
        private void LookAtCharacter(Transform anchor, float roationSpeed)
        {
            var rotation = Quaternion.LookRotation(anchor.position - m_Transform.position);
            m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, rotation, roationSpeed * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Should the camera be in the scope state?
        /// </summary>
        /// <param name="scope">True if the camera should be in the scope state.</param>
        private void OnStartScopeFocus(bool scope)
        {
            m_IsScoped = scope;
            // A scope UI will be shown on top of the game screen so instantly switch.
            if (m_IsScoped) {
                m_Camera.fieldOfView = m_ScopeFOV;
            } else {
                m_Camera.fieldOfView = m_NormalFOV;
                ImmediatePosition();
            }
        }

        /// <summary>
        /// The character has either just landed or is in the air. If the character is in the air and CanTurnInAir is false then the rotation should be limited.
        /// </summary>
        /// <param name="grounded"></param>
        private void OnCharacterGrounded(bool grounded)
        {
            if (!m_CanTurnInAir) {
                m_RestrictRotation = !grounded;
            }
        }

        /// <summary>
        /// The character hs left cover. The yaw no longer needs to be limited.
        /// </summary>
        private void OnCharacterLeaveCover()
        {
            m_LimitYaw = false;
        }

        /// <summary>
        /// The character has popped from cover or returned from a pop. Limit the yaw angle of the character has popped from cover.
        /// </summary>
        /// <param name="popped">True if the character has popped from cover.</param>
        private void OnCharacterPopFromCover(bool popped)
        {
            m_LimitYaw = popped;
            // The character rotation will be restricted to the inner angle so the initial yaw angle should be as well.
            if (m_LimitYaw) {
                var lowerAngle = Utility.RestrictInnerAngle(m_CharacterTransform.eulerAngles.y + m_MinYawLimit);
                var upperAngle = Utility.RestrictInnerAngle(m_CharacterTransform.eulerAngles.y + m_MaxYawLimit);
                if (upperAngle < lowerAngle) {
                    upperAngle += 360;
                }
                if (m_Yaw < lowerAngle || m_Yaw > upperAngle) {
                    m_Yaw = Utility.RestrictInnerAngle(m_Yaw);
                }
            }
        }

        /// <summary>
        /// The inventory has added or removed a dual wielded item. Prevent the camera from zooming if the item was added.
        /// </summary>
        /// <param name="prevent">The dual wielded item added. Can be null.</param>
        private void OnDualWieldItemChange(Item item)
        {
            m_PreventZoom = item != null;
        }

        /// <summary>
        /// Do not follow the character's position on the y axis. This may be triggered during a character animation such as a roll.
        /// </summary>
        /// <param name="staticHeight">True if the camera should not follow the character on the y axis.</param>
        private void OnStaticHeight(bool staticHeight)
        {
            if (staticHeight) {
                m_StaticYDifference = m_Transform.position.y - m_CharacterTransform.position.y;
            } else {
                m_StaticYDifference = -1;
            }
        }

        /// <summary>
        /// Allow the character to apply a vertical offset to the camera's position.
        /// </summary>
        /// <param name="offset">The vertical offset.</param>
        private void OnHeightOffset(float offset)
        {
            m_VerticalOffset = offset;
        }

        /// <summary>
        /// Should the camera check for character obstruction by other objects?
        /// </summary>
        /// <param name="check">True if the camera should check for object obstruction.</param>
        private void OnCheckObjectObstruction(bool check)
        {
            m_ObstructionCheck = check;
        }

        /// <summary>
        /// The character has died. Start orbiting around the player or looking at the player.
        /// </summary>
        private void OnCharacterDeath()
        {
            m_CharacterHasDied = true;
            m_TargetRecoil = m_Recoil = 0;
            // Set the previous target position to a negative number to prevent the first death orbit frame frame from thinking that there wasn't a change.
            m_PrevTargetPosition = -(m_DeathAnchor != null ? m_DeathAnchor : m_Anchor).position;
            EventHandler.ExecuteEvent<float>(m_Character, "OnCameraUpdateRecoil", m_Recoil);
        }

        /// <summary>
        /// The character has respawned. Reset the variables and move to the correct position.
        /// </summary>
        private void OnCharacterSpawn()
        {
            m_CharacterHasDied = false;
            m_PrevRenderersEnabled = false;
            m_Yaw = m_CharacterTransform.eulerAngles.y;
            m_Pitch = m_StartPitch;
            ImmediatePosition();
        }

        /// <summary>
        /// Immediately reset the position/rotation of the camera with the starting rotation.
        /// </summary>
        public void ImmediatePosition()
        {
            if (m_ViewMode != CameraMonitor.CameraViewMode.Pseudo3D) {
                ImmediatePosition(Quaternion.Euler(m_StartPitch, m_Yaw, 0));
            } else {
                ImmediatePosition(Quaternion.LookRotation(m_Anchor.position - m_Transform.position));
            }
        }

        /// <summary>
        /// Immediately reset the position/rotation of the camera.
        /// </summary>
        /// <param name="targetRotation">The target rotation of the camera.</param>
        public void ImmediatePosition(Quaternion targetRotation)
        {
            m_Transform.rotation = targetRotation;
            m_Pitch = m_Transform.eulerAngles.x;
            m_Yaw = m_Transform.eulerAngles.y;
            var lookPoint = m_CharacterTransform.position + m_AnchorStartOffset + (m_ApplyColliderOffset ? m_CharacterCapsuleCollider.center : Vector3.zero) + 
                                (m_CameraOffset.x * m_Transform.right) + (m_CameraOffset.y * m_CharacterTransform.up) + (m_CameraOffset.z * m_Transform.forward);
            if (m_ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || m_ViewMode == CameraMonitor.CameraViewMode.RPG) {
                m_Transform.position = lookPoint;
            } else if (m_ViewMode == CameraMonitor.CameraViewMode.TopDown) {
                var direction = Quaternion.Euler(m_MinPitchLimit, 0, 0) * -Vector3.forward;
                m_Transform.position = lookPoint + direction * m_ViewDistance;
            } else { // 2.5D.
                m_Transform.position = lookPoint - m_LookDirection * m_ViewDistance;
            }
            m_Camera.fieldOfView = m_NormalFOV;
        }
    }
}