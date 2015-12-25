using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System.Collections.Generic;
using Opsive.ThirdPersonController.Abilities;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Added to the same GameObject as the Animator, the AnimationMonitor will control the trigger based Animator and translate mecanim events to the event system used by the Third Person Controller.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class AnimatorMonitor : NetworkBehaviour
#else
    public class AnimatorMonitor : MonoBehaviour
#endif
    {
        [Tooltip("Should state changes be sent to the debug console?")]
        [SerializeField] private bool m_DebugStateChanges;
        [Tooltip("The horizontal input dampening time")]
        [SerializeField] private float m_HorizontalInputDampTime = 0.15f;
        [Tooltip("The forward input dampening time")]
        [SerializeField] private float m_ForwardInputDampTime = 0.15f;
        [Tooltip("The main lower body substate that holds all of the non-ability states")]
        [SerializeField] private string m_LowerBodyMainSubstate = "Grounded";
        [Tooltip("The lower and upper body idle state")]
        [SerializeField] private AnimatorItemStateData m_IdleState = new AnimatorItemStateData("Idle", 0.5f, true);
        [Tooltip("The lower and upper body moving state")]
        [SerializeField] private AnimatorItemStateData m_MovementState = new AnimatorItemStateData("Movement", 0.1f, true);
        [Tooltip("The default left arm state")]
        [SerializeField] private AnimatorStateData m_LeftArmState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default right arm state")]
        [SerializeField] private AnimatorStateData m_RightArmState = new AnimatorStateData("Idle", 0.1f);
        [Tooltip("The default additive state")]
        [SerializeField] private AnimatorStateData m_AdditiveState = new AnimatorStateData("Idle", 0.1f);

        // Static variables
        private static int s_HorizontalInputHash = Animator.StringToHash("Horizontal Input");
        private static int s_ForwardInputHash = Animator.StringToHash("Forward Input");
        private static int s_YawHash = Animator.StringToHash("Yaw");
        private static int s_StateHash = Animator.StringToHash("State");
        private static int s_IntDataHash = Animator.StringToHash("Int Data");
        private static int s_FloatDataHash = Animator.StringToHash("Float Data");
        private static int s_ColliderCurveDataHash = Animator.StringToHash("Collider Curve Data");

        // SharedFields
        private SharedMethod<bool, string> m_ItemName = null;
        private SharedProperty<Item> m_CurrentPrimaryItem = null;
        private SharedProperty<Item> m_CurrentSecondaryItem = null;
        private SharedProperty<Item> m_CurrentDualWieldItem = null;

        // Internal variables
        private Ability[] m_Abilities;
        private Dictionary<string, int> m_StateNamesHash = new Dictionary<string, int>();
        private List<int> m_ActiveStateHash = new List<int>();

        // Parameter values
        private float m_HorizontalInputValue;
        private float m_ForwardInputValue;
        private float m_YawValue;
        private int m_StateValue;
        private int m_IntDataValue;
        private float m_FloatDataValue;

        // Exposed properties
        public float HorizontalInputValue { get { return m_Animator.GetFloat(s_HorizontalInputHash); } }
        public float ForwardInputValue { get { return m_Animator.GetFloat(s_ForwardInputHash); } }
        public float YawValue { get { return m_Animator.GetFloat(s_YawHash); } }
        public int StateValue { get { return m_StateValue; } }
        public int IntDataValue { get { return m_IntDataValue; } }
        public float FloatDataValue { get { return m_FloatDataValue; } }

        // Component references
        private GameObject m_GameObject;
        private Animator m_Animator;
        private RigidbodyCharacterController m_Controller;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Animator = GetComponent<Animator>();

            if (m_Animator.avatar == null) {
                Debug.LogError("Error: The Animator Avatar on " + m_GameObject + " is not assigned. Please assign an avatar within the inspector.");
            }

            // Version 1.1 requires an AnimatePhysics update mode.
            if (m_Animator.updateMode != AnimatorUpdateMode.AnimatePhysics) {
                Debug.LogWarning("Warning: Version 1.1 requires an AnimatePhysics Update Mode on the Animator component with the character " + m_GameObject + "." +
                                 "Please make this change when the game is stopped.");
                m_Animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
            }

            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
            EventHandler.RegisterEvent(m_GameObject, "OnInventoryInitialized", OnInventoryInitialized);
        }

        /// <summary>
        /// Reset the active states list when the component is enabled again after a respawn.
        /// </summary>
        private void OnEnable()
        {
            for (int i = 0; i < m_ActiveStateHash.Count; ++i) {
                m_ActiveStateHash[i] = 0;
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Initialize the SharedFields so they are ready for when the server starts to send RPC calls.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            SharedManager.InitializeSharedFields(m_GameObject, this);
        }
#endif
        
        /// <summary>
        /// As soon as the inventory has been initialized finish initializing the AnimatorMonitor and play the correct states.
        /// </summary>
        private void OnInventoryInitialized()
        {
            // Cache the remaining components when the inventory is initialized. Don't cache these components within Awake because the character may be created
            // at runtime and there would be a circluar dependency 
            m_Controller = GetComponent<RigidbodyCharacterController>();
            m_Abilities = m_Controller.Abilities;

            // Do not listen for item events until the inventory initialization is complete.
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnItemStartUse", DetermineStates);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnItemUse", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemStopUse", DetermineStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemReload", DetermineUpperBodyStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemReloadComplete", DetermineUpperBodyStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemEquipped", DetermineUpperBodyStates);
            EventHandler.RegisterEvent(m_GameObject, "OnItemUnequipped", DetermineUpperBodyStates);

            EventHandler.RegisterEvent<Item>(m_GameObject, "OnInventoryDualWieldItemChange", OnDualWieldItemChange);

            // The SharedFields may not have been initialized yet so load them now.
            if (m_ItemName == null) {
                SharedManager.InitializeSharedFields(m_GameObject, this);
            }
            // Set the correct states.
            PlayDefaultStates();
        }

        /// <summary>
        /// Plays the starting Animator states. There is no blending.
        /// </summary>
        private void PlayDefaultStates()
        {
            // The Animator may not be enabled if the character died and the Inventory respawn event occurrs before the Animator Monitor respawn event.
            if (!m_Animator.enabled) {
                return;
            }

            m_Animator.SetFloat(s_HorizontalInputHash, m_HorizontalInputValue);
            m_Animator.SetFloat(s_ForwardInputHash, m_ForwardInputValue);
            m_Animator.SetFloat(s_YawHash, m_YawValue);
            m_Animator.SetInteger(s_StateHash, m_StateValue);
            m_Animator.SetInteger(s_IntDataHash, m_IntDataValue);
            m_Animator.SetFloat(s_FloatDataHash, m_FloatDataValue);

            var lowerBodyStateHash = GetStateNameHash(m_IdleState.Name);
            var item = m_CurrentPrimaryItem.Get();
            int upperBodyStateHash = 0;
            if (item != null) {
                var state = item.GetDestinationState(true, GetUpperBodyLayerIndex());
                if (state == null) {
                    state = item.GetDestinationState(false, GetUpperBodyLayerIndex());
                }
                upperBodyStateHash = GetStateNameHash(FormatUpperBodyState(state));
            } else {
                upperBodyStateHash = GetStateNameHash(FormatUpperBodyState(m_IdleState));
            }
            m_Animator.Play(lowerBodyStateHash, GetLowerBodyLayerIndex(), 0);
            if (GetUpperBodyLayerIndex() < m_Animator.layerCount) {
                m_Animator.Play(upperBodyStateHash, GetUpperBodyLayerIndex(), 0);
            }

            InitializeActiveState(GetLowerBodyLayerIndex(), lowerBodyStateHash);
            InitializeActiveState(GetUpperBodyLayerIndex(), upperBodyStateHash);
            InitializeActiveState(GetLeftArmLayerIndex(), GetStateNameHash(m_LeftArmState.Name));
            InitializeActiveState(GetRightArmLayerIndex(), GetStateNameHash(m_RightArmState.Name));
            InitializeActiveState(GetAdditiveLayerIndex(), GetStateNameHash(m_AdditiveState.Name));
        }

        /// <summary>
        /// Initializes the ActiveStateHash array.
        /// </summary>
        /// <param name="layerIndex">The layer index.</param>
        /// <param name="stateHash">The hash to initialize the array to.</param>
        private void InitializeActiveState(int layerIndex, int stateHash)
        {
            // Return early if there is no layer.
            if (layerIndex > m_Animator.layerCount - 1) {
                return;
            }

            if (layerIndex > m_ActiveStateHash.Count - 1) {
                m_ActiveStateHash.Add(0);
            }
            m_ActiveStateHash[layerIndex] = stateHash;
        }

        /// <summary>
        /// Determine the lower and upper body states.
        /// </summary>
        public void DetermineStates()
        {
            // Do not load the states if the inventory hasn't been initialized yet.
            if (m_ItemName == null) {
                return;
            }

            DetermineStates(true);
        }

        /// <summary>
        /// Determine the lower and upper body states.
        /// </summary>
        /// <param name="checkAbilities">Should the abilities be checked to determine if they have control?</param>
        public void DetermineStates(bool checkAbilities)
        {
            // The Animator may not be enabled if the character died and the weapon is stopping its use.
            if (!m_Animator.enabled) {
                return;
            }

            var lowerBodyChanged = DetermineLowerBodyState(checkAbilities);
            DetermineUpperBodyState(checkAbilities, lowerBodyChanged);
            DetermineState(GetLeftArmLayerIndex(), m_LeftArmState, true);
            DetermineState(GetRightArmLayerIndex(), m_RightArmState, true);
            DetermineState(GetAdditiveLayerIndex(), m_AdditiveState, false);
        }

        /// <summary>
        /// Determine the state that the lower body layer should be in.
        /// </summary>
        /// <param name="checkAbilities">Should the abilities be checked to determine if they have control?</param>
        /// <returns>True if the state was changed.</returns>
        protected virtual bool DetermineLowerBodyState(bool checkAbilities)
        {
            var layer = GetLowerBodyLayerIndex();

            // Item actions are the highest priority.
            bool stateChange;
            if (HasItemState(true, layer, 0, out stateChange)) {
                return stateChange;
            }

            // Followed by an ability's state.
            if (checkAbilities) {
                for (int i = 0; i < m_Abilities.Length; ++i) {
                    if (m_Abilities[i].IsActive && m_Abilities[i].HasAnimatorControl(layer)) {
                        var destinationState = m_Abilities[i].GetDestinationState(layer);
                        if (!string.IsNullOrEmpty(destinationState)) {
                            return ChangeAnimatorStates(m_Abilities[i], layer, destinationState, m_Abilities[i].GetTransitionDuration(), m_Abilities[i].CanReplayAnimationStates(),
                                                        m_Abilities[i].SpeedMultiplier, 0);
                        }
                        if (!m_Abilities[i].IsConcurrentAblity()) {
                            break;
                        }
                    }
                }
            }

            // If no abilities want to play a state then try to play the lower priority item state or if nothing wants to play then play the default moving state.
            if (HasItemState(false, layer, 0, out stateChange)) {
                return true;
            }
            if (m_Controller.Moving) {
                return ChangeAnimatorStates(null, layer, m_LowerBodyMainSubstate + "." + m_MovementState.Name, m_MovementState.TransitionDuration, m_MovementState.CanReplay, m_MovementState.SpeedMultiplier, 0);
            } else {
                return ChangeAnimatorStates(null, layer, m_LowerBodyMainSubstate + "." + m_IdleState.Name, m_IdleState.TransitionDuration, m_IdleState.CanReplay, m_IdleState.SpeedMultiplier, 0);
            }
        }

        /// <summary>
        /// Formats the AnimatorItemStateData to include use the lower state name if specified.
        /// </summary>
        /// <param name="stateData">The AnimatorItemStateData to format.</param>
        /// <returns>The formatted state name.</returns>
        private string FormatLowerBodyState(AnimatorItemStateData stateData)
        {
            var stateName = string.IsNullOrEmpty(stateData.LowerStateName) ? stateData.Name : stateData.LowerStateName;
            if (m_ItemName != null) {
                if (stateData.ItemNamePrefix) {
                    return m_ItemName.Invoke(stateData.ItemType == null ? true : stateData.ItemType is PrimaryItemType) + "." + stateName;
                } else {
                    return stateName;
                }
            }
            return m_LowerBodyMainSubstate + "." + stateName;
        }

        /// <summary>
        /// Formats the AnimatorItemStateData to include the ItemName if required.
        /// </summary>
        /// <param name="stateData">The AnimatorItemStateData to format.</param>
        /// <returns>The formatted state name.</returns>
        public string FormatUpperBodyState(AnimatorItemStateData stateData)
        {
            if (stateData == null) {
                return string.Empty;
            }

            if (stateData.ItemNamePrefix && m_ItemName != null) {
                return m_ItemName.Invoke(stateData.ItemType == null ? true : (stateData.ItemType is PrimaryItemType || stateData.ItemType is DualWieldItemType)) + "." + stateData.Name;
            }
            return stateData.Name;
        }

        /// <summary>
        /// Formats the stateName to include the ItemName if required.
        /// </summary>
        /// <param name="stateName">The state name to format.</param>
        /// <returns>The formatted state name.</returns>
        public string FormatUpperBodyState(string stateName)
        {
            return FormatUpperBodyState(stateName, true);
        }

        /// <summary>
        /// Formats the stateName to include the ItemName if required.
        /// </summary>
        /// <param name="stateName">The state name to format.</param>
        /// <param name="primaryItem">Is the item a PrimaryItemType?</param>
        /// <returns>The formatted state name.</returns>
        public string FormatUpperBodyState(string stateName, bool primaryItem)
        {
            // ItemName may be null if there is no inventory.
            if (m_ItemName != null) {
                return m_ItemName.Invoke(primaryItem) + "." + stateName;
            }
            return stateName;
        }

        /// <summary>
        /// Determine the state that the upper body layer should be in.
        /// </summary>
        /// <param name="checkAbilities">Should the abilities be checked to determine if they have control?</param>
        /// <param name="lowerBodyStart">Is the lower body being set at the same time?</param>
        /// <returns>True if the state was changed.</returns>
        public virtual bool DetermineUpperBodyState(bool checkAbilities, bool lowerBodyStart)
        {
            var layer = GetUpperBodyLayerIndex();
            // Return early if there is no upper body layer.
            if (layer > m_Animator.layerCount - 1) {
                return false;
            }

            // Item actions are the highest priority.
            bool stateChange;
            if (HasItemState(true, layer, 0, out stateChange)) {
                return stateChange;
            }

            // Synchronize with the lower body state.
            var normalizedTime = 0f;
            if (!lowerBodyStart) {
                var lowerBodyLayer = GetLowerBodyLayerIndex();
                if (m_Animator.IsInTransition(lowerBodyLayer)) {
                    normalizedTime = m_Animator.GetNextAnimatorStateInfo(lowerBodyLayer).normalizedTime % 1;
                } else {
                    normalizedTime = m_Animator.GetCurrentAnimatorStateInfo(lowerBodyLayer).normalizedTime % 1;
                }
            }

            if (checkAbilities) {
                for (int i = 0; i < m_Abilities.Length; ++i) {
                    if (m_Abilities[i].IsActive && m_Abilities[i].HasAnimatorControl(layer)) {
                        var destinationState = m_Abilities[i].GetDestinationState(layer);
                        if (!string.IsNullOrEmpty(destinationState)) {
                            // The upper body state should not be synchronized with the lower body state if there is no lower body state that goes along with the upper body state.
                            if (!m_Abilities[i].ForceUpperBodySynchronization() && m_Abilities[i].GetDestinationState(GetLowerBodyLayerIndex()) == string.Empty) {
                                normalizedTime = 0;
                            }
                            return ChangeAnimatorStates(m_Abilities[i], layer, destinationState, m_Abilities[i].GetTransitionDuration(), m_Abilities[i].CanReplayAnimationStates(),
                                                        m_Abilities[i].SpeedMultiplier, normalizedTime);
                        }
                        if (!m_Abilities[i].IsConcurrentAblity()) {
                            break;
                        }
                    }
                }
            }
            if (HasItemState(false, layer, normalizedTime, out stateChange)) {
                return true;
            }
            if (m_Controller.Moving) {
                return ChangeAnimatorStates(null, layer, m_MovementState, normalizedTime);
            } else {
                return ChangeAnimatorStates(null, layer, m_IdleState, normalizedTime);
            }
        }

        /// <summary>
        /// Determine the state that the specified layer should be in.
        /// </summary>
        /// <param name="layer">The layer to determine the state of.</param>
        /// <param name="defaultState">The default state to be in if no other states should run.</param>
        /// <param name="checkItemState">Should the item's state be checked for this layer?</param>
        /// <returns>True if the state was changed.</returns>
        public virtual bool DetermineState(int layer, AnimatorStateData defaultState, bool checkItemState)
        {
            // Return early if there is no additive layer.
            if (layer > m_Animator.layerCount - 1) {
                return false;
            }

            if (checkItemState) {
                bool stateChange;
                if (HasItemState(true, layer, 0, out stateChange)) {
                    return true;
                }
            }

            for (int i = 0; i < m_Abilities.Length; ++i) {
                if (m_Abilities[i].IsActive && m_Abilities[i].HasAnimatorControl(layer)) {
                    var destinationState = m_Abilities[i].GetDestinationState(layer);
                    if (!string.IsNullOrEmpty(destinationState)) {
                        return ChangeAnimatorStates(m_Abilities[i], layer, destinationState, m_Abilities[i].GetTransitionDuration(), m_Abilities[i].CanReplayAnimationStates(),
                                                    m_Abilities[i].SpeedMultiplier, 0);
                    }
                    if (!m_Abilities[i].IsConcurrentAblity()) {
                        break;
                    }
                }
            }
            return ChangeAnimatorStates(null, layer, defaultState, 0);
        }

        /// <summary>
        /// Tries to change to the state for each equipped item.
        /// </summary>
        /// <param name="highPriority">Should only the high priority states be retrieved?</param>
        /// <param name="layer">The current animator layer.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <param name="stateChange">True if the state was changed.</param>
        /// <returns>True if a valid state was retrieved. Note that the state may not necessarily be changed.</returns>
        private bool HasItemState(bool highPriority, int layer, float normalizedTime, out bool stateChange)
        {
            stateChange = false;
            if (HasItemState(highPriority, layer, normalizedTime, m_CurrentPrimaryItem.Get(), ref stateChange)) {
                return true;
            }
            if (HasItemState(highPriority, layer, normalizedTime, m_CurrentDualWieldItem.Get(), ref stateChange)) {
                return true;
            }
            // The SecondayItemType can only respond to high priority state changes.
            if (highPriority) {
                if (HasItemState(highPriority, layer, normalizedTime, m_CurrentSecondaryItem.Get(), ref stateChange)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to change to the state requested by the specified item.
        /// </summary>
        /// <param name="highPriority">Should only the high priority states be retrieved?</param>
        /// <param name="layer">The current animator layer.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <param name="item">The item which could change the Animator states.</param>
        /// <param name="stateChange">True if the state was changed.</param>
        /// <returns>True if a valid state was retrieved. Note that the state may not necessarily be changed.</returns>
        private bool HasItemState(bool highPriority, int layer, float normalizedTime, Item item, ref bool stateChange)
        {
            if (item != null) {
                var state = item.GetDestinationState(highPriority, layer);
                if (state != null) {
                    stateChange = ChangeAnimatorStates(null, layer, state, normalizedTime);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determine the state that the upperbody and arm layers should be in.
        /// </summary>
        private void DetermineUpperBodyStates()
        {
            DetermineUpperBodyState(false, false);
            DetermineState(GetLeftArmLayerIndex(), m_LeftArmState, true);
            DetermineState(GetRightArmLayerIndex(), m_RightArmState, true);
        }

        /*private void Update()
        {            
            // Useful for debugging synchronization problems:
            var lower = m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            lower -= (int)lower;
            var upper = m_Animator.GetCurrentAnimatorStateInfo(1).normalizedTime;
            upper -= (int)upper;
            if (Mathf.Abs(lower - upper) > 0.01f) {
                Debug.Log("Lower Time: " + lower + " Upper Time: " + upper);
            }
        }*/

        /// <summary>
        /// Change the Animator layer to the specified state with the desired transition.
        /// </summary>
        /// <param name="ability">The ability that active. Can be null.</param>
        /// <param name="layer">The layer to change the state on.</param>
        /// <param name="animatorStateData">The data about the destination state.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <returns>True if the state was changed.</returns>
        private bool ChangeAnimatorStates(Ability ability, int layer, AnimatorStateData animatorStateData, float normalizedTime)
        {
            // Animator state data may be null if there is no inventory and an item state is trying to be changed.
            if (animatorStateData == null) {
                return false;
            }

            // The destination state depends on the equipped item and whether or not that item specifies a lower body state name. Format the lower and upper
            // states to take the item into account.
            var destinationState = string.Empty;
            if (animatorStateData is AnimatorItemStateData) {
                var animatorItemStateData = animatorStateData as AnimatorItemStateData;
                if (layer == GetLowerBodyLayerIndex()) {
                    destinationState = FormatLowerBodyState(animatorItemStateData);
                } else {
                    destinationState = FormatUpperBodyState(animatorItemStateData);
                }
            } else {
                destinationState = animatorStateData.Name;
            }

            return ChangeAnimatorStates(ability, layer, destinationState, animatorStateData.TransitionDuration, animatorStateData.CanReplay, animatorStateData.SpeedMultiplier, normalizedTime);
        }

        /// <summary>
        /// Change the Animator layer to the specified state with the desired transition.
        /// </summary>
        /// <param name="ability">The ability that active. Can be null.</param>
        /// <param name="layer">The layer to change the state on.</param>
        /// <param name="destinationState">The name of the destination state.</param>
        /// <param name="transitionDuration">The transtiion duration to the destination state.</param>
        /// <param name="canReplay">Can the state be replayed if it is already playing?</param>
        /// <param name="speedMultiplier">The Animator speed multiplier of the destination state.</param>
        /// <param name="normalizedTime">The normalized time to start playing the animation state.</param>
        /// <returns>True if the state was changed.</returns>
        private bool ChangeAnimatorStates(Ability ability, int layer, string destinationState, float transitionDuration, bool canReplay, float speedMultiplier, float normalizedTime)
        {
            if (!string.IsNullOrEmpty(destinationState)) {
                // Do a check to ensure the destination state is unique.
                if (!canReplay && GetActiveStateHash(layer) == destinationState.GetHashCode()) {
                    return false;
                }

                // Prevent the transition duration from being infinitely long.
                var normalizedDuration = transitionDuration / m_Animator.GetCurrentAnimatorStateInfo(layer).length;
                if (float.IsInfinity(normalizedDuration)) {
                    normalizedDuration = 0;
                }
                m_ActiveStateHash[layer] = destinationState.GetHashCode();
                if (m_DebugStateChanges) {
                    Debug.Log("State Change - Layer: " + layer + " State: " + destinationState + " Duration: " + normalizedDuration + " Time: " + normalizedTime + " Frame: " + Time.frameCount);
                }

                var stateHash = GetStateNameHash(destinationState);
#if UNITY_EDITOR && !UNITY_4_6
                if (!m_Animator.HasState(layer, stateHash)) {
                    Debug.LogError("Error: Unable to transition to destination state " + destinationState + " within layer " + layer + " because the state doesn't exist.");
                    return false;
                }
#endif

#if ENABLE_MULTIPLAYER
                // Crossfade the states on all of the clients if in a network game and currently executing on the server.
                if (isServer) {
                    RpcCrossFade(stateHash, normalizedDuration, layer, normalizedTime, speedMultiplier);
                }
                // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                // in which case the method will be called with the Rpc call.
                if (!isClient) {
#endif
                    CrossFade(stateHash, normalizedDuration, layer, normalizedTime, speedMultiplier);
#if ENABLE_MULTIPLAYER
                }
#endif
                return true;
            }
            return false;
        }

        /// Returns the hashcode of the active state.
        /// </summary>
        /// <param name="layer">The layer index of the active state.</param>
        /// <returns>The active state hash code.</returns>
        private int GetActiveStateHash(int layer)
        {
            if (layer > m_ActiveStateHash.Count - 1) {
                // Layer 1 may be changed before layer 0 so add the initial hash in a loop.
                for (int i = m_ActiveStateHash.Count; i < layer + 1; ++i) {
                    m_ActiveStateHash.Add(0);
                }
            }
            return m_ActiveStateHash[layer];
        }

        /// <summary>
        /// Retrieves the hash given a state name.
        /// </summary>
        /// <param name="stateName">The state name to retrieve the hash of.</param>
        /// <returns>The Animator hash value.</returns>
        private int GetStateNameHash(string stateName)
        {
            // Use a dictionary for quick lookup.
            int hash;
            if (m_StateNamesHash.TryGetValue(stateName, out hash)) {
                return hash;
            }

            hash = Animator.StringToHash(stateName);
            m_StateNamesHash.Add(stateName, hash);
            return hash;
        }

        /// <summary>
        /// Create a dynamic transition between the current state and the destination state.
        /// </summary>
        /// <param name="stateHash">The name of the destination state.</param>
        /// <param name="normalizedDuration">The duration of the transition. Value is in source state normalized time.</param>
        /// <param name="layer">Layer index containing the destination state. </param>
        /// <param name="normalizedTime">Start time of the current destination state.</param>
#if ENABLE_MULTIPLAYER
        [ClientRpc]
        private void RpcCrossFade(int stateHash, float normalizedDuration, int layer, float normalizedTime, float speedMultiplier)
        {
            CrossFade(stateHash, normalizedDuration, layer, normalizedTime, speedMultiplier);
        }
#endif

        /// <summary>
        /// Create a dynamic transition between the current state and the destination state.
        /// </summary>
        /// <param name="stateHash">The name of the destination state.</param>
        /// <param name="normalizedDuration">The duration of the transition. Value is in source state normalized time.</param>
        /// <param name="layer">Layer index containing the destination state. </param>
        /// <param name="normalizedTime">Start time of the current destination state.</param>
        private void CrossFade(int stateHash, float normalizedDuration, int layer, float normalizedTime, float speedMultiplier)
        {
            m_Animator.CrossFade(stateHash, normalizedDuration, layer, normalizedTime);

            // The speed value is a global value per Animator instead of per state. Only set a new speed if the layer is currently the lower body layer.
            if (layer == GetLowerBodyLayerIndex()) {
                m_Animator.speed = speedMultiplier;
            }
        }

        /// <summary>
        /// Returns the index of the lower body layer.
        /// </summary>
        /// <returns>The lower body layer index.</returns>
        public virtual int GetLowerBodyLayerIndex()
        {
            return 0;
        }

        /// <summary>
        /// Returns the index of the upper body layer.
        /// </summary>
        /// <returns>The upper body layer index.</returns>
        public virtual int GetUpperBodyLayerIndex()
        {
            return 1;
        }

        /// <summary>
        /// Returns the index of the left arm layer.
        /// </summary>
        /// <returns>The left arm layer index.</returns>
        public virtual int GetLeftArmLayerIndex()
        {
            return 2;
        }

        /// <summary>
        /// Returns the index of the right arm layer.
        /// </summary>
        /// <returns>The right arm layer index.</returns>
        public virtual int GetRightArmLayerIndex()
        {
            return 3;
        }

        /// <summary>
        /// Returns the index of the additive layer.
        /// </summary>
        /// <returns>The additive layer index.</returns>
        public virtual int GetAdditiveLayerIndex()
        {
            return 4;
        }

        /// <summary>
        /// Updates the horizontal input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetHorizontalInputValue(float value)
        {
            SetHorizontalInputValue(value, m_HorizontalInputDampTime);
        }

        /// <summary>
        /// Updates the horizontal input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="dampTime">The time allowed for the parameter to reach the value.</param>
        public void SetHorizontalInputValue(float value, float dampTime)
        {
            if (value != HorizontalInputValue) {
                m_HorizontalInputValue = value;
                m_Animator.SetFloat(s_HorizontalInputHash, m_HorizontalInputValue, dampTime, Time.deltaTime);
            }
        }

        /// <summary>
        /// Updates the veritcal input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetForwardInputValue(float value)
        {
            SetForwardInputValue(value, m_ForwardInputDampTime);
        }

        /// <summary>
        /// Updates the forward input value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        /// <param name="dampTime">The time allowed for the parameter to reach the value.</param>
        public void SetForwardInputValue(float value, float dampTime)
        {
            if (value != ForwardInputValue) {
                m_ForwardInputValue = value;
                m_Animator.SetFloat(s_ForwardInputHash, m_ForwardInputValue, dampTime, Time.deltaTime);
            }
        }

        /// <summary>
        /// Updates the yaw value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetYawValue(float value)
        {
            if (value != YawValue) {
                m_YawValue = value;
                m_Animator.SetFloat(s_YawHash, m_YawValue, 0.1f, Time.deltaTime);
            }
        }

        /// <summary>
        /// Updates the state value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetStateValue(int value)
        {
            if (value != StateValue) {
                m_StateValue = value;
                m_Animator.SetInteger(s_StateHash, value);
            }
        }

        /// <summary>
        /// Updates the int data value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetIntDataValue(int value)
        {
            if (value != IntDataValue) {
                m_IntDataValue = value;
                m_Animator.SetInteger(s_IntDataHash, value);
            }
        }

        /// <summary>
        /// Updates the float data value and sets the Animator parameter.
        /// </summary>
        /// <param name="value">The new value.</param>
        public void SetFloatDataValue(float value)
        {
            if (value != FloatDataValue) {
                m_FloatDataValue = value;
                m_Animator.SetFloat(s_FloatDataHash, value);
            }
        }

        /// <summary>
        /// Returns the collider curve data value.
        /// </summary>
        public float GetColliderCurveData()
        {
            return m_Animator.GetFloat(s_ColliderCurveDataHash);
        }

        /// <summary>
        /// Executes an event on the EventHandler. Call the corresponding server or client method.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEvent(string eventName)
        {
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcExecuteEvent(eventName);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ExecuteEventLocal(eventName);
#if ENABLE_MULTIPLAYER
            }
#endif
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Executes an event on the EventHandler on the client.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        [ClientRpc]
        private void RpcExecuteEvent(string eventName)
        {
            ExecuteEventLocal(eventName);
        }
#endif

        /// <summary>
        /// Executes an event on the EventHandler.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        private void ExecuteEventLocal(string eventName)
        {
            EventHandler.ExecuteEvent(m_GameObject, eventName);
        }

        /// <summary>
        /// Executes an event on the EventHandler as soon as the lower body is no longer in a transition.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEventNoLowerTransition(string eventName)
        {
            if (m_Animator.IsInTransition(GetLowerBodyLayerIndex())) {
                return;
            }
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcExecuteEvent(eventName);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ExecuteEventLocal(eventName);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// Executes an event on the EventHandler as soon as the upper body is no longer in a transition.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        public void ExecuteEventNoUpperTransition(string eventName)
        {
            if (m_Animator.IsInTransition(GetUpperBodyLayerIndex())) {
                return;
            }
#if ENABLE_MULTIPLAYER
            if (isServer) {
                RpcExecuteEvent(eventName);
            }
            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
            // in which case the method will be called with the Rpc call.
            if (!isClient) {
#endif
                ExecuteEventLocal(eventName);
#if ENABLE_MULTIPLAYER
            }
#endif
        }

        /// <summary>
        /// An item has been used.
        /// </summary>
        /// <param name="itemTypeIndex">The corresponding index of the item used.</param>
        public void ItemUsed(int itemTypeIndex)
        {
            System.Type itemType;
            if (itemTypeIndex == 0) {
                itemType = typeof(PrimaryItemType);
            } else if (itemTypeIndex == 1) {
                itemType = typeof(SecondaryItemType);
            } else { // DualWieldItemType.
                itemType = typeof(DualWieldItemType);
            }
            EventHandler.ExecuteEvent<System.Type>(m_GameObject, "OnAnimatorItemUsed", itemType);
        }

        /// <summary>
        /// The Animator has moved the character into/out of position to take cover.
        /// </summary>
        public void AlignWithCover(int align)
        {
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnAnimatorAlignWithCover", align == 1);
        }

        /// <summary>
        /// The Animator has popped from cover or returned from a pop.
        /// </summary>
        /// <param name="popped">True of the character has popped from cover</param>
        public void PopFromCover(int popped)
        {
            EventHandler.ExecuteEvent<bool>(m_GameObject, "OnAnimatorPopFromCover", popped == 1);
        }

        /// <summary>
        /// An dual wielded item has been changed.
        /// </summary>
        /// <param name="item">The dual wield item. Can be null.</param>
        private void OnDualWieldItemChange(Item item)
        {
            DetermineUpperBodyStates();
        }

        /// <summary>
        /// The character has respawned. Play the default states.
        /// </summary>
        private void OnRespawn()
        {
            // The animator may have been disabled by the ragdoll so ensure it is enabled.
            m_Animator.enabled = true;
            PlayDefaultStates();
        }
    }
}