using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
#if !(UNITY_4_6 || UNITY_5_0)
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// A wizard that will build a new Third Person Controller.
    /// </summary>
    public class CharacterBuilder : EditorWindow
    {
        // Window properties
        private Vector2 m_ScrollPosition;
        private enum Sections { Intro, Components }
        private Sections m_CurrentSection = Sections.Intro;
        private GUIStyle m_HeaderLabelStyle;

        // Intro
        private GameObject m_Character;
        private bool m_IsHumanoid;
        private bool m_AIAgent;
#if !(UNITY_4_6 || UNITY_5_0)
        private bool m_IsNetworked;
#endif

        // RigidbodyCharacterController
        private RigidbodyCharacterController.MovementType m_MovementType = RigidbodyCharacterController.MovementType.Combat;

        // IK/Ragdoll
        private bool m_AddIK = true;
        private bool m_AddRagdoll = true;

        [MenuItem("Tools/Third Person Controller/Character Builder", false, 11)]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<CharacterBuilder>(true, "Character Builder");
            window.minSize = new Vector2(520, 300);
            DontDestroyOnLoad(window);
        }

        /// <summary>
        /// Initializes the GUIStyle used by the header.
        /// </summary>
        private void OnEnable()
        {
            if (m_HeaderLabelStyle == null) {
                m_HeaderLabelStyle = new GUIStyle(EditorStyles.label);
                m_HeaderLabelStyle.wordWrap = true;
            }
        }

        /// <summary>
        /// Shows the Character Builder.
        /// </summary>
        private void OnGUI()
        {
            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);

            ShowHeaderGUI();

            var canContinue = true;
            switch (m_CurrentSection) {
                case Sections.Intro:
                    canContinue = ShowIntroGUI();
                    break;
                case Sections.Components:
                    canContinue = ShowComponentsGUI();
                    break;
            }

            GUILayout.EndScrollView();

            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUI.enabled = m_CurrentSection != Sections.Intro;
            if (GUILayout.Button("Previous")) {
                m_CurrentSection--;
            }
            GUI.enabled = canContinue;
            if (m_CurrentSection == Sections.Components) {
                if (GUILayout.Button("Build")) {
                    BuildCharacter();
                }
            } else {
                if (GUILayout.Button("Next")) {
                    m_CurrentSection++;
                }
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Shows the current section's header.
        /// </summary>
        private void ShowHeaderGUI()
        {
            var title = "";
            var description = "";
            switch (m_CurrentSection) {
                case Sections.Intro:
                    title = "Base";
                    description = "This wizard will assist in the creation of a new character. The values used within this wizard can be changed after the character has been created.";
                    break;
                case Sections.Components:
                    title = "Components";
                    description = "Specify the type of movement and add any optional components.";
                    break;
            }

            GUILayout.Label(string.Format("{0} ({1}/{2})", title, (int)(m_CurrentSection + 1), (int)(Sections.Components + 1)), "BoldLabel");
            EditorGUILayout.LabelField(description, m_HeaderLabelStyle);
            GUILayout.Space(5);
        }

        /// <summary>
        /// Shows the intro options. These options are major optiosn that don't fit in any other section.
        /// </summary>
        private bool ShowIntroGUI()
        {
            var canContinue = true;
            m_Character = EditorGUILayout.ObjectField("Character", m_Character, typeof(GameObject), true) as GameObject;
            if (m_Character == null) {
                EditorGUILayout.HelpBox("Select the GameObject which will be used as the character. This object will have the majority of the components added to it.",
                                    MessageType.Error);
                canContinue = false;
            } else if (PrefabUtility.GetPrefabType(m_Character) == PrefabType.Prefab) {
                EditorGUILayout.HelpBox("Please drag your character into the scene. The Character Builder cannot add components to prefabs.",
                                    MessageType.Error);
                canContinue = false;
            }

            // Ensure the character is a humanoid.
            if (GUI.changed) {
                if (m_Character != null) {
                    var character = m_Character;
                    var spawnedCharacter = false;
                    // The character has to be spawned in order to be able to detect if it is a Humanoid.
                    if (AssetDatabase.GetAssetPath(m_Character).Length > 0) {
                        character = GameObject.Instantiate(character) as GameObject;
                        spawnedCharacter = true;
                    }
                    var animator = character.GetComponent<Animator>();
                    var hasAnimator = animator != null;
                    if (!hasAnimator) {
                        animator = character.AddComponent<Animator>();
                    }
                    // A human will have a head.
                    m_IsHumanoid = animator.GetBoneTransform(HumanBodyBones.Head) != null;
                    // Clean up.
                    if (!hasAnimator) {
                        UnityEngine.Object.DestroyImmediate(animator, true);
                    }
                    if (spawnedCharacter) {
                        UnityEngine.Object.DestroyImmediate(character, true);
                    }
                }
            }

            if (m_Character != null && !m_IsHumanoid) {
                EditorGUILayout.HelpBox(m_Character.name + " is not a humanoid. Please select the Humanoid Animation Type within the Rig Import Settings. " + 
                                                           "In addition, ensure all of the bones are configured correctly.", MessageType.Error);
                canContinue = false;
            }

            m_AIAgent = EditorGUILayout.Toggle("Is AI Agent", m_AIAgent);
            EditorGUILayout.HelpBox("Is this character going to be used for AI? Some components (such as PlayerInput) do not need to be added if the character is an AI agent.", 
                                MessageType.Info);

#if !(UNITY_4_6 || UNITY_5_0)
            m_IsNetworked = EditorGUILayout.Toggle("Is Networked", m_IsNetworked);
            EditorGUILayout.HelpBox("Is this character going to be used on the network with Unity 5's multiplayer implementation?",
                MessageType.Info);
#endif

            return canContinue;
        }

        /// <summary>
        /// Shows the character components options.
        /// </summary>
        private bool ShowComponentsGUI()
        {
            m_MovementType = (RigidbodyCharacterController.MovementType)EditorGUILayout.EnumPopup("Movement Type", m_MovementType);
            EditorGUILayout.HelpBox("Combat movement allows the character to move backwards and strafe. If the character has a camera following it then the character will always be " +
                                    "facing in the same direction as the camera. Adventure movement always moves the character in the direction they are facing and " +
                                    "the camera can be facing any direction. Top down movement moves rotates the character in the direction of the mouse and moves " +
                                    "relative to the camera. RPG is a blend between Combat and Adventure movement types. Psuedo3D is used for 2.5D games. " +
                                    "Point and Click moves the character according to the point clicked. The PointClickControllerHandler is required.", MessageType.Info);
            m_AddIK = EditorGUILayout.Toggle("Add IK", m_AddIK);
            m_AddRagdoll = EditorGUILayout.Toggle("Add Ragdoll", m_AddRagdoll);
            if (m_AddRagdoll) {
                EditorGUILayout.HelpBox("Unity's Ragdoll Builder will open when this wizard is complete.", MessageType.Info);
            }

            return true;
        }

        /// <summary>
        /// Builds the character.
        /// </summary>
        private void BuildCharacter()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m_Character))) {
                var name = m_Character.name;
                m_Character = GameObject.Instantiate(m_Character) as GameObject;
                m_Character.name = name;
            }
            // Call a runtime component to build the character so the character can be built at runtime.
            var isNetworked = false;
