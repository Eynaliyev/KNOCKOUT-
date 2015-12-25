using UnityEngine;
using UnityEngine.UI;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The ItemMonitor will keep the Item UI in sync with the rest of the game. This includes showing the current item and the amount of ammo that is remaining.
    /// </summary>
    public class ItemMonitor : MonoBehaviour
    {
        private enum MonitorType { Primary, Secondary, DualWield, RightHand, LeftHand }
        [Tooltip("Specifies how the UI monitors the item")]
        [SerializeField] private MonitorType m_MonitorType;
        [Tooltip("A reference to the UI Text component for the item count")]
        [SerializeField] private Text m_LoadedCountText;
        [Tooltip("A reference to the UI Text component for the primary item's unloaded count")]
        [SerializeField] private Text m_UnloadedCountText;
        [Tooltip("A reference to the GameObject that should be disabled when the item is empty")]
        [SerializeField] private GameObject m_DisableObjectOnEmpty;

        // Internal variables
        private bool m_IsPrimaryItem;

        // SharedFields
        private SharedProperty<int> m_UnloadedCount = null;
        private SharedProperty<int> m_PrimaryLoadedCount = null;
        private SharedProperty<int> m_DualWieldLoadedCount = null;
        private SharedProperty<int> m_ItemCount = null;
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;
        private SharedProperty<Item> m_UnequippedItem = null;

        // Component references
        private Image m_Image;
        private RectTransform m_RectTransform;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the GameObject.
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            if (character == null) {
                gameObject.SetActive(false);
                return;
            }

            SharedManager.InitializeSharedFields(character, this);

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            if (m_MonitorType != MonitorType.Secondary) {
                if (m_MonitorType == MonitorType.LeftHand || m_MonitorType == MonitorType.RightHand) {
                    EventHandler.RegisterEvent<Item>(character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                    EventHandler.RegisterEvent<Item>(character, "OnInventoryDualWieldItemChange", DualWieldItemChange);
                    PrimaryItemChange(m_CurrentPrimaryItem.Get());
                    DualWieldItemChange(m_CurrentDualWieldItem.Get());

                } else if (m_MonitorType == MonitorType.Primary) {
                    EventHandler.RegisterEvent<Item>(character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
                    PrimaryItemChange(m_CurrentPrimaryItem.Get());
                } else {
                    EventHandler.RegisterEvent<Item>(character, "OnInventoryDualWieldItemChange", DualWieldItemChange);
                    DualWieldItemChange(m_CurrentDualWieldItem.Get());
                }
                EventHandler.RegisterEvent<Item, bool, bool>(character, "OnInventoryConsumableItemCountChange", ConsumableItemCountChange);

            } else {
                EventHandler.RegisterEvent(character, "OnInventorySecondaryItemCountChange", SecondaryItemCountChange);

                // Initialize the secondary values.
                SecondaryItemCountChange();
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// The primary item has changed. Update the UI.
        /// </summary>
        /// <param name="item">The item that was equipped. Can be null.</param>
        private void PrimaryItemChange(Item item)
        {
            ItemChange(item, true);
        }

        /// <summary>
        /// The dual wield item has changed. Update the UI.
        /// </summary>
        /// <param name="item">The item that was equipped. Can be null.</param>
        private void DualWieldItemChange(Item item)
        {
            ItemChange(item, false);
        }

        /// <summary>
        /// The item has changed. Update the UI.
        /// </summary>
        /// <param name="item">The item that was equipped. Can be null.</param>
        /// <param name="primaryItem">Was the primary item changed?</param>
        private void ItemChange(Item item, bool primaryItem)
        {
            // Version 1.1 changed the loaded count text name.
            if (m_LoadedCountText == null) {
                Debug.LogError("Error: ItemMonitor.LoadedCountText is null on the " + gameObject + " GameObject. Please assign this value within the inspector.");
                return;
            }

            var disableUI = item == null || item.ItemSprite == null;
            // If the UI is per item then there's an addition chance that the images should be disabled. If a dual wield item is in the non-dominant hand and it is replaced by a primary item
            // in the dominant hand then the UI should be disabled for the non-dominant hand. The only way to ensure this is the case is by checking the hand transform against the unequipped item. 
            if (!disableUI && (m_MonitorType == MonitorType.LeftHand || m_MonitorType == MonitorType.RightHand)) {
                disableUI = item.Equals(m_UnequippedItem.Get()) && ((item.RightItemSprite && m_MonitorType == MonitorType.LeftHand) || (!item.RightItemSprite && m_MonitorType == MonitorType.RightHand));
            } else if (disableUI && item == null) {
                // DualWieldItemChange will be called when the dual wield item is null so ensure the UI is being disabled for the correct item.
                if (m_IsPrimaryItem ? m_CurrentPrimaryItem.Get() != null : m_CurrentDualWieldItem.Get() != null) {
                    return;
                }
            }
            if (disableUI) {
                // If the MonitorType is left or right then ItemChange will be called for both the left and right hand. Only hide the UI if the primary item matches.
                // This prevents the primary UI from clearing when the dual wield item is removed.
                if (primaryItem != m_IsPrimaryItem) {
                    return;
                }
                // Disable the UI if there is no item or sprite.
                m_Image.sprite = null;
                m_LoadedCountText.enabled = m_Image.enabled = false;
                if (m_UnloadedCountText != null) {
                    m_UnloadedCountText.enabled = false;
                }
                if (m_DisableObjectOnEmpty != null) {
                    m_DisableObjectOnEmpty.SetActive(false);
                }
            } else {
                // Don't update if monitoring a hand and the hand doesn't match.
                if ((item.RightItemSprite && m_MonitorType == MonitorType.LeftHand) || (!item.RightItemSprite && m_MonitorType == MonitorType.RightHand)) {
                    return;
                }

                // There is an item so ensure the UI is enabled.
                m_Image.sprite = item.ItemSprite;
                m_LoadedCountText.enabled = m_Image.enabled = true;
                m_IsPrimaryItem = item.Equals(m_CurrentPrimaryItem.Get());
                if (m_UnloadedCountText != null) {
                    m_UnloadedCountText.enabled = true;
                }
                if (m_DisableObjectOnEmpty != null) {
                    m_DisableObjectOnEmpty.SetActive(true);
                }

                // Position the sprite in the center.
                var sizeDelta = m_RectTransform.sizeDelta;
                sizeDelta.x = m_Image.sprite.textureRect.width;
                sizeDelta.y = m_Image.sprite.textureRect.height;
                m_RectTransform.sizeDelta = sizeDelta;

                // Update the loaded and unloaded count.
                ConsumableItemCountChange(item, false, false);
            }
        }

        /// <summary>
        /// The amount of consumable ammo has changed. Update the loaded and unloaded count.
        /// </summary>
        /// <param name="item">The item whose consumable ammo has changed.</param>
        /// <param name="added">True if the consumable items were added.</param>
        /// <param name="immediateChange">True if the consumable item count should be changed immediately. This is not used by the ItemMonitor.</param>
        private void ConsumableItemCountChange(Item item, bool added, bool immediateChange)
        {
            // Version 1.1 changed the loaded count text name.
            if (m_LoadedCountText == null) {
                Debug.LogError("Error: ItemMonitor.LoadedCountText is null on the " + gameObject + " GameObject. Please assign this value within the inspector.");
                return;
            }

            var loadedCount = m_IsPrimaryItem ? m_PrimaryLoadedCount.Get() : m_DualWieldLoadedCount.Get();
            // Don't update if monitoring a hand and the hand doesn't match.
            if ((item.RightItemSprite && m_MonitorType == MonitorType.LeftHand) || (!item.RightItemSprite && m_MonitorType == MonitorType.RightHand)) {
                if (m_UnloadedCountText != null) {
                    if (loadedCount != int.MaxValue && loadedCount != -1) {
                        m_UnloadedCountText.text = m_UnloadedCount.Get().ToString();
                    } else {
                        m_UnloadedCountText.text = string.Empty;
                    }
                }
                return;
            }

            if (loadedCount != int.MaxValue && loadedCount != -1) {
                // If ItemUnloadedCountText is null then the ItemCount should show the loaded and unloaded count.
                if (m_UnloadedCountText == null) {
                    m_LoadedCountText.text = (loadedCount + m_UnloadedCount.Get()).ToString();
                } else {
                    m_LoadedCountText.text = loadedCount.ToString();
                    m_UnloadedCountText.text = m_UnloadedCount.Get().ToString();
                }
            } else {
                // If the amount is unlimited then show an empty string.
                m_LoadedCountText.text = string.Empty;
                if (m_UnloadedCountText != null) {
                    m_UnloadedCountText.text = string.Empty;
                }
            }
        }

        /// <summary>
        /// The amount of secondary ammo has changed. Update the count.
        /// </summary>
        private void SecondaryItemCountChange()
        {
            // Version 1.1 changed the loaded count text name.
            if (m_LoadedCountText == null) {
                Debug.LogError("Error: ItemMonito LoadedCountText is null. Please assign this within the inspector.");
                return;
            }

            m_LoadedCountText.text = m_ItemCount.Get().ToString();
        }

        /// <summary>
        /// The EventHandler was cleared. This will happen when a new scene is loaded. Unregister the registered events to prevent old events from being fired.
        /// </summary>
        public void EventHandlerClear()
        {
            EventHandler.UnregisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.UnregisterEvent("OnEventHandlerClear", EventHandlerClear);
        }
    }
}