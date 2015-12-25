using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Extends the health component by allowing the character to take fall damage. The amount of damage is specified by a curve.
    /// </summary>
    public class CharacterHealth : Health
    {
        [Tooltip("Should fall damage be applied?")]
        [SerializeField] private bool m_UseFallDamage;
        [Tooltip("The minimum height that the character has to fall in order for any damage to be applied")]
        [SerializeField] private float m_MinFallDamageHeight;
        [Tooltip("A fall greater than this value is an instant death")]
        [SerializeField] private float m_DeathHeight;
        [Tooltip("The amount of damage to apply when the player falls by the minimum fall damage height")]
        [SerializeField] private float m_MinFallDamage;
        [Tooltip("The amount of damage to apply when the player falls just less than the death height")]
        [SerializeField] private float m_MaxFallDamage;
        [Tooltip("A curve specifying the amount of damage to apply if the character falls between the min and max fall damage values")]
        [SerializeField] private AnimationCurve m_DamageCurve = AnimationCurve.Linear(0, 0, 1, 1);

        /// <summary>
        /// Register for any events that the health should be aware of.
        /// </summary>
        private void OnEnable()
        {
            if (m_UseFallDamage) {
                EventHandler.RegisterEvent<float>(m_GameObject, "OnControllerLand", OnCharacterLand);
            }
        }

        /// <summary>
        /// Unregister for any events that the health was registered for.
        /// </summary>
        private void OnDisable()
        {
            if (m_UseFallDamage) {
                EventHandler.UnregisterEvent<float>(m_GameObject, "OnControllerLand", OnCharacterLand);
            }
        }

        /// <summary>
        /// The character has landed after falling a spcified amount. Determine if any damage should be taken.
        /// </summary>
        /// <param name="fallHeight"></param>
        private void OnCharacterLand(float fallHeight)
        {
            // Return immediately if the fall height is less than the minimum height that the character has to fall in order to start taking damage.
            if (fallHeight < m_MinFallDamageHeight) {
                return;
            }

            var damageAmount = 0f;
            // The fall was too great, specify an infinite amount of damage.
            if (fallHeight >= m_DeathHeight) {
                damageAmount = Mathf.Infinity;
            } else {
                // The fall was somewhere in between the min and max fall height. Use the damage curve to determine how much damage to apply.
                var normalizedHeight = (fallHeight - m_MinFallDamageHeight) / (m_DeathHeight - m_MinFallDamageHeight);
                var damageAmountMultiplier = m_DamageCurve.Evaluate(normalizedHeight);
                damageAmount = m_MinFallDamage + damageAmountMultiplier * (m_MaxFallDamage - m_MinFallDamage);
            }

            // Apply the damage.
            Damage(damageAmount, m_Transform.position, Vector3.zero);
        }
    }
}