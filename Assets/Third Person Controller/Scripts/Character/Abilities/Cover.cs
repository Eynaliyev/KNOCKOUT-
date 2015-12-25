using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Cover ability allows the character to take cover behind other objects.
    /// </summary>
    public class Cover : Ability
    {
        // The current Animator state that cover should be in.
        private enum CoverIDs { None = -1, StandStill, StandPopLeft, StandPopRight, CrouchStill, CrouchPopLeft, CrouchPopRight }

        [Tooltip("The maximum amount of distance that the character can take cover from")]
        [SerializeField] private float m_TakeCoverDistance = 0.5f;
        [Tooltip("The layers which the character can take cover on")]
        [SerializeField] private LayerMask m_CoverLayer;
        [Tooltip("The normalized speed that the character moves towards the cover point")]
        [SerializeField] private float m_MinMoveToTargetSpeed = 0.5f; 
        [Tooltip("The speed that the character can rotate while taking cover")]
        [SerializeField] private float m_TakeCoverRotationSpeed = 4;
        [Tooltip("The offset between the cover point and the point that the character should take cover at")]
        [SerializeField] private float m_CoverOffset = 0.05f;
        [Tooltip("Can move and continue to take cover behind objects as long as the new cover object has a normal angle difference less than this amount")]
        [SerializeField] private float m_CoverAngleThreshold = 1;
        [Tooltip("The normalized cover strafe speed")]
        [SerializeField] private float m_NormalizedStrafeSpeed = -0.5f;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private bool m_CanTakeCover;
        private bool m_UseCoverNormal;
        private Vector3 m_CoverNormal;
        private bool m_CoverCanMoveLeft;
        private bool m_CoverCanMoveRight;
        private bool m_PoppedFromCover;
        private CoverIDs m_CurrentCoverID = CoverIDs.None;
        private bool m_AgainstCover;
        private bool m_StandingCover;
        private bool m_CanPop;
        private bool m_ShouldPopFromCover;

        // Component references
        private CapsuleCollider m_CapsuleCollider;
        private HeightChange m_HeightChange;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_HeightChange = GetComponent<HeightChange>();
            m_CapsuleCollider = GetComponent<CapsuleCollider>();
        }

        /// <summary>
        /// Register for any events that the ability should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAnimatorAlignWithCover", OnAlignWithCover);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAnimatorPopFromCover", OnPopFromCover);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAbilityHeightChange", OnHeightChange);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
        }

        /// <summary>
        /// Unregister for any events that the ability was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnAnimatorAlignWithCover", OnAlignWithCover);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnAnimatorPopFromCover", OnPopFromCover);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnAbilityHeightChange", OnHeightChange);
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
        }

        /// <summary>
        /// Executed on every ability to allow the ability to update.
        /// </summary>
        public override void UpdateAbility()
        {
            // Notify interested objects when the cover status changes.
            var canTakeCover = CanStartAbility();
            if (canTakeCover != m_CanTakeCover) {
                m_CanTakeCover = canTakeCover;
                var abilityType = m_CanTakeCover ? Indicator : null;
                EventHandler.ExecuteEvent<Sprite>(m_GameObject, "OnControllerAbilityChange", abilityType);
            }
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The character can take cover if on the ground and near a cover object.
            return m_Controller.Grounded && Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, m_Transform.forward, out m_RaycastHit, m_TakeCoverDistance, m_CoverLayer.value);
        }

        /// <summary>
        /// Can the specified ability be started?
        /// </summary>
        /// <param name="ability">The ability that is trying to start.</param>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility(Ability ability)
        {
            // The HeightChange ability cannot start if there is only low cover available.
            if (ability is HeightChange && !m_StandingCover) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            m_CurrentCoverID = CoverIDs.None;
            m_Controller.ForceRootMotion = true;

            base.AbilityStarted();

            // The item colliders should be disabled to prevent interference with taking cover.
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnControllerEnableItemCollider", false);

            m_UseCoverNormal = false;
            m_CoverNormal = m_RaycastHit.normal;
            // Determine if the cover is high cover by firing a ray from the character's upper body.
            m_StandingCover = Physics.Raycast(m_Transform.position + m_CapsuleCollider.center + (Vector3.up * (m_CapsuleCollider.height / 2)), m_Transform.forward, out m_RaycastHit, m_TakeCoverDistance, m_CoverLayer.value);

            // Start moving to the cover point.
            var coverPoint = m_RaycastHit.point + m_RaycastHit.normal * m_CapsuleCollider.radius;
            coverPoint.y = m_Transform.position.y;
            MoveToTarget(coverPoint, m_Transform.rotation, m_MinMoveToTargetSpeed, InPosition);
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            if (m_CurrentCoverID == CoverIDs.None || (layer != m_AnimatorMonitor.GetLowerBodyLayerIndex() && layer != m_AnimatorMonitor.GetUpperBodyLayerIndex())) {
                return string.Empty;
            }
            if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                switch (m_CurrentCoverID) {
                    case CoverIDs.StandStill:
                        if (Quaternion.Angle(Quaternion.LookRotation(m_CoverNormal), m_Transform.rotation) < m_CoverAngleThreshold) {
                            return "Cover.Stand Strafe";
                        }
                        return "Cover.Take Standing Cover";
                    case CoverIDs.CrouchStill:
                        if (Quaternion.Angle(Quaternion.LookRotation(m_CoverNormal), m_Transform.rotation) < m_CoverAngleThreshold) {
                            return "Cover.Crouch Strafe";
                        }
                        return "Cover.Take Crouching Cover";
                    case CoverIDs.StandPopLeft:
                        return "Cover.Stand Pop Left Hold";
                    case CoverIDs.CrouchPopLeft:
                        return "Cover.Crouch Pop Left Hold";
                    case CoverIDs.StandPopRight:
                        return "Cover.Stand Pop Right Hold";
                    case CoverIDs.CrouchPopRight:
                        return "Cover.Crouch Pop Right Hold";
                }
            } else {
                // Standing cover is the only upper body state that has a unique cover animation.
                switch (m_CurrentCoverID) {
                    case CoverIDs.StandStill:
                        return m_AnimatorMonitor.FormatUpperBodyState("Cover Standing Movement");
                    case CoverIDs.CrouchStill:
                        return m_AnimatorMonitor.FormatUpperBodyState("Cover Crouching Movement");
                    case CoverIDs.StandPopLeft:
                    case CoverIDs.CrouchPopLeft:
                    case CoverIDs.StandPopRight:
                    case CoverIDs.CrouchPopRight:
                        return m_AnimatorMonitor.FormatUpperBodyState("Aim");
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// The character has arrived at the target position and the ability can start.
        /// </summary>
        private void InPosition()
        {
            // Take lower cover if crouching.
            if (m_StandingCover && m_HeightChange != null) {
                m_StandingCover = !m_HeightChange.IsActive;
            }
            if (m_StandingCover) {
                m_CurrentCoverID = CoverIDs.StandStill;
            } else {
                m_CurrentCoverID = CoverIDs.CrouchStill;
            }

            m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
            m_AnimatorMonitor.DetermineStates();

            // The character has arrived in cover. The character no longer needs to move and the user has control again.
            m_CoverCanMoveLeft = m_CoverCanMoveRight = true;
            m_AgainstCover = false;
            m_CanPop = false;
        }

        /// <summary>
        /// Moves the character according to the input.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        /// <returns>Should the RigidbodyCharacterController stop execution of its Move method?</returns>
        public override bool Move(ref float horizontalMovement, ref float forwardMovement, Quaternion lookRotation)
        {
            // Leave cover if the player tries to move backwards while in cover.
            if (m_Controller.InputVector.z < -0.1f) {
                StopAbility();
            }

            return m_CurrentCoverID == CoverIDs.None;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController stop execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public override bool CheckForExternalForces(float xPercent, float zPercent)
        {
            // If there is an external force then get out of cover.
            if ((m_PoppedFromCover || Quaternion.Angle(Quaternion.LookRotation(m_CoverNormal), m_Transform.rotation) < m_CoverAngleThreshold) &&
                (Mathf.Abs(m_Controller.Velocity.x * (1 - xPercent)) + Mathf.Abs(m_Controller.Velocity.z * (1 - zPercent))) > 0.5f) {
                StopAbility();
            }
            return false;
        }

        /// <summary>
        /// Only allow movement on the relative x axis to prevent the character from moving away from the cover point.
        /// </summary>
        /// <returns>Returns false to indicate that the movement should continue to be updated.</returns>
        public override bool UpdateMovement()
        {
            // There should be absolutely no root motion when popped from cover.
            if (m_PoppedFromCover) {
                m_Controller.RootMotionForce = Vector3.zero;
            } else {
                var coverNormalRotation = Quaternion.LookRotation(m_CoverNormal);
                var relativeForce = Quaternion.Inverse(coverNormalRotation) * m_Controller.RootMotionForce;
                relativeForce.z = 0;
                m_Controller.RootMotionForce = coverNormalRotation * relativeForce;

                // Stop taking standing cover if there is only crouching cover available.
                if (m_HeightChange != null && !m_HeightChange.IsActive && !m_CanPop) {
                    var canStand = Physics.Raycast(m_Transform.position + m_CapsuleCollider.center + (Vector3.up * m_CapsuleCollider.height), -m_CoverNormal, out m_RaycastHit, m_TakeCoverDistance, m_CoverLayer.value);
                    if (canStand != m_StandingCover) {
                        m_StandingCover = canStand;
                        m_CurrentCoverID += (int)CoverIDs.CrouchStill * (canStand ? -1 : 1);
                        m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
                        m_AnimatorMonitor.DetermineStates();
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            if (!m_PoppedFromCover) {
                m_Transform.rotation *= m_Controller.RootMotionRotation;
                m_Controller.RootMotionRotation = Quaternion.identity;
            }

            // While in cover always face away from the hit point.
            if (m_UseCoverNormal) {
                if (Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, -m_Transform.forward, out m_RaycastHit, m_CoverOffset + m_CapsuleCollider.radius / 2 + 0.01f, m_CoverLayer.value)) {
                    if (Quaternion.Angle(Quaternion.LookRotation(m_CoverNormal), Quaternion.LookRotation(m_RaycastHit.normal)) < m_CoverAngleThreshold) {
                        m_CoverNormal = m_RaycastHit.normal;
                    }
                }

                var coverRotation = Quaternion.LookRotation(m_CoverNormal);
                var rotation = Quaternion.Slerp(m_Transform.rotation, coverRotation, m_TakeCoverRotationSpeed * Time.deltaTime);
                m_AnimatorMonitor.SetYawValue(m_Controller.Aiming ? 0 : Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y));
                m_AgainstCover = Mathf.DeltaAngle(rotation.eulerAngles.y, m_Transform.eulerAngles.y) < m_CoverAngleThreshold;
                m_Transform.rotation = rotation;
            }
            return true;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>True so the cover ability completely takes control of the Animator.</returns>
        public override bool UpdateAnimator()
        {
            m_AnimatorMonitor.SetForwardInputValue(0, 0.1f);

            switch (m_CurrentCoverID) {
                case CoverIDs.StandStill:
                case CoverIDs.CrouchStill:
                    // While in cover the character can only strafe if there continues to be cover in the direction that the character should strafe.
                    var canMove = false;
                    if (m_Controller.InputVector.x > 0 && m_CoverCanMoveRight) {
                        canMove = CanMoveCoverDirection(true, m_CapsuleCollider.radius, m_CoverLayer.value);
                        if (!canMove) {
                            m_CoverCanMoveRight = false;
                        } else {
                            m_CoverCanMoveLeft = true;
                        }
                    } else if (m_Controller.InputVector.x < 0 && m_CoverCanMoveLeft) {
                        canMove = CanMoveCoverDirection(false, m_CapsuleCollider.radius, m_CoverLayer.value);
                        if (!canMove) {
                            m_CoverCanMoveLeft = false;
                        } else {
                            m_CoverCanMoveRight = true;
                        }
                    }
                    m_AnimatorMonitor.SetHorizontalInputValue(canMove ? m_Controller.InputVector.x * m_NormalizedStrafeSpeed : 0, 0.1f);

                    var popStatus = (!CanMoveCoverDirection(true, m_CapsuleCollider.radius + m_Controller.SkinWidth + m_CoverOffset, m_CoverLayer.value) ? CoverIDs.StandPopRight : 
                                    (!CanMoveCoverDirection(false, m_CapsuleCollider.radius + m_Controller.SkinWidth + m_CoverOffset, m_CoverLayer.value) ? CoverIDs.StandPopLeft : CoverIDs.StandStill));
                    m_CanPop = m_AgainstCover && popStatus != CoverIDs.StandStill && !CanMoveCoverDirection(!m_CoverCanMoveRight, m_CapsuleCollider.radius, -1);
                    if (m_CanPop && m_ShouldPopFromCover && m_UseCoverNormal) {
                        // The pop status will be for the standing states. Add to the status to covert it to the crouching state if the character is crouching.
                        if (!m_StandingCover) {
                            popStatus += (int)CoverIDs.CrouchStill;
                        }
                        m_CurrentCoverID = popStatus;
                        m_AnimatorMonitor.SetStateValue((int)popStatus);
                    }

                    break;
                case CoverIDs.StandPopLeft:
                case CoverIDs.StandPopRight:
                case CoverIDs.CrouchPopLeft:
                case CoverIDs.CrouchPopRight:
                    if (!m_ShouldPopFromCover && m_PoppedFromCover) {
                        if (m_StandingCover) {
                            m_CurrentCoverID = CoverIDs.StandStill;
                        } else {
                            m_CurrentCoverID = CoverIDs.CrouchStill;
                        }
                        m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
                    }
                    break;
            }

            return true;
        }

        /// <summary>
        /// Can the character move while in cover in the desired direction?
        /// </summary>
        /// <param name="right">Move to the right?</param>
        /// <param name="offset">An additional distance to apply to the collider radius.</param>
        /// <param name="layerValue">The layermask to check against.</param>
        /// <returns>True if there still exists cover in the desired direction.</returns>
        private bool CanMoveCoverDirection(bool right, float offset, int layerValue)
        {
            // Fire a raycast to the left or the right of the character. If it hits the cover layer then the character can keep moving in that direction
            var position = m_Transform.TransformPoint((m_CapsuleCollider.radius + offset) * (right ? -1 : 1), 0, 0) + m_CapsuleCollider.center;
            return Physics.SphereCast(position, m_CoverOffset, -m_Transform.forward, out m_RaycastHit, m_CapsuleCollider.radius + m_Controller.SkinWidth + m_CoverOffset + 0.1f, layerValue);
        }

        /// <summary>
        /// Callback when the cahracter starts or stops aiming.
        /// </summary>
        /// <param name="focus">Is the character aiming?</param>
        private void OnAim(bool aim)
        {
            m_ShouldPopFromCover = aim;
        }

        /// <summary>
        /// Callback from the animator. Will execute when the character is aligned with cover.
        /// </summary>
        private void OnAlignWithCover(bool align)
        {
            if (m_UseCoverNormal != align) {
                m_UseCoverNormal = align;
                if (align) {
                    m_AnimatorMonitor.DetermineUpperBodyState(true, false);
                }
            }
        }

        /// <summary>
        /// The character has popped from cover or returned from a pop. When this happens the character can pop/return again
        /// </summary>
        /// <param name="popped">True if the character has popped from cover.</param>
        private void OnPopFromCover(bool popped)
        {
            m_PoppedFromCover = popped;
            m_Controller.Aim = popped;
            if (!popped) {
                m_CoverCanMoveLeft = m_CoverCanMoveRight = true;
            } else {
                m_AnimatorMonitor.DetermineUpperBodyState(true, false);
            }
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <returns>True if comparing against the lower body layer. The upper body layer should update normally.</returns>
        public override bool HasAnimatorControl(int layer)
        {
            return true;
        }

        /// <summary>
        /// An item can be used if the character can pop from cover.
        /// </summary>
        /// <returns>True if the character can pop from cover.</returns>
        public override bool CanUseItem()
        {
            return m_PoppedFromCover;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer) {
            if (layer == m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                return m_PoppedFromCover;
            }
            return true;
        }

        /// <summary>
        /// The height change ability has started or stopped. Change cover states to reflect the change.
        /// </summary>
        /// <param name="active">Did the ability start?</param>
        private void OnHeightChange(bool active)
        {
            // Adding CoverIDs.CrouchStill will toggle the state ids between standing and crouching.
            m_CurrentCoverID += (int)CoverIDs.CrouchStill * (active ? 1 : -1);
            m_AnimatorMonitor.SetStateValue((int)m_CurrentCoverID);
            m_StandingCover = !active;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_Controller.ForceRootMotion = false;
            m_CurrentCoverID = CoverIDs.None;
            m_PoppedFromCover = false;
            EventHandler.ExecuteEvent(m_GameObject, "OnControllerLeaveCover");
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnControllerEnableItemCollider", true);
        }
    }
}