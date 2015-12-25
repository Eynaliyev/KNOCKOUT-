using UnityEngine;
using Opsive.ThirdPersonController.Input;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Allows the player to click to move the character to a position. Will translate the NavMeshAgent desired velocity into values that the RigidbodyCharacterController can understand.
    /// </summary>
    public class PointClickControllerHandler : MonoBehaviour
    {
        // Internal variables
        private Vector3 m_Velocity;
        private Quaternion m_LookRotation;

        // Component references
        private Transform m_Transform;
        private NavMeshAgent m_NavMeshAgent;
        private Camera m_Camera;

        // SharedFields
        private float SharedProperty_PointClickHorizontalMovement {  get { return m_Velocity.x; } }
        private float SharedProperty_PointClickForwardMovement {  get { return m_Velocity.z; } }
        private Quaternion SharedProperty_PointClickLookRotation {  get { return m_LookRotation; } }

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_NavMeshAgent = GetComponent<NavMeshAgent>();
            m_Camera = Utility.FindCamera();

            // The controller will update the position rather then the NavMeshAgent.
            m_NavMeshAgent.updatePosition = false;

            SharedManager.Register(this);
        }

        private void Start()
        {
#if UNITY_EDITOR
            // The controller must use the PointClick movement type with this component.
            if (GetComponent<RigidbodyCharacterController>().Movement != RigidbodyCharacterController.MovementType.PointClick) {
                Debug.LogWarning("Warning: The PointClickControllerHandler component has been started but the RigidbodyCharacterController is not using the PointClick movement type.");
            }
#endif
        }

        /// <summary>
        /// Move towards the mouse position if the MoveInput has been pressed. Translates the NavMeshAgent desired velocity into values that the RigidbodyCharacterController can understand.
        /// </summary>
        private void FixedUpdate()
        {
            var setRotation = false;
            if (PlayerInput.GetButton(Constants.MoveInputName, true)) {
                RaycastHit hit;
                // Fire a raycast in the direction that the camera is looking. Move to the hit point if the raycast hits the ground.
                if (Physics.Raycast(m_Camera.ScreenPointToRay(UnityEngine.Input.mousePosition), out hit, Mathf.Infinity, LayerManager.Mask.Ground)) {
                    // The raycast hit a ground object. Always rotate to face the mouse position, but only move to the position if the hit object is not an enemy.
                    setRotation = true;
                    if (hit.transform.gameObject.layer != LayerManager.Enemy) {
                        m_NavMeshAgent.SetDestination(hit.point);
                    }
                }
            }

            // Only move if a path exists.
            if (m_NavMeshAgent.desiredVelocity.sqrMagnitude > 0.01f) {
                m_LookRotation = Quaternion.LookRotation(m_NavMeshAgent.desiredVelocity);
                // The normalized velocity should be relative to the look direction.
                m_Velocity = Quaternion.Inverse(m_LookRotation) * m_NavMeshAgent.desiredVelocity;
                // Only normalize if the magnitude is greater than 1. This will allow the character to walk.
                if (m_Velocity.sqrMagnitude > 1) {
                    m_Velocity.Normalize();
                }
            } else {
                m_Velocity = Vector3.zero;
                // The rotation may need to be set even though there is no velocity.
                if (setRotation) {
                    var direction = (Vector3)PlayerInput.GetMousePosition() - m_Camera.WorldToScreenPoint(m_Transform.position);
                    // Convert the XY direction to an XYZ direction with Y equal to 0.
                    direction.z = direction.y;
                    direction.y = 0;
                    m_LookRotation = Quaternion.LookRotation(direction);
                } else {
                    m_LookRotation = Quaternion.LookRotation(m_Transform.forward);
                }
            }
            // Don't let the NavMeshAgent move the character - the controller can move it.
            m_NavMeshAgent.velocity = Vector3.zero;
            // Unity 5 requires the next position be set when update position is false.
            m_NavMeshAgent.nextPosition = m_Transform.position;
        }

        /// <summary>
        /// Is the mouse over an object on the enemy layer?
        /// </summary>
        /// <returns>Returns if the mouse position is over an object on the enemy layer.</returns>
        private bool SharedMethod_PointerOverEnemy()
        {
            return Physics.Raycast(Camera.main.ScreenPointToRay(UnityEngine.Input.mousePosition), Mathf.Infinity, 1 << LayerManager.Enemy);
        }
    }
}