using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Fall ability allows the character to play a falling animation when the character has a negative y velocity.
    /// </summary>
    public class Fall : Ability
    {
        [Tooltip("The minimum height that the ability starts. Set to 0 to fall for any negative velocity")]
        [SerializeField] private float m_MinFallHeight = 0;

        // Internal variables
        private bool m_Grounded;

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
            // The ground distance must be greater then the minimum fall height if a value is set.
            RaycastHit hit;
            if (m_MinFallHeight != 0 && Physics.Raycast(m_Transform.position + Vector3.up, Vector3.down, out hit, LayerManager.Mask.Ground)) {
                if (hit.distance < m_MinFallHeight + Vector3.up.y) { // Account for the y offset.
                    return false;
                }
            }
            // Fall can be started if the character is no on the ground and has a negative velocity.
            return !m_Grounded && m_Controller.Velocity.y <= 0;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorLand", OnLanded);
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

            // The Int Data parameter will contain an index value used to determine which leg should be in front.
            var prevStateData = m_AnimatorMonitor.IntDataValue;
            var stateName = string.Empty;
            if (prevStateData == 1) {
                stateName = "Fall Left";
            } else if (prevStateData == 2) {
                stateName = "Fall Right";
            } else {
                stateName = "Fall";
            }

            if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                return "Fall." + stateName;
            }
            return m_AnimatorMonitor.FormatUpperBodyState(stateName);
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            base.AbilityStopped();

            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorLand", OnLanded);
        }

        /// <summary>
        /// The character has changed grounded state. 
        /// </summary>
        /// <param name="grounded">Is the character on the ground?</param>
        private void OnGrounded(bool grounded)
        {
            m_Grounded = grounded;
            if (m_Grounded) {
                // Move to the fall end state when the character lands.
                m_AnimatorMonitor.SetStateValue(1);
            }
        }

        /// <summary>
        /// The land end animation has finished playing so the ability can now end.
        /// </summary>
        private void OnLanded()
        {
            StopAbility();
        }
    }
}