using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The Inventory manager all of the Items. It allows items to be used, reloaded, dropped, etc. It also communicates with the Animator to trigger animations when switching between
    /// items.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class Inventory : NetworkBehaviour
#else
    public class Inventory : MonoBehaviour
#endif
    {
        /// <summary>
        /// Specifies the amount of each ItemBaseType that the character can pickup or is loaded with the default inventory.
        /// </summary>
        [System.Serializable]
        public class ItemAmount
        {
            [Tooltip("The type of item")]
            [SerializeField] private ItemBaseType m_ItemType;
            [Tooltip("The number of ItemType units to pickup")]
            [SerializeField] private int m_Amount = 1;

            // Exposed properties
            public ItemBaseType ItemType { get { return m_ItemType; } set { m_ItemType = value; } }
            public int Amount { get { return m_Amount; } set { m_Amount = value; } }

            /// <summary>
            /// ItemAmount constructor with no parameters.
            /// </summary>
            public ItemAmount() { }

            /// <summary>
            /// ItemAmount constructor with two parameters.
            /// </summary>
            public ItemAmount(ItemBaseType itemBaseType, int amount)
            {
                Initialize(itemBaseType, amount);
            }

            /// <summary>
            /// Initializes the ItemAmount to the specified values.
            /// </summary>
            /// <param name="itemBaseType">The ItemType to set.</param>
            /// <param name="amount">The amount of ItemType.</param>
            public void Initialize(ItemBaseType itemBaseType, int amount)
            {
                m_ItemType = itemBaseType;
                m_Amount = amount;
            }
        }

        /// <summary>
        /// An ItemInstance is the actual object that is added to the Inventory. The ItemInstance can represent a PrimaryItemType or a SecondaryItemType. The ItemInstance keeps
        /// track of the variables for that particular item, such as the item count or a reference to the GameObject for the Item.
        /// </summary>
        private class ItemInstance
        {
            // Exposed properties
            public ItemBaseType ItemType { get { return m_ItemType; } }
            public GameObject GameObject { get { return m_GameObject; } }
            public Item Item { get { return m_Item; } }
            public int ItemCount { 
                get
                { 
                    return m_ItemCount;
                } 
                set 
                {
                    m_ItemCount = Mathf.Max(Mathf.Min(value, m_ItemType.GetCapacity()), 0); 
                } 
            }
            public ConsumableItemInstance ConsumableItem { get { return m_ConsumableItem; } set { m_ConsumableItem = value; } }

            // Internal variables
            private ItemBaseType m_ItemType;
            private GameObject m_GameObject;
            private Item m_Item;
            private int m_ItemCount;
            private ConsumableItemInstance m_ConsumableItem;

            /// <summary>
            /// Activates or deactivates the Item's GameObject.
            /// </summary>
            /// <param name="active">True if the GameObject should activate.</param>
            public void SetActive(bool active)
            {
                m_GameObject.SetActive(active);
            }

            /// <summary>
            /// Constructor for the ItemInstance. Will set the internal variables.
            /// </summary>
            /// <param name="item">A reference to the Item that is being added.</param>
            public ItemInstance(Item item)
            {
                m_Item = item;
                m_ItemType = item.ItemType;
                m_GameObject = item.gameObject;
                if (m_ItemType is PrimaryItemType || m_ItemType is DualWieldItemType) {
                    // On the network Awake isn't always immediately called so do it manually.
                    m_Item.Awake();
                    SetActive(false);
                }
            }
        }

        /// <summary>
        /// A ConsumableItemInstance is the actual object that is added to the Inventory. The ConsumableItemInstance keeps track of the variables for that particular
        /// item, such as the number of items that are loaded in a primary item or the number of items that are unloaded.
        /// </summary>
        private class ConsumableItemInstance
        {
            // Exposed properties
            public ConsumableItemType ItemType { get { return m_ItemType; } }
            public PrimaryItemType Owner { get { return m_Owner; } }
            public int Capacity { get { return m_Capacity; } }
            public int UnloadedCount
            {
                get
                {
                    return m_UnloadedCount;
                }
                set
                {
                    // Take the min of the value and capacity minus the loaded counts so the unloaded plus loaded is never more than the max capacity.
                    // Take the max of that value and zero to prevent the count from going below zero.
                    m_UnloadedCount = Mathf.Max(Mathf.Min(value, m_Capacity - m_PrimaryLoadedCount - m_DualWieldLoadedCount), 0);
                }
            }
            public int PrimaryLoadedCount { 
                get 
                { 
                    return m_PrimaryLoadedCount; 
                }
                set
                {
                    m_PrimaryLoadedCount = Mathf.Max(value, 0);
                }
            }
            public int DualWieldLoadedCount
            {
                get
                {
                    return m_DualWieldLoadedCount;
                }
                set
                {
                    m_DualWieldLoadedCount = Mathf.Max(value, 0);
                }
            }

            // Internal variables
            private int m_UnloadedCount;
            private int m_PrimaryLoadedCount;
            private int m_DualWieldLoadedCount;
            private ConsumableItemType m_ItemType;
            private PrimaryItemType m_Owner;
            private int m_Capacity;

            /// <summary>
            /// Constructor for ConsumableItemInstance. Will set the internal variables.
            /// </summary>
            /// <param name="item">The ConsumableItemType that this ConsumableItemInstance represents.</param>
            /// <param name="capacity">The maximum number of consumable items the primary item can hold.</param>
            /// <param name="owner">The PrimaryItemType that can consume this ConsumableItemInstance.</param>
            public ConsumableItemInstance(ConsumableItemType itemType, int capacity, PrimaryItemType owner)
            {
                m_ItemType = itemType;
                m_Capacity = capacity;
                m_Owner = owner;
            }
        }

        [Tooltip("Items to load when the Inventory is initially created or on a character respawn")]
        [SerializeField] private ItemAmount[] m_DefaultLoadout;
        [Tooltip("an the character use unlimited items?")]
        [SerializeField] private bool m_UnlimitedAmmo;
        [Tooltip("The item that should be used when unequipped. This can be used for the character's fist so they can punch when unarmed")]
        [SerializeField] private ItemBaseType m_UnequippedItemType;
        [Tooltip("When the character dies or drops a dual wielded item, should they drop the item?")]
        [SerializeField] private bool m_DropItems;
        [Tooltip("The Transform to parent the dropped items to")]
        [SerializeField] private Transform m_DroppedItemsParent;

        // Internal variables
        private List<ItemInstance> m_PrimaryInventory = new List<ItemInstance>();
        private List<ConsumableItemInstance> m_ConsumableInventory = new List<ConsumableItemInstance>();
        private List<ItemInstance> m_SecondaryInventory = new List<ItemInstance>();
        private List<ItemInstance> m_DualWieldInventory = new List<ItemInstance>();
        private Dictionary<int, ItemBaseType> m_IDItemTypeMap = new Dictionary<int, ItemBaseType>();
        private Dictionary<ItemBaseType, int> m_ItemIndexMap = new Dictionary<ItemBaseType, int>();
        private Dictionary<ItemBaseType, ItemBaseType> m_DualWieldPrimaryItemMap = new Dictionary<ItemBaseType, ItemBaseType>();
        private Dictionary<ItemBaseType, ItemBaseType> m_PrimaryDualWieldItemMap = new Dictionary<ItemBaseType, ItemBaseType>();
        private Dictionary<ItemBaseType, int> m_PrimadyDualWieldItemIndexMap = new Dictionary<ItemBaseType, int>();

        private Item m_UnequippedItem;
        private int m_CurrentPrimaryIndex = -1;
        private int m_CurrentSecondaryIndex = -1;
        private int m_CurrentDualWieldIndex = -1;
        private int m_LastEquipedItem = -1;
        private int m_EquipIndex = -1;
        private int m_UnequpIndex = -1;
        private int m_DualWieldEquipIndex = -1;
        private int m_DualWieldUnequpIndex = -1;
        private bool m_InventoryLoaded = false;
#if ENABLE_MULTIPLAYER
        private int m_StartItemID = -1;
        private int m_StartDualWieldItemID = -1;
#endif

        // SharedFields
        private SharedMethod<bool> m_CanInteractItem = null;
        private bool SharedProperty_ItemEquipped { get { return m_CurrentPrimaryIndex != -1; } }
        private Item SharedProperty_CurrentPrimaryItem { get { return GetCurrentItem(typeof(PrimaryItemType)); } }
        private Item SharedProperty_CurrentSecondaryItem { get { return GetCurrentItem(typeof(SecondaryItemType)); } }
        private Item SharedProperty_CurrentDualWieldItem { get { return GetCurrentItem(typeof(DualWieldItemType)); } }
        private Item SharedProperty_UnequippedItem { get { return m_UnequippedItem; } }
        private int SharedProperty_UnloadedCount { get { return GetCurrentItemCount(typeof(PrimaryItemType), false); } }
        private int SharedProperty_PrimaryLoadedCount { get { return GetCurrentItemCount(typeof(PrimaryItemType), true); } }
        private int SharedProperty_DualWieldLoadedCount { get { return GetCurrentItemCount(typeof(DualWieldItemType), true); } }
        private int SharedProperty_ItemCount { get { return GetCurrentItemCount(typeof(SecondaryItemType), false); } }

        // Exposed properties
        public ItemAmount[] DefaultLoadout { get { return m_DefaultLoadout; } set { m_DefaultLoadout = value; } }
        private int CurrentPrimaryIndex
        {
            set
            {
                m_CurrentPrimaryIndex = value;
                EventHandler.ExecuteEvent<Item>(m_GameObject, "OnInventoryPrimaryItemChange", GetCurrentItem(typeof(PrimaryItemType)));
            }
        }
        private int CurrentDualWieldIndex
        {
            set
            {
                m_CurrentDualWieldIndex = value;
                EventHandler.ExecuteEvent<Item>(m_GameObject, "OnInventoryDualWieldItemChange", GetCurrentItem(typeof(DualWieldItemType)));
            }
        }
        public bool IsSwitchingItems { get { return m_EquipIndex != -1 || m_UnequpIndex != -1; } }
        public ItemBaseType UnequippedItemType { get { return m_UnequippedItemType; } }

        // Component references
        private GameObject m_GameObject;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            EquipUnequpItem(false, -1, true);

            SharedManager.Register(this);

            InitInventory(GetComponentsInChildren<Item>(true));
        }

        /// <summary>
        /// Add all of the items that the inventory can carry to the internal lists. Note that this doesn't mean that the character now has these items and can use them,
        /// rather it means that the character has the potential to use these items. The character still needs to pick the actual item up or have it in their default loadout.
        /// </summary>
        /// <param name="items">An array of items that the Inventory can use.</param>
        private void InitInventory(Item[] items)
        {
            for (int i = 0; i < items.Length; ++i) {
                if (m_UnequippedItemType != null && items[i].ItemType == m_UnequippedItemType) {
                    m_UnequippedItem = items[i];
                    m_IDItemTypeMap.Add(m_UnequippedItemType.ID, m_UnequippedItemType);
                } else {
                    AddInventoryItem(items[i]);
                }

                items[i].Init(this);
            }
        }

        /// <summary>
        /// Adds a particular item type to the inventory. This will convert the item type to an actual item instance.
        /// </summary>
        /// <param name="item">The item that is being added.</param>
        private void AddInventoryItem(Item item)
        {
            // Add the item to the correct inventory list.
            if (item.ItemType is PrimaryItemType) {
                // If the item is a primary item then the consumable item should also be added.
                var primaryItemType = item.ItemType as PrimaryItemType;
                m_ItemIndexMap.Add(primaryItemType, m_PrimaryInventory.Count);
                var itemInstance = new ItemInstance(item);
                m_PrimaryInventory.Add(itemInstance);
                if (primaryItemType.ConsumableItem != null && primaryItemType.ConsumableItem.ItemType != null) {
                    m_ItemIndexMap.Add(primaryItemType.ConsumableItem.ItemType, m_ConsumableInventory.Count);
                    var consumableItemInventory = new ConsumableItemInstance(primaryItemType.ConsumableItem.ItemType, primaryItemType.ConsumableItem.Capacity, primaryItemType);
                    m_ConsumableInventory.Add(consumableItemInventory);
                    itemInstance.ConsumableItem = consumableItemInventory;
                    m_IDItemTypeMap.Add(primaryItemType.ConsumableItem.ItemType.ID, primaryItemType.ConsumableItem.ItemType);
                }
                // Add the ItemInstance to the DualWieldInventory if it can be dual wielded.
                if ((item.ItemType as PrimaryItemType).DualWieldItems.Length > 0) {
                    m_PrimadyDualWieldItemIndexMap.Add(primaryItemType, m_DualWieldInventory.Count);
                    m_DualWieldInventory.Add(itemInstance);
                }
            } else if (item.ItemType is SecondaryItemType) {
                m_ItemIndexMap.Add(item.ItemType, m_SecondaryInventory.Count);
                m_SecondaryInventory.Add(new ItemInstance(item));
            } else if (item.ItemType is DualWieldItemType) {
                m_ItemIndexMap.Add(item.ItemType, m_DualWieldInventory.Count);
                m_DualWieldInventory.Add(new ItemInstance(item));

                m_DualWieldPrimaryItemMap.Add(item.ItemType, (item.ItemType as DualWieldItemType).PrimaryItem);
                m_PrimaryDualWieldItemMap.Add((item.ItemType as DualWieldItemType).PrimaryItem, item.ItemType);
            }
#if UNITY_EDITOR
            // ItemID will be -1 for those updating to version 0.89. This can be removed in the future.
            if (item.ItemType.ID == -1) {
                item.ItemType.ID = UnityEngine.Random.Range(0, int.MaxValue);
                Debug.LogWarning("Warning: Item " + item.ItemType + " ID equals -1. Please click on this ItemType within the project view to update the ID.");
            }
#endif
            m_IDItemTypeMap.Add(item.ItemType.ID, item.ItemType);
        }

        /// <summary>
        /// Register for any events that the inventory should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipped", OnItemEquipped);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipped", OnItemUnequipped);
            EventHandler.RegisterEvent(m_GameObject, "OnAbilityToggleEquippedItem", ToggleEquippedItem);
