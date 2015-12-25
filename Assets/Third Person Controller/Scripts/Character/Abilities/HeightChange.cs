using UnityEngine;

namespace Opsive.ThirdPersonController.Abilities
{
    /// <summary>
    /// The HeightChange ability allows the character to toggle between pose height changes. This is a concurrent ability so it can be run while other abilities are active.
    /// </summary>
    public class HeightChange : Ability
    {
        // The current Animator state that the ability should be in.
        private enum HeightChangeIDs { Start, Movement, Stop }

        [Tooltip("The name of the state when the HeightChange ability starts. Can be empty")]
        [SerializeField] private string m_StartState;
        [Tooltip("The name of the state when the character is idle")]
        [SerializeField] private string m_IdleState;
        [Tooltip("The name of the state when the character is moving")]
        [SerializeField] private string m_MovementState;
        [Tooltip("The name of the state when the HeightChange ability stops. Can be empty")]
        [SerializeField] private string m_StopState;
        [Tooltip("Is the ability a concurrent ability?")]
        [SerializeField] private bool m_ConcurrentAbility = true;
        [Tooltip("Can the item be equipped while the ability is active?")]
        [SerializeField] private bool m_CanHaveItemEquipped = true;
        [Tooltip("Should IK be used?")]
        [SerializeField] private bool m_CanUseIK = true;
        [Tooltip("Should the states be used for the upper body?")]
        [SerializeField] private bool m_UseHeightChangeUpperBodyStates = true;

        // Internal variables
        private HeightChangeIDs m_CurrentStateID;

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
        /// Can the specified ability be started?
        /// </summary>
        /// <param name="ability">The ability that is trying to start.</param>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility(Ability ability)
        {
            // The HeightChange ability cannot be active as the same time as the SpeedChange and Roll abilities.
            if (ability is SpeedChange || ability is Roll) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts executing the ability.
        /// </summary>
        protected override void AbilityStarted()
        {
            // Go directly to the movement state if there is no start transition state.
            if (!string.IsNullOrEmpty(m_StartState)) {
                m_CurrentStateID = HeightChangeIDs.Start;
            } else {
                m_CurrentStateID = HeightChangeIDs.Movement;
            }

            base.AbilityStarted();

            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnAbilityHeightChange", true);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorStateComplete", OnStateComplete);
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
            switch (m_CurrentStateID) {
                case HeightChangeIDs.Start:
                    stateName = m_StartState;
                    break;
                case HeightChangeIDs.Movement:
                    if (m_Controller.Moving) {
                        stateName = m_MovementState;
                    } else {
                        stateName = m_IdleState;
                    }
                    break;
                case HeightChangeIDs.Stop:
                    stateName = m_StopState;
                    break;
            }

            if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                return stateName;
            }
            if (!m_UseHeightChangeUpperBodyStates) {
                return string.Empty;
            }
            if (m_CanHaveItemEquipped) {
                return m_AnimatorMonitor.FormatUpperBodyState(stateName);
            }
            return stateName;
        }

        /// <summary>
        /// Callback when a state has finished executing. The Start and End states trigger this callback so the ability knows when to change.
        /// </summary>
        private void OnStateComplete()
        {
            if (m_CurrentStateID == HeightChangeIDs.Start) {
                m_CurrentStateID = HeightChangeIDs.Movement;
                m_AnimatorMonitor.DetermineStates();
            } else if (m_CurrentStateID == HeightChangeIDs.Stop) {
                AbilityStopped(false);
            }
        }

        /// <summary>
        /// Can the ability be stopped?
        /// </summary>
        /// <returns>True if the ability can be stopped.</returns>
        public override bool CanStopAbility()
        {
            // Do not stand if there is an object obstructing the standing position.
            return !Physics.Raycast(m_Transform.position + m_CapsuleCollider.center, m_Transform.up * m_CapsuleCollider.height / 2, m_CapsuleCollider.height, LayerManager.Mask.IgnoreInvisibleLayersPlayer);
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        protected override void AbilityStopped()
        {
            AbilityStopped(true);
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="checkForStopState">Should the stop state be checked?</param>
        private void AbilityStopped(bool checkForStopState)
        {
            // If there is a stop state then ensure that has been played first.
            if (checkForStopState && !string.IsNullOrEmpty(m_StopState)) {
                m_CurrentStateID = HeightChangeIDs.Stop;
                m_AnimatorMonitor.DetermineStates();
                return;
            }

            // If a stop state exists then it has been played. Completely stop the ability.
            base.AbilityStopped();

            // Restore the collider height/center.
            m_CurrentStateID = HeightChangeIDs.Start;

            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnAbilityHeightChange", false);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorStateComplete", OnStateComplete);
        }

        /// <summary>
        /// Can this ability run at the same time as another ability?
        /// </summary>
        /// <returns>True if this ability can run with another ability.</returns>
        public override bool IsConcurrentAblity()
        {
            return m_ConcurrentAbility;
        }

        /// <summary>
        /// Can the character have an item equipped while the ability is active?
        /// </summary>
        /// <returns>True if the character can have an item equipped.</returns>
        public override bool CanHaveItemEquipped()
        {
            return m_CanHaveItemEquipped;
        }

        /// <summary>
        /// The character wants to interact with the item. Return false if there is a reason why the character shouldn't be able to.
        /// </summary>
        /// <returns>True if the item can be interacted with.</returns>
        public override bool CanInteractItem()
        {
            return m_CanHaveItemEquipped || m_CurrentStateID == HeightChangeIDs.Start;
        }

        /// <summary>
        /// Should IK at the specified layer be used?
        /// </summary>
        /// <param name="layer">The IK layer in question.</param>
        /// <returns>True if the IK should be used.</returns>
        public override bool CanUseIK(int layer)
        {
            return m_CanUseIK;
        }
    }
}