using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Creates an explision which applies a force and damage to any object that is within the specified radius.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        [Tooltip("Should the explosion explode when the object is enabled?")]
        [SerializeField] private bool m_ExplodeOnEnable;
        [Tooltip("The duration of the explosion")]
        [SerializeField] private float m_Lifespan;
        [Tooltip("How far out the explosion affects other objects")]
        [SerializeField] private float m_Radius;
        [Tooltip("The amount of force the explosion applies to other Rigidbody objects")]
        [SerializeField] private float m_ImpactForce;
        [Tooltip("Optionally specify an event to send to the object hit on damage")]
        [SerializeField] private string m_DamageEvent;
        [Tooltip("The amount of damage the explosion applies to other objects with the Health component")]
        [SerializeField] private float m_DamageAmount;
        [Tooltip("Sound to play during the explosion")]
        [SerializeField] private AudioClip m_Sound;

        // Internal variables
        private ScheduledEvent m_ExplisionEvent;
        private HashSet<GameObject> m_GameObjectExplosions = new HashSet<GameObject>();

        // Component references
        private Transform m_Transform;
        private AudioSource m_AudioSource;

        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_Transform = transform;
            m_AudioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Schedule an explosion if it should explode when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            if (m_ExplodeOnEnable) {
                m_ExplisionEvent = Scheduler.Schedule(0.1f, Explode);
            }
        }

        /// <summary>
        /// Do the explosion.
        /// </summary>
        public void Explode()
        {
            // Cancel the explosion event if it exists.
            Scheduler.Cancel(m_ExplisionEvent);

            // Loop through all of the nearby colliders and apply an explosion force and damage.
            Rigidbody colliderRigidbody = null;
            Health health = null;
            var colliders = Physics.OverlapSphere(m_Transform.position, m_Radius);
            for (int i = 0; i < colliders.Length; ++i) {
                // A GameObject can contain multiple colliders. Prevent the explosion from occurring on the same GameObject multiple times.
                if (m_GameObjectExplosions.Contains(colliders[i].gameObject)) {
                    continue;
                }
                m_GameObjectExplosions.Add(colliders[i].gameObject);

                // If the Health component exists it will apply an explosive force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
                if ((health = colliders[i].transform.GetComponentInParent<Health>()) != null) {
                    // The further out the collider is, the less it is damaged.
                    var direction = m_Transform.position - colliders[i].transform.position;
                    var damageModifier = (1 - (direction.magnitude / m_Radius));
                    health.Damage(m_DamageAmount * damageModifier, m_Transform.position, direction.normalized * -m_ImpactForce * damageModifier, m_Radius);
                } else if ((colliderRigidbody = colliders[i].GetComponent<Rigidbody>()) != null) {
                    colliderRigidbody.AddExplosionForce(m_ImpactForce, m_Transform.position, m_Radius);
                }

                // Execute any custom events.
                if (!string.IsNullOrEmpty(m_DamageEvent)) {
                    EventHandler.ExecuteEvent(colliders[i].gameObject, m_DamageEvent, m_DamageAmount, m_Transform.position, m_Transform.forward * -m_ImpactForce);
                }
            }
            m_GameObjectExplosions.Clear();

            // Boom.
            if (m_Sound != null) {
                m_AudioSource.clip = m_Sound;
                m_AudioSource.Play();
            }

            Scheduler.Schedule(m_Lifespan, Destroy);
        }

        /// <summary>
        /// Place ourselves back in the ObjectPool.
        /// </summary>
        private void Destroy()
        {
            ObjectPool.Destroy(gameObject);
        }
    }
}