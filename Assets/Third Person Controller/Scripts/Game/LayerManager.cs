using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Easy access to the Unity layer system.
    /// </summary>
    public class LayerManager : MonoBehaviour
    {
        private static LayerManager s_Instance;
        private static LayerManager Instance { get { return s_Instance; } }

        // Built-in Unity layers
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;

        // Custom layers
        public const int VisualEffect = 28;
        public const int MovingPlatform = 29;
        public const int Enemy = 30;
        public const int Player = 31;

        // Internal variables
        private Dictionary<Collider, List<Collider>> m_IgnoreCollisionMap;

        /// <summary>
        /// A set of masks used by raycasts/spherecasts.
        /// </summary>
        public static class Mask
        {
            // Mask that ignores any invisible objects/water.
            public const int IgnoreInvisibleLayers = ~((1 << TransparentFX) | (1 << IgnoreRaycast) | (1 << VisualEffect));
            // Mask that ignores the current player and any invisible objects.
            public const int IgnoreInvisibleLayersPlayer = ~((1 << TransparentFX) | (1 << IgnoreRaycast) | (1 << VisualEffect) | (1 << Player));
            // Mask that ignores the current player and any invisible objects/water.
            public const int IgnoreInvisibleLayersPlayerWater = ~((1 << TransparentFX) | (1 << IgnoreRaycast) | (1 << VisualEffect) | (1 << Player) | (1 << Water));
            // Mask that specifies the ground.
            public const int Ground = ~((1 << IgnoreRaycast) | (1 << Player) | (1 << Water) | (1 << VisualEffect));
        }

        /// <summary>
        /// Setups the layer collisions.
        /// </summary>
        public void Awake()
        {
            s_Instance = this;

            Physics.IgnoreLayerCollision(Player, VisualEffect);
            Physics.IgnoreLayerCollision(Enemy, VisualEffect);
            Physics.IgnoreLayerCollision(VisualEffect, VisualEffect);
            Physics.IgnoreLayerCollision(Player, Water);
        }

        /// <summary>
        /// A new collider has been spawned and should ignore another collider. Remember which collider this spawned collider is ignoring so this setting can be reverted.
        /// </summary>
        /// <param name="mainCollider">The collider being spawned.</param>
        /// <param name="otherCollider">The collider to ignore.</param>
        public static void IgnoreCollision(Collider mainCollider, Collider otherCollider)
        {
            Instance.IgnoreCollisionInternal(mainCollider, otherCollider);
        }

        /// <summary>
        /// A new collider has been spawned and should ignore another collider. Internal method to remember which collider this spawned collider is ignoring so this setting can be reverted.
        /// </summary>
        /// <param name="mainCollider">The collider being spawned.</param>
        /// <param name="otherCollider">The collider to ignore.</param>
        private void IgnoreCollisionInternal(Collider mainCollider, Collider otherCollider)
        {
            // Keep a mapping of the colliders that mainCollider is ignorning so when it goes back into the object pool the collision can be reverted.
            if (m_IgnoreCollisionMap == null) {
                m_IgnoreCollisionMap = new Dictionary<Collider, List<Collider>>();
            }

            // Add the collider to the list so it can be reverted.
            List<Collider> colliderList;
            if (!m_IgnoreCollisionMap.TryGetValue(mainCollider, out colliderList)) {
                colliderList = new List<Collider>();
                m_IgnoreCollisionMap.Add(mainCollider, colliderList);
            }
            colliderList.Add(otherCollider);

            // The otherCollider must also keep track of the mainCollder. This allows otherCollider to be removed before mainCollider.
            if (!m_IgnoreCollisionMap.TryGetValue(mainCollider, out colliderList)) {
                colliderList = new List<Collider>();
                m_IgnoreCollisionMap.Add(otherCollider, colliderList);
            }
            colliderList.Add(mainCollider);

            // Do the actual ignore.
            Physics.IgnoreCollision(mainCollider, otherCollider);
        }

        /// <summary>
        /// The collider's GameObject is being placed back in the object pool. Revert the IgnoreCollision settings so the next time the object spawns it won't be ignoring
        /// colliders that it shouldn't be ignoring.
        /// </summary>
        /// <param name="mainCollider">The collider to revert the settings on.</param>
        public static void RevertCollision(Collider mainCollider)
        {
            Instance.RevertCollisionInternal(mainCollider);
        }

        /// <summary>
        /// The collider's GameObject is being placed back in the object pool. internal method to revert the IgnoreCollision settings so the next time the object spawns it won't be ignoring
        /// colliders that it shouldn't be ignoring.
        /// </summary>
        /// <param name="mainCollider">The collider to revert the settings on.</param>
        private void RevertCollisionInternal(Collider mainCollider)
        {
            List<Collider> colliderList;
            List<Collider> otherColliderList;
            // Revert the IgnoreCollision setting on all of the colliders that the object is currently ignoring.
            if (m_IgnoreCollisionMap != null && m_IgnoreCollisionMap.TryGetValue(mainCollider, out colliderList)) {
                for (int i = colliderList.Count; i < colliderList.Count; ++i) {
                    // The collider must be enabled for IgnoreCollision to work. Temporarly enable the collider is currently disabled.
                    var elementColliderDisabled = false;
                    if (!colliderList[i].enabled) {
                        colliderList[i].enabled = true;
                        elementColliderDisabled = true;
                    }

                    Physics.IgnoreCollision(mainCollider, colliderList[i], false);

                    // A two way map was added when the initial IgnoreCollision was added. Remove that second map because the IgnoreCollision has been removed.
                    if (m_IgnoreCollisionMap.TryGetValue(colliderList[i], out otherColliderList)) {
                        for (int j = 0; j < otherColliderList.Count; ++j) {
                            if (otherColliderList[j].Equals(mainCollider)) {
                                otherColliderList.RemoveAt(j);
                                break;
                            }
                        }
                    }

                    if (elementColliderDisabled) {
                        colliderList[i].enabled = false;
                    }
                }
                colliderList.Clear();
            }
        }
    }
}