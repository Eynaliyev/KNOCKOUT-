using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The ItemWheelSliceMonitor will keep each slice of the ItemWheel's UI up in sync with the rest of the game. This includes selecting the currently active Item
    /// and disabling any slices that correspond to an Item that the character does not have in their inventory.
    /// </summary>
    public class ItemWheelSliceMonitor : MonoBehaviour
    {
        [Tooltip("The ItemType that this slice is monitoring")]
        [SerializeField] private PrimaryItemType m_PrimaryItemType;
        [Tooltip("The color of the image when the ItemType is currently active")]
        [SerializeField] private Color m_SelectedColor;
        [Tooltip("The color of the image when the ItemType is not in the character's inventory")]
        [SerializeField] private Color m_DisabledColor;

        // Internal variables
        private Color m_NotSelectedColor;

        // SharedFields
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedMethod<ItemBaseType, bool> m_HasItem = null;

        // Component references
        private GameObject m_GameObject;
        private Image m_Image;
        private ItemWheelMonitor m_ItemWheelMonitor;
        private GameObject m_ItemWheelGameObject;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Image = GetComponent<Image>();
            m_ItemWheelMonitor = transform.GetComponentInParent<ItemWheelMonitor>();
            m_ItemWheelGameObject = m_ItemWheelMonitor.gameObject;

            m_NotSelectedColor = m_Image.color;

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the GameObject.
            m_GameObject.SetActive(false);
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            if (character == null) {
                if (m_GameObject != null) {
                    m_GameObject.SetActive(false);
                }
                return;
            }

            SharedManager.InitializeSharedFields(character, this);

            ToggleVisiblity(false);

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            EventHandler.RegisterEvent<bool>(m_ItemWheelGameObject, "OnItemWheelToggleVisibility", ToggleVisiblity);
        }

        /// <summary>
        /// Show or hide the item slice.
        /// </summary>
        /// <param name="visible">Should the item slice be shown?</param>
        private void ToggleVisiblity(bool visible)
        {
            // Set the correct color if the slice is visible.
            if (visible) {
                if (m_PrimaryItemType == null || !m_HasItem.Invoke(m_PrimaryItemType)) {
                    m_Image.color = m_DisabledColor;
                } else if (m_PrimaryItemType == m_CurrentPrimaryItem.Get().ItemType) {
                    m_Image.color = m_SelectedColor;
                } else {
                    m_Image.color = m_NotSelectedColor;
                }
            }
            m_GameObject.SetActive(visible);
        }

        /// <summary>
        /// This item has been selected. Let the parent wheel monitor know of the change.
        /// </summary>
        public void ItemSelected()
        {
            m_ItemWheelMonitor.ItemSelected(m_PrimaryItemType);
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