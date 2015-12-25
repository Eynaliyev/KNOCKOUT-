using UnityEngine;
using UnityEngine.UI;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// Fades the Image in the specified number of seconds.
    /// </summary>
    public class ImageFader : MonoBehaviour
    {
        [Tooltip("The duration of the fade")]
        [SerializeField] private float m_FadeDuration = 0.75f;
        [Tooltip("Should the image fade when the component is enabled?")]
        [SerializeField] private bool m_FadeOnStart;
        [Tooltip("Should the iamge fade when the character respawns?")]
        [SerializeField] private bool m_FadeOnRespawn;

        // Component references
        private Image m_Image;
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            if (m_FadeOnRespawn) {
                EventHandler.RegisterEvent<GameObject>("OnCameraAttachCharacter", AttachCharacter);
                EventHandler.RegisterEvent("OnEventHandlerClear", EventHandlerClear);
            }
        }

        /// <summary>
        /// The character has been attached to the camera. Update the UI reference and initialze the character-related values.
        /// </summary>
        /// <param name="character"></param>
        private void AttachCharacter(GameObject character)
        {
            if (m_Character != null) {
                EventHandler.UnregisterEvent(m_Character, "OnRespawn", Fade);
            }
            m_Character = character;
            EventHandler.RegisterEvent(m_Character, "OnRespawn", Fade);
        }

        /// <summary>
        /// Start fading if specified.
        /// </summary>
        private void Start()
        {
            if (m_FadeOnStart) {
                Fade();
            }
        }

        /// <summary>
        /// Fade away.
        /// </summary>
        public void Fade()
        {
            if (m_Image == null) {
                m_Image = GetComponent<Image>();
            }

            m_Image.gameObject.SetActive(true);
            // Set the alpha to 1 and then slowly fade.
            m_Image.CrossFadeAlpha(1, 0, true);
            m_Image.CrossFadeAlpha(0, m_FadeDuration, true);
            Scheduler.Schedule(m_FadeDuration, Deactivate);
        }

        /// <summary>
        /// Deactivates the image GameObject.
        /// </summary>
        private void Deactivate()
        {
            m_Image.gameObject.SetActive(false);
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