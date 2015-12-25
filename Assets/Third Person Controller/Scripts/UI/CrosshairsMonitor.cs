using UnityEngine;
using UnityEngine.UI;

namespace Opsive.ThirdPersonController.UI
{
    /// <summary>
    /// The CrosshairsMonitor will keep the crosshairs UI in sync with the rest of the game. This includes showing the correct crosshairs type and accounting for any recoil.
    /// </summary>
    public class CrosshairsMonitor : MonoBehaviour
    {
        [Tooltip("The normal color of the crosshairs")]
        [SerializeField] private Color m_CrosshairsColor;
        [Tooltip("The color of the crosshairs when the character is targeting an enemy")]
        [SerializeField] private Color m_CrosshairsTargetColor;
        [Tooltip("The layer mask of the enemy")]
        [SerializeField] private LayerMask m_CrosshairsTargetLayer;
        [Tooltip("The image for the left crosshairs")]
        [SerializeField] private Image m_LeftCrosshairsImage;
        [Tooltip("The image for the top crosshairs")]
        [SerializeField] private Image m_TopCrosshairsImage;
        [Tooltip("The image for the right crosshairs")]
        [SerializeField] private Image m_RightCrosshairsImage;
        [Tooltip("The image for the bottom crosshairs")]
        [SerializeField] private Image m_BottomCrosshairsImage;
        [Tooltip("The sprite used when no item is active")]
        [SerializeField] private Sprite m_NoItemSprite;
        [Tooltip("The distance to start scaling the crosshairs")]
        [SerializeField] private float m_NearCrosshairsDistance;
        [Tooltip("The distance to end scaling the crosshairs")]
        [SerializeField] private float m_FarCrosshairsDistance;
        [Tooltip("The scale of the crosshairs when the crosshairs hits a near object")]
        [SerializeField] private Vector3 m_NearCrosshairsScale = Vector3.one;
        [Tooltip("The scale of the crosshairs when the crosshairs hits a far object")]
        [SerializeField] private Vector3 m_FarCrosshairsScale = Vector3.one;

        // SharedFields
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedMethod<bool, Vector3> m_TargetLookDirection = null;
        
        // Internal variables
        private float m_RecoilAmount;
        private RaycastHit m_RaycastHit;

        // Component references
        private GameObject m_GameObject;
        private Image m_Image;
        private RectTransform m_RectTransform;
        private Transform m_CameraTransform;

        private RectTransform m_ImageRectTransform;
        private RectTransform m_LeftCrosshairsRectTransform;
        private RectTransform m_TopCrosshairsRectTransform;
        private RectTransform m_RightCrosshairsRectTransform;
        private RectTransform m_BottomCrosshairsRectTransform;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_RectTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();
            var camera = Utility.FindCamera();
            m_CameraTransform = camera.transform;
            camera.GetComponent<CameraMonitor>().Crosshairs = transform;

            m_ImageRectTransform = m_Image.GetComponent<RectTransform>();
            m_LeftCrosshairsRectTransform = m_LeftCrosshairsImage.GetComponent<RectTransform>();
            m_TopCrosshairsRectTransform = m_TopCrosshairsImage.GetComponent<RectTransform>();
            m_RightCrosshairsRectTransform = m_RightCrosshairsImage.GetComponent<RectTransform>();
            m_BottomCrosshairsRectTransform = m_BottomCrosshairsImage.GetComponent<RectTransform>();

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
                gameObject.SetActive(false);
                return;
            }

            SharedManager.InitializeSharedFields(character, this);
            SharedManager.InitializeSharedFields(m_CameraTransform.gameObject, this);

            PrimaryItemChange(m_CurrentPrimaryItem.Get());

            // Register for the events. Do not register within OnEnable because the character may not be known at that time.
            EventHandler.RegisterEvent<Item>(character, "OnInventoryPrimaryItemChange", PrimaryItemChange);
            EventHandler.RegisterEvent<bool>(character, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<float>(character, "OnCameraUpdateRecoil", UpdateRecoil);
            EventHandler.RegisterEvent<bool>(character, "OnLaserSightUsableLaserSightActive", DisableCrosshairs);
            EventHandler.RegisterEvent<bool>(character, "OnItemShowScope", DisableCrosshairs);

            m_GameObject.SetActive(true);
        }

        /// <summary>
        /// Change the color of the crosshairs when the camera is looking at an object within the crosshairs target layer.
        /// </summary>
        private void Update()
        {
            var lookDirection = m_TargetLookDirection.Invoke(false);
            // Turn the GUI the target color if a target was hit.
            Color crosshairsColor;
            if (Physics.Raycast(m_CameraTransform.position, lookDirection, out m_RaycastHit, Mathf.Infinity, LayerManager.Mask.IgnoreInvisibleLayersPlayer)) {
                // Change to the target color if the raycast hit the target layer.
                if (Utility.InLayerMask(m_RaycastHit.transform.gameObject.layer, m_CrosshairsTargetLayer.value)) {
                    crosshairsColor = m_CrosshairsTargetColor;
                } else {
                    crosshairsColor = m_CrosshairsColor;
                }
                // Dynamically change the scale of the crosshairs based on the distance of the object hit.
                if (m_FarCrosshairsDistance != m_NearCrosshairsDistance) {
                    var scale = Vector3.Lerp(m_NearCrosshairsScale, m_FarCrosshairsScale, (m_RaycastHit.distance - m_NearCrosshairsDistance) / (m_FarCrosshairsDistance - m_NearCrosshairsDistance));
                    m_ImageRectTransform.localScale = scale;
                }
            } else {
                crosshairsColor = m_CrosshairsColor;
            }
            // Set the color of all of the crosshairs images.
            m_Image.color = m_LeftCrosshairsImage.color = m_TopCrosshairsImage.color = m_RightCrosshairsImage.color = m_BottomCrosshairsImage.color = crosshairsColor;
        }

