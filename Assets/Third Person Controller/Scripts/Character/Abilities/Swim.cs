using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Swim ability allows the character to swim while above water. The Swim ability activates as soon as the character enters the water even though they may not be swimming
    /// yet. It will allow normal character movement until the water reaches a predefined depth.
    /// </summary>
    public class Swim : Ability
    {
        // The list of enums that the swimming Animator states can be in
        private enum SwimIDs { Swim, End, None }

        [Tooltip("The amount of resistance to apply while moving")]
        [SerializeField] private float m_WaterResistance = 0.15f;
        [Tooltip("The speed that the character can rotate")]
        [SerializeField] private float m_RotationSpeed = 1;
        [Tooltip("The water depth to start swimming at")]
        [SerializeField] private float m_SwimDepth = 0.5f;
        [Tooltip("The vertical camera offset while swimming")]
        [SerializeField] private float m_SwimCameraOffset = -0.5f;
        [Tooltip("The amount of time that has to elapse before the character can transition between starting and stopping swimming again")]
        [SerializeField] private float m_TransitionGracePeriod = 0.2f;
        [Tooltip("Reference to the splash ParticleSystem on the left hand (optional)")]
        [SerializeField] private ParticleSystem m_LeftHandSplashParticle;
        [Tooltip("Reference to the splash ParticleSystem on the right hand (optional)")]
        [SerializeField] private ParticleSystem m_RightHandSplashParticle;
        [Tooltip("Reference to the splash ParticleSystem on the left foot (optional)")]
        [SerializeField] private ParticleSystem m_LeftFootSplashParticle;
        [Tooltip("Reference to the splash ParticleSystem on the right foot (optional)")]
        [SerializeField] private ParticleSystem m_RightFootSplashParticle;

        // Internal variables
        private RaycastHit m_RaycastHit;
        private SwimIDs m_SwimID;
        private bool m_Equipped;
        private float m_TransitionTime;
        private bool m_JumpIn;

        // Component references
        private Rigidbody m_Rigidbody;
        private CapsuleCollider m_CapsuleCollider;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody>();
            m_CapsuleCollider = GetComponent<CapsuleCollider>();
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            m_SwimID = SwimIDs.None;

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorSwimStop", StopSwim);

            // Determine if the character is starting to swim by walking or jumping in.
            if (Physics.Raycast(m_Transform.position + Vector3.up * m_CapsuleCollider.height, Vector3.down, out m_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                m_JumpIn = !Physics.Raycast(m_RaycastHit.point, Vector3.down, m_SwimDepth, LayerManager.Mask.IgnoreInvisibleLayersPlayerWater);
            }

            base.AbilityStarted();
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
            // Determine if the character should swim. Cast a ray down from the player's position to get the height of the water. Cast one more ray down from the
            // water's position to determine the height of the water. If no ground was hit then the character should swim.
            if (m_TransitionTime + m_TransitionGracePeriod < Time.time && Physics.Raycast(m_Transform.position + Vector3.up * m_CapsuleCollider.height, Vector3.down, out m_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                var swim = !Physics.Raycast(m_RaycastHit.point, Vector3.down, m_SwimDepth, LayerManager.Mask.IgnoreInvisibleLayersPlayerWater) && (m_RaycastHit.point.y - m_Transform.position.y > m_SwimDepth - 0.01f);
                if (swim && m_SwimID == SwimIDs.None) {
                    m_TransitionTime = Time.time;
                    m_SwimID = SwimIDs.Swim;
                    m_Controller.StopMovement();
                    m_Rigidbody.useGravity = false;
                    m_AnimatorMonitor.DetermineStates();

                    // Keep the camera above water.
                    EventHandler.ExecuteEvent<float>(m_GameObject, "OnCameraHeightOffset", m_SwimCameraOffset);

                    // The item cannot be equipped.
                    if (m_ItemEquipped.Get()) {
                        EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                        m_Equipped = true;
                    }
                } else if (!swim && m_SwimID == SwimIDs.Swim) {
                    var slope = Mathf.Acos(m_RaycastHit.normal.y) * Mathf.Rad2Deg;
                    if (slope <= m_Controller.SlopeLimit) {
                        EventHandler.ExecuteEvent<float>(m_GameObject, "OnCameraHeightOffset", 0);

                        m_SwimID = SwimIDs.End;
                        m_Rigidbody.useGravity = true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. An empty string indicates no change.</returns>
        public override string GetDestinationState(int layer)
        {
            // If the character is not swimming yet then apply the normal character movement.
            if (m_SwimID != SwimIDs.Swim) {
                return string.Empty;
            }

            if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex() || layer == m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                if (m_JumpIn) {
                    // If the character jumped into the water then the start animation does not need to play.
                    return "Swim.Swim";
                } else {
                    return "Swim.Start";
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Apply any external forces not caused by root motion, such as an explosion force.
        /// <param name="xPercent">The percent that the x root motion force affected the current velocity.</param>
        /// <param name="yPercent">The percent that the y root motion force affected the current velocity.</param>
        /// <returns>Should the RigidbodyCharacterController stop execution of its CheckForExternalForces method?</returns>
        /// </summary>
        public override bool CheckForExternalForces(float xPercent, float zPercent)
        {
            // If the character is not swimming yet then apply the normal character movement.
            if (m_SwimID != SwimIDs.Swim) {
                return false;
            }
            // Do not allow any external forces while swimming.
            m_Controller.Velocity = Vector3.zero;
            return true;
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
        /// Move according to the swimming root motion.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateMovement method?</returns>
        public override bool UpdateMovement()
        {
            if (m_SwimID == SwimIDs.None) {
                return false;
            }

            var force = Quaternion.Inverse(m_Transform.rotation) * m_Controller.RootMotionForce / (1 + m_WaterResistance);
            if (m_SwimID == SwimIDs.Swim) {
                // There is no horizontal movement with swimming. The character will rotate based on the camera direction.
                force.x = 0;
            } else {
                // Prevent the character from moving backwards when the ending animation is playing.
                force.z = Mathf.Abs(force.z);
            }
            m_Controller.SetPosition(m_Transform.position + (m_Transform.rotation * force));
            m_Controller.RootMotionForce = Vector3.zero;

            return true;
        }

        /// <summary>
        /// Update the rotation forces.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateRotation method?</returns>
        public override bool UpdateRotation()
        {
            // If the character is not swimming yet then apply the normal character movement.
            if (m_SwimID != SwimIDs.Swim) {
                return false;
            }

            // The character is swimming. Rotate based on the input.
            if (m_Controller.InputVector != Vector3.zero) {
                var targetRotation = Quaternion.Euler(0, Quaternion.LookRotation(m_Controller.LookRotation * m_Controller.InputVector.normalized).eulerAngles.y, 0);
                m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, targetRotation, m_RotationSpeed * Time.fixedDeltaTime);
            }
            return true;
        }

        /// <summary>
        /// Update the Animator.
        /// </summary>
        /// <returns>Should the RigidbodyCharacterController stop execution of its UpdateAnimator method?</returns>
        public override bool UpdateAnimator()
        {
            if (m_SwimID == SwimIDs.None) {
                return false;
            }
            m_AnimatorMonitor.SetStateValue((int)m_SwimID);
            m_AnimatorMonitor.SetForwardInputValue(m_Controller.InputVector.magnitude);
            return true;
        }

        /// <summary>
        /// The stopping swim animation has finished playing.
        /// </summary>
        private void StopSwim()
        {
            if (m_SwimID == SwimIDs.End) {
                m_TransitionTime = Time.time;
                m_SwimID = SwimIDs.None;
                m_Controller.Grounded = true;
                m_AnimatorMonitor.DetermineStates();

                if (m_Equipped) {
                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityToggleEquippedItem");
                    m_Equipped = false;
                }
            }
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            m_Rigidbody.useGravity = true;
            EventHandler.ExecuteEvent<float>(m_GameObject, "OnCameraHeightOffset", 0);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorSwimStop", StopSwim);
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return m_SwimID != SwimIDs.Swim;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return m_SwimID != SwimIDs.Swim;
        }

        /// <summary>
        /// Optionally play a spash particle.
        /// </summary>
        /// <param name="limbIndex">The index of the limb that should play the splash particle.</param>
        private void Splash(int limbIndex)
        {
            // Do not play a splash particle if the character isn't moving.
            if (m_Controller.InputVector.magnitude < 0.1f) {
                return;
            }

            ParticleSystem particleSystem = null;
            if (limbIndex == 0) { // Left Hand.
                particleSystem = m_LeftHandSplashParticle;
            } else if (limbIndex == 1) { // Right Hand.
                particleSystem = m_RightHandSplashParticle;
            } else if (limbIndex == 2) { // Left Foot.
                particleSystem = m_LeftFootSplashParticle;
            } else if (limbIndex == 3) { // Right Foot.
                particleSystem = m_RightFootSplashParticle;
            }

            // Play the splash particle if it isn't null.
            if (particleSystem != null) {
                particleSystem.Play();
            }
        }
    }
}