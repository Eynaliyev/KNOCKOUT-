using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// The Water object acts as a trigger which will start or stop the Swim ability.
    /// </summary>
    public class Water : MonoBehaviour
    {
        /// <summary>
        /// An object has entered the trigger. Determine if it is a character with the Swim ability.
        /// </summary>
        /// <param name="other">The potential character.</param>

        private void OnTriggerEnter(Collider other)
        {
            var controller = other.GetComponent<RigidbodyCharacterController>();
            if (controller != null) {
                var swimAbility = controller.GetComponent<Abilities.Swim>();
                if (swimAbility != null) {
                    controller.TryStartAbility(swimAbility);
                }
            }
        }

        /// <summary>
        /// An object has left the trigger. Stop the swim ability if the leaving object is a character.
        /// </summary>
        /// <param name="other">The potential character.</param>
        private void OnTriggerExit(Collider other)
        {
            var controller = other.GetComponent<RigidbodyCharacterController>();
            if (controller != null) {
                var swimAbility = controller.GetComponent<Abilities.Swim>();
                if (swimAbility != null) {
                    controller.TryStopAbility(swimAbility);
                }
            }
        }
    }
}