        /// <summary>
        /// The primary item has been changed. Update the crosshairs to reflect this new item.
        /// </summary>
        /// <param name="item">The new item. Can be null.</param>
        private void PrimaryItemChange(Item item)
        {
            CrosshairsType crosshairs = null;
            if (item == null) {
                m_Image.sprite = m_NoItemSprite;
            } else {
                crosshairs = item.CrosshairsSprite;
                m_Image.sprite = crosshairs.Center;
            }
            // Change the size of the crosshairs image according to the size of the sprite.
            SizeSprite(m_Image.sprite, m_RectTransform);

            // Assume the directional crosshairs are not active.
            var directionalCrosshairsActive = false;
            if (crosshairs != null && crosshairs.Left != null) {
                directionalCrosshairsActive = true;
                // Assign and position/size the left crosshairs.
                m_LeftCrosshairsImage.sprite = crosshairs.Left;
                PositionSprite(m_LeftCrosshairsRectTransform, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset), 0);
                SizeSprite(m_LeftCrosshairsImage.sprite, m_LeftCrosshairsRectTransform);

                // Assign and position/size the top crosshairs.
                m_TopCrosshairsImage.sprite = crosshairs.Top;
                PositionSprite(m_TopCrosshairsRectTransform, 0, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset);
                SizeSprite(m_TopCrosshairsImage.sprite, m_TopCrosshairsRectTransform);

                // Assign and position/size the right crosshairs.
                m_RightCrosshairsImage.sprite = crosshairs.Right;
                PositionSprite(m_RightCrosshairsRectTransform, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset, 0);
                SizeSprite(m_RightCrosshairsImage.sprite, m_RightCrosshairsRectTransform);

                // Assign and position/size the bottom crosshairs.
                m_BottomCrosshairsImage.sprite = crosshairs.Bottom;
                PositionSprite(m_BottomCrosshairsRectTransform, 0, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset));
                SizeSprite(m_BottomCrosshairsImage.sprite, m_BottomCrosshairsRectTransform);
            }

            m_LeftCrosshairsImage.enabled = m_TopCrosshairsImage.enabled = m_RightCrosshairsImage.enabled = m_BottomCrosshairsImage.enabled = directionalCrosshairsActive;
        }

        /// <summary>
        /// Positions the sprite according to the specified x and y position.
        /// </summary>
        /// <param name="spriteRectTransform">The transform to position.</param>
        /// <param name="xPosition">The x position of the sprite.</param>
        /// <param name="yPosition">The y position of the sprite.</param>
        private void PositionSprite(RectTransform spriteRectTransform, float xPosition, float yPosition)
        {
            var position = spriteRectTransform.localPosition;
            position.x = xPosition;
            position.y = yPosition;
            spriteRectTransform.localPosition = position;
        }

        /// <summary>
        /// Change the size of the RectTransform according to the size of the sprite.
        /// </summary>
        /// <param name="sprite">The sprite that the RectTransform should change its size to.</param>
        /// <param name="spriteRectTransform">A reference to the RectTransform.</param>
        private void SizeSprite(Sprite sprite, RectTransform spriteRectTransform)
        {
            if (sprite != null) {
                var sizeDelta = spriteRectTransform.sizeDelta;
                sizeDelta.x = sprite.textureRect.width;
                sizeDelta.y = sprite.textureRect.height;
                spriteRectTransform.sizeDelta = sizeDelta;
            }
        }

        /// <summary>
        /// The character has fired their weapon and a recoil has been added. Move the directional crosshair images according to that recoil amount.
        /// </summary>
        /// <param name="recoilAmount">The amount of recoil to apply.</param>
        private void UpdateRecoil(float recoilAmount)
        {
            // No need to apply recoil if there is no item.
            var primaryItem = m_CurrentPrimaryItem.Get();
            if (primaryItem == null) {
                return;
            }

            m_RecoilAmount = recoilAmount;
            var crosshairs = primaryItem.CrosshairsSprite;
            
            // The directional crosshairs should change position according to the amount of recoil.
            if (crosshairs.Left != null) {
                PositionSprite(m_LeftCrosshairsRectTransform, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset), 0);
                PositionSprite(m_TopCrosshairsRectTransform, 0, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset);
                PositionSprite(m_RightCrosshairsRectTransform, Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset, 0);
                PositionSprite(m_BottomCrosshairsRectTransform, 0, -(Screen.width * crosshairs.AccuracyLossPercent * m_RecoilAmount + crosshairs.Offset));
            }
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            m_GameObject.SetActive(allow);
        }

        /// <summary>
        /// Should the crosshairs be disabled?
        /// </summary>
        /// <param name="disable">True if the crosshairs should be disabled.</param>
        private void DisableCrosshairs(bool disable)
        {
            m_GameObject.SetActive(!disable);
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