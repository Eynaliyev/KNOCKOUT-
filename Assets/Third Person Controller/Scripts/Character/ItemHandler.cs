using UnityEngine;
using System;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the current Item. 
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class ItemHandler : NetworkBehaviour
#else
    public class ItemHandler : MonoBehaviour
#endif
    {
        [Tooltip("Can the flashlight be toggled through a button map?")]
        [SerializeField] private bool m_CanToggleFlashlight = true;
        [Tooltip("Can the laser sight be toggled through a button map?")]
        [SerializeField] private bool m_CanToggleLaserSight = true;

        // SharedFields
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedProperty<Item> m_CurrentSecondaryItem = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;
        private SharedMethod<bool> m_TryUseItem = null;
        private SharedMethod<bool> m_IsAI = null;
        private SharedMethod<bool> m_CanInteractItem = null;
        private SharedMethod<bool> m_CanUseItem = null;

        // Internal variables
        private enum UseType { None, Primary, DualWield}
        private UseType m_UseType = UseType.None;
        private bool m_UsePending;
        private bool m_StopUse;
        private bool m_Aiming;
        private bool m_AllowGameplayInput = true;
        private bool m_HasDualWieldItem;
        private bool m_SecondaryInputWait;

        // Component references
        private GameObject m_GameObject;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
        }

        /// <summary>
        /// Register for any events that the handler should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
            EventHandler.RegisterEvent<Item>(m_GameObject, "OnInventoryPrimaryItemChange", PrimaryItemChange);
            EventHandler.RegisterEvent<Type>(m_GameObject, "OnAnimatorItemUsed", OnUsed);
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Unregister for any events that the handler was aware of.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent<bool>(m_GameObject, "OnControllerAim", OnAim);
            EventHandler.UnregisterEvent<Item>(m_GameObject, "OnInventoryPrimaryItemChange", PrimaryItemChange);
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
            if (!m_IsAI.Invoke()) {
                EventHandler.UnregisterEvent<Type>(m_GameObject, "OnAnimatorItemUsed", OnUsed);
            }
        }


#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Initialize the SharedFields so they are ready for when the server starts to send RPC calls.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            SharedManager.InitializeSharedFields(m_GameObject, this);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowInventoryInput", AllowGameplayInput);
        }