#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
#endif
        }

        /// <summary>
        /// Unregister for any events that the inventory was registered for.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipped", OnItemEquipped);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipped", OnItemUnequipped);
            EventHandler.UnregisterEvent(m_GameObject, "OnAbilityToggleEquippedItem", ToggleEquippedItem);
#if ENABLE_MULTIPLAYER
            EventHandler.UnregisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Register for the inventory callbacks so the inventory can be synchronized with the current game state.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            SharedManager.InitializeSharedFields(m_GameObject, this);

            NetworkClient.allClients[0].RegisterHandler(NetworkEventManager.NetworkMessages.MSG_ACTIVE_ITEM, NetworkSetActiveItem);
            NetworkClient.allClients[0].RegisterHandler(NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, NetworkPickupItem);
        }

        /// <summary>
        /// Message which indicates which primary item is currently active.
        /// </summary>
        private class ActiveItemMessage : MessageBase
        {
            // Internal variables
            private int m_ItemID;
            private int m_DualWieldItemID;

            // Exposed properties
            public int ItemID { get { return m_ItemID; } set { m_ItemID = value; } }
            public int DualWieldItemID { get { return m_DualWieldItemID; } set { m_DualWieldItemID = value; } }

            /// <summary>
            /// Populate a message object from a NetworkReader stream.
            /// </summary>
            /// <param name="reader">Stream to read from.</param>
            public override void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);

                m_ItemID = reader.ReadInt32();
                m_DualWieldItemID = reader.ReadInt32();
            }

            /// <summary>
            /// Populate a NetworkWriter stream from a message object.
            /// </summary>
            /// <param name="writer">Stream to write to.</param>
            public override void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);

                writer.Write(m_ItemID);
                writer.Write(m_DualWieldItemID);
            }
        }

        /// <summary>
        /// Message which indicates which item the character currently has.
        /// </summary>
        private class PickupItemMessage : MessageBase
        {
            // Internal variables
            private int m_ItemID;
            private int m_ItemCount;
            private int m_LoadedCount;
            private int m_UnloadedCount;

            // Exposed properties
            public int ItemID { get { return m_ItemID; } set { m_ItemID = value; } }
            public int ItemCount { get { return m_ItemCount; } set { m_ItemCount = value; } }
            public int LoadedCount { get { return m_LoadedCount; } set { m_LoadedCount = value; } }
            public int UnloadedCount { get { return m_UnloadedCount; } set { m_UnloadedCount = value; } }

            /// <summary>
            /// Populate a message object from a NetworkReader stream.
            /// </summary>
            /// <param name="reader">Stream to read from.</param>
            public override void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);

                m_ItemID = reader.ReadInt32();
                m_ItemCount = reader.ReadInt32();
                m_LoadedCount = reader.ReadInt32();
                m_UnloadedCount = reader.ReadInt32();
            }

            /// <summary>
            /// Populate a NetworkWriter stream from a message object.
            /// </summary>
            /// <param name="writer">Stream to write to</param>
            public override void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);

                writer.Write(m_ItemID);
                writer.Write(m_ItemCount);
                writer.Write(m_ItemCount);
                writer.Write(m_UnloadedCount);
            }
        }

        /// <summary>
        /// A new client has just joined the server. Send that client the active primary item as well as all of the items that the character is carrying.
        /// </summary>
        /// <param name="netConn">The client connection.</param>
        private void OnServerReady(NetworkConnection netConn)
        {
            var activeItemMessage = ObjectPool.Get<ActiveItemMessage>();
            if (m_CurrentPrimaryIndex != -1) {
                var item = GetCurrentItem(typeof(PrimaryItemType));
                activeItemMessage.ItemID = item.ItemType.ID;
            } else {
                // Send the client a -1 to indicate that it should have all of the items unequipped.
                activeItemMessage.ItemID = -1;
            }
            if (m_CurrentDualWieldIndex != -1) {
                var item = GetCurrentItem(typeof(DualWieldItemType));
                activeItemMessage.DualWieldItemID = item.ItemType.ID;
            } else {
                // Sent the client a -1 to indicate that there is no dual wield item.
                activeItemMessage.DualWieldItemID = -1;
            }
            NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_ACTIVE_ITEM, activeItemMessage);
            ObjectPool.Return(activeItemMessage);

            var pickupItemMessage = ObjectPool.Get<PickupItemMessage>();
            // Send the new client all of the primary items and the number of consumable items that the character is carrying.
            for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                var item = m_PrimaryInventory[i].Item;
                // No reason to send the item if the character doesn't have the item.
                if (m_PrimaryInventory[i].ItemCount > 0) {
                    pickupItemMessage.ItemID = item.ItemType.ID;
                    pickupItemMessage.ItemCount = m_PrimaryInventory[i].ItemCount;
                    pickupItemMessage.LoadedCount = GetItemCount(item.ItemType, true);
                    pickupItemMessage.UnloadedCount = GetItemCount(item.ItemType, false);
                    NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, pickupItemMessage);
                }
            }

            // Send the new client all of the secondary items and the number of items that the character is carrying.
            for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                var item = m_SecondaryInventory[i].Item;
                // No reason to send the item if the character doesn't have the item.
                if (m_SecondaryInventory[i].ItemCount > 0) {
                    pickupItemMessage.ItemID = item.ItemType.ID;
                    pickupItemMessage.LoadedCount = GetItemCount(item.ItemType, true);
                    pickupItemMessage.UnloadedCount = 0;
                    NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_PICKUP_ITEM, pickupItemMessage);
                }
            }

            ObjectPool.Return(pickupItemMessage);
        }

        /// <summary>
        /// Load the correct item for each character.
        /// </summary>
        /// <param name="netMsg">The message being sent.</param>
        private void NetworkSetActiveItem(NetworkMessage netMsg)
        {
            var activeItemMessage = netMsg.ReadMessage<ActiveItemMessage>();
            m_StartItemID = activeItemMessage.ItemID;
            m_StartDualWieldItemID = activeItemMessage.DualWieldItemID;
            m_InventoryLoaded = true;
        }

        /// <summary>
        /// The server has sent the client an item that the character is carrying. Update the inventory.
        /// </summary>
        /// <param name="netMsg">The message being sent.</param>
        private void NetworkPickupItem(NetworkMessage netMsg)
        {
            var pickupItemMessage = netMsg.ReadMessage<PickupItemMessage>();
            // ActiveItemMessage is received before PickupItemMessage so it is safe to use StartItemID.
            PickupItemLocal(pickupItemMessage.ItemID, pickupItemMessage.ItemCount, m_StartItemID == pickupItemMessage.ItemID || m_StartDualWieldItemID == pickupItemMessage.ItemID, true);
            // The item has been added to the inventory. PickupItem assumes the item was just picked up so the loaded and unloaded counts need to be updated.
            SetItemCount(m_IDItemTypeMap[pickupItemMessage.ItemID], pickupItemMessage.LoadedCount, pickupItemMessage.UnloadedCount);
        }

        /// <summary>
        /// The client has left the server. Reset to the default values.
        /// </summary>
        public override void OnNetworkDestroy()
        {
            base.OnNetworkDestroy();

            m_InventoryLoaded = false;
            m_StartItemID = -1;
            m_StartDualWieldItemID = -1;
        }
