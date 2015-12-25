using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System.Collections;
using Opsive.ThirdPersonController.Abilities;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The RigidbodyCharacterController controls all of the character's movements. At a higher level it has three different types of movement: combat, adventure, and top down movement.
    /// In combat movement the camera is always behind the character and it allows the character to strafe and move backwards. In adventure movement the character can move forward
    /// in any direction and allows for a free camera movement. Top down movement moves relative to the camera and rotates to always look at the mouse.
    /// The controller uses a rigidbody and will respond to external forces.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
#if ENABLE_MULTIPLAYER
    public class RigidbodyCharacterController : NetworkBehaviour
#else
    public class RigidbodyCharacterController : MonoBehaviour
#endif
    {
        // Combat movement allows the character to move backwards and strafe. If the character has a camera following it then the character will always be
        // facing in the same direction as the camera. Adventure movement always moves the character in the direction they are facing and 
        // the camera can be facing any direction. Top down movement moves rotates the character in the direction of the mouse and moves relative to the camera.
        // RPG is a blend between Combat and Adventure movement types. Psuedo3D is used for 2.5D games. Point and Click moves the character according to the
        // point clicked. The PointClickControllerHandler is required.
        public enum MovementType { Combat, Adventure, TopDown, RPG, Pseudo3D, PointClick }

        // Allows the character's position to be constrained on the x or z axis.
        public enum MovementConstraint { None, RestrictX, RestrictZ, RestrictXZ }

        [Tooltip("The movement type")]
        [SerializeField] private MovementType m_MovementType = MovementType.Combat;
        [Tooltip("Should root motion be used?")]
        [SerializeField] private bool m_UseRootMotion = true;
        [Tooltip("The multiplier of the root motion movement")]
        [SerializeField] private float m_RootMotionSpeedMultiplier = 1;
        [Tooltip("The speed that the character can rotate")]
        [SerializeField] private float m_RotationSpeed = 6;
        [Tooltip("The speed that the character can rotate while aiming")]
        [SerializeField] private float m_FastRotationSpeed = 15;
        [Tooltip("The speed while on the ground and not using root motion")]
        [SerializeField] private float m_GroundSpeed = 1f;
        [Tooltip("The speed while in the air")]
        [SerializeField] private float m_AirSpeed = 0.5f;
        [Tooltip("The amount of dampening force to apply while on the ground")]
        [SerializeField] private float m_GroundDampening = 0.15f;
        [Tooltip("The amount of dampening force to apply while in the air")]
        [SerializeField] private float m_AirDampening = 0.15f;
        [Tooltip("Force which keeps the character sticking to the ground while stationary")]
        [SerializeField] private float m_GroundStickiness = 6;
        [Tooltip("The additional width of the character's collider")]
        [SerializeField] private float m_SkinWidth = 0.08f;
        [Tooltip("An extra distance used to determine if the player is on the ground while on a moving platform")]
        [SerializeField] private float m_SkinMovingPlatformStickiness = 0.5f;
        [Tooltip("Optionally restrict the x or z position")]
        [SerializeField] private MovementConstraint m_MovementConstraint;
        [Tooltip("If restricting the x axis, the minimum x position the character can occupy")]
        [SerializeField] private float m_MinXPosition;
        [Tooltip("If restricting the x axis, the maximum x position the character can occupy")]
        [SerializeField] private float m_MaxXPosition;
        [Tooltip("If restricting the z axis, the minimum z position the character can occupy")]
        [SerializeField] private float m_MinZPosition;
        [Tooltip("If restricting the z axis, the maximum z position the character can occupy")]
        [SerializeField] private float m_MaxZPosition;
        [Tooltip("The maximum height that the character can step")]
        [SerializeField] private float m_MaxStepHeight = 0.2f;
        [Tooltip("The offset relative to the character's position that should be used for checking if a step exists")]
        [SerializeField] private Vector3 m_StepOffset = new Vector3(0, 0.01f, 0.1f);
        [Tooltip("The vertical speed that the character moves when taking a step")]
        [SerializeField] private float m_StepSpeed = 2f;
        [Tooltip("The maximum slope angle that the character can move on (in degrees)")]
        [SerializeField] private float m_SlopeLimit = 30f;
        [Tooltip("Should the character always aim?")]
        [SerializeField] private bool m_AlwaysAim;
        [Tooltip("The character will rotate to face in the direction of the camera when using an item. If the character is not facing in the correct direction when trying " +
                 "to use an item, they will automatically rotate until an angle less than this value")]
        [SerializeField] private float m_ItemUseRotationThreshold = 1;
        [Tooltip("The duration that the character should forcibly use the item")]
        [SerializeField] private float m_ItemForciblyUseDuration = 0.3f;
        [Tooltip("The duration that the character should forcibly use the dual wielded item")]
        [SerializeField] private float m_DualWieldItemForciblyUseDuration = 0.3f;
        [Tooltip("The friction material to use while on the ground")]
        [SerializeField] private PhysicMaterial m_GroundedFrictionMaterial;
        [Tooltip("The friction material to use while stepping")]
        [SerializeField] private PhysicMaterial m_StepFrictionMaterial;
        [Tooltip("The friction material to use while on a slope")]
        [SerializeField] private PhysicMaterial m_SlopeFrictionMaterial;
        [Tooltip("The friction material to use while in the air")]
        [SerializeField] private PhysicMaterial m_AirFrictionMaterial;
        [Tooltip("Abilities allow for extra functionalities such as cover or interact")]
        [SerializeField] private Ability[] m_Abilities = new Ability[0];

        // Internal varaibles
        private Vector3 m_InputVector;
        private Quaternion m_LookRotation;
#if ENABLE_MULTIPLAYER
        [SyncVar(hook = "SetAim")]
#endif
        private bool m_Aim;
#if ENABLE_MULTIPLAYER
        [SyncVar]
#endif
        private bool m_ForceAim;
        private bool m_IsAiming;
        private bool m_IsForcedAiming;

        private bool m_IsAI;
        private bool m_Grounded;
        private bool m_Moving;
        private Vector3 m_Velocity;
        private Vector3 m_RootMotionForce;
        private Vector3 m_PrevRootMotionForce;
        private Quaternion m_RootMotionRotation = Quaternion.identity;
        private bool m_ForceRootMotion;
        private float m_PrevYRotation;
        private float m_Slope = -1;
        private bool m_Stepping;
        private Vector3 m_GroundVelocity;
        private Vector3 m_PrevGroundVelocity;
        private Vector3 m_AirVelocity;
        private Vector3 m_PrevAirVelocity;
        private float m_PrevGroundHeight;
        private ScheduledEvent m_ForcedItemUseEvent;
        private WaitForEndOfFrame m_EndOfFrame = new WaitForEndOfFrame();

        private RaycastHit m_RaycastHit;
        private Transform m_Platform;
        private Vector3 m_PlatformPosition;
        private Vector3 m_PlatformRelativePosition;
        private float m_PrevPlatformAngle;

        private float m_CapsuleColliderHeight;
        private Vector3 m_CapsuleColliderCenter;

        // SharedFields
        private SharedMethod<bool> m_IsSwitchingItem = null;
        private SharedMethod<bool> m_PointerOverEnemy = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;
        private bool SharedProperty_Aim { get { return Aim; } }

        // Component references
        private GameObject m_GameObject;
        private Transform m_Transform;
        private Rigidbody m_Rigidbody;
        private CapsuleCollider m_CapsuleCollider;
        private Animator m_Animator;
        private AnimatorMonitor m_AnimatorMonitor;

        // Exposed properties
        public MovementType Movement { get { return m_MovementType; } set { m_MovementType = value; } }
        public bool Moving
        {
            get { return m_Moving; }
            set
            {
                if (m_Moving != value) {
                    m_Moving = value;
                    m_AnimatorMonitor.DetermineStates();
                }
            }
        }
        public float RotationSpeed { get { return m_RotationSpeed; } set { m_RotationSpeed = value; } }
        public bool AlwaysAim { get { return m_AlwaysAim; } set { m_AlwaysAim = value; if (m_AnimatorMonitor != null) m_AnimatorMonitor.DetermineStates(); } }
        public PhysicMaterial GroundedFrictionMaterial { set { m_GroundedFrictionMaterial = value; } get { return m_GroundedFrictionMaterial; } }
        public PhysicMaterial StepFrictionMaterial { set { m_StepFrictionMaterial = value; } get { return m_StepFrictionMaterial; } }
        public PhysicMaterial SlopeFrictionMaterial { set { m_SlopeFrictionMaterial = value; } get { return m_SlopeFrictionMaterial; } }
        public PhysicMaterial AirFrictionMaterial { set { m_AirFrictionMaterial = value; } get { return m_AirFrictionMaterial; } }
        public float SkinWidth { get { return m_SkinWidth; } }
        public bool Grounded { get { return m_Grounded; } set { m_Grounded = value; } }
        public bool Aim { get { return m_Aim; } set { SetAim(value); } }
        public bool Aiming { get { return m_Aim || m_ForceAim || m_AlwaysAim; } }
        public float SlopeLimit { get { return m_SlopeLimit; } }
        public Vector3 InputVector { get { return m_InputVector; } }
        public Quaternion LookRotation { get { return m_LookRotation; } }
        public Vector3 RootMotionForce { get { return m_RootMotionForce; } set { m_RootMotionForce = value; } }
        public Quaternion RootMotionRotation { get { return m_RootMotionRotation; } set { m_RootMotionRotation = value; } }
        public float RootMotionSpeedMultiplier { get { return m_RootMotionSpeedMultiplier; } }
        public bool ForceRootMotion { set { m_ForceRootMotion = value; } }
        public bool UseRootMotion { get { return m_UseRootMotion || m_ForceRootMotion; } set { m_UseRootMotion = value; } }
        public Vector3 Velocity { get { return m_Velocity; } set { m_Velocity = value; } }
        public Ability[] Abilities { get { return m_Abilities; } set { m_Abilities = value; } }

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_Rigidbody = GetComponent<Rigidbody>();
            m_CapsuleCollider = GetComponent<CapsuleCollider>();
            m_Animator = GetComponent<Animator>();
            m_AnimatorMonitor = GetComponent<AnimatorMonitor>();

            SharedManager.Register(this);

            // An AI agent will not have the PlayerInput component.
            m_IsAI = GetComponent<PlayerInput>() == null;

#if !(UNITY_4_6 || UNITY_5_0)
            // A networked character will have the NetworkIdentity component.
            var networkIdentity = GetComponent<UnityEngine.Networking.NetworkIdentity>();
#if ENABLE_MULTIPLAYER
            if (networkIdentity == null) {
                Debug.LogError("Error: The Multiplayer symbol is defined but the NetworkIdentity component was not was found. Please remove the symbol within the RigidbodyCharacterController inspector.");
            } else if (networkIdentity.localPlayerAuthority) {
                Debug.LogWarning("Warning: Local Player Authority is enabled on the NetworkIdentity component. This value must be disabled.");
                networkIdentity.localPlayerAuthority = false;
            }
#else
            if (networkIdentity != null) {
                Debug.LogError("Error: A NetworkIdentity component was found but the Multiplayer symbol is not defined. Please define it within the RigidbodyCharacterController inspector.");
            }
#endif
#endif

            m_PrevGroundHeight = m_Transform.position.y;
            m_CapsuleColliderHeight = m_CapsuleCollider.height;
            m_CapsuleColliderCenter = m_CapsuleCollider.center;
            SetPosition(m_Transform.position);
            SetRotation(m_Transform.rotation);
        }

        /// <summary>
        /// Register for any events that the controller should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorAiming", OnAiming);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnItemUse", OnItemUse);
            EventHandler.RegisterEvent(m_GameObject, "OnItemStopUse", OnItemStopUse);
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Unregister for any events that the controller was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorAiming", OnAiming);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnItemUse", OnItemUse);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemStopUse", OnItemStopUse);
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
        }
        
        /// <summary>
        /// Initialize the shared fields.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
        }

        /// <summary>
        /// Is the character an AI agent?
        /// </summary>
        /// <returns>True if the character is an AI agent.</returns>
        private bool SharedMethod_IsAI()
        {
            return m_IsAI;
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Is the character running as a server?
        /// </summary>
        /// <returns>True if the character is running as a server.</returns>
        private bool SharedMethod_IsServer()
        {
            return isServer;
        }
#endif

        /// <summary>
        /// Moves the character according to the input. This method exists to allow AI to easily move the character instead of having to go through
        /// the ControllerHandler.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        public void Move(float horizontalMovement, float forwardMovement, Quaternion lookRotation)
        {
            // Store the velocity as it will be used by many of the functions below.
            m_Velocity = m_Rigidbody.velocity;

            var abilityHasControl = false;
            for (int i = 0; i < m_Abilities.Length; ++i) {
                m_Abilities[i].UpdateAbility();
                if (!abilityHasControl && m_Abilities[i].IsActive && m_Abilities[i].Move(ref horizontalMovement, ref forwardMovement, lookRotation)) {
                    abilityHasControl = true;
                }
            }
            if (abilityHasControl) {
                return;
            }

            // Store the input parameters.
            m_InputVector.x = horizontalMovement;
            m_InputVector.z = forwardMovement;
            m_LookRotation = lookRotation;

            // Is the character on the ground?
            CheckGround();

            // Are any external forces affecting the current velocity?
            CheckForExternalForces();

            // Ensures the current movement is valid.
            CheckMovement();

            // Set the correct physic material based on the grounded state.
            SetPhysicMaterial();

            // Update the velocity based on the grounded state.
            UpdateMovement();

            // Move with the platform if on a moving platform.
            UpdatePlatformMovement();

            // Rotate in the correct direction.
            UpdateRotation();

            // Update the animator so the correct animations will play.
            UpdateAnimator();

            // Update the collider to be sized correctly according to the animation played.
            UpdateCollider();

            // The velocity would have been modified by the above functions so reassign it when reassign velocity.
            if (!m_Rigidbody.isKinematic) {
                m_Rigidbody.velocity = m_Velocity;
            }

            // Update the abilities. This will allow new abilities to start based on the new controller data.
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (!m_Abilities[i].IsActive && m_Abilities[i].StartType == Ability.InputStartType.None) {
                    if (TryStartAbility(m_Abilities[i])) {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the grounded state if the character is on the ground. Will also check for a moving platform.
        /// </summary>
        private void CheckGround()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].CheckGround()) {
                    return;
                }
            }

            // If the character is on a platform extend the length to account for the platform's position change.
            var stickiness = 0f;
            if (m_Platform != null && m_Velocity.y < 0.1f) {
                stickiness = m_SkinMovingPlatformStickiness;
            }

            // Determine if the character is grounded by doing a spherecast from the character's knee to the ground.
            var colliderRadius = m_CapsuleCollider.radius - 0.05f;
            var grounded = m_Stepping || Physics.SphereCast(m_Transform.position + Vector3.up * colliderRadius * 2, colliderRadius, Vector3.down, out m_RaycastHit,
                                                            colliderRadius * 2 + m_SkinWidth + stickiness, LayerManager.Mask.Ground);
            if (grounded) {
                // Update the platform variables if on a moving platform. The characters position and rotation will change based off of that moving platform.
                if (m_RaycastHit.transform.gameObject.layer == LayerManager.MovingPlatform) {
                    if (m_Platform == null) {
                        m_Platform = m_RaycastHit.transform;
                        m_PlatformPosition = m_Transform.position;
                        m_PlatformRelativePosition = m_Platform.InverseTransformPoint(m_Transform.position);
                        m_PrevPlatformAngle = m_Platform.eulerAngles.y;
                    }
                } else {
                    m_Platform = null;
                }
                // Keep the player sticking to the ground if the player is grounded and the velocity isn't positive. This helps in the cases of slopes where the character shouldn't
                // be floating in the air because they moved down a slope.
                if (!m_Stepping) {
                    m_Velocity.y -= m_GroundStickiness * Time.fixedDeltaTime;
                }
            // The character is no longer on the ground. Reset the related variables.
            } else if (!grounded) {
                m_Platform = null;
                if (m_Grounded) {
                    // Add a small force in the moving direction to prevent the character from toggling between grounded and not grounded state.
                    m_Velocity += m_Velocity.normalized * 0.2f;
                }
                // Save out the max height of the character in the air so the fall height can be calculated and the grounded check can ensure the player is on the ground.
                if (m_Transform.position.y > m_PrevGroundHeight) {
                    m_PrevGroundHeight = m_Transform.position.y;
                }
            }

            if (m_Grounded != grounded) {
                EventHandler.ExecuteEvent<bool>(m_GameObject, "OnControllerGrounded", grounded);
                // Other objects are interested in when the character lands (such as CharacterHealth to determine if any fall damage should be applied).
                if (grounded) {
                    m_AirVelocity = m_PrevAirVelocity = Vector3.zero;
                    EventHandler.ExecuteEvent<float>(m_GameObject, "OnControllerLand", (m_PrevGroundHeight - m_Transform.position.y));
                } else {
                    m_GroundVelocity = m_PrevGroundVelocity = Vector3.zero;
                    m_PrevGroundHeight = float.NegativeInfinity;
                }
            }
            m_Grounded = grounded;
            m_Rigidbody.useGravity = !m_Stepping;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// </summary>
        private void CheckForExternalForces()
        {
            var xPercent = 0f;
            var zPercent = 0f;
            // Calculate the percentage that the root motion force affected the current velocity. 
            if (m_Grounded && UseRootMotion) {
                var prevTotalRootMotionForce = Mathf.Abs(m_PrevRootMotionForce.x) + Mathf.Abs(m_PrevRootMotionForce.z);
                xPercent = m_Velocity.x != 0 ? Mathf.Clamp01(Mathf.Abs(prevTotalRootMotionForce / m_Velocity.x)) : 1;
                zPercent = m_Velocity.z != 0 ? Mathf.Clamp01(Mathf.Abs(prevTotalRootMotionForce / m_Velocity.z)) : 1;
            }

            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].CheckForExternalForces(xPercent, zPercent)) {
                    return;
                }
            }

            if (m_Grounded) {
                if (UseRootMotion) {
                    // Only add a dampening to the non-root motion velocity. The root motion velocity has already had a dampening force added to it within UpdateMovment.
                    m_Velocity.x = ((m_Velocity.x * (1 - xPercent)) / (1 + m_GroundDampening)) + m_PrevRootMotionForce.x * xPercent;
                    m_Velocity.z = ((m_Velocity.z * (1 - zPercent)) / (1 + m_GroundDampening)) + m_PrevRootMotionForce.z * zPercent;
                } else {
                    // Don't use root motion so apply the ground dampening to the entire velocity.
                    m_Velocity.x /= (1 + m_GroundDampening);
                    m_Velocity.z /= (1 + m_GroundDampening);
                }
            } else {
                // Root motion doesn't affect the character at all in the air so just apply a dampening force.
                m_Velocity.x /= (1 + m_AirDampening);
                m_Velocity.z /= (1 + m_AirDampening);
            }
        }

        /// <summary>
        /// A new velocity has been set. Ensure this velocity is valid. For example, don't allow the character to keep running into a wall. Also apply a
        /// vertical velocity for steps.
        /// </summary>
        private void CheckMovement()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].CheckMovement()) {
                    return;
                }
            }

            var prevStepping = m_Stepping;
            m_Stepping = false;
            m_Slope = -1;
            if (m_InputVector.sqrMagnitude > 0.1f) {
                // Determine the relative direction of the input based on the movement type.
                var inputDirection = Vector3.zero;
                switch (m_MovementType) {
                    case MovementType.Combat:
                    case MovementType.Adventure:
                    case MovementType.PointClick:
                    case MovementType.RPG:
                        inputDirection = m_LookRotation * m_InputVector;
                        break;
                    case MovementType.TopDown:
                    case MovementType.Pseudo3D:
                        inputDirection = m_InputVector;
                        break;
                }
                // The Y axis should not contribute to the input direction.
                inputDirection.y = 0;
                // Fire a raycast in the direction that the character is moving. There is a chance that the character should step or stop moving if the raycast hits an object.
                if (Physics.Raycast(m_Transform.position + Vector3.up * m_StepOffset.y, inputDirection.normalized, out m_RaycastHit,
                                    m_CapsuleCollider.radius + m_StepOffset.z, LayerManager.Mask.Ground)) {
                    // An object was hit. The character should stop moving or step if the object does not have a Rigidbody or the Rigidbody is kinematic.
                    Rigidbody hitRigidbody = null;
                    if ((hitRigidbody = m_RaycastHit.transform.GetComponent<Rigidbody>()) == null || hitRigidbody.isKinematic == true) {
                        // The first raycast is extremely long compared to what the character can actually step up. Shorten the length of this next raycast to prevent the character from stepping
                        // on top of an object that doesn't exist.
                        var hitDistance = m_Transform.InverseTransformPoint(m_RaycastHit.point).magnitude;
                        var point = m_Transform.position + inputDirection.normalized * (Mathf.Min(hitDistance, (m_CapsuleCollider.radius + m_StepOffset.z)) + 0.01f) + Vector3.up * (m_StepOffset.y + m_MaxStepHeight);
                        var distance = m_RaycastHit.distance;
                        if (Physics.Raycast(point, Vector3.down, out m_RaycastHit, m_MaxStepHeight, LayerManager.Mask.Ground)) {
                            var slope = Mathf.Acos(m_RaycastHit.normal.y) * Mathf.Rad2Deg;
                            if (slope <= m_SlopeLimit) {
                                m_Slope = slope;
                                if (!prevStepping) {
                                    // Only trigger stepping if the slope is 0.
                                    m_Stepping = slope < 0.1f;
                                } else {
                                    // Keep the previous stepping value.
                                    m_Stepping = prevStepping;
                                }
                                if (m_Stepping) {
                                    // Add a small amount of extra force if this is the first time the character is stepping to allow the character to get over the initial step.
                                    m_Velocity.y += (m_StepSpeed * Time.fixedDeltaTime * (!prevStepping ? -Physics.gravity.y : 1));
                                }
                            } else {
                                // The slope is too great. Stop moving.
                                m_InputVector = Vector3.zero;
                            }
                        } else if (distance < m_CapsuleCollider.radius + 0.01f) {
                            // The object is taller than the step height. Stop moving if the character is near the object.
                            m_InputVector = Vector3.zero;
                        }
                    }
                }

                if (m_InputVector.sqrMagnitude > 0) {
                    // Prevent moving in the x or z directions if there are constraints set.
                    if (m_MovementConstraint != MovementConstraint.None) {
                        // Restrict the x axis if the constraint is set to anything but RestrictZ.
                        if (m_MovementConstraint != MovementConstraint.RestrictZ) {
                            if ((inputDirection.x < 0 && m_Transform.position.x < m_MinXPosition) ||
                                (inputDirection.x > 0 && m_Transform.position.x > m_MaxXPosition)) {
                                m_InputVector.x = 0;
                            }
                        }

                        // Restrict the z axis if the constraint is set to anything but RestrictX.
                        if (m_MovementConstraint != MovementConstraint.RestrictX) {
                            if ((inputDirection.z < 0 && m_Transform.position.z < m_MinZPosition) ||
                                (inputDirection.z > 0 && m_Transform.position.z > m_MaxZPosition)) {
                                m_InputVector.z = 0;
                            }
                        }
                    }
                }
            }

            // The character was previously stepping so extra vertical velocity was added. Quickly remove most of that extra velocity to prevent the character
            // from flying in the air after a step.
            if (prevStepping && !m_Stepping) {
                m_Velocity.y *= 0.5f;
            }
        }

        /// <summary>
        /// Set the physic material based on the grounded and stepping state.
        /// </summary>
        private void SetPhysicMaterial()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].SetPhysicMaterial()) {
                    return;
                }
            }

            if (m_Grounded) {
                if (m_Stepping && m_StepFrictionMaterial != null) {
                    m_CapsuleCollider.material = m_StepFrictionMaterial;
                } else if (m_Slope != -1 && m_SlopeFrictionMaterial != null) {
                    m_CapsuleCollider.material = m_SlopeFrictionMaterial;
                } else {
                    m_CapsuleCollider.material = m_GroundedFrictionMaterial;
                }
            } else {
                if (m_AirFrictionMaterial != null) {
                    m_CapsuleCollider.material = m_AirFrictionMaterial;
                }
            }
        }

        /// <summary>
        /// Update the grounded or in air movement.
        /// </summary>
        private void UpdateMovement()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].UpdateMovement()) {
                    return;
                }
            }

            if (m_Grounded) {
                UpdateGroundedMovement();
            } else {
                UpdateAirborneMovement();
            }
        }

        /// <summary>
        /// Apply grounded forces while on the group. Y velocity should be 0.
        /// </summary>
        private void UpdateGroundedMovement()
        {
            // Directly add to the velocity if not using root motion. This movement will be instantaneous.
            if (UseRootMotion) {
                var rootMotionForce = m_RootMotionForce / ((1 + m_GroundDampening) * Time.fixedDeltaTime);
                m_Velocity.x += (rootMotionForce.x - m_PrevRootMotionForce.x);
                m_Velocity.z += (rootMotionForce.z - m_PrevRootMotionForce.z);
                m_PrevRootMotionForce = rootMotionForce;
                m_RootMotionForce = Vector3.zero;
            } else {
                Vector3 direction;
                if (m_MovementType == MovementType.Adventure || m_MovementType == MovementType.Combat || m_MovementType == MovementType.RPG || m_MovementType == MovementType.PointClick) {
                    direction = Quaternion.LookRotation(m_LookRotation * Vector3.forward) * m_InputVector;
                    direction.y = 0;
                } else if (m_MovementType == MovementType.Pseudo3D && m_InputVector.sqrMagnitude > 0) {
                    direction = m_InputVector;
                } else {
                    direction = Quaternion.LookRotation(Vector3.forward) * m_InputVector;
                }

                // Move relative to the forward direction.
                m_GroundVelocity += direction * m_GroundSpeed;
                m_GroundVelocity -= m_PrevGroundVelocity;

                if (m_GroundVelocity.sqrMagnitude < 0.01f) {
                    m_GroundVelocity = Vector3.zero;
                }

                m_Velocity += m_GroundVelocity;
                m_PrevGroundVelocity = m_GroundVelocity;
            }
        }

        /// <summary>
        /// While in the air root motion doesn't exist so apply the input forces manually.
        /// </summary>
        private void UpdateAirborneMovement()
        {
            // Let gravity handle vertical movement.
            m_AirVelocity.y = 0;

            // Move in the correct direction.
            Vector3 direction;
            if (m_MovementType == MovementType.Adventure || m_MovementType == MovementType.Combat || m_MovementType == MovementType.RPG || m_MovementType == MovementType.PointClick) {
                direction = Quaternion.LookRotation(m_LookRotation * Vector3.forward) * m_InputVector;
                direction.y = 0;
                direction.Normalize();
            } else if (m_MovementType == MovementType.Pseudo3D && m_InputVector.sqrMagnitude > 0) {
                direction = m_InputVector;
            } else {
                direction = Quaternion.LookRotation(Vector3.forward) * m_InputVector;
            }

            // Move relative to the forward rotation.
            m_AirVelocity += direction * m_AirSpeed;
            m_AirVelocity -= m_PrevAirVelocity;

            if (m_AirVelocity.sqrMagnitude < 0.01f) {
                m_AirVelocity = Vector3.zero;
            }

            m_Velocity += m_AirVelocity;
            m_PrevAirVelocity = m_AirVelocity;
        }

        /// <summary>
        /// Move and rotate while on a moving platform.
        /// </summary>
        private void UpdatePlatformMovement()
        {
            if (m_Platform == null)
                return;

            // Keep the same relative position.
            var target = m_Transform.position + m_Platform.TransformPoint(m_PlatformRelativePosition) - m_PlatformPosition;
            m_Rigidbody.MovePosition(target);
            m_PlatformPosition = target;
            m_PlatformRelativePosition = m_Platform.InverseTransformPoint(target);

            // Keep the same relative rotation.
            var eulerAngles = m_Transform.eulerAngles;
            eulerAngles.y -= Mathf.DeltaAngle(m_Platform.eulerAngles.y, m_PrevPlatformAngle);
            m_Rigidbody.MoveRotation(Quaternion.Euler(eulerAngles));
            m_PrevPlatformAngle = m_Platform.eulerAngles.y;
            m_PrevYRotation = eulerAngles.y;
        }

        /// <summary>
        /// Rotate in the correct direction. The rotation direction depends on if the character movement type.
        /// </summary>
        private void UpdateRotation()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].UpdateRotation()) {
                    return;
                }
            }

            // Face in the direction that the character is moving if not in combat mode.
            if (m_MovementType == MovementType.Adventure && !m_IsAiming && !m_IsForcedAiming) {
                if (m_InputVector != Vector3.zero) {
                    var targetRotation = Quaternion.Euler(0, Quaternion.LookRotation(m_LookRotation * m_InputVector.normalized).eulerAngles.y, 0);
                    m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, targetRotation, m_RotationSpeed * Time.fixedDeltaTime);
                }
            } else {
                // Only rotate the y angle.
                var rotation = m_Transform.eulerAngles;
                rotation.y = m_LookRotation.eulerAngles.y;
                m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.Euler(rotation), (m_Aim || m_ForceAim ? m_FastRotationSpeed : m_RotationSpeed) * Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Update the animator with the correct parameters.
        /// </summary>
        private void UpdateAnimator()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].UpdateAnimator()) {
                    return;
                }
            }

            // In non-combat mode the character can freely rotate to move in any direction so the speed is the magnitude of the input vector.
            // While in combat mode, the z direction only contributes to the speed and the x direction contributes to the strafe.
            // Either value can be negative.
            var horizontalSpeed = 0f;
            var forwardSpeed = 0f;
            if (m_MovementType == MovementType.Combat || m_MovementType == MovementType.RPG || m_MovementType == MovementType.PointClick || 
                    (m_MovementType == MovementType.Adventure && (m_IsAiming || m_IsForcedAiming))) {
                horizontalSpeed = m_InputVector.x;
                forwardSpeed = m_InputVector.z;
            } else if (m_MovementType == MovementType.Adventure) {
                // Clamp to a value higher then one if the x or z value is greater then one. This can happen if the character is sprinting.
                var clampValue = Mathf.Max(Mathf.Abs(m_InputVector.x), Mathf.Max(Mathf.Abs(m_InputVector.z), 1));
                forwardSpeed = Mathf.Clamp(m_InputVector.magnitude, -clampValue, clampValue);
            } else if (m_MovementType == MovementType.TopDown || m_MovementType == MovementType.Pseudo3D) {
                var localDirection = m_Transform.InverseTransformDirection(m_InputVector.x, 0, m_InputVector.z);
                horizontalSpeed = localDirection.x;
                forwardSpeed = localDirection.z;
            }
            m_AnimatorMonitor.SetHorizontalInputValue(horizontalSpeed);
            m_AnimatorMonitor.SetForwardInputValue(forwardSpeed);
            m_AnimatorMonitor.SetYawValue(Mathf.DeltaAngle(m_PrevYRotation, m_Transform.eulerAngles.y));
            m_PrevYRotation = m_Transform.eulerAngles.y;

            if (m_Moving) {
                Moving = m_Velocity.sqrMagnitude > 0.01f || m_InputVector.sqrMagnitude > 0.01f;
            } else {
                Moving = m_InputVector.sqrMagnitude > 0.01f;
            }
        }

        /// <summary>
        /// Size the collider according to the animation being played.
        /// </summary>
        private void UpdateCollider()
        {
            var curveData = m_AnimatorMonitor.GetColliderCurveData();
            m_CapsuleCollider.height = m_CapsuleColliderHeight - curveData;
            var center = m_CapsuleColliderCenter;
            center.y *= (m_CapsuleCollider.height / m_CapsuleColliderHeight);
            m_CapsuleCollider.center = center;
        }

        /// <summary>
        /// Callback from the animator when root motion has updated.
        /// </summary>
        private void OnAnimatorMove()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].AnimatorMove()) {
                    return;
                }
            }

            // Don't read the delta position/rotation if not using root motion.
            if (!UseRootMotion) {
                return;
            }
            if (m_Grounded) {
                m_RootMotionForce += m_Animator.deltaPosition * m_RootMotionSpeedMultiplier;
            } else {
                m_RootMotionForce = Vector3.zero;
            }
            m_RootMotionRotation *= m_Animator.deltaRotation;
        }

        /// <summary>
        /// Immediately sets the position. This is not a smooth movement.
        /// </summary>
        /// <param name="position">The target position.</param>
        public void SetPosition(Vector3 position)
        {
            m_Rigidbody.position = m_Transform.position = position;
        }

        /// <summary>
        /// Immediately sets the rotation. This is not a smooth rotation.
        /// </summary>
        /// <param name="rotation">The target rotation.</param>
        public void SetRotation(Quaternion rotation)
        {
            m_Rigidbody.rotation = m_Transform.rotation = rotation;
            m_PrevYRotation = rotation.eulerAngles.y;
        }

        /// <summary>
        /// Stops the Rigidbody from moving.
        /// </summary>
        public void StopMovement()
        {
            StopMovement(true);
        }

        /// <summary>
        /// Stops the Rigidbody from moving.
        /// <param name="stopMovingState">Should the moving state be set?</param>
        /// </summary>
        public void StopMovement(bool stopMovingState)
        {
            m_PrevRootMotionForce = m_RootMotionForce = m_Velocity = Vector3.zero;
            m_Rigidbody.velocity = Vector3.zero;
            m_RootMotionRotation = Quaternion.identity;
            m_Rigidbody.angularVelocity = Vector3.zero;
            if (stopMovingState) {
                Moving = false;
            }
        }

        /// <summary>
        /// Tries to start the specified ability.
        /// </summary>
        /// <param name="ability">The ability to try to start.</param>
        /// <returns>True if the ability was started.</returns>
        public bool TryStartAbility(Ability ability)
        {
            // Start the ability if it is not active and can be started.
            if (!ability.IsActive && ability.CanStartAbility()) {
                // If the ability is not a concurrent ability then it can only be started if it has a lower index than any other active abilities.
                if (!ability.IsConcurrentAblity()) {
                    for (int i = 0; i < m_Abilities.Length; ++i) {
                        if (m_Abilities[i].IsActive) {
                            if (m_Abilities[i].IsConcurrentAblity()) {
                                // The ability cannot be started if a concurrent ability is active and has a lower index.
                                if (i < ability.Index && !m_Abilities[i].CanStartAbility(ability)) {
                                    return false;
                                }
                            } else {
                                // The ability cannot be started if another ability is already active and has a lower index or if the active ability says the current ability cannot be started.
                                if (i < ability.Index || !m_Abilities[i].CanStartAbility(ability)) {
                                    return false;
                                } else {
                                    // Stop any abilities that have a higher index to prevent two non-concurrent abilities from running at the same time.
                                    m_Abilities[i].StopAbility();
                                }
                            }
                        }
                    }
                } else {
                    for (int i = 0; i < m_Abilities.Length; ++i) {
                        // The ability cannot be started if the active ability says the current ability cannot be started.
                        if (m_Abilities[i].IsActive && !m_Abilities[i].CanStartAbility(ability)) {
                            return false;
                        }
                    }
                }
                // Prevent the character from aiming if the ability doesn't allow it.
                if (Aiming && (!ability.CanInteractItem() || !ability.CanInteractItem())) {
                    Aim = false;
                }
                ability.StartAbility();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to stop all active abilities.
        /// </summary>
        public void TryStopAllAbilities()
        {
            for (int i = m_Abilities.Length - 1; i > -1; --i) {
                if (m_Abilities[i].IsActive) {
                    m_Abilities[i].StopAbility();
                }
            }
        }

        /// <summary>
        /// Tries to stop the specified ability.
        /// </summary>
        /// <param name="ability">The ability to try to stop.</param>
        public void TryStopAbility(Ability ability)
        {
            if (ability.IsActive) {
                ability.StopAbility();
            }
        }

        /// <summary>
        /// When an item wants to be used it will invoke this SharedMethod. Item may not be ready to used though (for example, if the item is a weapon then it first needs to be
        /// aimed). TryUseItem will call PrepareToUseItem if the character is not aiming or if the character is not rotated to face the target.
        /// </summary>
        /// <returns></returns>
        private bool SharedMethod_TryUseItem()
        {
            // In order to be able to use an item the character must be aiming and looking at the target. While in combat mode the character will always be looking at the target
            // so the item use threshold does not apply here.
            var rotation = m_Transform.eulerAngles;
            if (!(m_IsAiming || m_IsForcedAiming) || 
                (m_MovementType != MovementType.Combat && Mathf.Abs(Mathf.DeltaAngle(m_Transform.eulerAngles.y, rotation.y)) > (m_ItemUseRotationThreshold + Mathf.Epsilon))) {
                // The character is either not aiming at the target or is not looking in the correct direction. Fix it.
                StartCoroutine(PrepareToUseItem());
                return false;
            }
            return true;
        }

        /// <summary>
        /// An item is about to be used. Start aiming and looking at the target. Will execute the OnItemReadyForUse event when the character has satisfied both of these conditions.
        /// </summary>
        private IEnumerator PrepareToUseItem()
        {
            // ForceAim will start the aim animation and also force the rotation in to face the correct direction.
            m_ForceAim = true;
            m_IsForcedAiming = false;
            var rotation = m_Transform.eulerAngles;
            Scheduler.Cancel(m_ForcedItemUseEvent);
            m_AnimatorMonitor.DetermineStates();
            m_ForcedItemUseEvent = null;

            // Keep waiting until the character is aiming and looking in the correct direction. The standard Move method will aim and rotate based off of the ForceAim variable so
            // this coroutine doesn't need to actually do anything beside wait and keep checking.
            while (!(m_IsAiming || m_IsForcedAiming) || 
                    (m_MovementType != MovementType.Combat && Mathf.Abs(Mathf.DeltaAngle(m_Transform.eulerAngles.y, rotation.y)) > (m_ItemUseRotationThreshold + Mathf.Epsilon))) {
                yield return m_EndOfFrame;
            }

            // The item is ready for use, send the event.
            EventHandler.ExecuteEvent(m_GameObject, "OnItemReadyForUse");
        }

        /// <summary>
        /// An item has been used so the force use schedule should be stopped.
        /// </summary>
        /// <param name="primaryItem">Is this item a PrimaryItemType item?</param>
        private void OnItemUse(bool primaryItem)
        {
            Scheduler.Cancel(m_ForcedItemUseEvent);
            m_ForcedItemUseEvent = null;
        }

        /// <summary>
        /// The weapon is no longer being fired. Reset the force aim variables after a small duration.
        /// </summary>
        private void OnItemStopUse()
        {
            if (m_ForceAim) {
                Scheduler.Cancel(m_ForcedItemUseEvent);
                m_ForcedItemUseEvent = Scheduler.Schedule(m_CurrentDualWieldItem.Get() == null ? m_ItemForciblyUseDuration : m_DualWieldItemForciblyUseDuration, StopForceUse);
            }
        }

        /// <summary>
        /// The weapon is no longer being fired. Call the corresponding server or client method.
        /// </summary>
        private void StopForceUse()
        {
            m_ForceAim = false;
            m_IsForcedAiming = false;
            m_ForcedItemUseEvent = null;
            m_AnimatorMonitor.DetermineStates();
            EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", false);
        }

        /// <summary>
        /// The character should start or stop aiming. Will notify the server if on the network.
        /// </summary>
        /// <param name="aim"></param>
        private void SetAim(bool aim)
        {
#if ENABLE_MULTIPLAYER
            // Aiming must be run on the server.
            if (!isServer) {
                CmdAim(aim);
            } else {
#endif
                AimLocal(aim);
#if ENABLE_MULTIPLAYER
            }
#endif 
        }

        /// <summary>
        /// The character should start or stop aiming.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        private void AimLocal(bool aim) 
        {
            // Don't change aim states if the character can't interact with the item.
            if (aim && !SharedMethod_CanInteractItem()) {
                return;
            }

            var change = aim != m_Aim;
            m_Aim = aim;
            if (change) {
                m_AnimatorMonitor.DetermineStates();
                // Change the aiming status immediately if the character is no longer aiming. If the character starts to aim then the OnAiming method will change the aiming status.
                if (!aim) {
                    m_IsAiming = false;
                    EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", false);
                }
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The character should start or stop aiming on the server.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        [Command]
        private void CmdAim(bool aim)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcAim(aim);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                AimLocal(aim);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// The character should start or stop aiming on the client.
        /// </summary>
        /// <param name="aim">Should the character aim?</param>
        [ClientRpc]
        private void RpcAim(bool aim)
        {
            AimLocal(aim);
        }
#endif

        /// <summary>
        /// Callback from the animator. The aim animation is done playing so the character is ready to use the item.
        /// </summary>
        private void OnAiming()
        {
            if (m_ForceAim && !m_IsForcedAiming) {
                m_IsForcedAiming = true;
                EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", true);
            }

            if ((m_Aim || m_AlwaysAim) && !m_IsAiming) {
                m_IsAiming = true;
                EventHandler.ExecuteEvent(m_GameObject, "OnControllerAim", true);
            }
        }

        /// <summary>
        /// Can the item be interacted with? Interactions include reload, equip, fire, etc.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        private bool SharedMethod_CanInteractItem()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CanInteractItem()) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Can the item be Used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        private bool SharedMethod_CanUseItem()
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CanUseItem()) {
                    return false;
                }
            }

            // Cannot use the item if the inventory is switching items.
            if (m_IsSwitchingItem.Invoke()) {
                return false;
            }

            // Cannot use the item if in PointClick mode and the cursor is not over an enemy.
            if (m_MovementType == MovementType.PointClick && !m_PointerOverEnemy.Invoke()) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Should the upper body IK be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the upper body IK should be used.</returns>
        private bool SharedMethod_CanUseIK(int layer)
        {
            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && !m_Abilities[i].CanUseIK(layer)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The character has died. Disable the unnecessary components.
        /// </summary>
        private void OnDeath()
        {
            for (int i = m_Abilities.Length - 1; i > -1; --i) {
                if (m_Abilities[i].IsActive) {
                    m_Abilities[i].StopAbility();
                }
            }
            StopMovement();
            Aim = false;
            m_CapsuleCollider.enabled = false;
            m_Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = false;
        }

        /// <summary>
        /// The character has respawned. Enable the necessary components.
        /// </summary>
        private void OnRespawn()
        {
            m_CapsuleCollider.enabled = true;
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            m_PrevYRotation = m_Transform.eulerAngles.y;
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = true;
        }
    }
}