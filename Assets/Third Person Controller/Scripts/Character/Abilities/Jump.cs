using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Jump ability allows the character to jump into the air. Jump is only active when the character has a positive y velocity.
    /// </summary>
    public class Jump : Ability
    {
        [Tooltip("The amount of force that should be applied when the character jumps")]
        [SerializeField] private float m_Force = 5;
        [Tooltip("The force to apply for a double jump. 0 To indicate that a double jump is not possible")]
        [SerializeField] private float m_DoubleJumpForce;
        [Tooltip("Prevent the character from jumping too quickly after jumping")]
        [SerializeField] private float m_RecurrenceDelay = 0.2f;
        [Tooltip("Determines the correct leg to jump off of")]
        [SerializeField] private float m_RunCycleLegOffset = 0.2f;

        // Internal variables
        private bool m_Grounded;
        private bool m_Jumping;
        private float m_LandTime = -1;
        private bool m_FrameWait;
        private bool m_DoubleJumped;
        private float m_JumpForce;

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

        /// <summary>
        /// Register for any events that the ability should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerGrounded", OnGrounded);
        }

        /// <summary>
        /// Unregister for any events that the ability was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnControllerGrounded", OnGrounded);
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return m_Grounded && m_LandTime + m_RecurrenceDelay < Time.time;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorStartJump", OnStartJump);

            // Tell the ControllerHandler to listen for the double jump event.
            if (m_DoubleJumpForce != 0) {
                EventHandler.RegisterEvent(m_GameObject, "OnJumpAbilityDoubleJump", OnDoubleJump);
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityRegisterInput", InputName, "OnJumpAbilityDoubleJump");
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // The ability only affects the lower and upper layers.
            if (layer != m_AnimatorMonitor.GetLowerBodyLayerIndex() && layer != m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                return string.Empty;
            }

            var stateName = string.Empty;

            if (Mathf.Abs(m_AnimatorMonitor.ForwardInputValue) < 0.1f) {
                m_AnimatorMonitor.SetIntDataValue(0);
                stateName = "Jump Start";
            } else {
                // Calculate which leg is behind, so as to leave that leg trailing in the jump animation.
                // This code is reliant on the specific run cycle offset in our animations,
                // and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5.
                var runCycle = Mathf.Repeat(m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + m_RunCycleLegOffset, 1);
                if (runCycle < 0.5f) {
                    m_AnimatorMonitor.SetIntDataValue(1);
                    stateName = "Jump Up Left";
                } else {
                    m_AnimatorMonitor.SetIntDataValue(2);
                    stateName = "Jump Up Right";
                }
            }

            if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                return "Jump." + stateName;
            }
            return m_AnimatorMonitor.FormatUpperBodyState(stateName);
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_FrameWait = false;
            m_Jumping = false;
            m_DoubleJumped = false;
            m_JumpForce = 0;
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorStartJump", OnStartJump);

            // No longer listen for the double jump event.
            if (m_DoubleJumpForce != 0) {
                EventHandler.UnregisterEvent(m_GameObject, "OnJumpAbilityDoubleJump", OnDoubleJump);
                EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnregisterInput", InputName, "OnJumpAbilityDoubleJump");
            }
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
            if (m_Jumping) {
                // Wait a frame to allow the Rigidbody to react to the newly added force.
                if (m_FrameWait) {
                    m_FrameWait = false;
                    return true;
                }
                if (m_JumpForce != 0) {
                    var velocity = m_Controller.Velocity;
                    velocity.y += m_JumpForce;
                    m_Controller.Velocity = velocity;
                    m_JumpForce = 0;
                }
                // The Jump ability is done if the velocity is less than a small value. Use a non-zero small value because the jump should stop if the character is 
                // intersecting with another object. The velocity will still be positive by the jump ability should end.
                if (m_Controller.Velocity.y <= 0.001f) {
                    StopAbility();
                }
                // Set the Float Data parameter for the blend tree.
                m_AnimatorMonitor.SetFloatDataValue(m_Controller.Velocity.y);
            }
            return false;
        }

        /// <summary>
        /// Prevents a ground check while the character is getting ready to jump. This prevents the character from sticking to the ground.
        /// </summary>
        /// <returns>True if the character hasn't started their jump yet.</returns>
        public override bool CheckGround()
        {
            return !m_Jumping;
        }

        /// <summary>
        /// Set the physic material based on the grounded and stepping state.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its SetPhysicsMaterial method?</returns>
        public override bool SetPhysicMaterial()
        {
            m_CapsuleCollider.material = m_Controller.AirFrictionMaterial;
            return true;
        }

        /// <summary>
        /// The character has either landed or just left the ground.
        /// </summary>
        /// <param name="grounded">Is the character on the ground?</param>
        private void OnGrounded(bool grounded)
        {
            m_Grounded = grounded;
            if (m_Grounded) {
                if (m_Jumping) {
                    StopAbility();
                }
                // Remember the land time to prevent jumping more than the JumpReoccuranceDelay.
                m_LandTime = Time.time;
                m_Jumping = false;
            }
        }

        /// <summary>
        /// The start jump animation has finished playing so now the Rigidbody should have a force added to it.
        /// </summary>
        private void OnStartJump()
        {
            if (!m_Jumping) {
                m_Jumping = true;
                m_FrameWait = true;
                m_JumpForce = m_Force;
            }
        }

        /// <summary>
        /// Perform a double jump.
        /// </summary>
        private void OnDoubleJump()
        {
            // Do not allow multiple double jumps.
            if (!m_DoubleJumped) {
                m_DoubleJumped = true;
                m_JumpForce = m_DoubleJumpForce;
                // Optionally allow an animation to play when the character double jumps.
                m_AnimatorMonitor.SetIntDataValue(3);
            }
        }
    }
}