#endif

        /// <summary>
        /// Load the default loadout. Do this within Start to ensure all of the components have gone through the Awake phase.
        /// </summary>
        private void Start()
        {
#if ENABLE_MULTIPLAYER
            // OnStartClient may have already initialized the SharedFields.
            if (m_CanInteractItem == null) {
#endif
                SharedManager.InitializeSharedFields(m_GameObject, this);
#if ENABLE_MULTIPLAYER
            }
#endif
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);

            // Do not load the default loadout if the client is joining an existing game. The inventory has already been loaded at this point.
            if (!m_InventoryLoaded) {
                LoadDefaultLoadout();
            }

            EventHandler.ExecuteEvent(m_GameObject, "OnInventoryInitialized");
        }

        /// <summary>
        /// Loop through the default loadout list picking up each item.
        /// </summary>
        public void LoadDefaultLoadout()
        {
            if (m_DefaultLoadout != null) {
                for (int i = 0; i < m_DefaultLoadout.Length; ++i) {
                    PickupItemLocal(m_DefaultLoadout[i].ItemType.ID, m_DefaultLoadout[i].Amount, true, true);
                }
                m_InventoryLoaded = true;
            }
        }

        /// <summary>
        /// Pickup an item. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemBaseType">The type of item to pickup.</param>
        /// <param name="amount">The number of items to pickup.</param>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="immediateActivation">Should the item be shown immediately? This only applies to the PrimaryItemType. If false the item will be added with an animation.</param>
        public void PickupItem(int itemID, int amount, bool equip, bool immediateActivation)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcPickupItem(itemID, amount, equip, immediateActivation);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                PickupItemLocal(itemID, amount, equip, immediateActivation);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Pickup an item on the client.
        /// </summary>
        /// <param name="itemBaseType">The type of item to pickup.</param>
        /// <param name="amount">The number of items to pickup.</param>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="immediateActivation">Should the item be shown immediately? This only applies to the PrimaryItemType. If false the item will be added with an animation.</param>
        [ClientRpc]
        private void RpcPickupItem(int itemID, int amount, bool equip, bool immediateActivation)
        {
            PickupItemLocal(itemID, amount, equip, immediateActivation);
        }
