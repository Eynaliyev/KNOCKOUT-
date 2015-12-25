using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// When the character dies it should turn into a ragdoll to let the physics engine react to the damage.
    /// </summary>
    public class CharacterRagdoll : MonoBehaviour
    {
        [Tooltip("The ragdoll's rigidbodies will be set to kinematic as soon as the transforms have moved a total difference less than this amount between two frames")]
        [SerializeField] private float m_SettledThreshold = 0.01f;
        [Tooltip("The number of frames that the rigidbodies have to be settled before they are set to kinematic")]
        [SerializeField] private int m_SettledFrameCount = 5;

        // Internal variables
        private int m_FrameCount = 0;
        private List<Transform> m_Transforms = new List<Transform>();
        private List<Collider> m_Colliders = new List<Collider>();
        private List<Rigidbody> m_Rigidbodies = new List<Rigidbody>();
        private List<Vector3> m_PrevTransformPosition = new List<Vector3>();

        // Component references
        private Animator m_Animator;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Animator = GetComponent<Animator>();

            // Cache the components for quick access.
            var transforms = GetComponentsInChildren<Transform>();
            for (int i = 0; i < transforms.Length; ++i) {
                if (transforms[i].gameObject == gameObject) {
                    continue;
                }
                m_Transforms.Add(transforms[i]);
                m_PrevTransformPosition.Add(transforms[i].position);
            }
            var colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; ++i) {
                // Don't add the collider to the list if the collider is the main character's collider, a trigger, or on an item. The item can have its collider enabled.
                if (colliders[i].gameObject == gameObject || colliders[i].isTrigger || colliders[i].GetComponent<Item>() != null) {
                    continue;
                }
                m_Colliders.Add(colliders[i]);
            }
            var rigidbodies = GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < rigidbodies.Length; ++i) {
                if (rigidbodies[i].gameObject == gameObject) {
                    continue;
                }
                m_Rigidbodies.Add(rigidbodies[i]);
            }
        }

        /// <summary>
        /// When the game starts the ragdoll should initially be disabled.
        /// </summary>
        private void Start()
        {
            EventHandler.RegisterEvent<Vector3, Vector3>(gameObject, "OnDeathDetails", OnDeath);

            EnableRagdoll(false);
        }

        /// <summary>
        /// Set the ragdoll's rigidbodies to kinematic as soon as all of the transforms have settled. This will prevent the ragdolls from twitching.
        /// </summary>
        private void Update()
        {
            var settledValue = 0f;
            for (int i = 0; i < m_Transforms.Count; ++i) {
                settledValue += (m_PrevTransformPosition[i] - m_Transforms[i].position).sqrMagnitude;
                m_PrevTransformPosition[i] = m_Transforms[i].position;
            }

            if (settledValue != 0 && Mathf.Sqrt(settledValue) < m_SettledThreshold) {
                if (m_FrameCount < m_SettledFrameCount) {
                    m_FrameCount++;
                } else {
                    for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                        m_Rigidbodies[i].isKinematic = true;
                        m_Rigidbodies[i].constraints = RigidbodyConstraints.FreezeAll;
                    }
                    enabled = false;
                }
            }
        }

        /// <summary>
        /// The character has died. Enable the ragdoll and add the damaging force to all of the rigidbodies.
        /// </summary>
        /// <param name="force">The amount of force which killed the character.</param>
        /// <param name="position">The position of the force.</param>
        private void OnDeath(Vector3 force, Vector3 position)
        {
            EventHandler.UnregisterEvent<Vector3, Vector3>(gameObject, "OnDeathDetails", OnDeath);
            EventHandler.RegisterEvent(gameObject, "OnRespawn", OnRespawn);

            m_FrameCount = 0;
            m_Animator.enabled = false;

            EnableRagdoll(true);

            for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                m_Rigidbodies[i].AddForceAtPosition(force, position);
            }
        }

        /// <summary>
        /// The character has respawned. Disable the ragdoll.
        /// </summary>
        private void OnRespawn()
        {
            EventHandler.RegisterEvent<Vector3, Vector3>(gameObject, "OnDeathDetails", OnDeath);
            EventHandler.UnregisterEvent(gameObject, "OnRespawn", OnRespawn);

            EnableRagdoll(false);

            m_Animator.enabled = true;
        }

        /// <summary>
        /// Enable or disable all of the ragdoll colliders and rigidbodies. 
        /// If enabling the ragdoll then save off the transform positions so we know when the character has settled into position.
        /// </summary>
        /// <param name="enable">Should the ragdoll be enabled?</param>
        private void EnableRagdoll(bool enable)
        {
            if (enable) {
                for (int i = 0; i < m_Transforms.Count; ++i) {
                    m_PrevTransformPosition[i] = m_Transforms[i].position;
                }
            }
            for (int i = 0; i < m_Colliders.Count; ++i) {
                m_Colliders[i].enabled = enable;
            }
            for (int i = 0; i < m_Rigidbodies.Count; ++i) {
                m_Rigidbodies[i].useGravity = enable;
                m_Rigidbodies[i].isKinematic = !enable;
                m_Rigidbodies[i].constraints = (enable ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeAll);
                m_Rigidbodies[i].detectCollisions = true;
            }
            enabled = enable;
        }
    }
}