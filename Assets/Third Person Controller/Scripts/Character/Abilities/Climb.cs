using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Climb ability allows the character to climb ladders, pipes, and vines.
    /// </summary>
    public class Climb : Ability
    {
        // There are a lot of different states that the climb ability can be in. The climb ability supports a ladder, vine, and pipe. The following are valid states for each climb type:
        // Ladder:
        //          MountTop, MountBottom, ClimbLeftUp, ClimbRightUp, ClimbLeftDown, ClimbLeftUp, DismountTopLeft, DismountTopRight, DismountBottomLeft, DismountBottomRight
        //
        // Vine:
        //          MountTop, MountBottom, ClimbVine, DismountTop, DismountBottom
        //
        // Pipe:
        //          MountBottom, ClimbUp, ClimbDown, VerticalHorizontalLeftTransition, VerticalHorizontalRightTransition, ClimbHorizontalForwardRight, ClimbHorizontalBackwardRight
        //          ClimbHorizontalForwardLeft, ClimbHorizontalBackwardRight, HorizontalVerticalBackwardLeftTransition, HorizontalVerticalBackwardRightTransition, 
        //          HorizontalVerticalForwardLeftTransition, HorizontalVerticalForwardRightTransition, DismountBottom
        private enum ClimbIDs { None = -1, MountTop, MountBottom, DismountTop, DismountBottom,
                                ClimbLeftUp, ClimbRightUp, ClimbLeftDown, ClimbRightDown, ClimbUp, ClimbDown, ClimbVine,
                                VerticalHorizontalLeftTransition, VerticalHorizontalRightTransition,
                                HorizontalVerticalBackwardLeftTransition, HorizontalVerticalBackwardRightTransition, HorizontalVerticalForwardLeftTransition, HorizontalVerticalForwardRightTransition,
                                ClimbHorizontalForwardRight, ClimbHorizontalBackwardRight, ClimbHorizontalForwardLeft, ClimbHorizontalBackwardLeft,
                                DismountTopLeft, DismountTopRight, DismountBottomLeft, DismountBottomRight }

        [Tooltip("The layers that can be climbed")]
        [SerializeField] private LayerMask m_ClimbableLayer;
        [Tooltip("Start climbing when the angle between the character and the climbable object is less than this amount")]
        [SerializeField] private float m_StartClimbMaxAngle = 15;
        [Tooltip("Start climbing when the distance between the character and the climbable object is less than this amount")]
        [SerializeField] private float m_StartClimbMaxDistance = 0.5f;
        [Tooltip("The normalized speed to move to the start climbing position")]
        [SerializeField] private float m_MinMoveToTargetSpeed = 0.5f; 
        [Tooltip("The radius of the SphereCast to search for a climbable object")]
        [SerializeField] private float m_SearchRadius = 0.2f;
        [Tooltip("Should the character move relative to the character's look direction?")]
        [SerializeField] private bool m_RelativeLookDirectionMovement;

        // Internal Variables
        private bool m_ShowIndicator;
        private RaycastHit m_RaycastHit;
        private Vector3 m_ClimbNormal;
        private Vector3 m_StartNormal;
        private Vector3 m_RaycastHitPosition;
        private ClimbIDs m_ClimbID = ClimbIDs.None;
#if UNITY_EDITOR || !UNITY_WEBPLAYER
        private Vector3 m_MountDistance;
        private float m_MountStartTime;
#endif
        private Vector3 m_MountStartPosition;

        private bool m_AcceptInput;
        private bool m_RightFootUp;
        private bool m_Mounted;
        private bool m_Transitioning;
        private bool m_Vertical;
        private bool m_RightTransition;
        private bool m_RightSide;
        private Transform m_TargetTransitionTransform;
        private float m_VerticalDistance;

        // SharedFields
#if !ENABLE_MULTIPLAYER
        private SharedMethod<bool> m_IsAI = null;
#endif
        private SharedMethod<bool, Vector3> m_TargetLookDirection = null;

        // Component references
        private CapsuleCollider m_CapsuleCollider;
        private ClimbableObject m_ClimbableObject;
        private Transform m_ClimbableTransform;
        private Rigidbody m_Rigidbody;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_CapsuleCollider = GetComponent<CapsuleCollider>();
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        protected override void Start()
        {
            base.Start();

#if !ENABLE_MULTIPLAYER
            if (!m_IsAI.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
#endif
        }

        /// <summary>
        /// Executed on every ability to allow the ability to update.
        /// </summary>
        public override void UpdateAbility()
        {
            var showIndicator = !IsActive && CanStartAbility();
            if (showIndicator != m_ShowIndicator) {
                m_ShowIndicator = showIndicator;
                var abilityType = m_ShowIndicator ? Indicator : null;
                EventHandler.ExecuteEvent<Sprite>(m_GameObject, "OnControllerAbilityChange", abilityType);
            }
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The character can climb if the character is on the ground and a climbable object is near.
            if (m_Controller.Grounded &&
                    Physics.SphereCast(m_Transform.position + Vector3.up * 0.1f - m_Transform.forward * m_SearchRadius, m_SearchRadius, m_Transform.forward, out m_RaycastHit, m_StartClimbMaxDistance + m_SearchRadius, m_ClimbableLayer.value)) {
                // The character must be mostly looking at the climbable object.
                if (Vector3.Angle(-m_RaycastHit.normal, m_Transform.forward) < m_StartClimbMaxAngle) {
                    if ((m_ClimbableObject = (m_ClimbableTransform = m_RaycastHit.transform).GetComponent<ClimbableObject>()) != null) {
                        m_ClimbableObject.Mount(m_Transform);
                        if (m_ClimbableObject.GetComponent<BoxCollider>() != null) {
                            m_StartNormal = m_ClimbableTransform.forward * (Mathf.Sign(m_ClimbableTransform.InverseTransformPoint(m_Transform.position).z) == -1 ? 1 : -1);
                            m_ClimbNormal = m_ClimbableTransform.forward * (m_ClimbableObject.CanReverseMount && Mathf.Sign(m_ClimbableTransform.InverseTransformPoint(m_Transform.position).z) == -1 ? 1 : -1);
                            m_MountStartPosition = m_ClimbableObject.MountPosition(false);
                        } else {
                            m_ClimbNormal = m_StartNormal = -m_RaycastHit.normal;
                            m_StartNormal.y = 0;
                            m_StartNormal.Normalize();
                            // If there is no BoxCollider then the start position is based off of the raycast hit position.
                            m_MountStartPosition = m_RaycastHit.point + Quaternion.LookRotation(-m_StartNormal) * m_ClimbableObject.MountPosition(true);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            // The character isn't in position yet.
            m_AnimatorMonitor.DetermineStates(false);
            m_CapsuleCollider.enabled = false;
            m_Vertical = true;
            m_Controller.ForceRootMotion = true;
            m_Rigidbody.isKinematic = true;

            // Move into climb position.
            MoveToTarget(m_MountStartPosition, Quaternion.LookRotation(m_StartNormal), m_MinMoveToTargetSpeed, InPosition);

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbStateComplete", OnClimbStateComplete);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbTransitionComplete", OnClimbTransitionComplete);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbAutomaticMount", OnClimbAutomaticMount);
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            m_ClimbID = m_ClimbableObject.TopMount() ? ClimbIDs.MountTop : ClimbIDs.MountBottom;
            m_AnimatorMonitor.SetStateValue((int)m_ClimbID);
            m_AnimatorMonitor.DetermineStates();
            m_Controller.Grounded = false;
#if UNITY_EDITOR || !UNITY_WEBPLAYER
            m_MountDistance = Vector3.zero;
            m_MountStartTime = -1;
#endif
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbMount", OnMount);
        }

        /// <summary>
        /// The mount animation is done playing.
        /// </summary>
        private void OnMount()
        {
            m_AcceptInput = true;
            m_Mounted = true;
            m_RightFootUp = false;
            m_CapsuleCollider.enabled = true;
#if UNITY_EDITOR || !UNITY_WEBPLAYER
            m_MountDistance = Vector3.zero;
#endif

            if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Vine) {
                m_ClimbID = ClimbIDs.ClimbVine;
                m_AnimatorMonitor.DetermineStates();
            }

            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorClimbMount", OnMount);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (m_ClimbID == ClimbIDs.None || (layer != m_AnimatorMonitor.GetLowerBodyLayerIndex() && layer != m_AnimatorMonitor.GetUpperBodyLayerIndex())) {
                return string.Empty;
            }

            // Prefix the state name based on the climbable object type.
            var stateName = string.Empty;
            switch (m_ClimbableObject.Type) {
                case ClimbableObject.ClimbableType.Ladder:
                    stateName = "Ladder ";
                    break;
                case ClimbableObject.ClimbableType.Vine:
                    stateName = "Vine ";
                    break;
                case ClimbableObject.ClimbableType.Pipe:
                    stateName = "Pipe ";
                    break;
            }

            // Lots of different state possibilities.
            switch (m_ClimbID) {
                case ClimbIDs.MountTop:
                    stateName += "Top Mount";
                    break;
                case ClimbIDs.MountBottom:
                    stateName += "Bottom Mount";
                    break;
                case ClimbIDs.ClimbLeftUp:
                    stateName += "Left Up";
                    break;
                case ClimbIDs.ClimbRightUp:
                    stateName += "Right Up";
                    break;
                case ClimbIDs.ClimbLeftDown:
                    stateName += "Left Down";
                    break;
                case ClimbIDs.ClimbRightDown:
                    stateName += "Right Down";
                    break;
                case ClimbIDs.ClimbUp:
                    stateName += "Up";
                    break;
                case ClimbIDs.ClimbDown:
                    stateName += "Down";
                    break;
                case ClimbIDs.ClimbVine:
                    stateName += "Climb";
                    break;
                case ClimbIDs.VerticalHorizontalLeftTransition:
                    stateName += "Vertical to Horizontal Left";
                    break;
                case ClimbIDs.VerticalHorizontalRightTransition:
                    stateName += "Vertical to Horizontal Right";
                    break;
                case ClimbIDs.HorizontalVerticalBackwardLeftTransition:
                    stateName += "Horizontal to Vertical Backward Left";
                    break;
                case ClimbIDs.HorizontalVerticalBackwardRightTransition:
                    stateName += "Horizontal to Vertical Backward Right";
                    break;
                case ClimbIDs.HorizontalVerticalForwardLeftTransition:
                    stateName += "Horizontal to Vertical Forward Left";
                    break;
                case ClimbIDs.HorizontalVerticalForwardRightTransition:
                    stateName += "Horizontal to Vertical Forward Right";
                    break;
                case ClimbIDs.ClimbHorizontalForwardRight:
                    stateName += "Horizontal Forward Right";
                    break;
                case ClimbIDs.ClimbHorizontalBackwardRight:
                    stateName += "Horizontal Backward Right";
                    break;
                case ClimbIDs.ClimbHorizontalForwardLeft:
                    stateName += "Horizontal Forward Left";
                    break;
                case ClimbIDs.ClimbHorizontalBackwardLeft:
                    stateName += "Horizontal Backward Left";
                    break;
                case ClimbIDs.DismountTop:
                    stateName += "Top Dismount";
                    break;
                case ClimbIDs.DismountBottom:
                    stateName += "Bottom Dismount";
                    break;
                case ClimbIDs.DismountTopLeft:
                    stateName += "Top Dismount Left";
                    break;
                case ClimbIDs.DismountTopRight:
                    stateName += "Top Dismount Right";
                    break;
                case ClimbIDs.DismountBottomLeft:
                    stateName += "Bottom Dismount Left";
                    break;
                case ClimbIDs.DismountBottomRight:
                    stateName += "Bottom Dismount Right";
                    break;
            }
            return "Climb." + stateName;
        }

        /// <summary>
        /// Returns the duration of the state transition.
        /// </summary>
        /// <returns>The duration of the state transition.</returns>
        public override float GetTransitionDuration()
        {
            // Do a normal crossfade if mounting or dismounting. Everything else should be instant.
            if (!m_Mounted) {
                return base.GetTransitionDuration();
            }

            return 0;
        }

        /// <summary>
        /// Can the ability replay animation states?
        /// </summary>
        public override bool CanReplayAnimationStates()
        {
            return true;
        }

        /// <summary>
        /// Prevent the controller from having control when the MoveToTarget coroutine is updating.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        /// <returns>True if the character is moving into the mount position.</returns>
        public override bool Move(ref float horizontalMovement, ref float forwardMovement, Quaternion lookRotation)
        {
            if (!m_Mounted) {
                // Return early if the character isn't in climb position yet.
                horizontalMovement = forwardMovement = 0;
                if (m_ClimbID == ClimbIDs.None) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Perform checks to determine if the character is on the ground.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its CheckGround method?</returns>
        public override bool CheckGround()
        {
            return true;
        }

        /// <summary>
        /// Ensures the current movement is valid.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its CheckMovement method?</returns>
        public override bool CheckMovement()
        {
            return true;
        }

        /// <summary>
        /// Move vertically with the climbable object.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            // While on a climbable object only one relative axis needs to move. While climbing vertically the x and z are constant, and while climbing horizontally the y and z are constant.
            var relativeForce = Quaternion.Inverse(Quaternion.LookRotation(m_ClimbNormal)) * m_Controller.RootMotionForce;
            if (m_ClimbID == ClimbIDs.MountBottom || m_ClimbID == ClimbIDs.MountTop) {
                relativeForce.x = 0;
            } else if (m_ClimbID == ClimbIDs.ClimbLeftDown || m_ClimbID == ClimbIDs.ClimbRightDown || m_ClimbID == ClimbIDs.ClimbLeftUp || m_ClimbID == ClimbIDs.ClimbRightUp || m_ClimbID == ClimbIDs.ClimbUp || m_ClimbID == ClimbIDs.ClimbDown) {
                relativeForce.x = relativeForce.z = 0;
            } else if (m_ClimbID == ClimbIDs.ClimbHorizontalForwardRight || m_ClimbID == ClimbIDs.ClimbHorizontalBackwardRight || 
                       m_ClimbID == ClimbIDs.ClimbHorizontalForwardLeft || m_ClimbID == ClimbIDs.ClimbHorizontalBackwardLeft) {
                relativeForce.y = relativeForce.z = 0;
            } else if (m_ClimbID == ClimbIDs.ClimbVine) {
                relativeForce.z = 0;
            }

            // Prevent the character from moving too far beyond the vertical pipe from a horizontal transition.
            if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Pipe && m_Transitioning && !m_Vertical) {
                var requirePositive = false;
                if ((m_RightSide && (m_ClimbID == ClimbIDs.HorizontalVerticalForwardRightTransition || m_ClimbID == ClimbIDs.HorizontalVerticalForwardLeftTransition)) ||
                    (!m_RightSide && (m_ClimbID == ClimbIDs.HorizontalVerticalBackwardRightTransition || m_ClimbID == ClimbIDs.HorizontalVerticalBackwardLeftTransition))) {
                    requirePositive = true;
                }
                if ((requirePositive && m_TargetTransitionTransform.InverseTransformPoint(m_Transform.position).x < 0) ||
                    (!requirePositive && m_TargetTransitionTransform.InverseTransformPoint(m_Transform.position).x > 0)) {
                    relativeForce.x = 0;
                }
            // Similar to the pipe, prevent the character from moving beyond the rung separation on the ladder.
            } else if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Ladder && m_Mounted) {
                relativeForce.y = Mathf.Min(Mathf.Abs(relativeForce.y), Mathf.Abs(m_ClimbableObject.RungSeparation - m_VerticalDistance)) * Mathf.Sign(relativeForce.y);
                m_VerticalDistance += Mathf.Abs(relativeForce.y);
            }

            // When the character mounts the root motion position delta isn't precise because it will be transitioning from the previous animation. Move the character
            // manually based off of the animation clip time. This will ensure the character is always precisly lined up with the mount position.
            var manualMove = false;
            // Unity WebPlayer does not implement AnimatorClip.averageSpeed.
#if UNITY_EDITOR || !UNITY_WEBPLAYER
            if (!m_Mounted && (m_ClimbID == ClimbIDs.MountBottom || m_MountDistance.sqrMagnitude != 0)) {
                // Determine the total distance that the character travels in the mounting clip. This distance only needs to be determined once each time the character mounts.
                if (m_MountDistance.sqrMagnitude < 0.01f && m_Controller.RootMotionForce.sqrMagnitude > 0 && !m_Animator.IsInTransition(m_AnimatorMonitor.GetLowerBodyLayerIndex())) {
#if UNITY_4_6
                    var mountClip = m_Animator.GetCurrentAnimationClipState(m_AnimatorMonitor.GetLowerBodyLayerIndex());
#else
                    var mountClip = m_Animator.GetCurrentAnimatorClipInfo(m_AnimatorMonitor.GetLowerBodyLayerIndex());
#endif
                    if (mountClip.Length > 0) {
                        for (int i = 0; i < mountClip.Length; ++i) {
                            m_MountDistance += mountClip[i].clip.averageSpeed;
                        }
                        m_MountDistance /= mountClip.Length;

                        // Mounting will always have some sort of vertical displacement. If it doesn't then it isn't the mount animation that is playing.
                        if (Mathf.Abs(m_MountDistance.y) < 0.01f) {
                            m_MountDistance = Vector3.zero;
                        }
                        // While mounting from the bottom of a ladder do not use the vertical speed because the distance is already known through the rung separation. 
                        else if (m_ClimbID == ClimbIDs.MountBottom && m_ClimbableObject.Type == ClimbableObject.ClimbableType.Ladder) {
                            m_MountDistance.y = m_ClimbableObject.RungSeparation;
                        }
                    } else {
                        Debug.LogError("Error: No mounting animation clip has been specified.");
                    }
                }

                // Once the distance has been determined move the character to the target position.
                if (m_MountDistance.sqrMagnitude > 0.01f) {
                    var mountState = m_Animator.GetCurrentAnimatorStateInfo(m_AnimatorMonitor.GetLowerBodyLayerIndex());
                    if (m_MountStartTime == -1) {
                        m_MountStartTime = mountState.normalizedTime;
                    }
                    var targetPosition = m_MountStartPosition + (Quaternion.LookRotation(m_ClimbNormal) * m_MountDistance) * Mathf.Clamp01((mountState.normalizedTime - m_MountStartTime) / (1 - m_MountStartTime));
                    m_Controller.SetPosition(targetPosition);
                }
                // The movement will be done per frame so root motion is not necessary.
                manualMove = true;
            }
#endif
            if (!manualMove) {
                relativeForce = Quaternion.LookRotation(m_ClimbNormal) * relativeForce;
                m_Controller.SetPosition(m_Transform.position + relativeForce);
            }
            m_Controller.RootMotionForce = Vector3.zero;

            return true;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // Face the climbable object.
            if (m_Mounted) {
                if (Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_StartClimbMaxDistance + m_CapsuleCollider.radius * 2, m_ClimbableLayer.value)) {
                    if (Vector3.Angle(-m_RaycastHit.normal, m_Transform.forward) < m_StartClimbMaxAngle) {
                        m_ClimbNormal = -m_RaycastHit.normal;
                    }
                }
                var targetRotation = m_Transform.eulerAngles;
                targetRotation.y = Quaternion.LookRotation(m_ClimbNormal).eulerAngles.y;
                m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.Euler(targetRotation), m_Controller.RotationSpeed * Time.deltaTime);
            }

            // Rotate according to the animations.
            m_Transform.rotation *= m_Controller.RootMotionRotation;
            m_Controller.RootMotionRotation = Quaternion.identity;

            return true;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController stop execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public override bool CheckForExternalForces(float xPercent, float zPercent)
        {
            return true;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            // Do not change animation states if the mount, dismount, or transition animations are playing.
            if (!m_Mounted || m_Transitioning) {
                return true;
            }

            var moveType = ClimbableObject.MoveType.None;
            // Vines use a blend tree so they don't require as much logic to change states.
            if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Vine) {
                // Check for a top or bottom dismount.
                var input = m_RelativeLookDirectionMovement ? m_TargetLookDirection.Invoke(false) : m_Controller.InputVector;
                if (m_RelativeLookDirectionMovement ? (m_Controller.InputVector.z > 0.1f && (m_Vertical ? input.y > -0.2f : input.z < 0f)) : input.z > 0.1f) {
                    moveType = ClimbableObject.MoveType.Up;
                } else if (m_RelativeLookDirectionMovement ? (m_Controller.InputVector.z > 0.1f && (m_Vertical ? input.y < -0.2f : input.z > 0f)) : input.z < -0.1f) {
                    moveType = ClimbableObject.MoveType.Down;
                }
                if (m_ClimbableObject.CanDismount(moveType, true)) {
                    StartDismount(moveType);
                    return true;
                }

                var canMove = true;
                // The character should not dismount. Ensure the left or right movement is valid.
                if (Mathf.Abs(m_Controller.InputVector.x) > 0.01f) {
                    if (Physics.Raycast(m_Transform.TransformPoint(m_ClimbableObject.HorizontalPadding * (m_Controller.InputVector.x < -0.01f ? -1 : 1), 0, 0), m_Transform.forward, out m_RaycastHit, m_StartClimbMaxDistance + m_CapsuleCollider.radius * 2, m_ClimbableLayer.value)) {
                        canMove = m_RaycastHit.transform.Equals(m_ClimbableTransform);
                    } else {
                        canMove = false;
                    }
                }

                // Set the parameters for the blend tree if the character can move.
                m_AnimatorMonitor.SetHorizontalInputValue(canMove ? m_Controller.InputVector.x : 0);
                m_AnimatorMonitor.SetForwardInputValue(canMove ? input.z : 0); // Forward is "up".

                return true;
            }

            var changed = false;
            // Do not change animation states if a climbing state is current playing.
            if (m_AcceptInput) {
                var input = m_RelativeLookDirectionMovement ? m_TargetLookDirection.Invoke(false) : m_Controller.InputVector;
                // If not moving relative to the look direction, move up if vertical, otherwise forward if horizontal. If moving relatove to the look direction,
                // move when the z input is greater then a minimum value.
                if (m_RelativeLookDirectionMovement ? (m_Controller.InputVector.z > 0.1f && (m_Vertical ? input.y > -0.2f : input.z < 0f)) : input.z > 0.1f) {
                    if (m_Vertical) {
                        if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Pipe) {
                            m_ClimbID = ClimbIDs.ClimbUp;
                        } else {
                            // The ladder and vine require the feet to alternate while moving up.
                            if (m_RightFootUp) {
                                m_ClimbID = ClimbIDs.ClimbRightUp;
                            } else {
                                m_ClimbID = ClimbIDs.ClimbLeftUp;
                            }
                        }
                    } else {
                        // Move in the forward direction if horizontal.
                        if (m_RightTransition) {
                            m_ClimbID = ClimbIDs.ClimbHorizontalForwardRight;
                        } else {
                            m_ClimbID = ClimbIDs.ClimbHorizontalForwardLeft;
                        }
                    }
                    changed = true;
                } else if (m_RelativeLookDirectionMovement ? (m_Controller.InputVector.z > 0.1f && (m_Vertical ? input.y < -0.2f : input.z > 0f)) : input.z < -0.1f) {
                    // Move down if vertical, otherwise backward if horizontal.
                    if (m_Vertical) {
                        if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Pipe) {
                            m_ClimbID = ClimbIDs.ClimbDown;
                        } else {
                            // The ladder and vine require the feet to alternate while moving down.
                            if (m_RightFootUp) {
                                m_ClimbID = ClimbIDs.ClimbLeftDown;
                            } else {
                                m_ClimbID = ClimbIDs.ClimbRightDown;
                            }
                        }
                    } else {
                        // Move in the forward direction if horizontal.
                        if (m_RightTransition) {
                            m_ClimbID = ClimbIDs.ClimbHorizontalBackwardRight;
                        } else {
                            m_ClimbID = ClimbIDs.ClimbHorizontalBackwardLeft;
                        }
                    }
                    changed = true;
                }
            }

            // Map the ClimbID to the ClimbableObject MoveType so the climbable object knows which direction the character is moving.
            // This is used to determine if the character should dismount or transition.
            if (m_ClimbID == ClimbIDs.ClimbLeftUp || m_ClimbID == ClimbIDs.ClimbRightUp || m_ClimbID == ClimbIDs.ClimbUp) {
                moveType = ClimbableObject.MoveType.Up;
            } else if (m_ClimbID == ClimbIDs.ClimbLeftDown || m_ClimbID == ClimbIDs.ClimbRightDown || m_ClimbID == ClimbIDs.ClimbDown) {
                moveType = ClimbableObject.MoveType.Down;
            } else if (m_ClimbID == ClimbIDs.ClimbHorizontalBackwardRight || m_ClimbID == ClimbIDs.ClimbHorizontalBackwardLeft) {
                moveType = ClimbableObject.MoveType.HorizontalBackward;
            } else if (m_ClimbID == ClimbIDs.ClimbHorizontalForwardRight || m_ClimbID == ClimbIDs.ClimbHorizontalForwardLeft) {
                moveType = ClimbableObject.MoveType.HorizontalForward;
            }

            // Determine if the character can dismount, trasition, or should just move along the climbable object.
            if (m_ClimbableObject.CanDismount(moveType, changed)) {
                StartDismount(moveType);
            } else if (m_ClimbableObject.ShouldStartPipeTransition(moveType, m_Vertical, m_RightSide)) {
                if (!m_Vertical) {
                    m_TargetTransitionTransform = m_ClimbableObject.HorizontalVerticalTransitionTargetTransform();
                }
                StartTransition();
            } else if (changed) {
                m_AcceptInput = false;
                m_ClimbableObject.Move(moveType);
                m_VerticalDistance = 0;
                m_AnimatorMonitor.DetermineStates();
            }
            
            return true;
        }

        /// <summary>
        /// The Animator has changed positions or rotations.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its OnAnimatorMove method?</returns>
        public override bool AnimatorMove()
        {
            if (m_ClimbID == ClimbIDs.None) {
                return false;
            }

            // Move according to root motion.
            m_Controller.RootMotionForce = m_Animator.deltaPosition * m_Controller.RootMotionSpeedMultiplier;
            
            // Rotate according to root motion.
            m_Controller.RootMotionRotation *= m_Animator.deltaRotation;
            return true;
        }

        /// <summary>
        /// The character has finished climbing one state.
        /// </summary>
        private void OnClimbStateComplete()
        {
            if (!m_AcceptInput) {
                m_AcceptInput = true;
                m_RightFootUp = !m_RightFootUp;
            }
        }

        /// <summary>
        /// The character should start to transition between a vertical and horizontal climb.
        /// </summary>
        private void StartTransition()
        {
            // The character should toggle between a vertical and horizontal transition.
            if (m_Vertical) {
                if (m_RightTransition = m_ClimbableObject.ShouldTransitionRight()) {
                    m_ClimbID = ClimbIDs.VerticalHorizontalRightTransition;
                } else {
                    m_ClimbID = ClimbIDs.VerticalHorizontalLeftTransition;
                }
                m_RightSide = m_ClimbableObject.OnRightSide();
            } else {
                // Always transition to the same side of the object that the character started from.
                if (m_ClimbID == ClimbIDs.ClimbHorizontalBackwardLeft) {
                    m_ClimbID = ClimbIDs.HorizontalVerticalBackwardRightTransition;
                } else if (m_ClimbID == ClimbIDs.ClimbHorizontalBackwardRight) {
                    m_ClimbID = ClimbIDs.HorizontalVerticalBackwardLeftTransition;
                } else if (m_ClimbID == ClimbIDs.ClimbHorizontalForwardLeft) {
                    m_ClimbID = ClimbIDs.HorizontalVerticalForwardLeftTransition;
                } else {
                    m_ClimbID = ClimbIDs.HorizontalVerticalForwardRightTransition;
                }
            }

            m_AcceptInput = false;
            m_Transitioning = true;
            m_CapsuleCollider.enabled = false;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The character has transitioned between a vertical and horizontal climb.
        /// </summary>
        private void OnClimbTransitionComplete()
        {
            if (m_Transitioning) {
                m_Transitioning = false;
                m_AcceptInput = true;
                m_Vertical = !m_Vertical;
                m_CapsuleCollider.enabled = true;
            }
        }

        /// <summary>
        /// The mount animation is ready to switch from using root motion to moving the character's position based on the animation duration.
        /// </summary>
        private void OnClimbAutomaticMount()
        {
            m_MountStartPosition = m_Transform.position;
            // MountDistance is expected to be in local space. UpdateMovement will translate it back to world space.
#if !UNITY_EDITOR && !UNITY_WEBPLAYER
            m_MountDistance = Quaternion.Inverse(Quaternion.LookRotation(m_ClimbNormal)) * (m_ClimbableObject.TopMountCompletePosition() - m_MountStartPosition);
#endif
        }

        /// <summary>
        /// Start the dismounting animation from the climbable object.
        /// </summary>
        /// <param name="moveType">The type of movement that caused the dismount.</param>
        private void StartDismount(ClimbableObject.MoveType moveType)
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorClimbStateComplete", OnClimbStateComplete);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorClimbTransitionComplete", OnClimbTransitionComplete);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorClimbAutomaticMount", OnClimbAutomaticMount);

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbDismount", OnDismount);

            // The character is no longer mounted and the collider should be disabled to prevent it from getting in the way of the dismount.
            m_Mounted = false;
            m_CapsuleCollider.enabled = false;

            // Determine the type of dismount. Vines and pipes are easy to determine how to dismount.
            if (m_ClimbableObject.Type == ClimbableObject.ClimbableType.Vine || m_ClimbableObject.Type == ClimbableObject.ClimbableType.Pipe) {
                if (moveType == ClimbableObject.MoveType.Up) {
                    m_ClimbID = ClimbIDs.DismountTop;
                } else {
                    m_ClimbID = ClimbIDs.DismountBottom;
                }
            } else {
                // Ladders require more work to determine the correct dismount animation because the character can start to dismount off of either foot.
                if (m_ClimbID == ClimbIDs.ClimbUp || m_ClimbID == ClimbIDs.ClimbRightUp || m_ClimbID == ClimbIDs.ClimbLeftUp) {
                    if (m_RightFootUp) {
                        m_ClimbID = ClimbIDs.DismountTopRight;
                    } else {
                        m_ClimbID = ClimbIDs.DismountTopLeft;
                    }
                } else {
                    if (m_RightFootUp) {
                        m_ClimbID = ClimbIDs.DismountBottomLeft;
                    } else {
                        m_ClimbID = ClimbIDs.DismountBottomRight;
                    }
                }
            }

            // Start the dismount animation.
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The character has dismounted. End the ability.
        /// </summary>
        private void OnDismount()
        {
            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            // Reset the variables
            m_ClimbID = ClimbIDs.None;
            m_AcceptInput = m_Transitioning = m_Mounted = false;

            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorClimbDismount", OnDismount);
            m_CapsuleCollider.enabled = true;
            m_Controller.ForceRootMotion = false;
            m_Controller.Grounded = true;
            m_Rigidbody.isKinematic = false;
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <param name="layer">The layer to check against.</param>
        /// <returns>True if the character can run the ability.</returns>
        public override bool HasAnimatorControl(int layer)
        {
            return m_ClimbID != ClimbIDs.None;
        }

        /// <summary>
        /// Can the character have an item equipped while the ability is active?
        /// </summary>
        /// <returns>False to indicate that the character cannot have an item equipped.</returns>
        public override bool CanHaveItemEquipped()
        {
            return false;
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>False to indicate that the character cannot interact with an item while climbing.</returns>
        public override bool CanInteractItem()
        {
            return m_ClimbID == ClimbIDs.None;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>False to indicate that the IK should not be used.</returns>
        public override bool CanUseIK(int layer) { return false; }
    }
}