#endif

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            // The SharedFields would have already been initialized if in a network game.
            if (m_CurrentPrimaryItem == null) {
                SharedManager.InitializeSharedFields(m_GameObject, this);
                EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
                EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowInventoryInput", AllowGameplayInput);
            }

            // An AI Agent does not use PlayerInput so Update does not need to run.
            if (m_IsAI.Invoke()) {
                enabled = false;
            }
        }

        /// <summary>
        /// Notify the item that the user wants to perform an action.
        /// </summary>
        private void Update()
        {
            if (!m_AllowGameplayInput) {
                return;
            }

#if ENABLE_MULTIPLAYER
            if (!isLocalPlayer) {
                return;
            }
#endif

            // Try to use the item.
            var hasDualWieldItem = m_CurrentDualWieldItem.Get() != null;
            var useType = UseType.None;
            if (hasDualWieldItem) {
                // Use a different mapping when dual wielded items exist.
                if (PlayerInput.GetButton(Constants.DualWieldLeftUseInputName, true)) {
                    useType |= UseType.DualWield;
                }
                if (PlayerInput.GetButton(Constants.DualWieldRightUseInputName, true)) {
                    useType |= UseType.Primary;
                }
            } else {
                if (PlayerInput.GetButton(Constants.UseInputName, true)) {
                    useType |= UseType.Primary;
                }
            }
            if (useType != UseType.None) {
#if ENABLE_MULTIPLAYER
                CmdTryUseItem(true, useType);
#else
                TryUseItem(true, useType);
#endif
            // Stop the use as soon as the player releases the Use input.
            } else {
                if (hasDualWieldItem) {
                    if (PlayerInput.GetButtonUp(Constants.DualWieldLeftUseInputName, true)) {
#if ENABLE_MULTIPLAYER
                        CmdTryStopUse(false);
#else
                        TryStopUse(false);
#endif
                    }
                    if (PlayerInput.GetButtonUp(Constants.DualWieldRightUseInputName, true)) {
#if ENABLE_MULTIPLAYER
                        CmdTryStopUse(true);
#else
                        TryStopUse(true);
#endif
                    }
                } else {
                    if (PlayerInput.GetButtonUp(Constants.UseInputName, true)) {
#if ENABLE_MULTIPLAYER
                        CmdTryStopUse(true);
#else
                        TryStopUse(true);
#endif
                    }
                }
            }

            // Reload the item if the item can be reloaded.
            if (PlayerInput.GetButtonDown(Constants.ReloadInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryReload();
#else
                TryReload();
#endif
            }

            // Use the Secondary Item.
            if (!m_SecondaryInputWait && PlayerInput.GetButton(Constants.UseSecondaryInputName, true)) {
                // The drop dual wield item may be mapped to the same input as the secondary item. Prevent the secondary item from being used until the button has returned
                // to the up position.
                if (!m_HasDualWieldItem && !m_SecondaryInputWait) {
#if ENABLE_MULTIPLAYER
                    CmdTryUseItem(false, UseType.Primary);
#else
                    TryUseItem(false);
#endif
                } else {
                    m_SecondaryInputWait = true;
                }
            } else if (PlayerInput.GetButtonUp(Constants.UseSecondaryInputName, true)) {
                m_SecondaryInputWait = false;
            }

            if (m_CanToggleFlashlight && PlayerInput.GetButtonDown(Constants.ToggleFlashlightInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryToggleFlashlight();
#endif
                TryToggleFlashlight();
            }

            if (m_CanToggleLaserSight && PlayerInput.GetButtonDown(Constants.ToggleLaserSightInputName)) {
#if ENABLE_MULTIPLAYER
                CmdTryToggleLaserSight();
#endif
                TryToggleLaserSight();
            }

            // The Update execution order isn't guarenteed to be in any sort of order. Store if the item has been dual wielded to allow the secondary input to use the value from the last
            // frame to prevent the InventoryHandler from dropping the item in the current frame and the ItemHandler thinking that it has already been dropped.
            m_HasDualWieldItem = hasDualWieldItem;
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Tries to use the primary or secondary item on the server. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be used?</param>
        /// <param name="useType">Should the primary item and/or the dual wielded item be used?</param>
        /// <returns>True if the item was used.</returns>
        [Command]
        private void CmdTryUseItem(bool primaryItem, UseType useType)
        {
            TryUseItem(primaryItem, useType);
        }

        /// <summary>
        /// Tries to stop the active item from being used on the server.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be stopped?</param>
        [Command]
        private void CmdTryStopUse(bool primaryItem)
        {
            TryStopUse(primaryItem);
        }

        /// <summary>
        /// Tries to reload the item on the server.
        /// </summary>
        [Command]
        private void CmdTryReload()
        {
            TryReload();
        }

        /// <summary>
        /// Tries to toggle the flashlight on the server.
        /// </summary>
        [Command]
        private void CmdTryToggleFlashlight()
        {
            RpcTryToggleFlashlight();
        }

        /// <summary>
        /// Tries to toggle the flashlight on the clients.
        /// </summary>
        [ClientRpc]
        private void RpcTryToggleFlashlight()
        {
            // The flashlight would have already been toggled if a local player.
            if (isLocalPlayer) {
                return;
            }
            TryToggleFlashlight();
        }

        /// <summary>
        /// Tries to toggle the laser sight on the clients.
        /// </summary>
        [Command]
        private void CmdTryToggleLaserSight()
        {
            RpcTryToggleLaserSight();
        }

        [ClientRpc]
        private void RpcTryToggleLaserSight()
        {
            // The laser sight would have already been toggled if a local player.
            if (isLocalPlayer) {
                return;
            }
            TryToggleLaserSight();
        }
#endif

        /// <summary>
        /// Tries to use the primary or secondary item. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be used?</param>
        /// <returns>True if the item was used.</returns>
        public bool TryUseItem(bool primaryItem)
        {
            return TryUseItem(primaryItem, UseType.Primary);
        }

        /// <summary>
        /// Tries to use the primary, secondary, or dual wield item. The item may not be able to be used if it isn't equipped or is in use.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be used?</param>
        /// <param name="useType">Should the primary item and/or the dual wielded item be used?</param>
        /// <returns>True if the item was used.</returns>
        private bool TryUseItem(bool primaryItem, UseType useType)
        {
            // Return early if the item cannot be interacted with or used.
            if (!m_CanInteractItem.Invoke() || !m_CanUseItem.Invoke()) {
                return false;
            }
            IUsableItem usableItem;
            if (primaryItem) {
                if (((int)useType & (int)UseType.Primary) == (int)UseType.Primary) {
                    usableItem = m_CurrentPrimaryItem.Get() as IUsableItem;
                } else {
                    usableItem = m_CurrentDualWieldItem.Get() as IUsableItem;
                }
                if (usableItem != null && usableItem.CanUse()) {
                    // The UseType should always be updated.
                    m_UseType |= useType;
                    if (!m_UsePending) {
                        // The SharedMethod TryUseItem will return failure if the item cannot be used for any reason, such as a weapon not being aimed. If this happens
                        // register for the event which will let us know when the item is ready to be used.
                        if (m_TryUseItem.Invoke()) {
                            ReadyForUse();
                        } else {
                            EventHandler.RegisterEvent(m_GameObject, "OnItemReadyForUse", ReadyForUse);
                            m_UsePending = true;
                        }
                    }
                    return true;
                }
            } else {
                usableItem = m_CurrentSecondaryItem.Get() as IUsableItem;
                if (usableItem != null && usableItem.CanUse()) {
                    if (usableItem.TryUse()) {
                        // After the item is used the character may no longer be alive so don't execuate the events.
                        if (enabled || m_IsAI.Invoke()) {
                            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnItemStartUse", false);
                        }
                        return true;
                    }
                }
            }

            return false;
        }
        
        /// <summary>
        /// The primary item isn't always ready when the user wants to use it. For example, the primary item may be a weapon and that weapon needs to aim
        /// before it can fire. ReadyForUse will be called when the item is ready to be used.
        /// </summary>
        private void ReadyForUse()
        {
            // No longer need to listen to the event.
            EventHandler.UnregisterEvent(m_GameObject, "OnItemReadyForUse", ReadyForUse);

            // Try to use the item.
            IUsableItem usableItem;
            if (((int)m_UseType & (int)UseType.Primary) == (int)UseType.Primary) {
                usableItem = m_CurrentPrimaryItem.Get() as IUsableItem;
                if (usableItem != null) {
                    // After the item is used the character may no longer be alive so don't execuate the events.
                    if (usableItem.TryUse() && (enabled || m_IsAI.Invoke())) {
                        EventHandler.ExecuteEvent<bool>(m_GameObject, "OnItemStartUse", true);
                    }

                    // The item may have been stopped in the time that it took for the item to be ready. Let the item be used once and then stop the use.
                    if (m_StopUse) {
                        usableItem.TryStopUse();
                    }
                }
            }

            // Try to use the dual wielded item.
            if (((int)m_UseType & (int)UseType.DualWield) == (int)UseType.DualWield) {
                usableItem = m_CurrentDualWieldItem.Get() as IUsableItem;
                if (usableItem != null) {
                    usableItem.TryUse();

                    // The item may have been stopped in the time that it took for the item to be ready. Let the item be used once and then stop the use.
                    if (m_StopUse) {
                        usableItem.TryStopUse();
                    }
                }
            }

            m_UseType = UseType.None;
            m_UsePending = false;
            m_StopUse = false;
        }

        /// <summary>
        /// Callback from the Animator. Will be called when an item is registered for the Used callback.
        /// </summary>
        /// <param name="itemType">The type of item used.</param>
        private void OnUsed(Type itemType)
        {
            IUsableItem item;
            if (itemType.Equals(typeof(PrimaryItemType))) {
                item = m_CurrentPrimaryItem.Get() as IUsableItem;
            } else if (itemType.Equals(typeof(SecondaryItemType))) {
                item = m_CurrentSecondaryItem.Get() as IUsableItem;
            } else { // DualWieldItemType.
                item = m_CurrentDualWieldItem.Get() as IUsableItem;
            }
            if (item != null && item.InUse()) {
                item.Used();
            }
        }

        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        private void OnAim(bool aim)
        {
            m_Aiming = aim;
            Item item;
            if ((item = m_CurrentPrimaryItem.Get()) != null) {
                if (item is IFlashlightUsable) {
                    (item as IFlashlightUsable).ActivateFlashlightOnAim(aim);
                }
                if (item is ILaserSightUsable) {
                    (item as ILaserSightUsable).ActivateLaserSightOnAim(aim);
                }
                var usableItem = item as IUsableItem;
                if (!aim && usableItem != null && usableItem.InUse()) {
                    (item as IUsableItem).TryStopUse();
                }
            }
            if ((item = m_CurrentDualWieldItem.Get()) != null) {
                if (item is IFlashlightUsable) {
                    (item as IFlashlightUsable).ActivateFlashlightOnAim(aim);
                }
                if (item is ILaserSightUsable) {
                    (item as ILaserSightUsable).ActivateLaserSightOnAim(aim);
                }
                var usableItem = item as IUsableItem;
                if (!aim && usableItem != null && usableItem.InUse()) {
                    (item as IUsableItem).TryStopUse();
                }
            }
        }

        /// <summary>
        /// Callback from the inventory when the item is changed.
        /// </summary>
        /// <param name="item">The new item.</param>
        private void PrimaryItemChange(Item item)
        {
            // Do not listen for the ready event when the inventory switches items.
            if (m_UsePending) {
                EventHandler.UnregisterEvent(m_GameObject, "OnItemReadyForUse", ReadyForUse);
                m_UsePending = false;
            }
        } 

        /// <summary>
        /// Tries to stop the current primary item from being used.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be stopped?</param>
        public void TryStopUse(bool primaryItem)
        {
            Item item;
            if (primaryItem) {
                if ((item = m_CurrentPrimaryItem.Get()) != null && item is IUsableItem) {
                    (item as IUsableItem).TryStopUse();
                }
            } else {
                if ((item = m_CurrentDualWieldItem.Get()) != null && item is IUsableItem) {
                    (item as IUsableItem).TryStopUse();
                }
            }

            if (m_UsePending) {
                m_StopUse = true;
            }
        }

        /// <summary>
        /// Tries to reload the current item. Will return false if the item doesn't derive from IReloadableItem
        /// </summary>
        /// <returns>True if the item was reloaded.</returns>
        public bool TryReload()
        {
            // Return early if the item cannot be interacted with.
            if (!m_CanInteractItem.Invoke()) {
                return false;
            }

            var startReload = false;
            Item item;
            if ((item = m_CurrentPrimaryItem.Get()) != null && item is IReloadableItem) {
                (item as IReloadableItem).StartReload();
                startReload = true;
            }

            if ((item = m_CurrentDualWieldItem.Get()) != null && item is IReloadableItem) {
                (item as IReloadableItem).StartReload();
                startReload = true;
            }
            return startReload;
        }

        /// <summary>
        /// Tries to toggle the flashlight on or off.
        /// </summary>
        private void TryToggleFlashlight()
        {
            // The flashlight can only be toggled while aiming.
            if (m_Aiming) {
                Item item;
                if ((item = m_CurrentPrimaryItem.Get()) != null && item is IFlashlightUsable) {
                    var flashlightUsable = item as IFlashlightUsable;
                    flashlightUsable.ToggleFlashlight();
                }

                if ((item = m_CurrentDualWieldItem.Get()) != null && item is IFlashlightUsable) {
                    var flashlightUsable = item as IFlashlightUsable;
                    flashlightUsable.ToggleFlashlight();
                }
            }
        }

        /// <summary>
        /// Tries to toggle the laser sight on or off.
        /// </summary>
        private void TryToggleLaserSight()
        {
            // The laser sight can only be toggled while aiming.
            if (m_Aiming) {
                Item item;
                if ((item = m_CurrentPrimaryItem.Get()) != null && item is ILaserSightUsable) {
                    var laserSightUsable = item as ILaserSightUsable;
                    laserSightUsable.ToggleLaserSight();
                }

                if ((item = m_CurrentDualWieldItem.Get()) != null && item is ILaserSightUsable) {
                    var laserSightUsable = item as ILaserSightUsable;
                    laserSightUsable.ToggleLaserSight();
                }
            }
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            m_AllowGameplayInput = allow;
            if (!allow) {
                TryStopUse(true);
                TryStopUse(false);
            }
        }

        /// <summary>
        /// The character has died. Disable the component.
        /// </summary>
        private void OnDeath()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = false;
        }

        /// <summary>
        /// The character has respawned. Enable the component.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            enabled = true;
        }
    }
}