using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A SphereCollider is added to the Items to prevent the item from clipping into walls while the character is walking forward. If the character uses a ragoll
    /// then there are many GameObjects of sleeping Rigidbodies between the main (active) Rigidbody and the Item GameObject. This prevents collisions from occuring
    /// with the collider specified on the Item. To get around this, add a SphereCollider to the main Character GameObject and position it where the Item collider
    /// would be positioned.
    /// </summary>
    public class ItemColliderPositioner : MonoBehaviour
    {
        // Component references
        private Transform m_Transform;
        private SphereCollider m_SphereCollider;
        private CapsuleCollider m_CapsuleCollider;
        private RigidbodyCharacterController m_Controller;

        private SphereCollider m_ItemSphereCollider;
        private Transform m_ItemTransform;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_SphereCollider = GetComponent<SphereCollider>();
            m_CapsuleCollider = GetComponent<CapsuleCollider>();
            m_Controller = GetComponent<RigidbodyCharacterController>();

            SharedManager.Register(this);

            // Start disabled. When SetItemCollider gets called the component will be enabled.
            m_SphereCollider.enabled = enabled = false;
        }

        /// <summary>
        /// Update the position and radius of the sphere collider to match that of the item sphere collider.
        /// </summary>
        private void Update()
        {
            var center = m_ItemTransform.TransformPoint(m_ItemSphereCollider.center);
            m_SphereCollider.center = m_Transform.InverseTransformPoint(center);
            m_SphereCollider.radius = m_ItemSphereCollider.radius;

            // Disable the sphere collider if an object exists between the character's center and the sphere collider. This prevents the character from getting stuck in a wall.
            var enableSphereCollider = !Physics.Linecast(m_Transform.position + m_CapsuleCollider.center, m_ItemTransform.TransformPoint(m_ItemSphereCollider.center), LayerManager.Mask.IgnoreInvisibleLayersPlayer);

            // The sphere collider should also be disabled if the character isn't aiming.
            if (enableSphereCollider) {
                enableSphereCollider = m_Controller.Aiming;
            }

            m_SphereCollider.enabled = enableSphereCollider;
        }

        /// <summary>
        /// Start matching the item's collider position/radius if the collider is enabled, otherwise disable the component if itemSphereCollider is null.
        /// </summary>
        /// <param name="itemSphereCollider">The sphere collider to match the position/radius of.</param>
        public void SharedMethod_SetItemCollider(SphereCollider itemSphereCollider)
        {
            m_ItemSphereCollider = itemSphereCollider;
            m_SphereCollider.enabled = enabled = m_ItemSphereCollider != null;
            if (enabled) {
                m_ItemTransform = m_ItemSphereCollider.transform;
            }
        }
    }
}