#endif

        /// <summary>
        /// Pickup an item. Picking up an item will allow the item to actually be used. Note that if the item is a primary item that it will still need to be equipped.
        /// </summary>
        /// <param name="itemBaseType">The type of item to pickup.</param>
        /// <param name="amount">The number of items to pickup.</param>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="immediateActivation">Should the item be shown immediately? This only applies to the PrimaryItemType. If false the item will be added with an animation.</param>
        private void PickupItemLocal(int itemID, int amount, bool equip, bool immediateActivation)
        {
            ItemBaseType itemType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemType)) {
                Debug.LogError("Unable to pickup item with id " + itemID + ": has it been added to an item object?");
                return;
            }

            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                Debug.LogError("Unable to pickup " + itemType + ": has it been added to an item object?");
                return;
            }

            // If an item is being picked up it must at least have a count of one, even if only consumable items are used.
            if (amount == 0) {
                amount = 1;
            }

            // Immediately activate the item if the item cannot be interacted with. The roll ability will prevent animations since the character cannot play the 
            // equip/unequip animations while rolling. CanInteractItem will be null if the item is being initialized over the network - InventoryItem is called before start. 
            if (m_CanInteractItem != null && !m_CanInteractItem.Invoke()) {
                immediateActivation = true;
            }
            if (itemType is PrimaryItemType) {
                // Only add the item if it hasn't already been added.
                if (m_PrimaryInventory[itemIndex].ItemCount == 0) {
                    m_PrimaryInventory[itemIndex].ItemCount = 1;
                    amount -= 1;
                    if (equip) {
                        // Deactivate the previous item. If the item should not be immediately activated then the animation needs to deactivate the old item
                        // and activate the new item as soon as the old item has been removed.
                        var waitForUnequip = false;
                        var dualWieldItem = false;
                        if (m_CurrentPrimaryIndex != -1) {
                            if (!(dualWieldItem = CanDualWield(m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType, itemType as PrimaryItemType))) {
                                EquipUnequpItem(false, m_CurrentPrimaryIndex, immediateActivation);
                                if (!immediateActivation) {
                                    m_EquipIndex = itemIndex;
                                    waitForUnequip = true;
                                }
                            }
                        }
                        if (!dualWieldItem) {
                            if (!waitForUnequip) {
                                EquipUnequpItem(true, itemIndex, immediateActivation);
                            }
                        } else {
                            PickupDualWieldItem(itemType, itemIndex, immediateActivation);
                        }
                    }

                    // Let the item know that it already has a consumable item if the consumable item has already been added.
                    var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                    if (consumableItem != null && consumableItem.UnloadedCount > 0) {
                        EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_PrimaryInventory[itemIndex].Item, true, m_ConsumableInventory[itemIndex].PrimaryLoadedCount == 0);
                    }
                }

                if (m_PrimaryInventory[itemIndex].ItemCount == 1 && amount == 1) {
                    // If the inventory already has the item and a differnet primary item is equipped then determine if the newly acquired item can be dualwielded. 
                    // Do not pick up the item if it can't.
                    if (m_CurrentPrimaryIndex != -1 || m_EquipIndex != -1) {
                        // If the EquipIndex is not -1 then that item is about to be equipped.
                        var index = m_EquipIndex != -1 ? m_EquipIndex : m_CurrentPrimaryIndex;
                        if (index != itemIndex) {
                            if (CanDualWield(m_PrimaryInventory[index].ItemType as PrimaryItemType, itemType as PrimaryItemType)) {
                                PickupDualWieldItem(itemType, itemIndex, immediateActivation);
                            }
                            return;
                        }
                    }
                    // Only pickup the dual wield item if the primary item is already equipped.
                    ItemBaseType dualWieldItemType;
                    if (m_CurrentPrimaryIndex == itemIndex && m_PrimaryDualWieldItemMap.TryGetValue(itemType, out dualWieldItemType)) {
                        var primaryItemIndex = itemIndex;
                        // Get the ConsumableItem of the PrimaryItemType before switching to the DualWieldItemType. There are no ConsumableItems with the DualWieldItemType.
                        var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                        if (!m_ItemIndexMap.TryGetValue(dualWieldItemType, out itemIndex)) {
                            Debug.LogError("Unable to pickup " + dualWieldItemType + ": has it been added to an item object?");
                            return;
                        }
                        m_PrimaryInventory[primaryItemIndex].ItemCount = 2;

                        // Do not equip the dual wield item if a primary item is currently being equipped.
                        if (m_EquipIndex == -1) {
                            if (equip && primaryItemIndex == m_CurrentPrimaryIndex) {
                                if (m_CurrentDualWieldIndex != -1) {
                                    if (m_DropItems) {
                                        DropItem(m_DualWieldInventory[m_CurrentDualWieldIndex]);
                                    }
                                    RemoveItem(m_DualWieldInventory[m_CurrentDualWieldIndex].ItemType, true);
                                }
                                EquipUnequipDualWieldItem(true, itemIndex, immediateActivation);
                            }
                        } else {
                            m_DualWieldEquipIndex = itemIndex;
                        }
                        // Let the item know that it can reload.
                        if (consumableItem != null && consumableItem.UnloadedCount > 0) {
                            EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_DualWieldInventory[itemIndex].Item, true, true);
                        }
                    }
                }
            } else if (itemType is ConsumableItemType) {
                m_ConsumableInventory[itemIndex].UnloadedCount += amount;
                // The item should be reloaded immediately if immediateActivation is true (coming from a default loadout) or if the item currently is not equipped. If the item
                // is equipped then the reload animation should play.
                var primaryIndex = m_ItemIndexMap[m_ConsumableInventory[itemIndex].Owner];
                var primaryEquipped = primaryIndex == m_CurrentPrimaryIndex && m_EquipIndex == -1;
                // Let any interested objects know that a consumable item has been added.
                EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", m_PrimaryInventory[primaryIndex].Item, true, immediateActivation || !primaryEquipped);
            } else if (itemType is SecondaryItemType) {
                m_SecondaryInventory[itemIndex].ItemCount += amount;
                // Equip the secondary item if it has just been added.
                if (m_SecondaryInventory[itemIndex].ItemCount <= amount) {
                    m_CurrentSecondaryIndex = itemIndex;
                }

                EventHandler.ExecuteEvent(m_GameObject, "OnInventorySecondaryItemCountChange");
            }
        }

        /// <summary>
        /// Can the current ItemType be dual wielded with the new ItemType?
        /// </summary>
        /// <param name="currentItemType">The Item currently equipped.</param>
        /// <param name="newItemType">The Item that is being equipped.</param>
        /// <returns>True if the new ItemType can be dual wielded with the currentItemType.</returns>
        private bool CanDualWield(PrimaryItemType currentItemType, PrimaryItemType newItemType)
        {
            for (int i = 0; i < currentItemType.DualWieldItems.Length; ++i) {
                if (currentItemType.DualWieldItems[i].ItemType.Equals(newItemType)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Pickup the new dual wield Item. 
        /// </summary>
        /// <param name="itemType">The ItemType of the dual wield item being picked up.</param>
        /// <param name="primaryItemIndex">The index of the item within the PrimaryItemInventory list.</param>
        /// <param name="immediateActivation">Should the dual wield item be activated immediately?</param>
        private void PickupDualWieldItem(ItemBaseType itemType, int primaryItemIndex, bool immediateActivation)
        {
            int itemIndex;
            // A dual wield item of a different PrimaryItemType has been picked up. Enable that item.
            if (!m_PrimadyDualWieldItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return;
            }
            // Only one dual wield item can be equipped.
            if (m_CurrentDualWieldIndex != -1) {
                if (m_DropItems) {
                    DropItem(m_DualWieldInventory[m_CurrentDualWieldIndex]);
                }
                RemoveItem(m_DualWieldInventory[m_CurrentDualWieldIndex].ItemType, true);
            }
            // Determine which item should be the primary item.
            var currentPrimaryItemType = m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType;
            var newPrimary = false;
            for (int i = 0; i < currentPrimaryItemType.DualWieldItems.Length; ++i) {
                if (currentPrimaryItemType.DualWieldItems[i].ItemType.Equals(itemType)) {
                    newPrimary = currentPrimaryItemType.DualWieldItems[i].PrimaryName;
                    break;
                }
            }
            // The new dual wield item should be the primary item.
            if (newPrimary) {
                m_CurrentDualWieldIndex = m_PrimadyDualWieldItemIndexMap[m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType];
                EquipUnequpItem(true, primaryItemIndex, immediateActivation);
            } else {
                EquipUnequipDualWieldItem(true, itemIndex, immediateActivation);
            }
        }

        /// <summary>
        /// Returns the DualWieldItemType for the specified PrimaryItemType.
        /// </summary>
        /// <param name="itemType">The PrimaryItemType to get the DualWieldItemType of.</param>
        /// <returns>The DualWieldItemType mapped to the specified PrimaryItemType. Can be null.</returns>
        public ItemBaseType DualWieldItemForPrimaryItem(ItemBaseType itemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return null;
            }

            // The character doesn't have the dual wielded item if the count isn't greater then 1.
            if (m_PrimaryInventory[itemIndex].ItemCount != 2) {
                return null;
            }

            ItemBaseType dualWieldItemType;
            if (m_PrimaryDualWieldItemMap.TryGetValue(itemType, out dualWieldItemType)) {
                return dualWieldItemType;
            }
            return null;
        }

        /// <summary>
        /// Returns a list of items which are currently available in the inventory.
        /// </summary>
        /// <param name="itemTypes">The list of items which are currently available.</param>
        public void GetAvailableItems(ref List<ItemBaseType> itemTypes)
        {
            itemTypes.Clear();

            // Return all of the primary, secondary, and unequipped items that have a count greater than 0.
            for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                if (m_PrimaryInventory[i].ItemCount > 0) {
                    itemTypes.Add(m_PrimaryInventory[i].ItemType);
                }
            }
            for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                if (m_SecondaryInventory[i].ItemCount > 0) {
                    itemTypes.Add(m_SecondaryInventory[i].ItemType);
                }
            }
            if (m_UnequippedItem != null) {
                itemTypes.Add(m_UnequippedItem.ItemType);
            }
        }

        /// <summary>
        /// Returns the current primary, secondary, or dual wield item.
        /// </summary>
        /// <param name="itemType">The type of item that should be retrieved.</param>
        /// <returns>The current primary, secondary, or dual wield item.</returns>
        public Item GetCurrentItem(Type itemType)
        {
            if (itemType.IsAssignableFrom(typeof(PrimaryItemType))) {
                if (m_CurrentPrimaryIndex == -1) {
                    return m_UnequippedItem;
                }
                return m_PrimaryInventory[m_CurrentPrimaryIndex].Item;
            } else if (itemType.IsAssignableFrom(typeof(SecondaryItemType))) {
                if (m_CurrentSecondaryIndex == -1) {
                    return null;
                }
                return m_SecondaryInventory[m_CurrentSecondaryIndex].Item;
            } else {
                if (m_CurrentDualWieldIndex == -1) {
                    return null;
                }
                return m_DualWieldInventory[m_CurrentDualWieldIndex].Item;
            }
        }

        public Item GetItem(ItemBaseType itemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                return null;
            }

            if (itemType is PrimaryItemType) {
                return m_PrimaryInventory[itemIndex].Item;
            } else if (itemType is SecondaryItemType) {
                return m_SecondaryInventory[itemIndex].Item;
            } else { // DualWieldItemType.
                return m_DualWieldInventory[itemIndex].Item;
            }
        }

        /// <summary>
        /// Returns the count for the current primary or secondary item. If primary item is specified then loadedCount specifies if the loaded or unloaded count should be returned.
        /// </summary>
        /// <param name="itemType">The type of item that should be retrieved.</param>
        /// <param name="loadedCount">If the primary item count is requested, should the loaded count be returned? If false the unloaded count will be returned.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetCurrentItemCount(Type itemType, bool loadedCount)
        {
            var item = GetCurrentItem(itemType);
            if (item == null) {
                return -1;
            }
            return GetItemCount(item.ItemType, loadedCount);
        }

        /// <summary>
        /// Returns the item count for the specified item type.
        /// </summary>
        /// <param name="itemBaseType">The interested item type.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetItemCount(ItemBaseType itemBaseType)
        {
            return GetItemCount(itemBaseType, true);
        }

        /// <summary>
        /// Returns the item count for the specified item type and if the loaded or unloaded count should be returned. The loaded parameter is only used for primary items.
        /// </summary>
        /// <param name="itemBaseType">The interested item type.</param>
        /// <param name="loadedCount">If the primary item count is requested, should the loaded count be returned? If false the unloaded count will be returned.</param>
        /// <returns>The number of items remaining of the specified type.</returns>
        public int GetItemCount(ItemBaseType itemBaseType, bool loadedCount)
        {
            if (m_UnlimitedAmmo) {
                return int.MaxValue;
            }

            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                if (itemBaseType is PrimaryItemType && itemBaseType.Equals(m_UnequippedItemType)) {
                    return int.MaxValue;
                }
                return -1;
            }

            // A DualWieldItemType Item uses the mapped ItemType for all of its properties.
            var dualWieldItem = false;
            if ((dualWieldItem = itemBaseType is DualWieldItemType)) {
                if (!m_DualWieldPrimaryItemMap.TryGetValue(itemBaseType, out itemBaseType)) {
                    return -1;
                }
                // The item index for the DualWieldItemType has been found. However, the item index for the PrimaryItemType needs to be used. At this point
                // itemBaseType has been changed to a PrimaryItemType so get the index one more time.
                if (!m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                    return -1;
                }
            }

            // Return the actual item count if the item is a secondary item. If the item is a primary item then
            // return the loaded or unloaded count of the consumable item.
            if (itemBaseType is PrimaryItemType) {
                var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                if (consumableItem != null) {
                    if (loadedCount) {
                        // At this point the ItemType will never be a DualWieldItemType. However, the counts are separated so get the DualWieldLoadedCount if the Item Type
                        // used to be a DualWieldItemType.
                        if (dualWieldItem) {
                            return consumableItem.DualWieldLoadedCount;
                        } else {
                            return consumableItem.PrimaryLoadedCount;
                        }
                    } else {
                        return consumableItem.UnloadedCount;
                    }
                }
                // If there is no ConsumableItem then the item has unlimited ammo.
                return int.MaxValue;
            } else { // SecondaryItemType.
                return m_SecondaryInventory[itemIndex].ItemCount;
            }
        }

        /// <summary>
        /// Sets the loaded and unloaded count for the specified item type.
        /// </summary>
        /// <param name="itemBaseType">The interested item type.</param>
        /// <param name="loadedCount">The item's loaded count.</param>
        /// <param name="unloadedCount">The item's unloaded count.</param>
        public void SetItemCount(ItemBaseType itemBaseType, int loadedCount, int unloadedCount)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                return;
            }

            if (m_UnlimitedAmmo) {
                return;
            }

            if (itemBaseType is PrimaryItemType) {
                var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                if (consumableItem != null) {
                    consumableItem.PrimaryLoadedCount = loadedCount;
                    consumableItem.UnloadedCount = unloadedCount;
                }
            } else { // SecondaryItemType
                m_SecondaryInventory[itemIndex].ItemCount = loadedCount;
            }
        }

        /// <summary>
        /// An item has been used. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemBaseType">The type of item used.</param>
        /// <param name="amount">The number of items used.</param>
        public void UseItem(ItemBaseType itemBaseType, int amount)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcUseItem(itemBaseType.ID, amount);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                UseItem(itemBaseType.ID, amount);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// An item has been used on the client.
        /// </summary>
        /// <param name="itemID">The id of the item.</param>
        /// <param name="amount">The number of items used.</param>
        [ClientRpc]
        private void RpcUseItem(int itemID, int amount)
        {
            UseItem(itemID, amount);
        }
