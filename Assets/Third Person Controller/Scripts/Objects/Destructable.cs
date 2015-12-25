using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The Destructable class is an abstract class which acts as the base class for any object that destroys itself and applies a damange. Primary uses include projectiles and grenades.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public abstract class Destructable : NetworkBehaviour
#else
    public abstract class Destructable : MonoBehaviour
#endif
    {
        [Tooltip("The parent Item that spawned the Destructable object")]
        [SerializeField] private ItemBaseType m_ParentItemType;
        [Tooltip("The amount of damage done to the object hit. Will not be used if an explosion is specified")]
        [SerializeField] private float m_DamageAmount = 100;
        [Tooltip("How much force is applied to the object hit. Will not be used if an explosion is specified")]
        [SerializeField] private float m_ImpactForce = 5;
        [Tooltip("Optionally specify an event to send when the object is hit. Will not be used if an explosion is specified")]
        [SerializeField] private string m_DamageEvent;
        [Tooltip("Optionally specify an explosion prefab")]
        [SerializeField] private GameObject m_Explosion;
        [Tooltip("Optionally specify a decal when the destructable activates. This is only used if no per-object decal is setup in the ObjectManager")]
        [SerializeField] private GameObject m_DefaultDecal;
        [Tooltip("Optionally specify dust when the destructable activates. This is only used if no per-object dust is setup in the ObjectManager")]
        [SerializeField] private GameObject m_DefaultDust;

        // Component references
        private GameObject m_GameObject;
        protected Transform m_Transform;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        protected virtual void Awake()
        {
            m_GameObject = gameObject;
            m_Transform = transform;
        }
        
        /// <summary>
        /// The projectile has collided. Apply damage to the object hit and spawn various effects (explosion, decal, etc).
        /// </summary>
        /// <param name="collisionTransform">The collision object that the destructable collided with. Can be null.</param>
        /// <param name="collisionPoint">The point of destruction.</param>
        /// <param name="collisionNormal">The normal at the destruction point.</param>
        /// <param name="destroy">Should the projectile be destroyed and placed back in the ObjectPool?</param>
        protected virtual void Collide(Transform collisionTransform, Vector3 collisionPoint, Vector3 collisionNormal, bool destroy)
        {
#if ENABLE_MULTIPLAYER
            if (!isServer) {
                return;
            }
#endif

            // Spawn the explosion. The explosion will apply the damage and impact force. If there is no explosion then just apply the damage and impact force now.
            if (m_Explosion != null) {
                var explosion = ObjectPool.Spawn(m_Explosion, m_Transform.position, m_Transform.rotation).GetComponent<Explosion>();
                explosion.Explode();
            } else if (collisionTransform != null) {
                // Execute any custom events.
                if (!string.IsNullOrEmpty(m_DamageEvent)) {
                    EventHandler.ExecuteEvent(collisionTransform.gameObject, m_DamageEvent, m_DamageAmount, m_Transform.position, m_Transform.forward * -m_ImpactForce);
                }

                // If the Health component exists it will apply a force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
                Health hitHealth;
                if ((hitHealth = collisionTransform.GetComponent<Health>()) != null) {
                    hitHealth.Damage(m_DamageAmount, collisionPoint, m_Transform.forward * -m_ImpactForce);
                } else {
                    var m_CollisionRigidbody = collisionTransform.GetComponent<Rigidbody>();
                    if (m_ImpactForce > 0 && m_CollisionRigidbody != null && !m_CollisionRigidbody.isKinematic) {
                        m_CollisionRigidbody.AddForceAtPosition(m_Transform.forward * -m_ImpactForce, collisionPoint);
                    }
                }
            }

            // Add any collision effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
            RpcAddCollisionEffects(collisionTransform != null ? collisionTransform.gameObject : null, collisionPoint, collisionNormal);
#else
            AddCollisionEffects(collisionTransform, collisionPoint, collisionNormal);
#endif

            if (destroy) {
                // Place back in the ObjectPool.
                ObjectPool.Destroy(m_GameObject);
            } else {
                // Let the ObjectManager manage the project. It will remove the projectile when too many of the same projectiles have been instantiated.
                ObjectManager.AddObject(gameObject);
            }
            enabled = false;
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The destructable object has collided itself. Add any effects on the client.
        /// </summary>
        /// <param name="collisionTransform">The collision object that the destructable collided with. Can be null.</param>
        /// <param name="collisionPoint">The point of destruction.</param>
        /// <param name="collisionNormal">The normal at the destruction point.</param>
        [ClientRpc]
        private void RpcAddCollisionEffects(GameObject collisionGameObject, Vector3 collisionPoint, Vector3 collisionNormal)
        {
            AddCollisionEffects(collisionGameObject != null ? collisionGameObject.transform : null, collisionPoint, collisionNormal);
        }
#endif

        /// <summary>
        /// The destructable object has collided itself. Add any effects.
        /// </summary>
        /// <param name="collisionTransform">The collision object that the destructable collided with. Can be null.</param>
        /// <param name="collisionPoint">The point of destruction.</param>
        /// <param name="collisionNormal">The normal at the destruction point.</param>
        private void AddCollisionEffects(Transform collisionTransform, Vector3 collisionPoint, Vector3 collisionNormal)
        {
            var hitRotation = Quaternion.LookRotation(collisionNormal);

            // Don't add the decal if the hit layer doesnt allow decals (such as other characters).
            if ((collisionTransform == null || DecalManager.CanAddDecal(collisionTransform.gameObject.layer))) {
                var decal = collisionTransform != null ? ObjectManager.ObjectForItem(collisionTransform.tag, m_ParentItemType, ObjectManager.ObjectCategory.Decal) as GameObject : null;
                if (decal == null) {
                    decal = m_DefaultDecal;
                }
                if (decal != null) {
                    // Apply a decal to the hit point. Offset the decal by a small amount so it doesn't interset with the object hit.
                    DecalManager.Add(decal, collisionPoint + collisionNormal * 0.1f, decal.transform.rotation * hitRotation, (collisionTransform == null ? null : collisionTransform));
                }
            }

            // Spawn dust particle effect.
            var dust = collisionTransform != null ? ObjectManager.ObjectForItem(collisionTransform.tag, m_ParentItemType, ObjectManager.ObjectCategory.Dust) as GameObject : null;
            if (dust == null) {
                dust = m_DefaultDust;
            }
            if (dust != null) {
                ObjectPool.Instantiate(dust, collisionPoint, dust.transform.rotation * hitRotation);
            }
        }
    }
}