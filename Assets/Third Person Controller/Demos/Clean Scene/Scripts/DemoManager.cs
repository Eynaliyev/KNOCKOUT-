using UnityEngine;
using UnityEngine.UI;
using Opsive.ThirdPersonController.UI;

namespace Opsive.ThirdPersonController.Demos.Clean
{
    /// <summary>
    /// Manages the various Third Person Controller clean scene demos.
    /// </summary>
    public class DemoManager : MonoBehaviour
    {
        public enum Genre { ThirdPerson, Platformer, RPG, Pseudo3D, TopDown, PointClick, Last }
        private string[] m_Title = new string[] { "Third Person", "Platformer", "RPG", "2.5D", "Top Down", "Point and Click"};
        private string[] m_Description = new string[] {
            "This Third Person demo shows a combat movement with root motion.",
            "This Platformer demo the character does not use root motion. Root motion allows for more \"realistic\" movement which is not desired for all game types.",
            "This RPG demo shows an alternate control type more commonly found in RPG games.",
            "This 2.5D demo shows the character and camera controller being used in a 2.5D game. All of the items and abilities work as they do in a third person view.",
            "This Top Down demo shows the camera with a birds eye perspective.",
            "This Point and Click demo enables the character to move based on a mouse click instead of regular keyboard controls." };
        [Tooltip("A reference to the third person demo character")]
        [SerializeField] private GameObject m_ThirdPersonCharacter;
        [Tooltip("A reference to the RPG demo character")]
        [SerializeField] private GameObject m_RPGCharacter;
        [Tooltip("A reference to the 2.5D demo character")]
        [SerializeField] private GameObject m_Pseudo3DCharacter;
        [Tooltip("A reference to the top down demo character")]
        [SerializeField] private GameObject m_TopDownCharacter;
        [Tooltip("A reference to the genre title text")]
        [SerializeField] private Text m_GenreTitle;
        [Tooltip("A reference to the genre description text")]
        [SerializeField] private Text m_GenreDescription;
        [Tooltip("A reference to the crosshairs GameObject")]
        [SerializeField] private GameObject m_Crosshairs;
        [Tooltip("A reference to the 2.5D scene objects")]
        [SerializeField] private GameObject m_Pseudo3DObjects;
        [Tooltip("A reference to the ImageFader")]
        [SerializeField] private ImageFader m_ImageFader;
        [Tooltip("A reference to the item pickup GameObject")]
        [SerializeField] private Transform m_ItemPickups;

        public Genre CurrentGenre { get { return m_CurrentGenre; } }

        // Internal variables
        private Genre m_CurrentGenre = Genre.ThirdPerson;

        // Component references
        private RigidbodyCharacterController m_CharacterController;
        private CharacterIK m_CharacterIK;
        private Inventory m_CharacterInventory;
        private AnimatorMonitor m_CharacterAnimatorMonitor;
        private PointClickControllerHandler m_PointClickControllerHandler;
        private NavMeshAgent m_NavMeshAgent;
        private CameraMonitor m_CameraMonitor;
        private CameraController m_CameraController;
        
        /// <summary>
        /// Cache the component references.
        /// </summary>
        private void Awake()
        {
            m_CameraController = Camera.main.GetComponent<CameraController>();
            if (m_CameraController == null) {
                Debug.LogError("Error: Unable to find the CameraController.");
                enabled = false;
            }
            m_CameraMonitor = m_CameraController.GetComponent<CameraMonitor>();
        }

        /// <summary>
        /// Set the default values.
        /// </summary>
        private void Start()
        {
            m_GenreTitle.text = m_Title[(int)m_CurrentGenre];
            m_GenreDescription.text = m_Description[(int)m_CurrentGenre];
            m_Pseudo3DObjects.SetActive(false);
            SwitchCharacters();
        }

        /// <summary>
        /// A new genre has been selected. Switch characters.
        /// </summary>
        private void SwitchCharacters()
        {
            m_ImageFader.Fade();

            GameObject character = null;
            switch (m_CurrentGenre) {
                case Genre.ThirdPerson:
                case Genre.Platformer:
                    character = m_ThirdPersonCharacter;
                    break;
                case Genre.RPG:
                    character = m_RPGCharacter;
                    break;
                case Genre.Pseudo3D:
                    character = m_Pseudo3DCharacter;
                    break;
                case Genre.TopDown:
                case Genre.PointClick:
                    character = m_TopDownCharacter;
                    break;
            }
            character.SetActive(true);

            // Toggle the scheduler enable state by disabling and enabling it.
            var scheduler = GameObject.FindObjectOfType<Scheduler>();
            scheduler.enabled = false;
            scheduler.enabled = true;

            // Cache the character components.
            m_CharacterController = character.GetComponent<RigidbodyCharacterController>();
            m_CharacterIK = character.GetComponent<CharacterIK>();
            m_CharacterInventory = character.GetComponent<Inventory>();
            m_CharacterAnimatorMonitor = character.GetComponent<AnimatorMonitor>();
            m_CameraMonitor.Character = m_CameraController.Character = character;
            if (m_CurrentGenre == Genre.PointClick) {
                m_PointClickControllerHandler = character.GetComponent<PointClickControllerHandler>();
                m_NavMeshAgent = character.GetComponent<NavMeshAgent>();
            }
            if (m_PointClickControllerHandler != null) {
                m_PointClickControllerHandler.enabled = m_NavMeshAgent.enabled = m_CurrentGenre == Genre.PointClick;
            }
        }

