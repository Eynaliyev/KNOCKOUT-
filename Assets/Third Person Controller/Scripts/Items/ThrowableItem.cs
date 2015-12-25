using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any Item that can be thrown, such as a grenade or baseball. The GameObject that the ThrowableItem attaches to is not the actual object that is thrown - the ThrownObject field
    /// specifies this instead. This GameObject is used by the Inventory to know that a ThrowableItem exists, and by the ItemHandler to actually use the Item.
    /// </summary>
    public class ThrowableItem : Item, IUsableItem
    {
        [Tooltip("The object that can be thrown. Must have a component that implements IThrownObject")]
        [SerializeField] private GameObject m_ThrownObject;
        [Tooltip("The number of objects that can be thrown per second")]
        [SerializeField] private float m_ThrowRate = 1;
        [Tooltip("The force applied to the object thrown")]
        [SerializeField] private Vector3 m_ThrowForce;
        [Tooltip("The torque applied to the object thrown")]
        [SerializeField] private Vector3 m_ThrowTorque;
        [Tooltip("A random spread to allow some inconsistency in each throw")]
        [SerializeField] private float m_Spread;
        [Tooltip("The state while using the item")]
        [SerializeField] protected AnimatorItemStatesData m_UseStates = new AnimatorItemStatesData("Attack", 0.1f, true);

        // SharedFields
#if !ENABLE_MULTIPLAYER
        private SharedMethod<bool> m_IsAI = null;
#endif
        private SharedMethod<bool, Vector3> m_TargetLookDirection = null;

        // Internal variables
        private float m_ThrowDelay;
        private float m_LastThrowTime;
        private bool m_Throwing;
        private bool m_Initialized;

        // Component references
        private Transform m_Transform;
        private Transform m_CharacterTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_Transform = transform;

            m_ThrowDelay = 1.0f / m_ThrowRate;
            m_LastThrowTime = -m_ThrowDelay;
            m_UseStates.ItemType = m_ItemType;
        }

        /// <summary>
        /// Initializes the item after it has been added to the Inventory.
        /// </summary>
        public override void Init(Inventory inventory)
        {
            base.Init(inventory);

            // The AI agent may need to use the CharacterTransform for throw direction if the AIAgent component is not attached.
#if !ENABLE_MULTIPLAYER
            if (m_IsAI.Invoke()) {
#endif
                m_CharacterTransform = m_Character.transform;
#if !ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// Returns the destination state for the given layer.
        /// </summary>
        /// <param name="highPriority">Should the high priority animation be retrieved? High priority animations get tested before the character abilities.</param>
        /// <param name="layer">The Animator layer index.</param>
        /// <returns>The state that the Animator should be in for the given layer. A null value indicates no change.</returns>
        public override AnimatorItemStateData GetDestinationState(bool highPriority, int layer)
        {
            // The arm layer should be used for dual wielded items and secondary items. If only one item is equipped then the entire upper body layer can be used.
            var useArmLayer = (m_CurrentDualWieldItem.Get() != null) || m_ItemType is SecondaryItemType;

            // Item use is a high priority item.
            if (highPriority) {
                if (layer == m_AnimatorMonitor.GetUpperBodyLayerIndex()) {
                    if (!useArmLayer) {
                        if (InUse()) {
                            return m_UseStates.GetState();
                        }
                    }
                } else if (layer == m_ArmLayer) {
                    if (useArmLayer) {
                        if (InUse()) {
                            return m_UseStates.GetState();
                        }
                    }
                }
            }
            return base.GetDestinationState(highPriority, layer);
        }

        /// <summary>
        /// Try to throw the object. An object may not be able to be thrown if another object was thrown too recently, or if there are no more thrown objects remaining (out of ammo).
        /// <returns>True if the item was used.</returns>
        /// </summary>
        public bool TryUse()
        {
            // Throwable Items aren't always visible (such a Secondary item) so Start() isn't always called. Initialize the SharedFields when the item is trying to be used.
            if (!m_Initialized) {
                SharedManager.InitializeSharedFields(m_Character, this);
                // An AI Agent does not need to communicate with the camera. Do not initialze the SharedFields on the network to prevent non-local characters from
                // using the main camera to determine their look direction. TargetLookPosition has been implemented by the NetworkMonitor component.
#if !ENABLE_MULTIPLAYER
                if (!m_IsAI.Invoke()) {
                    SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
                }
#endif
                m_Initialized = true;
            }
            if (m_LastThrowTime + m_ThrowDelay < Time.time && m_Inventory.GetItemCount(m_ItemType) > 0) {
                // Returns true to tell the ItemHandler that the item was used. The Used callback will be registered and the object will actually be thrown within that method.
                m_Throwing = true;
                EventHandler.ExecuteEvent(m_Character, "OnItemUse", m_ItemType is PrimaryItemType);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the maximum distance that the item can be used.
        /// </summary>
        /// <returns>The maximum distance that hte item can be used.</returns>
        public virtual float MaxUseDistance()
        {
            return float.MaxValue;
        }

        /// <summary>
        /// Can the item be thrown?
        /// </summary>
        /// <returns>True if the item can be thrown.</returns>
        public bool CanUse()
        {
            return !m_Throwing;
        }

        /// <summary>
        /// Is the object currently being thrown?
        /// </summary>
        /// <returns>True if the object is currently being thrown.</returns>
        public bool InUse()
        {
            return m_Throwing; 
        }

        /// <summary>
        /// The thrown object cannot be stopped because it is atomic.
        /// </summary>
        public void TryStopUse()
        {

        }

        /// <summary>
        /// Throw the object.
        /// </summary>
        public void Used()
        {
#if ENABLE_MULTIPLAYER
            // The server will spawn the GameObject and it will be sent to the clients.
            if (!m_IsServer.Invoke()) {
                return;
            }
#endif

            var thrownGameObject = ObjectPool.Spawn(m_ThrownObject, m_Transform.position, Quaternion.LookRotation(ThrowDirection()) * m_ThrownObject.transform.rotation);
            var thrownObject = (IThrownObject)(thrownGameObject.GetComponent(typeof(IThrownObject)));
            thrownObject.ApplyThrowForce(m_ThrowForce, m_ThrowTorque);

            m_LastThrowTime = Time.time;
            m_Throwing = false;
            m_Inventory.UseItem(m_ItemType, 1);

            EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
        }

        /// <summary>
        /// Determines the direction to throw based on the camera's look position and a random spread.
        /// </summary>
        /// <returns>The direction to throw.</returns>
        private Vector3 ThrowDirection()
        {
            Vector3 direction;
            // If TargetLookPosition is null then use the forward direction. It may be null if the AI agent doesn't have the AIAgent component attached.
            if (m_TargetLookDirection == null) {
                direction = m_CharacterTransform.forward;
            } else {
                direction = m_TargetLookDirection.Invoke(true).normalized;
            }

            // Add a random spread.
            if (m_Spread > 0) {
                var variance = Quaternion.AngleAxis(Random.Range(0, 360), direction) * Vector3.up * Random.Range(0, m_Spread);
                direction += variance;
            }

            return direction;
        }
    }
}