using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Base class for any item that can attack
    /// </summary>
    public abstract class Weapon : Item, IUsableItem, IReloadableItem
    {
        // The state while using the item
        [SerializeField] protected AnimatorItemStatesData m_UseStates = new AnimatorItemStatesData("Attack", 0.1f, true);
        // The state while reloading the item
        [SerializeField] protected AnimatorItemStateData m_ReloadState = new AnimatorItemStateData("Reload", 0.1f, true);

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_UseStates.ItemType = m_ReloadState.ItemType = m_ItemType;
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="highPriority">Should the high priority animation be retrieved? High priority animations get tested before the character abilities.</param>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. A null value indicates no change.</returns>
        public override AnimatorItemStateData GetDestinationState(bool highPriority, int layer)
        {
            // The arm layer should be used for dual wielded items and secondary items. If only one item is equipped then the entire upper body layer can be used.
            var useArmLayer = (m_CurrentDualWieldItem.Get() != null) || m_ItemType is SecondaryItemType;

            // Any animation called by the Weapon component is a high priority animation.
            if (highPriority) {
                if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                    if (InUse()) {
                        // Not all weapons have a lower body use animation. Only play the animation if the state exists.
                        var useState = m_UseStates.GetState();
                        if (useState != null && !string.IsNullOrEmpty(useState.LowerStateName)) {
                            return useState;
                        }
                    }
                } else if (layer == m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                    if (!useArmLayer) {
                        var upperBodyState = GetUpperBodyState();
                        if (upperBodyState != null) {
                            return upperBodyState;
                        }
                    }
                } else if (layer == m_ArmLayer) {
                    if (useArmLayer) {
                        var upperBodyState = GetUpperBodyState();
                        if (upperBodyState != null) {
                            return upperBodyState;
                        }
                    }
                }
            }
            return base.GetDestinationState(highPriority, layer);
        }

        /// <summary>
        /// Returns any upper body states that the item should use.
        /// </summary>
        /// <returns>The upper body state that the item should use.</returns>
        private AnimatorItemStateData GetUpperBodyState()
        {
            if (IsReloading()) {
                return m_ReloadState;
            }
            if (InUse()) {
                return m_UseStates.GetState();
            }
            return null;
        }

        /// <summary>
        /// Try to perform the use. Depending on the weapon this may not always succeed. For example, if the user is trying to shoot a weapon that was shot a half
        /// second ago cannot be used if the weapon can only be fired once per second.
        /// <returns>True if the item was used.</returns>
        /// </summary>
        public virtual bool TryUse() { return false; }

        /// <summary>
        /// Returns the maximum distance that the item can be used.
        /// </summary>
        /// <returns>The maximum distance that hte item can be used.</returns>
        public virtual float MaxUseDistance()
        {
            return float.MaxValue;
        }

        /// <summary>
        /// Can the item be used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        public virtual bool CanUse() { return true; }

        /// <summary>
        /// Is the weapon currently in use?
        /// </summary>
        /// <returns>True if the weapon is in use.</returns>
        public virtual bool InUse() { return false; }

        /// <summary>
        /// Stop the weapon from being used. This may not always succeed. For example, a melee weapon cannot be interrupted if it is already in the middle of its motion. 
        /// </summary>
        public virtual void TryStopUse() { }

        /// <summary>
        /// Starts to reload the weapon.
        /// </summary>
        public virtual void StartReload() { }

        /// <summary>
        /// Is the item reloading?
        /// </summary>
        /// <returns>True if the item is reloading.</returns>
        public virtual bool IsReloading() { return false; }

        /// <summary>
        /// Stop the item from being used. This may not always succeed. For example, a melee weapon cannot be interrupted if it is already in the middle of its motion. 
        /// </summary>
        public virtual void Used() { }
    }
}