#endif

        /// <summary>
        /// An item has been used. Decrement the used amount from the inventory.
        /// </summary>
        /// <param name="primaryItem">Is the item a primary item?</param>
        /// <param name="itemID">The ID of the item within the inventory list.</param>
        /// <param name="amount">The number of items used.</param>
        private int UseItem(int itemID, int amount)
        {
            ItemBaseType itemBaseType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemBaseType)) {
                return 0;
            }

            // A DualWieldItemType Item uses the mapped ItemType for all of its properties.
            var dualWieldItem = false;
            var origBaseType = itemBaseType;
            if ((dualWieldItem = itemBaseType is DualWieldItemType)) {
                if (!m_DualWieldPrimaryItemMap.TryGetValue(itemBaseType, out itemBaseType)) {
                    Debug.LogError("Error: The DualWieldItemType of " + itemBaseType + " is not mapped to a PrimaryItemType");
                    return 0;
                }
            }

            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                return 0;
            }

            if (m_UnlimitedAmmo) {
                amount = 0;
            }

            if (itemBaseType is PrimaryItemType) {
                // Do not subtract from the primary item type as we are interested in the consumable item count.
                var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                // int.MaxValue indicates unlimited ammo.
                if (consumableItem.PrimaryLoadedCount == int.MaxValue) {
                    amount = 0;
                }
                // At this point the ItemType will never be a DualWieldItemType. However, the counts are separated so set the DualWieldLoadedCount if the Item Type
                // used to be a DualWieldItemType.
                Item item = null;
                if (dualWieldItem) {
                    consumableItem.DualWieldLoadedCount -= amount;
                    // Convert back to the dual wield item for the consumable item count change event.
                    item = m_DualWieldInventory[m_ItemIndexMap[origBaseType]].Item;
                } else {
                    consumableItem.PrimaryLoadedCount -= amount;
                    item = m_PrimaryInventory[itemIndex].Item;
                }
#if ENABLE_MULTIPLAYER
                EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, isServer, false);
#else
                EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, false, false);
