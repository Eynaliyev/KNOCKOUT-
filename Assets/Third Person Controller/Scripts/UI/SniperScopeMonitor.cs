using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The SniperScopeMonitor will monitor the visiblity of the scope UI.
    /// </summary>
    public class SniperScopeMonitor : MonoBehaviour
    {
        // Component references
        private GameObject m_GameObject;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;

            EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
            EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);

            // Start disabled. AttachCharacter will enable the GameObject.
            ShowScope(false);
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            ShowScope(false);

            if (character == null) {
                return;
            }

            EventHandler.RegisterEvent<bool>(character, "OnItemShowScope", ShowScope);
        }

        /// <summary>
        /// Shows or hides the scope.
        /// </summary>
        /// <param name="show">Should the scope be shown?</param>
        private void ShowScope(bool show)
        {
            m_GameObject.SetActive(show);
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