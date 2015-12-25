using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The HitIndicatorMonitor will show and reposition the hit indicators UI images to point in the direction that the character was hit.
    /// </summary>
    public class HitIndicatorMonitor : MonoBehaviour
    {
        /// <summary>
        /// Indicates where the character took damage.
        /// </summary>
        private struct HitIndicator
        {
            // Internal variables
            private Vector3 m_Position;
            private float m_DisplayTime;
            private Image m_Image;
            private RectTransform m_RectTransform;

            // Exposed properties
            public Vector3 Position { get { return m_Position; } }
            public float DisplayTime { get { return m_DisplayTime; } set { m_DisplayTime = value; } }
            public Image Image { get { return m_Image; } }
            public RectTransform RectTransform { get { return m_RectTransform; } }

            /// <summary>
            /// Set the pooled HitIndicator values.
            /// </summary>
            /// <param name="position">The position of the damage.</param>
            /// <param name="image">A reference to the UI Image component being used.</param>
            public void Initialize(Vector3 position, Image image)
            {
                m_Position = position;
                m_Image = image;
                m_Image.enabled = true;
                m_RectTransform = image.GetComponent<RectTransform>();
                m_DisplayTime = Time.time;
            }
        }

        [Tooltip("Prevent a new hit indicator from appearing if the angle is less than this threshold compared to an already displayed indicator")]
        [SerializeField] private float m_HitIndicatorAngleThreshold;
        [Tooltip("The offset of the hit indicator from the center of the screen")]
        [SerializeField] private float m_HitIndicatorOffset;
        [Tooltip("The maximum number of hit indicators to show at any one time")]
        [SerializeField] private float m_MaxHitIndicators = 3;
        [Tooltip("The amount of time the hit indicator should be fully visible for")]
        [SerializeField] private float m_HitIndicatorVisiblityTime;
        [Tooltip("The amount of time it takes the hit indicator to fade")]
        [SerializeField] private float m_HitIndicatorFadeTime;

        // Internal variables
        private int m_NextHitIndicatorImage;
        private List<HitIndicator> m_HitIndicators = new List<HitIndicator>();

        // Component references
        private Image[] m_Images;
        private Transform m_CharacterTransform;
        private Transform m_CameraTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_CameraTransform = Utility.FindCamera().transform;
            m_Images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < m_Images.Length; ++i) {
                m_Images[i].enabled = false;
            }

            if (m_Images.Length < m_MaxHitIndicators) {
                Debug.LogWarning("Warning: The number of hit indicator images is less than the maximum number of hit indicators. The number of images should be greater than or equal to " +
                                 "the maximum number of indicators.");
            }

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

            m_CharacterTransform = character.transform;

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            EventHandler.RegisterEvent<float, Vector3, Vector3>(character, "OnHealthDamageDetails", ShowHitIndicator);

            gameObject.SetActive(true);
        }

        /// <summary>
        /// One or more hit indicators are shown. 
        /// </summary>
        private void Update()
        {
            // Move from a 3D coordinate to a 2D coordinate by ignoring the Y position.
            var cameraForward = m_CameraTransform.forward;
            cameraForward.y = 0;
            var characterPosition = m_CharacterTransform.position;
            characterPosition.y = 0;

            for (int i = m_HitIndicators.Count - 1; i > -1; --i) {
                // Fade out the older hit indicators more quickly if there are too many hit indicators.
                var visibilityTime = m_HitIndicatorVisiblityTime;
                if (m_HitIndicators.Count > m_MaxHitIndicators && i + m_MaxHitIndicators < m_HitIndicators.Count) {
                    visibilityTime = 0;
                }

                // The alpha value is determined by the amount of time the damage arrow has been visible. The arrow should be visible for a time of m_HitIndicatorVisiblityTime
                // with no fading. After m_HitIndicatorVisiblityTime, the arrow should fade for visibilityTime.
                var alpha = (m_HitIndicatorFadeTime - (Time.time - (m_HitIndicators[i].DisplayTime + visibilityTime))) / m_HitIndicatorFadeTime;
                if (alpha <= 0) {
                    m_HitIndicators[i].Image.enabled = false;
                    ObjectPool.Return(m_HitIndicators[i]);
                    m_HitIndicators.RemoveAt(i);
                    if (m_HitIndicators.Count == 0) {
                        enabled = false;
                    }
                    continue;
                }
                var color = m_HitIndicators[i].Image.color;
                color.a = alpha;
                m_HitIndicators[i].Image.color = color;

                // Determine the direction of the indicator by the position that the damage was inflicted and the direction the camera is facing. 
                var direction = (characterPosition - m_HitIndicators[i].Position).normalized;
                var angle = Vector3.Angle(direction, cameraForward.normalized) * Mathf.Sign(Vector3.Dot(direction, m_CameraTransform.right));
                var rotation = m_HitIndicators[i].RectTransform.localEulerAngles;
                rotation.z = -angle;
                m_HitIndicators[i].RectTransform.localEulerAngles = rotation;

                // Position the indicator relative to the direction.
                var position = m_HitIndicators[i].RectTransform.localPosition;
                position.x = -Mathf.Sin(angle * Mathf.Deg2Rad) * m_HitIndicatorOffset;
                position.y = -Mathf.Cos(angle * Mathf.Deg2Rad) * m_HitIndicatorOffset;
                m_HitIndicators[i].RectTransform.localPosition = position;
            }
        }

        /// <summary>
        /// The character took some damage at the specified position. Point to that position.
        /// </summary>
        /// <param name="amount">The total amount of damage inflicted on the character.</param>
        /// <param name="position">The position that the character took the damage.</param>
        /// <param name="force">The amount of force applied to the object while taking the damage.</param>
        private void ShowHitIndicator(float amount, Vector3 position, Vector3 force)
        {
            // Don't show a hit indicator if the force is 0. This prevents damage such as fall damage from appearing in the hit indicator.
            if (force.sqrMagnitude == 0) {
                return;
            }

            // Ignore y position.
            var cameraForward = m_CameraTransform.forward;
            var characterPosition = m_CharacterTransform.position;
            characterPosition.y = 0;
            position.y = 0;

            // Determine the new angle of the damage position to determine if a new hit indicator should be shown.
            var direction = (characterPosition - position).normalized;
            var newAngle = Vector3.Angle(direction, cameraForward.normalized) * Mathf.Sign(Vector3.Dot(direction, m_CameraTransform.right));

            // Do not show a new hit indicator if the angle is less than a threshold compared to an already displayed indicator
            HitIndicator hitIndicator;
            for (int i = 0; i < m_HitIndicators.Count; ++i) {
                hitIndicator = m_HitIndicators[i];
                direction = (characterPosition - hitIndicator.Position).normalized;
                var angle = Vector3.Angle(direction, cameraForward.normalized) * Mathf.Sign(Vector3.Dot(direction, m_CameraTransform.right));
                if (Mathf.Abs(angle - newAngle) < m_HitIndicatorAngleThreshold) {
                    hitIndicator.DisplayTime = Time.time;
                    m_HitIndicators[i] = hitIndicator;
                    return;
                }
            }

            // Add the indicator to the active hit indicators list and enable the component.
            hitIndicator = ObjectPool.Get<HitIndicator>();
            hitIndicator.Initialize(position, m_Images[m_NextHitIndicatorImage]);
            m_HitIndicators.Add(hitIndicator);
            m_NextHitIndicatorImage = (m_NextHitIndicatorImage + 1) % m_Images.Length;
            enabled = true;
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