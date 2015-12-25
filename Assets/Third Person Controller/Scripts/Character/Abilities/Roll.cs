using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The Roll ability allows the character to roll. The character can only roll when moving in a relative positive z velocity.
    /// </summary>
    public class Roll : Ability
    {
        [Tooltip("Prevent the character from rolling too quickly after rolling")]
        [SerializeField] private float m_RollRecurrenceDelay = 0.2f;

        // Internal variables
        private float m_LastRollTime;

        // GameObject references
        private CapsuleCollider m_CapsuleCollider;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_CapsuleCollider = GetComponent<CapsuleCollider>();

            m_LastRollTime = -m_RollRecurrenceDelay;
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            return m_LastRollTime + m_RollRecurrenceDelay < Time.time && m_Controller.InputVector.z >= 0;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_Controller.ForceRootMotion = true;

            // When starting to roll divide the collider height in half.
            var height = m_CapsuleCollider.height;
            height /= 2;
            var center = m_CapsuleCollider.center;
            center.y = center.y - (m_CapsuleCollider.height - height) / 2;
            m_CapsuleCollider.height = height;
            m_CapsuleCollider.center = center;

            // The item colliders should be disabled to prevent interference with rolling.
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnControllerEnableItemCollider", false);

            // Notify interested objects that the roll has started. As an example the camera will stop following the anchor point on the y position.
            EventHandler.ExecuteEvent(m_GameObject, "OnCameraStaticHeight", true);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorRollComplete", OnRollComplete);
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

            return "Roll";
        }

        /// <summary>
        /// The roll animation has ended. 
        /// </summary>
        private void OnRollComplete()
        {
            m_LastRollTime = Time.time;
            m_Controller.ForceRootMotion = false;
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnControllerEnableItemCollider", true);
            EventHandler.ExecuteEvent(m_GameObject, "OnCameraStaticHeight", false);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorRollComplete", OnRollComplete);

            // When ending the crouch restore the collider height.
            var height = m_CapsuleCollider.height;
            height *= 2;
            m_CapsuleCollider.height = height;
            var center = m_CapsuleCollider.center;
            center.y = m_CapsuleCollider.height / 2;
            m_CapsuleCollider.center = center;

            StopAbility();
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>False to indicate that the item cannot be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return false;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return false;
        }
    }
}