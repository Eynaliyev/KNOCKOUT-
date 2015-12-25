using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the inventory. 
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class InventoryHandler : NetworkBehaviour
#else
    public class InventoryHandler : MonoBehaviour
#endif
    {
        [Tooltip("Can the items be switched through a button map?")]
        [SerializeField] private bool m_CanSwitchItems = true;
        [Tooltip("Can the item be toggled between equipped or unequipped through a button map?")]
        [SerializeField] private bool m_CanToggleEquippedItem = true;
        [Tooltip("Can the input scroll through the items?")]
        [SerializeField] private bool m_CanScrollItems;
        [Tooltip("If Can Scroll Items is enabled, the sensitivity for scrolling between items")]
        [SerializeField] private float m_ScrollSensitivity;
        [Tooltip("Can items be equipped via a specified button map?")]
        [SerializeField] private bool m_CanEquipSpecifiedItems = true;

        // SharedFields
        private SharedMethod<bool> m_IsAI = null;

        // Component references
        private GameObject m_GameObject;
        private Inventory m_Inventory;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Inventory = GetComponent<Inventory>();
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowInventoryInput", AllowGameplayInput);

            // An AI Agent does not use PlayerInput so Update does not need to run.
            if (m_IsAI.Invoke()) {
                enabled = false;
            }
        }

        /// <summary>
        /// Notify the inventory that the user wants to perform an action.
        /// </summary>
        private void Update()
        {

#if ENABLE_MULTIPLAYER
            if (!isLocalPlayer) {
                return;
            }
#endif

            if (m_CanSwitchItems) {
                if (PlayerInput.GetButtonDown(Constants.NextItemInputName)) {
#if ENABLE_MULTIPLAYER
                    CmdSwitchItem(true, false);
#else
                    m_Inventory.SwitchItem(true, false);
#endif
                }

                if (PlayerInput.GetButtonDown(Constants.PrevItemInputName)) {
#if ENABLE_MULTIPLAYER
                    CmdSwitchItem(true, true);
#else
                    m_Inventory.SwitchItem(true, true);
#endif
                }
            }

            if (m_CanToggleEquippedItem && PlayerInput.GetButtonDown(Constants.EquipItemToggleInputName)) {
#if ENABLE_MULTIPLAYER
                CmdToggleEquippedItem();
#else
                m_Inventory.ToggleEquippedItem();
#endif
            }

            if (m_CanScrollItems) {
                float scrollInput;
                if (Mathf.Abs(scrollInput = PlayerInput.GetAxis(Constants.ItemScrollName)) > m_ScrollSensitivity && !m_Inventory.IsSwitchingItems) {
#if ENABLE_MULTIPLAYER
                    CmdSwitchItem(true, scrollInput > 0);
#else
                    m_Inventory.SwitchItem(true, scrollInput > 0);
#endif
                }
            }

            if (m_CanEquipSpecifiedItems) {
                for (int i = 0; i < Constants.EquipSpecifiedItem.Length; ++i) {
                    if (PlayerInput.GetButtonDown(Constants.EquipSpecifiedItem[i])) {
#if ENABLE_MULTIPLAYER
                        CmdEquipItem(i);
#else
                        m_Inventory.EquipItem(i);
#endif
                    }
                }
            }

            Item dualWieldItem;
            if ((dualWieldItem = m_Inventory.GetCurrentItem(typeof(DualWieldItemType))) != null && PlayerInput.GetButtonDown(Constants.DropDualWieldItemInputName)) {
                m_Inventory.DropItem(dualWieldItem.ItemType);
            }
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Switch the item to the next item in the inventory list on the server.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        [Command]
        private void CmdSwitchItem(bool primaryItem, bool next)
        {
            m_Inventory.SwitchItem(primaryItem, next);
        }

        /// <summary>
        /// If an item is equipped then unequip it on the server. If an item is unequipped or equal to the unequipped type then equip the previous item.
        /// </summary>
        [Command]
        private void CmdToggleEquippedItem()
        {
            m_Inventory.ToggleEquippedItem();
        }

        /// <summary>
        /// Equips the primary item in the specified index on the server.
        /// </summary>
        /// <param name="itemIndex">The unventory index to equip</param>
        [Command]
        private void CmdEquipItem(int itemIndex)
        {
            m_Inventory.EquipItem(itemIndex);
        }
#endif

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            enabled = allow;
        }
    }
}