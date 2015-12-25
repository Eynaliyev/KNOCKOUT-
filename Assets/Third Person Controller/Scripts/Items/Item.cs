using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// An item is the base class of anything that can be picked up. 
    /// Items can be used by subscribing to the IUsableItem interface and reloaded by subscribing to the IReloadableItem interface.
    /// </summary>
    public abstract class Item : MonoBehaviour
    {
        [Tooltip("A reference to the base item type")]
        [SerializeField] protected ItemBaseType m_ItemType;
        [Tooltip("The name of the Animator substate")]
        [SerializeField] protected string m_ItemName;
        [Tooltip("The state while idle")]
        [SerializeField] protected AnimatorItemStateData m_IdleState = new AnimatorItemStateData("Idle", 0.1f, true);
        [Tooltip("The state while idle")]
        [SerializeField] protected AnimatorItemStateData m_MovementState = new AnimatorItemStateData("Movement", 0.1f, true);
        [Tooltip("The state while aiming")]
        [SerializeField] protected AnimatorItemStatesData m_AimStates = new AnimatorItemStatesData("Aim", 0.1f, true);
        [Tooltip("The state while equipping the item")]
        [SerializeField] protected AnimatorItemStateData m_EquipState = new AnimatorItemStateData("Equip", 0.1f, false);
        [Tooltip("The state while unequipping the item")]
        [SerializeField] protected AnimatorItemStateData m_UnequipState = new AnimatorItemStateData("Unequip", 0.1f, false);
        [Tooltip("The Item sprite used by the UI")]
        [SerializeField] private Sprite m_ItemSprite;
        [Tooltip("Should the item sprite appear on the right side? Only used when the ItemMonitor.MonitorType is set to left or right hand.")]
        [SerializeField] private bool m_RightItemSprite = true;
        [Tooltip("The Item crosshairs used by the UI")]
        [SerializeField] private CrosshairsType m_CrosshairsSprite;
        [Tooltip("Does the character hold the item with both hands?")]
        [SerializeField] private bool m_TwoHandedItem;
        [Tooltip("A reference to the ItemPickup prefab which will spawn after the character has died")]
        [SerializeField] private GameObject m_ItemPickup;

        // Exposed properties
        public ItemBaseType ItemType { set { m_ItemType = value; } get { return m_ItemType; } }
        public string ItemName
        {
            set { m_ItemName = value; }
            get
            {
                // If a dual wield item exists then the item name is a concatination between the two items.
                var dualWieldItem = m_CurrentDualWieldItem.Get();
                if (dualWieldItem != null && dualWieldItem.ItemType != m_ItemType) {
                    var primaryName = true;
                    // Determine which name comes first. While this step isn't necessarily necessary, it does make things easier on the animator
                    // because there only needs to be one substate machine per set of dual wielded items. This will normalize the names so one name always comes first.
                    // For example, if the primary item is a Pistol and the dual wielded item is a Shield, the name will be "Pistol Shield". The name will still be
                    // "Pistol Shield" even if the primary item is a Shield and the dual wielded item is a Pistol.
                    if (dualWieldItem.ItemType is PrimaryItemType) {
                        var dualWieldItemType = dualWieldItem.ItemType as PrimaryItemType;
                        for (int i = 0; i < dualWieldItemType.DualWieldItems.Length; ++i) {
                            if (dualWieldItemType.DualWieldItems[i].ItemType.Equals(m_ItemType)) {
                                primaryName = dualWieldItemType.DualWieldItems[i].PrimaryName;
                                break;
                            }
                        }
                    }

                    if (primaryName) {
                        return string.Format("{0} {1}", m_ItemName, dualWieldItem.ItemName);
                    }
                    return string.Format("{0} {1}", dualWieldItem.ItemName, m_ItemName);
                }
                return m_ItemName;
            }
        }
        public Transform HandTransform { get { return m_HandTransform; } }
        public Sprite ItemSprite { get { return m_ItemSprite; } }
        public bool RightItemSprite { get { return m_RightItemSprite; } }
        public CrosshairsType CrosshairsSprite { get { return m_CrosshairsSprite; } }
        public bool TwoHandedItem { get { return m_TwoHandedItem; } }
        public GameObject ItemPickup { get { return m_ItemPickup; } }

        // SharedFields
        private SharedMethodArg<SphereCollider> m_SetItemCollider = null;
        protected SharedProperty<Item> m_CurrentDualWieldItem = null;
#if ENABLE_MULTIPLAYER
        protected SharedMethod<bool> m_IsServer = null;
#endif

        // Internal fields
        protected int m_ArmLayer;
        private bool m_IsEquipping;
        private bool m_IsUnequipping;

        // Component references
        private SphereCollider m_SphereCollider;
        private Transform m_HandTransform;
        protected AnimatorMonitor m_AnimatorMonitor;
        protected GameObject m_Character;
        protected RigidbodyCharacterController m_Controller;
        protected Inventory m_Inventory;
#if ENABLE_MULTIPLAYER
        protected NetworkMonitor m_NetworkMonitor;
#endif

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public virtual void Awake()
        {
            // The parent of ItemPlacement will be a bone transform.
            m_HandTransform = GetComponentInParent<ItemPlacement>().transform.parent;

            m_SphereCollider = GetComponent<SphereCollider>();
            if (m_SphereCollider != null) {
                EventHandler.RegisterEvent<bool>(transform.GetComponentInParent<RigidbodyCharacterController>().gameObject, "OnControllerEnableItemCollider", EnableItemCollider);
            }

            // The states need to know the item type so they can pass this information onto the AnimatorMonitor.
            m_IdleState.ItemType = m_MovementState.ItemType = m_AimStates.ItemType = m_EquipState.ItemType = m_UnequipState.ItemType = m_ItemType;
        }

        /// <summary>
        /// Initializes all of the SharedFields and initializes the sphere collider.
        /// </summary>
        protected virtual void Start()
        {
            // If SetItemCollider is not null then the component ItemColliderPositioner exists and is managing the colliders.
            if (m_SphereCollider != null && m_SetItemCollider != null) {
                m_SetItemCollider.Invoke(m_SphereCollider);
                m_SphereCollider.enabled = false;
            }
        }

        /// <summary>
        /// Tell ItemColliderPositioner that the collider has been enabled.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (m_SetItemCollider != null && m_SphereCollider != null) {
                m_SetItemCollider.Invoke(m_SphereCollider);
                m_SphereCollider.enabled = false;
            }
        }

        /// <summary>
        /// Tell ItemColliderPositioner that the collider has been disabled.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (m_SetItemCollider != null) {
                m_SetItemCollider.Invoke(null);
            }
        }

        /// <summary>
        /// Initializes the item after it has been added to the Inventory.
        /// </summary>
        public virtual void Init(Inventory inventory)
        {
            m_Inventory = inventory;
            m_Character = inventory.gameObject;
#if ENABLE_MULTIPLAYER
            m_NetworkMonitor = m_Character.GetComponent<NetworkMonitor>();
#endif
            m_AnimatorMonitor = inventory.GetComponent<AnimatorMonitor>();
            m_Controller = inventory.GetComponent<RigidbodyCharacterController>();
            var rightHand = inventory.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand).Equals(m_HandTransform);
            // Cache the layer that the Animator arm layer is in.
            m_ArmLayer = rightHand ? m_AnimatorMonitor.GetRightArmLayerIndex() : m_AnimatorMonitor.GetLeftArmLayerIndex();

            SharedManager.InitializeSharedFields(m_Character, this);

            EventHandler.RegisterEvent<bool>(gameObject, "OnInventoryItemEquipping", OnItemEquipping);
            EventHandler.RegisterEvent(gameObject, "OnInventoryItemEquipped", OnItemEquipped);
            EventHandler.RegisterEvent(gameObject, "OnInventoryItemUnequipped", OnItemUnequipped);
        }

        /// <summary>
        /// Enables or disables the collider. Will be called from
        /// </summary>
        /// <param name="enable">Should the collider be enabled?</param>
        private void EnableItemCollider(bool enable)
        {
            // SetItemCollider will not be null if the component ItemColliderPositioner exists. If it does not exist then enable/disable the collider normally.
            if (m_SetItemCollider != null) {
                m_SetItemCollider.Invoke(enable ? m_SphereCollider : null);
            } else {
                m_SphereCollider.enabled = enable;
            }
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="highPriority">Should the high priority animation be retrieved? High priority animations get tested before the character abilities.</param>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. A null value indicates no change.</returns>
        public virtual AnimatorItemStateData GetDestinationState(bool highPriority, int layer)
        {
            // Secondary Items are not visible so cannot aim or play any idle/movement states.
            if (m_ItemType is SecondaryItemType) {
                return null;
            }

            // Only aiming is a high priority animation, the idle and movement animations can be run if no abilities need to play an animation.
            if (highPriority) {
                // The arm layer should be used for dual wielded items and secondary items. If only one item is equipped then the entire upper body layer can be used.
                var useArmLayer = (m_CurrentDualWieldItem.Get() != null) || m_ItemType is SecondaryItemType;

                if (layer == m_AnimatorMonitor.GetLowerBodyLayerIndex()) {
                    if (m_Controller.Aiming) {
                        // Not all items have a lower body aim animation. Only play the animation if the state exists.
                        var aimstate = m_AimStates.GetState();
                        if (aimstate != null && !string.IsNullOrEmpty(aimstate.LowerStateName)) {
                            return aimstate;
                        }
                    }
                } else if (layer == m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                    if (!useArmLayer) {
                        if (m_IsEquipping) {
                            return GetEquipState();
                        }
                        if (m_IsUnequipping) {
                            return GetUnequipState();
                        }
                    }
                    // Both items have to aim at the same time so an arm check is not necessary.
                    if (m_Controller.Aiming) {
                        return m_AimStates.GetState();
                    }
                } else if (layer == m_ArmLayer) {
                    // Equip/unequip is done on the individual layer.
                    if (useArmLayer) {
                        if (m_IsEquipping) {
                            return GetEquipState();
                        }
                        if (m_IsUnequipping) {
                            return GetUnequipState();
                        }
                    }
                }
                // No high priority animations need to play. Return null.
                return null;
            }

            // A lower priority animation can play. Play the move or idle state.
            if (layer == m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                if (m_Controller.Moving) {
                    return GetMovementState();
                }
                return GetIdleState();
            }
            return null;
        }

        /// <summary>
        /// The item is being equipped or unequipped. Play the corresponding animation.
        /// </summary>
        /// <param name="equip">Is the item being equipped?</param>
        private void OnItemEquipping(bool equip)
        {
            if (equip) {
                m_IsEquipping = true;
            } else {
                m_IsUnequipping = true;
            }

            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The item has been equipped.
        /// </summary>
        private void OnItemEquipped()
        {
            m_IsEquipping = false;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// The item has been unequipped.
        /// </summary>
        private void OnItemUnequipped()
        {
            m_IsUnequipping = false;
            m_AnimatorMonitor.DetermineStates();
        }

        /// <summary>
        /// Returns the Item's idle state.
        /// </summary>
        /// <returns>The Item's idle state.</returns>
        public virtual AnimatorItemStateData GetIdleState()
        {
            return m_IdleState;
        }

        /// <summary>
        /// Returns the Item's movement state.
        /// </summary>
        /// <returns>The Item's movement state.</returns>
        public virtual AnimatorItemStateData GetMovementState()
        {
            return m_MovementState;
        }

        /// <summary>
        /// Returns the Item's equip state.
        /// </summary>
        /// <returns>The Item's equip state.</returns>
        public virtual AnimatorItemStateData GetEquipState()
        {
            return m_EquipState;
        }

        /// <summary>
        /// Returns the Item's unequip state.
        /// </summary>
        /// <returns>The Item's unequip state.</returns>
        public virtual AnimatorItemStateData GetUnequipState()
        {
            return m_UnequipState;
        }
    }
}