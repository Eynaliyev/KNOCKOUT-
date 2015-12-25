using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Interface for an object that can be thrown.
    /// </summary>
    public interface IThrownObject
    {
        /// Applies the forces to thrown the object.
        /// </summary>
        /// <param name="force">The force to apply.</param>
        /// <param name="torque">The torque to apply.</param>
        void ApplyThrowForce(Vector3 force, Vector3 torque);
    }
}