#if !(UNITY_4_6 || UNITY_5_0)
            isNetworked = m_IsNetworked;
#endif
            ThirdPersonController.CharacterBuilder.BuildCharacter(m_Character, m_AIAgent, isNetworked, m_MovementType, m_AddIK);
            if (isNetworked) {
#if !ENABLE_MULTIPLAYER
                // The character is networked so enable the multiplayer symbol.
                RigidbodyCharacterControllerInspector.ToggleMultiplayerSymbol();
#endif
            } else {
#if ENABLE_MULTIPLAYER
                // The character isn't networked so disable the multiplayer symbol.
                RigidbodyCharacterControllerInspector.ToggleMultiplayerSymbol();
#endif
            }
            Selection.activeGameObject = m_Character;

            // Open up the ragdoll builder. This class is internal to the Unity editor so reflection must be used to access it.
            if (m_AddRagdoll) {
                AddRagdoll();
            }

            Close();
        }

        /// <summary>
        /// Opens Unity's Ragdoll Builder and populates as many fields as it can.
        /// </summary>
        private void AddRagdoll()
        {
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterRagdoll>();

            var ragdollBuilderType = Type.GetType("UnityEditor.RagdollBuilder, UnityEditor");
            var windows = Resources.FindObjectsOfTypeAll(ragdollBuilderType);
            // Open the Ragdoll Builder if it isn't already opened.
            if (windows == null || windows.Length == 0) {
                EditorApplication.ExecuteMenuItem("GameObject/3D Object/Ragdoll...");
                windows = Resources.FindObjectsOfTypeAll(ragdollBuilderType);
            }

            if (windows != null && windows.Length > 0) {
                var ragdollWindow = windows[0] as ScriptableWizard;
                var animator = m_Character.GetComponent<Animator>();
#if UNITY_4_6
                SetFieldValue(ragdollWindow, "root", animator.GetBoneTransform(HumanBodyBones.Hips));
#else
                SetFieldValue(ragdollWindow, "pelvis", animator.GetBoneTransform(HumanBodyBones.Hips));
#endif
                SetFieldValue(ragdollWindow, "leftHips", animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg));
                SetFieldValue(ragdollWindow, "leftKnee", animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg));
                SetFieldValue(ragdollWindow, "leftFoot", animator.GetBoneTransform(HumanBodyBones.LeftFoot));
                SetFieldValue(ragdollWindow, "rightHips", animator.GetBoneTransform(HumanBodyBones.RightUpperLeg));
                SetFieldValue(ragdollWindow, "rightKnee", animator.GetBoneTransform(HumanBodyBones.RightLowerLeg));
                SetFieldValue(ragdollWindow, "rightFoot", animator.GetBoneTransform(HumanBodyBones.RightFoot));
                SetFieldValue(ragdollWindow, "leftArm", animator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
                SetFieldValue(ragdollWindow, "leftElbow", animator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
                SetFieldValue(ragdollWindow, "rightArm", animator.GetBoneTransform(HumanBodyBones.RightUpperArm));
                SetFieldValue(ragdollWindow, "rightElbow", animator.GetBoneTransform(HumanBodyBones.RightLowerArm));
                SetFieldValue(ragdollWindow, "middleSpine", animator.GetBoneTransform(HumanBodyBones.Spine));
                SetFieldValue(ragdollWindow, "head", animator.GetBoneTransform(HumanBodyBones.Head));

                var method = ragdollWindow.GetType().GetMethod("CheckConsistency", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null) {
                    ragdollWindow.errorString = (string)method.Invoke(ragdollWindow, null);
                    ragdollWindow.isValid = string.IsNullOrEmpty(ragdollWindow.errorString);
                }
            }
        }

        /// <summary>
        /// Use reflection to set the value of the field.
        /// </summary>
        private void SetFieldValue(ScriptableWizard obj, string name, object value)
        {
            if (value == null) {
                return;
            }

            var field = obj.GetType().GetField(name);
            if (field != null) {
                field.SetValue(obj, value);
            }
        }
    }
}