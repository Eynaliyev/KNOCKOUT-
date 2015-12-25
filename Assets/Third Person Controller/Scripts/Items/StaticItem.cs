using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any Item that doesn't do anything besides exist. Using the Item will allow the character to start the aim state. Examples include a shield or book.
    /// </summary>
    public class StaticItem : Item, IUsableItem
    {
        /// <summary>
        /// Can the item be used?
        /// </summary>
        /// <returns>True if the item can be used.</returns>
        public bool CanUse()
        {
            return true;
        }

        /// <summary>
        /// Is the item currently in use?
        /// </summary>
        /// <returns>True if the item is in use.</returns>
        public bool InUse()
        {
            return false;
        }

        /// <summary>
        /// Returns the maximum distance that the item can be used.
        /// </summary>
        /// <returns>The maximum distance that hte item can be used.</returns>
        public float MaxUseDistance()
        {
            return float.MaxValue;
        }

        /// <summary>
        /// Stop the item from being used.
        /// </summary>
        public void TryStopUse()
        {
            EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
        }

        /// <summary>
        /// Try to perform the use. Depending on the item this may not always succeed. For example, if the user is trying to shoot a weapon that was shot a half
        /// second ago cannot be used if the weapon can only be fired once per second.
        /// <returns>True if the item was used.</returns>
        /// </summary>
        public bool TryUse()
        {
            return true;
        }

        /// <summary>
        /// Callback for when an item has been used.
        /// </summary>
        public void Used()
        {
        }
    }
}