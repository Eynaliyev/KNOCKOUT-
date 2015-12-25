using UnityEngine;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Plays a random footstep sound when the foot touches the ground. This is sent through an animation event.
    /// </summary>
    public class CharacterFootsteps : MonoBehaviour
    {
        /// <summary>
        /// Stores the footstep sound along with what foot that sound maps to. The foot specification is only used if PerFootSounds is enabled.
        /// </summary>
        [System.Serializable]
        private class FootstepSound
        {
            [Tooltip("The sound to play")]
            [SerializeField] private AudioClip m_Sound;
            [Tooltip("Does this sound correspond to the right foot?")]
            [SerializeField] private bool m_RightFoot;

            // Exposed properties
            public AudioClip Sound { get { return m_Sound; } }
            public bool RightFoot { get { return m_RightFoot; } }
        }

        [Tooltip("Should a unique sound play for each foot?")]
        [SerializeField] private bool m_PerFootSounds;
        [Tooltip("A reference to the feet which contain an AudioSource")]
        [SerializeField] private GameObject[] m_Feet = new GameObject[0];
        [Tooltip("A list of sounds to play when the foot hits the ground")]
        [SerializeField] private FootstepSound[] m_Footsteps = new FootstepSound[0];

        // Exposed properties
        public GameObject[] Feet { set { m_Feet = value; } }

        // Internal variables
        private List<AudioClip> m_LeftFootSounds;
        private List<AudioClip> m_RightFootSounds;

        // Component references
        private GameObject m_GameObject;
        private AudioSource[] m_FootAudioSource;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        private void Awake()
        {
            m_GameObject = gameObject;
            m_FootAudioSource = new AudioSource[m_Feet.Length];
            for (int i = 0; i < m_Feet.Length; ++i) {
                m_FootAudioSource[i] = m_Feet[i].GetComponent<AudioSource>();
                if (m_FootAudioSource[i] == null) {
                    Debug.LogError("Error: The " + (i == 0 ? "left" : "right") + "foot does not have a AudioSource required for footsteps.");
                }
            }
            
            // Initialze the left and right foot arrays if each foot has a specific sound.
            if (m_PerFootSounds) {
                m_LeftFootSounds = new List<AudioClip>();
                m_RightFootSounds = new List<AudioClip>();
                for (int i = 0; i < m_Footsteps.Length; ++i) {
                    if (m_Footsteps[i].RightFoot) {
                        m_RightFootSounds.Add(m_Footsteps[i].Sound);
                    } else {
                        m_LeftFootSounds.Add(m_Footsteps[i].Sound);
                    }
                }
                if (m_RightFootSounds.Count == 0 || m_LeftFootSounds.Count == 0) {
                    Debug.LogError("Error: At least one sound must be specified for each foot if PerFootSounds is enabled");
                }
            }
        }

        /// <summary>
        /// Register for any events that the footsteps should be aware of.
        /// </summary>
        private void OnEnable()
        {
            if (m_Footsteps.Length > 0) {
                EventHandler.RegisterEvent<bool>(m_GameObject, "OnTriggerFootDown", PlayFootstep);
            }
        }

        /// <summary>
        /// Unregister for any events that the footsteps should be aware of.
        /// </summary>
        private void OnDisable()
        {
            if (m_Footsteps.Length > 0) {
                EventHandler.UnregisterEvent<bool>(m_GameObject, "OnTriggerFootDown", PlayFootstep);
            }
        }

        /// <summary>
        /// An animation event says that a foot touched the ground - play a footstep.
        /// </summary>
        /// <param name="rightFoot">Did the right foot touch the ground?</param>
        private void PlayFootstep(bool rightFoot)
        {
            var footIndex = rightFoot ? 1 : 0;
            if (footIndex >= m_FootAudioSource.Length) {
                return;
            }
            m_FootAudioSource[footIndex].clip = SoundForFoot(rightFoot);
            m_FootAudioSource[footIndex].Play();
        }

        /// <summary>
        /// Returns the AudioClip for the specified foot.
        /// </summary>
        /// <param name="rightFoot">Should the right foot sound be returned?</param>
        /// <returns>The corresponding foot AudioClip.</returns>
        private AudioClip SoundForFoot(bool rightFoot)
        {
            if (m_PerFootSounds) {
                if (rightFoot) {
                    return m_RightFootSounds[Random.Range(0, m_RightFootSounds.Count)];
                }
                return m_LeftFootSounds[Random.Range(0, m_LeftFootSounds.Count)];
            }
            // Each foot does not have a unique sound so return any sound within the array.
            return m_Footsteps[Random.Range(0, m_Footsteps.Length)].Sound;
        }
    }
}