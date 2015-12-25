using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Notifies the CharacterFootsteps component that the foot has collided with the ground. This will allow a sound to be played.
    /// </summary>
    public class CharacterFootTrigger : MonoBehaviour
    {
        // Internal variables
        private bool m_RightFoot;

        // Component referneces
        private GameObject m_Character;

        /// <summary>
        /// Cache the component references and initialize default values.
        /// </summary>
        private void Awake()
        {
            var characterAnimator = GetComponentInParent<Animator>();
            m_Character = characterAnimator.gameObject;

            // This trigger is on the right foot if the transforms match.
            m_RightFoot = transform.Equals(characterAnimator.GetBoneTransform(HumanBodyBones.RightFoot));
        }

        /// <summary>
        /// The trigger has collided with another object. Send the event if the object isn't invisible or the character.
        /// </summary>
        /// <param name="other"></param>
        private void OnTriggerEnter(Collider other)
        {
            if (Utility.InLayerMask(other.gameObject.layer, LayerManager.Mask.IgnoreInvisibleLayersPlayerWater)) {
                EventHandler.ExecuteEvent<bool>(m_Character, "OnTriggerFootDown", m_RightFoot);
            }
        }
    }
}