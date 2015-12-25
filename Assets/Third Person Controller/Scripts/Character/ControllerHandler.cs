using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using Opsive.ThirdPersonController.Abilities;
using Opsive.ThirdPersonController.Input;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Acts as an interface between the user input and the RigidbodyCharacterController.
    /// </summary>
    [RequireComponent(typeof(RigidbodyCharacterController))]
#if ENABLE_MULTIPLAYER
    public class ControllerHandler : NetworkBehaviour
#else
    public class ControllerHandler : MonoBehaviour
#endif
    {
        private enum AimType { Down, Toggle, None }
        [Tooltip("Specifies if the character should aim when the button is down, toggled, or not at all")]
        [SerializeField] private AimType m_AimType;

        // Internal variables
        private float m_HorizontalMovement;
        private float m_ForwardMovement;
        private Quaternion m_LookRotation;
        private List<Ability> m_AbilityInputComponents;
        private List<string> m_AbilityInputNames;
        private bool m_AllowGameplayInput = true;
        private List<string> m_AbilityInputName;
        private List<string> m_AbilityInputEvent;

        // SharedFields
        private SharedMethod<bool> m_IsAI = null;
        private SharedProperty<float> m_PointClickHorizontalMovement = null;
        private SharedProperty<float> m_PointClickForwardMovement = null;
        private SharedProperty<Quaternion> m_PointClickLookRotation = null;

        // Component references
        private GameObject m_GameObject;
        private Transform m_Transform;
        private CapsuleCollider m_CapsuleCollider;
        private RigidbodyCharacterController m_Controller;
        private Camera m_Camera;
        private Transform m_CameraTransform;

        /// <summary>
        /// Cache the component references and register for any network events.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
            m_CapsuleCollider = GetComponent<CapsuleCollider>();
            m_Controller = GetComponent<RigidbodyCharacterController>();
            
#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent("OnNetworkStopClient", OnNetworkDestroy);
#endif
        }

        /// <summary>
        /// Register for any events that the handler should be aware of.
        /// </summary>
        private void OnEnable()
        {
            EventHandler.RegisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Unregister for any events that the handler was registered for and stop the character from moving.
        /// </summary>
        private void OnDisable()
        {
            EventHandler.UnregisterEvent(m_GameObject, "OnDeath", OnDeath);
        }

        /// <summary>
        /// Initializes all of the SharedFields and default values.
        /// </summary>
        private void Start()
        {
            SharedManager.InitializeSharedFields(m_GameObject, this);
            EventHandler.RegisterEvent<bool>(m_GameObject, "OnAllowGameplayInput", AllowGameplayInput);
            EventHandler.RegisterEvent<string, string>(m_GameObject, "OnAbilityRegisterInput", RegisterAbilityInput);
            EventHandler.RegisterEvent<string, string>(m_GameObject, "OnAbilityUnregisterInput", UnregisterAbilityInput);

            if (!m_IsAI.Invoke()) {
                m_Camera = Utility.FindCamera();
                m_CameraTransform = m_Camera.transform;

                CameraMonitor cameraMonitor;
                if ((cameraMonitor = m_Camera.GetComponent<CameraMonitor>()) != null) {
#if ENABLE_MULTIPLAYER
                    // While in a networked game, only assign the camera's character property if the current instance is the local player. Non-local players have their own
                    // cameras within their own client.
                    if (cameraMonitor.Character == null && isLocalPlayer) {
#else
                    if (cameraMonitor.Character == null) {
#endif
                        cameraMonitor.Character = gameObject;
                    }
                }
            }

            // An AI Agent does not use PlayerInput so Update does not need to run.
            enabled = !m_IsAI.Invoke();
        }

        /// <summary>
        /// Accepts input and will perform an immediate action (such as crouching or jumping).
        /// </summary>
        private void Update()
        {
#if ENABLE_MULTIPLAYER
            if (!isLocalPlayer) {
                return;
            }
#endif
            if (!m_AllowGameplayInput) {
                m_HorizontalMovement = m_ForwardMovement = 0;
                return;
            }

            // Detect horizontal and forward movement.
            if (m_Controller.Movement != RigidbodyCharacterController.MovementType.PointClick) {
                m_HorizontalMovement = PlayerInput.GetAxis(Constants.HorizontalInputName);
                m_ForwardMovement = PlayerInput.GetAxis(Constants.ForwardInputName);
            } else {
                m_HorizontalMovement = m_PointClickHorizontalMovement.Get();
                m_ForwardMovement = m_PointClickForwardMovement.Get();
            }

            // Should the controller aim?
            if (m_AimType == AimType.Down) {
                if (PlayerInput.GetButtonDown(Constants.AimInputName)) {
                    m_Controller.Aim = true;
                } else if (m_Controller.Aim && !PlayerInput.GetButton(Constants.AimInputName, true)) {
                    m_Controller.Aim = false;
                }
            } else if (m_AimType == AimType.Toggle) {
                if (PlayerInput.GetButtonDown(Constants.AimInputName)) {
                    m_Controller.Aim = !m_Controller.Aiming;
                }
            }

#if ENABLE_MULTIPLAYER
            if (isLocalPlayer) {
                CmdSetInputParameters(m_HorizontalMovement, m_ForwardMovement, m_LookRotation);
            }
#endif

            // Abilities can have their own input.
            if (m_AbilityInputName != null) {
                for (int i = 0; i < m_AbilityInputName.Count; ++i) {
                    if (PlayerInput.GetButtonDown(m_AbilityInputName[i])) {
#if ENABLE_MULTIPLAYER
                        CmdExecuteAbilityEvent(m_AbilityInputEvent[i]);
                        // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                        // in which case the method will be called with the Rpc call.
                        if (!isClient) {
#endif
                            EventHandler.ExecuteEvent(m_GameObject, m_AbilityInputEvent[i]);
#if ENABLE_MULTIPLAYER
                        }
#endif
                    }
                }
            }

            // Start or stop the abilities.
            if (m_AbilityInputComponents != null) {
                for (int i = 0; i < m_AbilityInputComponents.Count; ++i) {
                    if (PlayerInput.GetButtonDown(m_AbilityInputNames[i])) {
                        // Start the ability if it is not started and can be started when a button is down. Stop the ability if it is already started and
                        // the stop type is button toggle. A toggled button means the button has to be pressed and released before the ability can be stopped.
                        if (!m_AbilityInputComponents[i].IsActive && m_AbilityInputComponents[i].StartType == Ability.InputStartType.ButtonDown) {
#if ENABLE_MULTIPLAYER
                            CmdTryStartAbility(i);
                            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                            // in which case the method will be called with the Rpc call.
                            if (!isClient) {
#endif
                                m_Controller.TryStartAbility(m_AbilityInputComponents[i]);
#if ENABLE_MULTIPLAYER
                            }
#endif
                        } else if (m_AbilityInputComponents[i].StopType == Ability.InputStopType.ButtonToggle) {
#if ENABLE_MULTIPLAYER
                            CmdTryStopAbility(i);
                            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                            // in which case the method will be called with the Rpc call.
                            if (!isClient) {
#endif
                                m_Controller.TryStopAbility(m_AbilityInputComponents[i]);
#if ENABLE_MULTIPLAYER
                            }
#endif
                        }
                    } else if (PlayerInput.GetButtonUp(m_AbilityInputNames[i])) {
                        // Stop the ability if the ability can be stopped when the button is up.
                        if (m_AbilityInputComponents[i].StopType == Ability.InputStopType.ButtonUp) {
#if ENABLE_MULTIPLAYER
                            CmdTryStopAbility(i);
                            // Execute the method on the local instance. Use isClient instead of isServer because the client and server may be the same instance
                            // in which case the method will be called with the Rpc call.
                            if (!isClient) {
#endif
                                m_Controller.TryStopAbility(m_AbilityInputComponents[i]);
#if ENABLE_MULTIPLAYER
                            }
#endif
                        }
                    } else if (PlayerInput.GetDoublePress()) {
                        // Start the ability if the ability should be started with a double press.
                        if (m_AbilityInputComponents[i].StartType == Ability.InputStartType.DoublePress && !m_AbilityInputComponents[i].IsActive) {
                            m_Controller.TryStartAbility(m_AbilityInputComponents[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Call Move directly on the character. A similar approach could have been used as the CameraController/Handler where the RigidbodyCharacterController
        /// directly checks the input storage variable but this would not allow the RigidbodyCharacterController to act as an AI agent as easily. 
        /// </summary>
        private void FixedUpdate()
        {
#if ENABLE_MULTIPLAYER
            if ( isLocalPlayer) {
#endif
                if (m_Controller.Movement == RigidbodyCharacterController.MovementType.Combat || m_Controller.Movement == RigidbodyCharacterController.MovementType.Adventure) {
                    m_LookRotation = m_CameraTransform.rotation;
                } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.TopDown) {
                    var direction = (Vector3)PlayerInput.GetMousePosition() - m_Camera.WorldToScreenPoint(m_Transform.position);
                    // Convert the XY direction to an XYZ direction with Y equal to 0.
                    direction.z = direction.y;
                    direction.y = 0;
                    m_LookRotation = Quaternion.LookRotation(direction);
                } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.RPG) {
                    if (PlayerInput.IsDisabledButtonDown(false)) {
                        m_LookRotation = m_CameraTransform.rotation;
                        if (PlayerInput.IsDisabledButtonDown(true)) {
                            m_ForwardMovement = 1;
                        }
                    } else if (!PlayerInput.IsDisabledButtonDown(true)) {
                        if (m_ForwardMovement != 0 || m_HorizontalMovement != 0) {
                            m_LookRotation = m_CameraTransform.rotation;
                        }
                        m_HorizontalMovement = 0;
                    }
                } else if (m_Controller.Movement == RigidbodyCharacterController.MovementType.Pseudo3D) {
                    var direction = (Vector3)PlayerInput.GetMousePosition() - m_Camera.WorldToScreenPoint(m_Transform.position + m_CapsuleCollider.center);
                    m_LookRotation = Quaternion.LookRotation(direction);
                } else { // Point and Click.
                    m_LookRotation = m_PointClickLookRotation.Get();
                }
#if ENABLE_MULTIPLAYER
            }
#endif
            m_Controller.Move(m_HorizontalMovement, m_ForwardMovement, m_LookRotation);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Set the input parameters on the server.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        [Command(channel = (int)QosType.Unreliable)]
        private void CmdSetInputParameters(float horizontalMovement, float forwardMovement, Quaternion lookRotation)
        {
            m_HorizontalMovement = horizontalMovement;
            m_ForwardMovement = forwardMovement;
            m_LookRotation = lookRotation;

            RpcSetInputParameters(horizontalMovement, forwardMovement, lookRotation);
        }

        /// <summary>
        /// Set the input parameters on the client.
        /// </summary>
        /// <param name="horizontalMovement">-1 to 1 value specifying the amount of horizontal movement.</param>
        /// <param name="forwardMovement">-1 to 1 value specifying the amount of forward movement.</param>
        /// <param name="lookRotation">The direction the character should look or move relative to.</param>
        [ClientRpc(channel = (int)QosType.Unreliable)]
        private void RpcSetInputParameters(float horizontalMovement, float forwardMovement, Quaternion lookRotation)
        {
            // The parameters would have already been set if a local player.
            if (isLocalPlayer) {
                return;
            }
            m_HorizontalMovement = horizontalMovement;
            m_ForwardMovement = forwardMovement;
            m_LookRotation = lookRotation;
        }

        /// <summary>
        /// Try to start an ability on the server.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [Command]
        private void CmdTryStartAbility(int abilityIndex)
        {
            m_Controller.TryStartAbility(m_AbilityInputComponents[abilityIndex]);

            RpcTryStartAbility(abilityIndex);
        }

        /// <summary>
        /// Try to start an ability on the client.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [ClientRpc]
        private void RpcTryStartAbility(int abilityIndex)
        {
            m_Controller.TryStartAbility(m_AbilityInputComponents[abilityIndex]);
        }

        /// <summary>
        /// Try to stop an ability on the server.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [Command]
        private void CmdTryStopAbility(int abilityIndex)
        {
            m_Controller.TryStopAbility(m_AbilityInputComponents[abilityIndex]);

            RpcTryStopAbility(abilityIndex);
        }

        /// <summary>
        /// Try to start an ability on the client.
        /// </summary>
        /// <param name="abilityIndex">The index of the ability.</param>
        [ClientRpc]
        private void RpcTryStopAbility(int abilityIndex)
        {
            m_Controller.TryStopAbility(m_AbilityInputComponents[abilityIndex]);
        }
        
        /// <summary>
        /// Execute an ability event on the server.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        [Command]
        private void CmdExecuteAbilityEvent(string eventName)
        {
            EventHandler.ExecuteEvent(eventName);
            
            RpcExecuteAbilityEvent(eventName);
        }
        
        /// <summary>
        /// Execute an ability event on the client.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        [ClientRpc]
        private void RpcExecuteAbilityEvent(string eventName)
        {
            EventHandler.ExecuteEvent(eventName);
        }
#endif
        
        /// <summary>
        /// The abilities will register themselves with the handler so the handler can start or stop the ability.
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="inputName"></param>
        public void RegisterAbility(Ability ability, string inputName)
        {
            // The ability doesn't need to be registered with the handler if the ability can't be started or stopped by the handler.
            if (ability.StartType == Ability.InputStartType.None && ability.StopType == Ability.InputStopType.None) {
                return;
            }

            // Create two lists that will have an index that will point to the ability and its button input name.
            if (m_AbilityInputComponents == null) {
                m_AbilityInputComponents = new List<Ability>();
                m_AbilityInputNames = new List<string>();
            }
            m_AbilityInputComponents.Add(ability);
            m_AbilityInputNames.Add(inputName);
        }

        /// <summary>
        /// The character has died. Disable the handler.
        /// </summary>
        private void OnDeath()
        {
            m_HorizontalMovement = m_ForwardMovement = 0;
            enabled = false;
            EventHandler.RegisterEvent(m_GameObject, "OnRespawn", OnRespawn);
        }

        /// <summary>
        /// The character has respawned. Enable the handler.
        /// </summary>
        private void OnRespawn()
        {
            enabled = true;
            EventHandler.UnregisterEvent(m_GameObject, "OnRespawn", OnRespawn);
        }

        /// <summary>
        /// Is gameplay input allowed? An example of when it will not be allowed is when there is a fullscreen UI over the main camera.
        /// </summary>
        /// <param name="allow">True if gameplay is allowed.</param>
        private void AllowGameplayInput(bool allow)
        {
            m_AllowGameplayInput = allow;
        }

        /// <summary>
        /// Adds a new input that the handler should listen for.
        /// </summary>
        /// <param name="inputName">The input name which will trigger the event.</param>
        /// <param name="eventName">The event to trigger when the button is down.</param>
        private void RegisterAbilityInput(string inputName, string eventName)
        {
            if (m_AbilityInputName == null) {
                m_AbilityInputName = new List<string>();
                m_AbilityInputEvent = new List<string>();
            }
            m_AbilityInputName.Add(inputName);
            m_AbilityInputEvent.Add(eventName);
        }

        /// <summary>
        /// Removes an input event that the handler should no longer for.
        /// </summary>
        /// <param name="inputName">The input name which will trigger the event.</param>
        /// <param name="eventName">The event to trigger when the button is down.</param>
        private void UnregisterAbilityInput(string inputName, string eventName)
        {
            // The input name and event list will always correspond to the same abilitie's input event.
            for (int i = m_AbilityInputName.Count - 1; i >= 0; --i) {
                if (inputName.Equals(m_AbilityInputName[i]) && eventName.Equals(m_AbilityInputEvent[i])) {
                    m_AbilityInputName.RemoveAt(i);
                    m_AbilityInputEvent.RemoveAt(i);
                    break;
                }
            }
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has left the network game. Tell the camera not to follow the character anymore.
        /// </summary>
        public override void OnNetworkDestroy()
        {
            base.OnNetworkDestroy();

            if (isLocalPlayer && m_Camera != null) {
                m_Camera.GetComponent<CameraMonitor>().Character = null;
            }

            // The event will be registered again if the character joins the game again.
            EventHandler.UnregisterEvent("OnNetworkStopClient", OnNetworkDestroy);
        }
#endif
    }
}