using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Singleton object which randomly selects a new spawn location out of the spawn location list.
    /// </summary>
    public class SpawnSelection : MonoBehaviour
    {
        // Static variables
        private static SpawnSelection s_Instance;
        private static SpawnSelection Instance { get { return s_Instance; } }

        [Tooltip("The locations that the object can spawn")]
        [SerializeField] private Transform[] m_SpawnLocations;
        
        /// <summary>
        /// Assign the static variables.
        /// </summary>
        private void Awake()
        {
            s_Instance = this;
        }

        /// <summary>
        /// Static method for returning a random spawn location from the spawn location list.
        /// </summary>
        /// <returns>The Transform of a random spawn location.</returns>
        public static Transform GetSpawnLocation()
        {
            return Instance.GetSpawnLocationInternal();
        }

        /// <summary>
        /// Internal method for returning a random spawn location from the spawn location list.
        /// </summary>
        /// <returns>The Transform of a random spawn location.</returns>
        protected virtual Transform GetSpawnLocationInternal()
        {
            if (m_SpawnLocations.Length == 0) {
                Debug.LogError("SpawnSelection Error: No spawn positions have been added.");
                return null;
            }

            return m_SpawnLocations[Random.Range(0, m_SpawnLocations.Length - 1)];
        }
    }
}