#endif

                return dualWieldItem ? consumableItem.DualWieldLoadedCount : consumableItem.PrimaryLoadedCount;
            } else { // SecondaryItemType.
                // int.MaxValue indicates unlimited ammo.
                if (m_SecondaryInventory[itemIndex].ItemCount == int.MaxValue) {
                    amount = 0;
                }
                m_SecondaryInventory[itemIndex].ItemCount -= amount;
                EventHandler.ExecuteEvent(m_GameObject, "OnInventorySecondaryItemCountChange");

                return m_SecondaryInventory[itemIndex].ItemCount;
            }
        }
        
        /// <summary>
        /// Reload the item with the specified amount. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemBaseType">The type of item that should be reloaded.</param>
        /// <param name="amount">The amount of consumable items to reload the item with.</param>
        public void ReloadItem(ItemBaseType itemBaseType, int amount)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcReloadItem(itemBaseType.ID, amount);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            // ReloadItem may be called before the SharedFields have been initialized when it is synchronizing with the server on the first run
            // In this case call ReloadItem to allow the server to reload the item on the client.
            if (m_CanInteractItem == null || !isClient) {
#endif
                ReloadItemLocal(itemBaseType.ID, amount);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Reload the item with the specified amount on the client.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        /// <param name="amount">The amount of consumable items to reload the item with.</param>
        [ClientRpc]
        private void RpcReloadItem(int itemIndex, int amount)
        {
            ReloadItemLocal(itemIndex, amount);
        }
#endif

        /// <summary>
        /// Reload the item with the specified amount. Only primary items can be reloaded.
        /// </summary>
        /// <param name="itemID">The ID of the item within the inventory list.</param>
        /// <param name="amount">The amount of consumable items to reload the item with.</param>
        private void ReloadItemLocal(int itemID, int amount)
        {
            ItemBaseType itemBaseType;
            if (!m_IDItemTypeMap.TryGetValue(itemID, out itemBaseType)) {
                return;
            }

            // A DualWieldItemType Item uses the mapped ItemType for all of its properties.
            var dualWieldItem = false;
            var origBaseType = itemBaseType;
            if (dualWieldItem = itemBaseType is DualWieldItemType) {
                if (!m_DualWieldPrimaryItemMap.TryGetValue(itemBaseType, out itemBaseType)) {
                    return;
                }
            }

            int itemIndex;
            if (!(itemBaseType is PrimaryItemType) || !m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                return;
            }

            var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;

            // The item can't load more than what is in unloaded.
            if (amount > consumableItem.UnloadedCount) {
                amount = consumableItem.UnloadedCount;
            }
            // At this point the ItemType will never be a DualWieldItemType. However, the counts are separated so set the DualWieldLoadedCount if the ItemType
            // used to be a DualWieldItemType.
            if (dualWieldItem) {
                consumableItem.DualWieldLoadedCount += amount;
            } else {
                consumableItem.PrimaryLoadedCount += amount;
            }

            // int.MaxValue indicates unlimited ammo.
            if (consumableItem.UnloadedCount != int.MaxValue) {
                consumableItem.UnloadedCount -= amount;
            }

            // Notify the ConsumableItemChange with the original item.
            Item item;
            if (dualWieldItem) {
                item = m_DualWieldInventory[m_ItemIndexMap[origBaseType]].Item;
            } else {
                item = m_PrimaryInventory[itemIndex].Item;
            }

            EventHandler.ExecuteEvent<Item, bool, bool>(m_GameObject, "OnInventoryConsumableItemCountChange", item, false, false);
        }

        /// <summary>
        /// Switch the item to the next item in the inventory list. Call the corresponding client or server method.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        public void SwitchItem(bool primaryItem, bool next)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcSwitchItem(primaryItem, next);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                SwitchItemLocal(primaryItem, next);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Switch the item to the next item in the inventory list on the client.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        [ClientRpc]
        private void RpcSwitchItem(bool primaryItem, bool next)
        {
            SwitchItemLocal(primaryItem, next);
        }
#endif

        /// <summary>
        /// Switch the item to the next item in the inventory list.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item should be used.</param>
        /// <param name="next">Should the next item be switched to? If false then the previous item will be switched to.</param>
        private void SwitchItemLocal(bool primaryItem, bool next)
        {
            var currentIndex = (primaryItem ? m_CurrentPrimaryIndex : m_CurrentSecondaryIndex);
            var itemIndex = SwitchItem(primaryItem, next, currentIndex);
            if (itemIndex == currentIndex) {
                return;
            }

            // A new item index has been retrieved and now the current variables need to be updated.
            if (primaryItem) {
                if (itemIndex != -1) {
                    EquipItemLocal(itemIndex);
                } else {
                    // The current primary item is null. Animate the removal of the item.
                    EquipUnequpItem(false, -1, false);
                }
            } else { // SecondaryItemType
                m_CurrentSecondaryIndex = itemIndex;
            }
        }

        /// <summary>
        /// Determine the index of the next/previous item in the inventory. A valid index number will always be returned, meaning an item which does not have any ammo will not be returned.
        /// </summary>
        /// <param name="primaryItem">Should the primary item be switched? If false then the secondary item will be used.</param>
        /// <param name="next">Should the inventory switch to the next item? If false then the inventory will switch to the previous item.</param>
        /// <param name="currentItemIndex">The index of the current item activated.</param>
        /// <returns>The index of the item that the inventory should switch to.</returns>
        private int SwitchItem(bool primaryItem, bool next, int currentItemIndex)
        {
            if (currentItemIndex != -1) {
                var inventory = (primaryItem ? m_PrimaryInventory : m_SecondaryInventory);
                var itemIndex = (currentItemIndex + (next ? 1 : -1)) % inventory.Count;
                if (itemIndex < 0) itemIndex = inventory.Count - 1;
                // Loop through the inventory list until an item is found or the entire inventory has been searched..
                var i = 0;
                while (i < inventory.Count) {
                    if (inventory[itemIndex].ItemCount > 0 && (!primaryItem || m_CurrentDualWieldIndex == -1 || m_DualWieldInventory[m_CurrentDualWieldIndex].Item != inventory[itemIndex].Item)) {
                        return itemIndex;
                    }
                    itemIndex = (itemIndex + (next ? 1 : -1)) % inventory.Count;
                    if (itemIndex < 0) itemIndex = inventory.Count - 1;
                    i++;
                }
            }
            return -1;
        }

        /// <summary>
        /// Does the character have the specified item?
        /// </summary>
        /// <param name="itemBaseType">The item to check against.</param>
        public bool SharedMethod_HasItem(ItemBaseType itemBaseType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                if (m_UnequippedItem != null) {
                    return m_UnequippedItem.ItemType.Equals(itemBaseType);
                }
                return false;
            }

            if (itemBaseType is PrimaryItemType) {
                var item = m_PrimaryInventory[itemIndex];
                return item.ItemCount > 0;
            } else {
                var item = m_SecondaryInventory[itemIndex];
                return item.ItemCount > 0;
            }
        }

        /// <summary>
        /// Returns the Item which has the given id.
        /// </summary>
        /// <param name="id">The id of the Item.</param>
        /// <returns>The Item corresponding to the id.</returns>
        public Item SharedMethod_ItemWithID(int id)
        {
            ItemBaseType itemBaseType = null;
            if (m_IDItemTypeMap.TryGetValue(id, out itemBaseType)) {
                int itemIndex;
                // Unequipped items are not in the primary or secondary inventory.
                if (itemBaseType.Equals(m_UnequippedItemType)) {
                    return m_UnequippedItem;
                }
                if (m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                    if (itemBaseType is PrimaryItemType) {
                        return m_PrimaryInventory[itemIndex].Item;
                    } else if (itemBaseType is DualWieldItemType) {
                        return m_DualWieldInventory[itemIndex].Item;
                    }
                    return m_SecondaryInventory[itemIndex].Item;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the name of the item.
        /// </summary>
        /// <param name="primaryItem">Is the item a primary item?</param>
        /// <returns>The name of the item.</returns>
        private string SharedMethod_ItemName(bool primaryItem)
        {
            var item = GetCurrentItem(primaryItem ? typeof(PrimaryItemType) : typeof(SecondaryItemType));
            if (item != null) {
                return item.ItemName;
            }
            return "No Item";
        }

        /// <summary>
        /// If an item is equipped then unequip it. If an item is unequipped or equal to the unequipped type then equip the previous item.
        /// </summary>
        public void ToggleEquippedItem()
        {
            if (m_LastEquipedItem != -1 && (m_CurrentPrimaryIndex == -1 || (m_UnequippedItemType != null && m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType.Equals(m_UnequippedItemType)))) {
                EquipItem(m_LastEquipedItem);
            } else {
                UnequipCurrentItem();
            }
        }

        /// <summary>
        /// Equip the specified primary item.
        /// </summary>
        /// <param name="primaryItemType">The primary item type.</param>
        public void EquipItem(PrimaryItemType primaryItemType)
        {
            int itemIndex;
            if (!m_ItemIndexMap.TryGetValue(primaryItemType, out itemIndex)) {
                if (m_UnequippedItem != null && m_UnequippedItem.ItemType.Equals(primaryItemType)) {
                    UnequipCurrentItem();
                }
                return;
            }

            EquipItem(itemIndex);
        }

        /// <summary>
        /// Equips the primary item in the specified index. Call the corresponding server or client method.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        public void EquipItem(int itemIndex)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcEquipItemIndex(itemIndex);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                EquipItemLocal(itemIndex);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Equips the primary item in the specified index on the client.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        [ClientRpc]
        private void RpcEquipItemIndex(int itemIndex)
        {
            EquipItemLocal(itemIndex);
        }
#endif

        /// <summary>
        /// Equips the primary item in the specified index.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        private void EquipItemLocal(int itemIndex)
        {
            // Cannot equip the item if it hasn't been picked up yet or the current item is already equipped.
            if (itemIndex >= m_PrimaryInventory.Count || m_PrimaryInventory[itemIndex].ItemCount == 0 || m_CurrentPrimaryIndex == itemIndex) {
                return;
            }

            if (m_CurrentPrimaryIndex != -1) {
                var canDualWield = CanDualWield(m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType, m_PrimaryInventory[itemIndex].ItemType as PrimaryItemType);

                // The item that should be equipped can dual wield with the current item. Determine which item is the primary item, and equip the new item.
                if (canDualWield) {
                    var newPrimary = false;
                    var currentItemPrimaryItemType = m_PrimaryInventory[m_CurrentPrimaryIndex].ItemType as PrimaryItemType;
                    for (int i = 0; i < currentItemPrimaryItemType.DualWieldItems.Length; ++i) {
                        if (currentItemPrimaryItemType.DualWieldItems[i].ItemType.Equals(m_PrimaryInventory[itemIndex].ItemType)) {
                            newPrimary = currentItemPrimaryItemType.DualWieldItems[i].PrimaryName;
                            break;
                        }
                    }

                    if (newPrimary) {
                        // The current item should not be the primary item. Set the current item to the dual wield item.
                        m_CurrentDualWieldIndex = m_PrimadyDualWieldItemIndexMap[currentItemPrimaryItemType];
                        EquipUnequpItem(true, itemIndex, false);
                    } else {
                        EquipDualWieldItemLocal(itemIndex);
                    }
                } else {
                    // If an item is equipped then it first needs to be unequipped before the new item can ben equipped. Run AnimateEquipUnequip to unequip the item
                    // and within OnItemUnequipped start equipping the new item.
                    EquipUnequpItem(false, m_CurrentPrimaryIndex, false);
                    // Unequip may still be -1 if the items are quickly being changed and the animator doesn't need to wait for a callback.
                    if (m_UnequpIndex != -1) {
                        m_EquipIndex = itemIndex;
                    } else {
                        EquipUnequpItem(true, itemIndex, true);
                    }

                    // Equip the dual wield item if it exists.
                    var dualWieldItemType = DualWieldItemForPrimaryItem(m_PrimaryInventory[itemIndex].ItemType);
                    if (dualWieldItemType != null) {
                        if (!m_ItemIndexMap.TryGetValue(dualWieldItemType, out itemIndex)) {
                            return;
                        }
                        m_DualWieldEquipIndex = itemIndex;
                    }
                }
            } else {
                EquipUnequpItem(true, itemIndex, false);

                // Equip the dual wield item if it exists.
                var dualWieldItemType = DualWieldItemForPrimaryItem(m_PrimaryInventory[itemIndex].ItemType);
                if (dualWieldItemType != null) {
                    if (!m_ItemIndexMap.TryGetValue(dualWieldItemType, out itemIndex)) {
                        return;
                    }
                    EquipDualWieldItemLocal(itemIndex);
                }
            }
        }

        /// <summary>
        /// Equips the dual wield item in the specified index.
        /// </summary>
        /// <param name="itemIndex">The index of the item within the inventory list.</param>
        private void EquipDualWieldItemLocal(int itemIndex)
        {
            // Cannot equip the item if it hasn't been picked up yet or the current item is already equipped.
            if (itemIndex >= m_DualWieldInventory.Count || m_CurrentDualWieldIndex == itemIndex) {
                return;
            }

            if (m_CurrentDualWieldIndex != -1) {
                if (m_DropItems) {
                    DropItem(m_DualWieldInventory[m_CurrentDualWieldIndex]);
                }
                RemoveItem(m_DualWieldInventory[m_CurrentDualWieldIndex].ItemType, true);
            }
            EquipUnequipDualWieldItem(true, itemIndex, false);
        }

        /// <summary>
        /// Unequip the current item. Call the corresponding server or client method.
        /// </summary>
        public void UnequipCurrentItem()
        {
            // Cannot unequip an item if there isn't an item or the animator is currently aiming.
            if (m_CurrentPrimaryIndex == -1) {
                return;
            }

#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcUnequipCurrentItem();
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                UnequipCurrentItemLocal();
#if ENABLE_MULTIPLAYER
            }
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Unequip the current item on the client.
        /// </summary>
        [ClientRpc]
        private void RpcUnequipCurrentItem()
        {
            UnequipCurrentItemLocal();
        }
#endif

        /// <summary>
        /// Unequip the current item.
        /// </summary>
        private void UnequipCurrentItemLocal()
        {
            EquipUnequpItem(false, m_CurrentPrimaryIndex, false);
        }

        /// <summary>
        /// Drops the specified item.
        /// </summary>
        /// <param name="itemType">The item to drop.</param>
        public void DropItem(ItemBaseType itemType)
        {
            if (m_DropItems) {
                int itemIndex;
                if (!m_ItemIndexMap.TryGetValue(itemType, out itemIndex)) {
                    return;
                }

                ItemInstance itemInstance;
                if (itemType is PrimaryItemType) {
                    itemInstance = m_PrimaryInventory[itemIndex];
                } else if (itemType is SecondaryItemType) {
                    itemInstance = m_SecondaryInventory[itemIndex];
                } else { // DualWieldItemType.
                    itemInstance = m_DualWieldInventory[itemIndex];
                }

                DropItem(itemInstance);
            }
            RemoveItem(itemType, false);
        }

        /// <summary>
        /// Drops the specified item.
        /// </summary>
        /// <param name="itemInstance">The instance of the item to drop.</param>
        private void DropItem(ItemInstance itemInstance)
        {
            if (itemInstance.Item.ItemPickup == null) {
                return;
            }
            var itemPickup = ObjectPool.Spawn(itemInstance.Item.ItemPickup, itemInstance.Item.transform.position,
                                                itemInstance.Item.transform.rotation, m_DroppedItemsParent).GetComponent<ItemPickup>();
            var itemAmount = itemPickup.ItemList;
            ConsumableItemInstance consumableItemInstance = null;
            ItemBaseType itemType = null;
            if (itemInstance.ItemType is DualWieldItemType) {
                // The DualWieldItemType uses the PrimaryItemType's ConsumableItem.
                ItemBaseType primaryItemType;
                if (m_DualWieldPrimaryItemMap.TryGetValue(itemInstance.ItemType, out primaryItemType)) {
                    int primaryItemIndex;
                    itemType = primaryItemType;
                    if (m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                        consumableItemInstance = m_PrimaryInventory[primaryItemIndex].ConsumableItem;
                    }
                }
            } else {
                itemType = itemInstance.ItemType;
                consumableItemInstance = itemInstance.ConsumableItem;
            }

            var pickupCount = consumableItemInstance != null ? 2 : 1;
            // Prepare the ItemAmount for reuse. Add space for the main and Consumable ItemType. Remove any extra elements.
            for (int j = itemAmount.Count - 1; j < pickupCount; ++j) {
                itemAmount.Add(new ItemAmount());
            }
            if (itemAmount.Count > pickupCount) {
                itemAmount.RemoveRange(pickupCount, itemAmount.Count - pickupCount);
            }
            // Both the main ItemType and the ConsumableItemType can be dropped.
            itemAmount[0].Initialize(itemType, 1);
            if (consumableItemInstance != null) {
                if (itemInstance.ItemType is DualWieldItemType) {
                    itemAmount[1].Initialize(consumableItemInstance.ItemType, GetItemCount(itemInstance.ItemType, true));
                } else {
                    itemAmount[1].Initialize(consumableItemInstance.ItemType, GetItemCount(itemInstance.ItemType, true) + GetItemCount(itemInstance.ItemType, false));
                }
            }
        }

        /// <summary>
        /// Remove the count of all items from the inventory. This will happen will the character dies.
        /// </summary>
        public void RemoveAllItems()
        {
            for (int i = 0; i < m_DualWieldInventory.Count; ++i) {
                ItemBaseType primaryItemType;
                if (!m_DualWieldPrimaryItemMap.TryGetValue(m_DualWieldInventory[i].ItemType, out primaryItemType)) {
                    continue;
                }

                int primaryItemIndex;
                if (!m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                    continue;
                }

                if (m_PrimaryInventory[primaryItemIndex].ItemCount > 1) {
                    RemoveItem(m_DualWieldInventory[i].ItemType, true);
                }
            }
            for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                if (m_PrimaryInventory[i].ItemCount > 0) {
                    RemoveItem(m_PrimaryInventory[i].ItemType, true);
                }
            }
            for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                if (m_SecondaryInventory[i].ItemCount > 0) {
                    RemoveItem(m_SecondaryInventory[i].ItemType, true);
                }
            }
            CurrentPrimaryIndex = m_CurrentDualWieldIndex = m_CurrentSecondaryIndex = -1;
        }

        /// <summary>
        /// Remove the count of the specified item. This in effect removes it from the inventory.
        /// </summary>
        /// <param name="itemBaseType">The item type to remove.</param>
        /// <param name="immediateRemoval">Should the item be removed immediately? This only applies to the PrimaryItemType. If false the animation will remove the item.</param>
        public void RemoveItem(ItemBaseType itemBaseType, bool immediateRemoval)
        {
            int itemIndex;
            if (m_ItemIndexMap.TryGetValue(itemBaseType, out itemIndex)) {
                if (itemBaseType is PrimaryItemType && m_PrimaryInventory[itemIndex].ItemCount == 1) {
                    m_PrimaryInventory[itemIndex].ItemCount -= 1;
                    // The PrimaryItem may be equipped in the DualWield inventory.
                    if (m_CurrentDualWieldIndex != -1 && m_DualWieldInventory[m_CurrentDualWieldIndex].ItemType.Equals(itemBaseType)) {
                        m_DualWieldInventory[itemIndex].SetActive(false);
                        CurrentDualWieldIndex = -1;
                    } else {
                        EquipUnequpItem(false, itemIndex, immediateRemoval);
                    }

                    // Remove all of the consumable items.
                    var consumableItem = m_PrimaryInventory[itemIndex].ConsumableItem;
                    if (consumableItem != null) {
                        consumableItem.PrimaryLoadedCount = consumableItem.DualWieldLoadedCount = consumableItem.UnloadedCount = 0;
                    }
                } else if (itemBaseType is SecondaryItemType) { // SecondaryItemType
                    m_SecondaryInventory[itemIndex].ItemCount = 0;
                } else { // DualWieldItemType
                    ItemBaseType primaryItemType;
                    if (!m_DualWieldPrimaryItemMap.TryGetValue(itemBaseType, out primaryItemType)) {
                        return;
                    }

                    int primaryItemIndex;
                    if (!m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                        return;
                    }

                    m_PrimaryInventory[primaryItemIndex].ItemCount -= 1;
                    var consumableItem = m_PrimaryInventory[primaryItemIndex].ConsumableItem;
                    if (consumableItem != null) {
                        consumableItem.DualWieldLoadedCount = 0;
                    }

                    m_DualWieldInventory[itemIndex].SetActive(false);
                    CurrentDualWieldIndex = -1;
                }
            }
        }

        /// <summary>
        /// Equip or unequip an item. Smoothly animate the transition unless immediate is called (in which case just activate/deactivate the GameObject).
        /// The animator knows what type of weapon is active by the ItemID parameter.
        /// </summary>
        /// <param name="equip">Should the item be equipped?</param>
        /// <param name="itemIndex">The index of the item in the inventory.</param>
        /// <param name="immediate">Should the item be equipped immediately and not animated?</param>
        private void EquipUnequpItem(bool equip, int itemIndex, bool immediate)
        {
            // Immediately equip or unequip the item if the item cannot be interacted with. The roll ability will 
            // prevent animations since the character cannot play the equip/unequip animations while rolling.
            if (m_CanInteractItem != null && !m_CanInteractItem.Invoke()) {
                immediate = true;
            }
            if (equip) {
                if (immediate) {
                    m_PrimaryInventory[itemIndex].SetActive(true);
                } else {
                    m_EquipIndex = itemIndex;
                }
                m_LastEquipedItem = itemIndex;
                CurrentPrimaryIndex = itemIndex;
            } else {
                if (immediate) {
                    if (itemIndex != -1) {
                        m_PrimaryInventory[itemIndex].SetActive(false);
                        CurrentPrimaryIndex = -1;
                    }
                } else {
                    m_UnequpIndex = itemIndex;
                }
                // Set the equip index to -1 to ensure OnItemUnequip doesn't equip a previous item after it has been unequipped.
                m_EquipIndex = -1;

                if (m_CurrentDualWieldIndex != -1) {
                    if (m_DropItems) {
                        DropItem(m_DualWieldInventory[m_CurrentDualWieldIndex]);
                    }
                    RemoveItem(m_DualWieldInventory[m_CurrentDualWieldIndex].ItemType, true);
                }
            }
            if (immediate) {
                m_EquipIndex = -1;
            } else {
                EventHandler.ExecuteEvent<bool>(m_PrimaryInventory[itemIndex].GameObject, "OnInventoryItemEquipping", equip);
            }
        }

        private void EquipUnequipDualWieldItem(bool equip, int itemIndex, bool immediate)
        {
            // Immediately equip or unequip the item if the item cannot be interacted with. The roll ability will 
            // prevent animations since the character cannot play the equip/unequip animations while rolling.
            if (m_CanInteractItem != null && !m_CanInteractItem.Invoke()) {
                immediate = true;
            }
            if (equip) {
                if (immediate) {
                    m_DualWieldInventory[itemIndex].SetActive(true);
                } else {
                    m_DualWieldEquipIndex = itemIndex;
                }
                CurrentDualWieldIndex = itemIndex;
            } else {
                // Do not allow dual wield items to be stored. Drop the item.
                if (m_DropItems) {
                    DropItem(m_DualWieldInventory[m_CurrentDualWieldIndex]);
                }
                RemoveItem(m_DualWieldInventory[m_CurrentDualWieldIndex].ItemType, true);
            }
            if (immediate) {
                m_DualWieldEquipIndex = -1;
            } else {
                EventHandler.ExecuteEvent<bool>(m_DualWieldInventory[itemIndex].GameObject, "OnInventoryItemEquipping", equip);
            }
        }

        /// <summary>
        /// Is the inventory currently switching items?
        /// </summary>
        /// <returns>True if the inventory is switching items.</returns>
        public bool SharedMethod_IsSwitchingItem()
        {
            return m_EquipIndex != -1 || m_UnequpIndex != -1;
        }

        /// <summary>
        /// The character has died. Remove all of the items from the inventory.
        /// </summary>
        private void OnDeath()
        {
            EventHandler.UnregisterEvent(gameObject, "OnDeath", OnDeath);
            EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);

            // Drop any of the remaining items.
            if (m_DropItems) {
                // Drop any primary items.
                for (int i = 0; i < m_PrimaryInventory.Count; ++i) {
                    var itemInstance = m_PrimaryInventory[i];
                    if (itemInstance.Item.ItemPickup != null && m_PrimaryInventory[i].ItemCount > 0) {
                        DropItem(itemInstance);
                    }
                }

                // Drop any secondary items.
                for (int i = 0; i < m_SecondaryInventory.Count; ++i) {
                    var itemInstance = m_SecondaryInventory[i];
                    if (itemInstance.Item.ItemPickup != null && GetItemCount(itemInstance.ItemType) > 0) {
                        DropItem(itemInstance);
                    }
                }

                // Drop any dual wield items.
                for (int i = 0; i < m_DualWieldInventory.Count; ++i) {
                    var itemInstance = m_DualWieldInventory[i];
                    ItemBaseType primaryItemType;
                    if (!m_DualWieldPrimaryItemMap.TryGetValue(itemInstance.ItemType, out primaryItemType)) {
                        continue;
                    }
                    int primaryItemIndex;
                    if (!m_ItemIndexMap.TryGetValue(primaryItemType, out primaryItemIndex)) {
                        continue;
                    }
                    if (itemInstance.Item.ItemPickup != null && m_PrimaryInventory[primaryItemIndex].ItemCount > 1) {
                        DropItem(itemInstance);
                    }
                }
            }

            RemoveAllItems();
        }

        /// <summary>
        /// The character has respawned. Load the default loadout.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
            EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);

            LoadDefaultLoadout();
        }

        /// <summary>
        /// The Animator says an item has been equipped so the GameObject should now activate.
        /// </summary>
        private void OnItemEquipped()
        {
            if (m_EquipIndex != -1) {
                m_PrimaryInventory[m_EquipIndex].SetActive(true);
                // Let the item know that it has been equipped.
                EventHandler.ExecuteEvent(m_PrimaryInventory[m_EquipIndex].GameObject, "OnInventoryItemEquipped");
                m_EquipIndex = -1;
            }
            if (m_DualWieldEquipIndex != -1) {
                m_DualWieldInventory[m_DualWieldEquipIndex].SetActive(true);
                CurrentDualWieldIndex = m_DualWieldEquipIndex;
                // Let the item know that it has been equipped.
                EventHandler.ExecuteEvent(m_DualWieldInventory[m_DualWieldEquipIndex].GameObject, "OnInventoryItemEquipped");
                m_DualWieldEquipIndex = -1;
            }
        }

        /// <summary>
        /// The Animator says an item has been unequipped so the GameObject should now deactivate. If an item is waiting to be equipped then start the equip animation.
        /// </summary>
        private void OnItemUnequipped()
        {
            if (m_UnequpIndex != -1) {
                // Unequip the dual wield item first to prevent the state submachine names from mismatching.
                if (m_DualWieldUnequpIndex != -1) {
                    m_DualWieldInventory[m_DualWieldUnequpIndex].SetActive(false);
                    CurrentDualWieldIndex = -1;
                }

                m_PrimaryInventory[m_UnequpIndex].SetActive(false);
                CurrentPrimaryIndex = -1;

                if (m_EquipIndex != -1) {
                    EquipUnequpItem(true, m_EquipIndex, true);
                }
                if (m_DualWieldEquipIndex != -1) {
                    EquipUnequipDualWieldItem(true, m_DualWieldEquipIndex, true);
                }

                // Notify the item that it has been unequipped.
                EventHandler.ExecuteEvent(m_PrimaryInventory[m_UnequpIndex].GameObject, "OnInventoryItemUnequipped");
            }

            if (m_DualWieldUnequpIndex != -1) {
                // If there was a primary item then the dual wield item has already been unequipped.
                if (m_UnequpIndex != -1) {
                    m_DualWieldInventory[m_DualWieldUnequpIndex].SetActive(false);
                    CurrentDualWieldIndex = -1;
                }
                // Notify the item that it has been unequipped.
                EventHandler.ExecuteEvent(m_DualWieldInventory[m_DualWieldUnequpIndex].GameObject, "OnInventoryItemUnequipped");
            }

            m_DualWieldUnequpIndex = -1;
            m_UnequpIndex = -1;
        }
    }
}