        /// <summary>
        /// Switch characters when the enter key is pressed.  
        /// </summary>
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return)) {
                SwitchGenres(true);
            }
        }

        /// <summary>
        /// Switch to the previous or next genre.
        /// </summary>
        /// <param name="next">Switch to the next genre?</param>
        public void SwitchGenres(bool next)
        {
            m_CharacterController.gameObject.SetActive(false);
            if (m_PointClickControllerHandler != null) {
                m_PointClickControllerHandler.enabled = m_NavMeshAgent.enabled = false;
            }
            m_CurrentGenre = (Genre)(((int)m_CurrentGenre + (next ? 1 : -1)) % (int)Genre.Last);
            if ((int)m_CurrentGenre < 0) m_CurrentGenre = Genre.PointClick;
            m_GenreTitle.text = m_Title[(int)m_CurrentGenre];
            m_GenreDescription.text = m_Description[(int)m_CurrentGenre];
            SwitchCharacters();
            GenreSwitched();
        }

        /// <summary>
        /// The genre has been switched. Update the various object references.
        /// </summary>
        private void GenreSwitched()
        {
            switch (m_CurrentGenre) {
                case Genre.ThirdPerson:
                    m_CharacterController.Movement = RigidbodyCharacterController.MovementType.Combat;
                    m_CharacterController.UseRootMotion = true;
                    m_CameraController.ViewMode = CameraMonitor.CameraViewMode.ThirdPerson;
                    m_CameraController.CameraOffset = new Vector3(0.5f, 0.9f, -2f);
                    m_CameraController.MinPitchLimit = -60;
                    m_Crosshairs.SetActive(true);
                    break;
                case Genre.Platformer:
                    m_CharacterController.Movement = RigidbodyCharacterController.MovementType.Adventure;
                    m_CharacterController.UseRootMotion = false;
                    break;
                case Genre.RPG:
                    m_CharacterController.UseRootMotion = true;
                    m_CameraController.ViewMode = CameraMonitor.CameraViewMode.RPG;
                    m_CameraController.CameraOffset = new Vector3(0.5f, 0.9f, -2f);
                    m_Crosshairs.SetActive(true);
                    m_Pseudo3DObjects.SetActive(false);
                    break;
                case Genre.Pseudo3D:
                    m_CameraController.ViewMode = CameraMonitor.CameraViewMode.Pseudo3D;
                    m_CameraController.CameraOffset = new Vector3(0.5f, 2.5f, -2f);
                    m_CameraController.ViewDistance = 7;
                    m_CameraController.MinPitchLimit = -60;
                    m_Crosshairs.SetActive(false);
                    m_Pseudo3DObjects.SetActive(true);
                    break;
                case Genre.TopDown:
                    m_CharacterController.Movement = RigidbodyCharacterController.MovementType.TopDown;
                    m_CameraController.ViewMode = CameraMonitor.CameraViewMode.TopDown;
                    m_CameraController.CameraOffset = Vector3.zero;
                    m_CameraController.ViewDistance = 10;
                    m_CameraController.MinPitchLimit = 50;
                    m_Crosshairs.SetActive(false);
                    m_Pseudo3DObjects.SetActive(false);
                    break;
                case Genre.PointClick:
                    m_CharacterController.Movement = RigidbodyCharacterController.MovementType.PointClick;
                    m_CameraController.ViewMode = CameraMonitor.CameraViewMode.TopDown;
                    m_CameraController.CameraOffset = Vector3.zero;
                    m_CameraController.ViewDistance = 10;
                    m_CameraController.MinPitchLimit = 50;
                    m_Crosshairs.SetActive(false);
                    break;
            }
            for (int i = 0; i < m_ItemPickups.childCount; ++i) {
                m_ItemPickups.GetChild(i).gameObject.SetActive(true);
            }

            // Start fresh.
            m_CharacterInventory.RemoveAllItems();
            m_CharacterInventory.LoadDefaultLoadout();
            m_CharacterAnimatorMonitor.DetermineStates();
            Respawn();
        }

        /// <summary>
        /// Use a demo SpawnSelection component to respawn.
        /// </summary>
        private void Respawn()
        {
            var spawnLocation = CleanSpawnSelection.GetSpawnLocation();
            m_CharacterController.SetPosition(spawnLocation.position);
            m_CharacterController.SetRotation(spawnLocation.rotation);
            m_CharacterIK.InstantMove = true;
            if (m_CurrentGenre == Genre.ThirdPerson || m_CurrentGenre == Genre.Platformer || m_CurrentGenre == Genre.RPG) {
                m_CameraController.ImmediatePosition();
            } else if (m_CurrentGenre == Genre.TopDown || m_CurrentGenre == Genre.PointClick) {
                m_CameraController.ImmediatePosition(Quaternion.Euler(53.2f, 0, 0));
            } else {
                m_CameraController.ImmediatePosition(Quaternion.Euler(30, 0, 0));
            }
        }
    }
}