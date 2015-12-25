using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Ledge Strafe ability allows the character to place their back against a wall and strafe.
    /// </summary>
    public class LedgeStrafe : Ability
    {
        [Tooltip("The maximum amount of distance that the character can start to strafe from")]
        [SerializeField] private float m_StartStrafeDistance = 0.5f;
        [Tooltip("The layers that can be used to strafe on")]
        [SerializeField] private LayerMask m_StrafeLayer;
        [Tooltip("The normalized speed to move to the strafe point")]
        [SerializeField] private float m_MinMoveToTargetSpeed = 0.5f; 
        [Tooltip("The offset between the strafe point and the point that the character should strafe")]
        [SerializeField] private float m_StrafeOffset;
        [Tooltip("Can move and continue to strafe behind objects as long as the new strafe object has a normal angle difference less than this amount")]
        [SerializeField] private float m_StrafeAngleThreshold;
        [Tooltip("The speed that the character can rotate while strafing")]
        [SerializeField] private float m_StrafeRotationSpeed;

        // Internal variables
        private bool m_ShowIndicator;
        private RaycastHit m_RaycastHit;
        private bool m_RaycastDidHit;
        private Vector3 m_StrafeNormal;
        private bool m_CanStrafe;

        // Component references
        private CapsuleCollider m_CapsuleCollider;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_CapsuleCollider = GetComponent<CapsuleCollider>();
        }

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
            // The character can strafe if on the ground and near a strafe object.
            return m_Controller.Grounded && Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_StartStrafeDistance, m_StrafeLayer.value);
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            // While moving to the strafe point the player no longer has control until the character has arrived.
            m_AnimatorMonitor.SetHorizontalInputValue(0, 0);
            m_Controller.StopMovement();
            m_Controller.Moving = true;
            m_Controller.ForceRootMotion = true;
            m_CanStrafe = false;

            // Start moving to the strafe point.
            m_StrafeNormal = m_RaycastHit.normal;
            var targetPoint = m_RaycastHit.point + m_RaycastHit.normal * (m_CapsuleCollider.radius + m_StrafeOffset);
            targetPoint.y = m_Transform.position.y;

            MoveToTarget(targetPoint, m_Transform.rotation, m_MinMoveToTargetSpeed, InPosition);
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            m_CanStrafe = true;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (!m_CanStrafe || (layer != m_AnimatorMonitor.GetLowerBodyLayerIndex() && layer != m_AnimatorMonitor.GetUpperBodyLayerIndex())) {
                return string.Empty;
            }
            if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                return "Ledge Strafe.Movement";
            }
            return m_AnimatorMonitor.FormatUpperBodyState("Ledge Strafe Movement");
        }

        /// <summary>
        /// Prevent the controller from having control when the MoveToTarget coroutine is updating.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        /// <returns>True if the character is moving into position.</returns>
        public override bool Move(ref float horizontalMovement, ref float forwardMovement, Quaternion lookRotation)
        {
            // Leave strafe if the player tries to move backwards while in cover or is no longer on the ground.
            if (m_Controller.InputVector.z < -0.1f || !m_Controller.Grounded) {
                StopAbility();
            }

            return !m_CanStrafe;
        }

        /// <summary>
        /// Only allow movement on the relative x axis to prevent the character from moving away from the strafe object.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            var coverNormalRotation = Quaternion.LookRotation(m_StrafeNormal);
            var relativeForce = Quaternion.Inverse(coverNormalRotation) * m_Controller.RootMotionForce;
            relativeForce.z = 0;
            if ((m_RaycastDidHit = Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, -m_StrafeNormal, out m_RaycastHit, m_StrafeOffset + (m_CapsuleCollider.radius * 2), m_StrafeLayer.value))) {
                // Keep the character sticking to the wall by applying a small backward force.
                if (m_RaycastHit.distance > m_StrafeOffset + m_CapsuleCollider.radius) {
                    relativeForce.z -= 0.01f;
                }
            }
            m_Controller.RootMotionForce = coverNormalRotation * relativeForce;
            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // Ensure the strafe normal is correct.
            if (m_RaycastDidHit) {
                if (Quaternion.Angle(Quaternion.LookRotation(m_StrafeNormal), Quaternion.LookRotation(m_RaycastHit.normal)) < m_StrafeAngleThreshold) {
                    m_StrafeNormal = m_RaycastHit.normal;
                }
            }
            // Rotate to face in the same direction as the strafe normal.
            var rotation = Quaternion.Slerp(m_Transform.rotation, Quaternion.LookRotation(m_StrafeNormal), m_StrafeRotationSpeed * Time.deltaTime);
            m_AnimatorMonitor.SetYawValue(m_Controller.Aiming ? 0 : Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y));
            m_Controller.SetRotation(rotation);
            return true;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            // The character may not be able to move if there is no strafe object to back up against.
            var canMove = CanMoveDirection(m_Controller.InputVector.x < 0);
            m_AnimatorMonitor.SetHorizontalInputValue(canMove ? -m_Controller.InputVector.x : 0);
            m_AnimatorMonitor.SetForwardInputValue(0);
            // Update the controller to indicate if the character is moving.
            if (m_Controller.Moving) {
                m_Controller.Moving = m_Controller.Velocity.sqrMagnitude > 0.01f || Mathf.Abs(m_Controller.InputVector.x) > 0.01f;
            } else if (canMove) {
                m_Controller.Moving = Mathf.Abs(m_Controller.InputVector.x) > 0.01f;
            }

            return true;
        }

        /// <summary>
        /// Can the character move in the requested direction?
        /// </summary>
        /// <param name="right">Move in the relative right direction?</param>
        /// <returns>True if the character can move.</returns>
        private bool CanMoveDirection(bool right)
        {
            var position = m_Transform.TransformPoint((m_CapsuleCollider.radius + m_Controller.SkinWidth) * (right ? 1 : -1), 0, 0) + m_CapsuleCollider.center;
            return Physics.Raycast(position, -m_Transform.forward, out m_RaycastHit, m_CapsuleCollider.radius + m_Controller.SkinWidth + m_StrafeOffset * 2, m_StrafeLayer.value);
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();
            m_CanStrafe = false;

            m_Controller.ForceRootMotion = false;
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return !m_CanStrafe;
        }
    }
}