using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// A small class that stores the state name and the amount of time that it takes to transition to that state.
    /// </summary>
    [System.Serializable]
    public class AnimatorStateData
    {
        [Tooltip("The name of the state")]
        [SerializeField] private string m_Name;
        [Tooltip("The time it takes to transition to the state")]
        [SerializeField] private float m_TransitionDuration;
        [Tooltip("The Animator multiplier of the state")]
        [SerializeField] private float m_SpeedMultiplier = 1;
        [Tooltip("Can the animation be replayed while it is already playing?")]
        [SerializeField] private bool m_CanReplay;

        // Exposed properties
        public string Name { get { return m_Name; } }
        public float TransitionDuration { get { return m_TransitionDuration; } }
        public float SpeedMultiplier { get { return m_SpeedMultiplier; } }
        public bool CanReplay { get { return m_CanReplay; } }

        /// <summary>
        /// Constructor for AnimatorStateData.
        /// </summary>
        public AnimatorStateData(string name, float transitionDuration)
        {
            m_Name = name;
            m_TransitionDuration = transitionDuration;
        }
    }

    /// <summary>
    /// Extends AnimatorStateData to store data specific to the item states.
    /// </summary>
    [System.Serializable]
    public class AnimatorItemStateData : AnimatorStateData
    {
        [Tooltip("The lower state name")]
        [SerializeField] private string m_LowerStateName;
        [Tooltip("Should the Item name be added to the start of the state name?")]
        [SerializeField] private bool m_ItemNamePrefix;

        // Internal variables
        private ItemBaseType m_ItemType;

        // Exposed properties
        public string LowerStateName { get { return m_LowerStateName; } }
        public bool ItemNamePrefix { get { return m_ItemNamePrefix; } }
        public ItemBaseType ItemType { get { return m_ItemType; } set { m_ItemType = value; } }

        /// <summary>
        /// Constructor for AnimatorItemStateData.
        /// </summary>
        public AnimatorItemStateData(string name, float transitionDuration, bool itemNamePrefix)
            : base(name, transitionDuration)
        {
            m_ItemNamePrefix = itemNamePrefix;
        }
    }

    /// <summary>
    /// Represents an array of AnimatorItemStateData. Can cycle through the states randomly or sequentially.
    /// </summary>
    [System.Serializable]
    public class AnimatorItemStatesData
    {
        [Tooltip("Should the states be used sequentially? If false a random ordering will be used")]
        [SerializeField] private bool m_SequentialStates;
        [Tooltip("The list of states to cycle through")]
        [SerializeField] private AnimatorItemStateData[] m_States;

        // Internal variables
        private int m_NextStateIndex;

        // Exposed properties
        public ItemBaseType ItemType
        {
            set
            {
                for (int i = 0; i < m_States.Length; ++i) {
                    m_States[i].ItemType = value;
                }
            }
        }

        /// <summary>
        /// Constructor for AnimatorItemStatesData.
        /// </summary>
        public AnimatorItemStatesData(string name, float transitionDuration, bool itemNamePrefix)
        {
            m_States = new AnimatorItemStateData[] { new AnimatorItemStateData(name, transitionDuration, itemNamePrefix) };
        }

        /// <summary>
        /// Returns the state in the array.
        /// </summary>
        /// <returns>The state in the array.</returns>
        public AnimatorItemStateData GetState()
        {
            return m_States[m_NextStateIndex];
        }

        /// <summary>
        /// Advance to the next state.
        /// </summary>
        public void NextState()
        {
            if (m_SequentialStates) {
                m_NextStateIndex += 1;
                // If the next state index is too large then move back to the start.
                if (m_NextStateIndex == m_States.Length) {
                    m_NextStateIndex = 0;
                }
            } else {
                m_NextStateIndex = Random.Range(0, m_States.Length);
            }
        }

        /// <summary>
        /// The next state index should be reset back to the beginning.
        /// </summary>
        public void ResetNextState()
        {
            m_NextStateIndex = 0;
        }
    }
}