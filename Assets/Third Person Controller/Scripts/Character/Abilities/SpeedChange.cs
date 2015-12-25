using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The SpeedChange ability allows the character to move at a different rate. Optionally a stamina can be used to prevent character from changing speeds for too long.
    /// </summary>
    public class SpeedChange : Ability
    {
        [Tooltip("The speed multiplier when the ability is active")]
        [SerializeField] private float m_SpeedChangeMultiplier = 2;
        [Tooltip("Can the ability be active while the character is aiming?")]
        [SerializeField] private bool m_CanAim;
        [Tooltip("Should the character have stamina while in a different speed?")]
        [SerializeField] private bool m_UseStamina;
        [Tooltip("The amount of stamina the character has")]
        [SerializeField] private float m_MaxStamina = 100;
        [Tooltip("The rate at which the stamina decreases while in a different speed")]
        [SerializeField] private float m_StaminaDecreaseRate = 0.5f;
        [Tooltip("The rate at which the stamina increases while not in a different speed")]
        [SerializeField] private float m_StaminaIncreaseRate = 0.1f;

        // Internal variables
        private float m_CurrentStamina;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_CurrentStamina = m_MaxStamina;
        }

        /// <summary>
        /// Executed on every ability to allow the ability to update.
        /// </summary>
        public override void UpdateAbility()
        {
            // Restore the stamina when not changing speeds.
            if (!IsActive && m_UseStamina && m_CurrentStamina < m_MaxStamina) {
                m_CurrentStamina = Mathf.Clamp(m_CurrentStamina + m_StaminaIncreaseRate, 0, m_MaxStamina);
            }
        }

        /// <summary>
        /// Can this ability run at the same time as another ability?
        /// </summary>
        /// <returns>True if this ability can run with another ability.</returns>
        public override bool IsConcurrentAblity()
        {
            return true;
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
            // The character can't change while aiming. In addition, the ability should stop if the character runs out of stamina.
            var canChangeSpeeds = m_CanAim || !m_Controller.Aiming;
            if (m_UseStamina) {
                m_CurrentStamina = Mathf.Clamp(m_CurrentStamina - m_StaminaDecreaseRate, 0, m_MaxStamina);
                if (m_CurrentStamina == 0) {
                    StopAbility();
                    return false;
                }
            }

            if (canChangeSpeeds) {
                horizontalMovement *= m_SpeedChangeMultiplier;
                forwardMovement *= m_SpeedChangeMultiplier;
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
            // The ability only affects the upper layers.
            if (layer != m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                return string.Empty;
            }

            return m_AnimatorMonitor.FormatUpperBodyState(m_Controller.Moving ? "Movement" : "Idle");
        }

        /// <summary>
        /// Should the upper body be forced to have the same time as the lower body? 
        /// </summary>
        /// <returns>True to indicate that the upper body should be forced to have the same time as the lower body.</returns>
        public override bool ForceUpperBodySynchronization()
        {
            return true;
        }

        /// <summary>
        /// Does the ability have complete control of the Animator states?
        /// </summary>
        /// <param name="layer">The layer to check against.</param>
        /// <returns>True if the character can run the ability.</returns>
        public override bool HasAnimatorControl(int layer)
        {
            return false;
        }
    }
}