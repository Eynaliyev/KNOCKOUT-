using UnityEngine;
using UnityEditor;
using System;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// A window which will launch the different builders and documentation links.
    /// </summary>
    [InitializeOnLoad]
    public class StartWindow : EditorWindow
    {
        private static string m_Version = "1.1.1";

        private GUIStyle m_TitleTextGUIStyle;
        private GUIStyle m_HeaderTextGUIStyle;
        private GUIStyle m_UpperTextGUIStyle;
        private GUIStyle m_LowerTextGUIStyle;
        private GUIStyle m_VersionTextGUIStyle;

        private Texture2D m_HeaderTexture;
        private Texture2D m_CharacterBuilderTexture;
        private Texture2D m_ItemTypeBuilderTexture;
        private Texture2D m_ItemBuilderTexture;
        private Texture2D m_SourceCodeTexture;
        private Texture2D m_InputTexture;
        private Texture2D m_SetupSceneTexture;
        private Texture2D m_BehaviorDesignerTexture;
        private Texture2D m_DocumentationTexture;
        private Texture2D m_VideosTexture;
        private Texture2D m_ForumTexture;
        private Texture2D m_ContactTexture;

        private Texture2D m_SeparatorTexture;

        private Rect m_TitleTextRect = new Rect(0, 5, 480, 30);
        private Rect m_ObjectBuildersTextRect = new Rect(5, 35, 200, 20);
        private Rect m_AssetTextRect = new Rect(5, 190, 200, 20);
        private Rect m_ResourcesTextRect = new Rect(5, 320, 200, 20);

        private Rect m_TitleSeparatorRect = new Rect(0, 31, 480, 1);
        private Rect m_ObjectBuildersSeparatorRect = new Rect(0, 184, 480, 1);
        private Rect m_AssetSeparatorRect = new Rect(0, 314, 480, 1);
        private Rect m_ResourcesSeparatorRect = new Rect(0, 436, 480, 1);

        private Rect m_HeaderTextureRect = new Rect(0, 0, 480, 30);
        private Rect m_CharacterBuilderTextureRect = new Rect(60, 55, 80, 80);
        private Rect m_ItemTypeBuilderTextureRect = new Rect(200, 55, 80, 80);
        private Rect m_ItemBuilderTextureRect = new Rect(340, 55, 80, 80);
        private Rect m_InputTextureRect = new Rect(45, 210, 60, 60);
        private Rect m_SetupSceneTextureRect = new Rect(155, 210, 60, 60);
        private Rect m_SourceCodeTextureRect = new Rect(265, 210, 60, 60);
        private Rect m_BehaviorDesignerTextureRect = new Rect(375, 210, 60, 60);
        private Rect m_DocumentationTextureRect = new Rect(45, 343, 60, 60);
        private Rect m_VideosTextureRect = new Rect(155, 343, 60, 60);
        private Rect m_ForumTextureRect = new Rect(265, 343, 60, 60);
        private Rect m_ContactTextureRect = new Rect(375, 343, 60, 60);

        private Rect m_CharacterBuilderTextRect = new Rect(30, 140, 140, 40);
        private Rect m_ItemTypeBuilderTextRect = new Rect(170, 140, 140, 40);
        private Rect m_ItemBuilderTextRect = new Rect(310, 140, 140, 40);
        private Rect m_InputTextRect = new Rect(20, 275, 110, 35);
        private Rect m_SetupSceneTextRect = new Rect(130, 275, 110, 35);
        private Rect m_SourceCodeTextRect = new Rect(240, 275, 110, 20);
        private Rect m_BehaviorDesignerTextRect = new Rect(350, 275, 110, 35);
        private Rect m_DocumentationTextRect = new Rect(20, 408, 110, 20);
        private Rect m_VideosTextRect = new Rect(130, 408, 110, 20);
        private Rect m_ForumTextRect = new Rect(240, 408, 110, 20);
        private Rect m_ContactTextRect = new Rect(350, 408, 110, 20);

        private Rect m_NoticeTextRect = new Rect(5, 440, 330, 20);
        private Rect m_VersionTextRect = new Rect(365, 440, 110, 20);

        /// <summary>
        /// Perform editor checks as soon as the scripts are done compiling.
        /// </summary>
        static StartWindow()
        {
            EditorApplication.update += EditorStartup;
        }

        [MenuItem("Tools/Third Person Controller/Start Window", false, 0)]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<StartWindow>(true, "Third Person Controller Start Window");
            window.minSize = window.maxSize = new Vector2(480, 459);
            DontDestroyOnLoad(window);
        }

        /// <summary>
        /// Show the StartWindow if it hasn't been shown before. Also ensure the editor and runtime script versions match.
        /// </summary>
        private static void EditorStartup()
        {
            if (!EditorApplication.isCompiling) {
                if (!EditorPrefs.GetBool("Opsive.ThirdPersonController.StartWindowShown", false)) {
                    EditorPrefs.SetBool("Opsive.ThirdPersonController.StartWindowShown", true);
                    ShowWindow();
                }

                // Ensure the editor version matches the runtime version.
                if (!m_Version.Equals(Constants.RuntimeVersion)) {
                    Debug.LogError("Error: The Third Person Controller editor scripts do not match the runtime scripts. Editor version: " + m_Version + ", runtime verison: " + Constants.RuntimeVersion);
                }

                EditorApplication.update -= EditorStartup;
            }
        }

        /// <summary>
        /// Show the textures and text, along with launching the clicked topic.
        /// </summary>
        private void OnGUI()
        {
            Initialize();

            // Draw the title and header text.
            GUI.DrawTexture(m_HeaderTextureRect, m_HeaderTexture);
            GUI.Label(m_TitleTextRect, "Third Person Controller", m_TitleTextGUIStyle);
            GUI.DrawTexture(m_TitleSeparatorRect, m_SeparatorTexture);
            GUI.Label(m_ObjectBuildersTextRect, "Object Builders", m_HeaderTextGUIStyle);
            GUI.DrawTexture(m_ObjectBuildersSeparatorRect, m_SeparatorTexture);
            GUI.Label(m_AssetTextRect, "Asset", m_HeaderTextGUIStyle);
            GUI.DrawTexture(m_AssetSeparatorRect, m_SeparatorTexture);
            GUI.Label(m_ResourcesTextRect, "Resources", m_HeaderTextGUIStyle);
            GUI.DrawTexture(m_ResourcesSeparatorRect, m_SeparatorTexture);

            // Draw the textures.
            GUI.DrawTexture(m_CharacterBuilderTextureRect, m_CharacterBuilderTexture);
            GUI.DrawTexture(m_ItemTypeBuilderTextureRect, m_ItemTypeBuilderTexture);
            GUI.DrawTexture(m_ItemBuilderTextureRect, m_ItemBuilderTexture);
            GUI.DrawTexture(m_InputTextureRect, m_InputTexture);
            GUI.DrawTexture(m_SetupSceneTextureRect, m_SetupSceneTexture);
            GUI.DrawTexture(m_SourceCodeTextureRect, m_SourceCodeTexture);
            GUI.DrawTexture(m_BehaviorDesignerTextureRect, m_BehaviorDesignerTexture);
            GUI.DrawTexture(m_DocumentationTextureRect, m_DocumentationTexture);
            GUI.DrawTexture(m_VideosTextureRect, m_VideosTexture);
            GUI.DrawTexture(m_ForumTextureRect, m_ForumTexture);
            GUI.DrawTexture(m_ContactTextureRect, m_ContactTexture);

            // Draw the text.
            GUI.Label(m_CharacterBuilderTextRect, "Character\nBuilder", m_UpperTextGUIStyle);
            GUI.Label(m_ItemTypeBuilderTextRect, "Item Type\nBuilder", m_UpperTextGUIStyle);
            GUI.Label(m_ItemBuilderTextRect, "Item\nBuilder", m_UpperTextGUIStyle);
            GUI.Label(m_InputTextRect, "Update\nInput Manager", m_LowerTextGUIStyle);
            GUI.Label(m_SetupSceneTextRect, "Setup Scene", m_LowerTextGUIStyle);
            GUI.Label(m_SourceCodeTextRect, "Source Code", m_LowerTextGUIStyle);
            GUI.Label(m_BehaviorDesignerTextRect, "AI\nIntegration", m_LowerTextGUIStyle);
            GUI.Label(m_DocumentationTextRect, "Documentation", m_LowerTextGUIStyle);
            GUI.Label(m_VideosTextRect, "Videos", m_LowerTextGUIStyle);
            GUI.Label(m_ForumTextRect, "Forum", m_LowerTextGUIStyle);
            GUI.Label(m_ContactTextRect, "Contact", m_LowerTextGUIStyle);

            GUI.Label(m_NoticeTextRect, "Note: This window can be accessed from the Tools menu");
            GUI.Label(m_VersionTextRect, "Version " + m_Version, m_VersionTextGUIStyle);

            // Change to the correct cursor type when the user is hovering over a link.
            EditorGUIUtility.AddCursorRect(m_CharacterBuilderTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ItemTypeBuilderTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ItemBuilderTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_InputTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_SetupSceneTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_SourceCodeTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_BehaviorDesignerTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_DocumentationTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_VideosTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ForumTextureRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ContactTextureRect, MouseCursor.Link);

            EditorGUIUtility.AddCursorRect(m_CharacterBuilderTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ItemTypeBuilderTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ItemBuilderTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_InputTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_SetupSceneTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_SourceCodeTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_BehaviorDesignerTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_DocumentationTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_VideosTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ForumTextRect, MouseCursor.Link);
            EditorGUIUtility.AddCursorRect(m_ContactTextRect, MouseCursor.Link);

            // Open the window/link on a click.
            if (Event.current.type == EventType.MouseUp) {
                var mousePosition = Event.current.mousePosition;
                if (m_CharacterBuilderTextureRect.Contains(mousePosition) || m_CharacterBuilderTextRect.Contains(mousePosition)) {
                    CharacterBuilder.ShowWindow();
                } else if (m_ItemTypeBuilderTextureRect.Contains(mousePosition) || m_ItemTypeBuilderTextRect.Contains(mousePosition)) {
                    ItemTypeBuilder.ShowWindow();
                } else if (m_ItemBuilderTextureRect.Contains(mousePosition) || m_ItemBuilderTextRect.Contains(mousePosition)) {
                    ItemBuilder.ShowWindow();
                } else if (m_InputTextureRect.Contains(mousePosition) || m_InputTextRect.Contains(mousePosition)) {
                    if (EditorUtility.DisplayDialog("Update Input Manager", "Are you sure you want to overwrite the Input Manager with the Third Person Controller inputs?", "Yes", "No")) {
                        Input.UnityInputInspector.UpdateInputManager();
                        EditorUtility.DisplayDialog("Input Manager Updated", "The Input Manager has been successfully updated.", "OK");
                    }
                } else if (m_SetupSceneTextureRect.Contains(mousePosition) || m_SetupSceneTextRect.Contains(mousePosition)) {
                    SetupScene();
                } else if (m_SourceCodeTextureRect.Contains(mousePosition) || m_SourceCodeTextRect.Contains(mousePosition)) {
                    Application.OpenURL("http://www.opsive.com/assets/ThirdPersonController/source.php");
                } else if (m_BehaviorDesignerTextureRect.Contains(mousePosition) || m_BehaviorDesignerTextRect.Contains(mousePosition)) {
                    Application.OpenURL("http://www.opsive.com/assets/BehaviorDesigner/documentation.php?id=111");
                } else if (m_DocumentationTextureRect.Contains(mousePosition) || m_DocumentationTextRect.Contains(mousePosition)) {
                    Application.OpenURL("http://www.opsive.com/assets/ThirdPersonController/documentation.php");
                } else if (m_VideosTextureRect.Contains(mousePosition) || m_VideosTextRect.Contains(mousePosition)) {
                    Application.OpenURL("http://www.opsive.com/assets/ThirdPersonController/videos.php");
                } else if (m_ForumTextureRect.Contains(mousePosition) || m_ForumTextRect.Contains(mousePosition)) {
                    Application.OpenURL("http://www.opsive.com/forum");
                } else if (m_ContactTextureRect.Contains(mousePosition) || m_ContactTextRect.Contains(mousePosition)) {
                    Application.OpenURL("http://www.opsive.com/assets/ThirdPersonController/documentation.php?id=2");
                }
            }
        }

        /// <summary>
        /// Initialize the GUI textures and styles.
        /// </summary>
        private void Initialize()
        {
            if (m_HeaderTexture == null) {
                m_HeaderTexture = Resources.Load<Texture2D>("StartWindowHeader");
                m_CharacterBuilderTexture = Resources.Load<Texture2D>("Icons/CharacterBuilder");
                m_ItemTypeBuilderTexture = Resources.Load<Texture2D>("Icons/ItemTypeBuilder");
                m_ItemBuilderTexture = Resources.Load<Texture2D>("Icons/ItemBuilder");
                m_InputTexture = Resources.Load<Texture2D>("Icons/Input");
                m_SetupSceneTexture = Resources.Load<Texture2D>("Icons/SetupScene");
                m_SourceCodeTexture = Resources.Load<Texture2D>("Icons/SourceCode");
                m_BehaviorDesignerTexture = Resources.Load<Texture2D>("Icons/BehaviorDesigner");
                m_DocumentationTexture = Resources.Load<Texture2D>("Icons/Documentation");
                m_VideosTexture = Resources.Load<Texture2D>("Icons/Videos");
                m_ForumTexture = Resources.Load<Texture2D>("Icons/Forum");
                m_ContactTexture = Resources.Load<Texture2D>("Icons/Contact");
            }

            if (m_TitleTextGUIStyle == null) {
                m_TitleTextGUIStyle = new GUIStyle(GUI.skin.label);
                m_TitleTextGUIStyle.alignment = TextAnchor.UpperCenter;
                m_TitleTextGUIStyle.fontSize = 17;
                m_TitleTextGUIStyle.fontStyle = FontStyle.Bold;
            }

            if (m_HeaderTextGUIStyle == null) {
                m_HeaderTextGUIStyle = new GUIStyle(GUI.skin.label);
                m_HeaderTextGUIStyle.alignment = TextAnchor.UpperLeft;
                m_HeaderTextGUIStyle.fontSize = 15;
                m_HeaderTextGUIStyle.fontStyle = FontStyle.Bold;
            }

            if (m_UpperTextGUIStyle == null) {
                m_UpperTextGUIStyle = new GUIStyle(GUI.skin.label);
                m_UpperTextGUIStyle.alignment = TextAnchor.UpperCenter;
                m_UpperTextGUIStyle.fontSize = 14;
                m_UpperTextGUIStyle.fontStyle = FontStyle.Bold;
            }

            if (m_LowerTextGUIStyle == null) {
                m_LowerTextGUIStyle = new GUIStyle(GUI.skin.label);
                m_LowerTextGUIStyle.alignment = TextAnchor.UpperCenter;
                m_LowerTextGUIStyle.fontSize = 12;
                m_LowerTextGUIStyle.fontStyle = FontStyle.Bold;
            }

            if (m_VersionTextGUIStyle == null) {
                m_VersionTextGUIStyle = new GUIStyle(GUI.skin.label);
                m_VersionTextGUIStyle.alignment = TextAnchor.UpperRight;
            }

            if (m_SeparatorTexture == null) {
                m_SeparatorTexture = new Texture2D(1, 1);
                m_SeparatorTexture.SetPixel(0, 0, m_HeaderTextGUIStyle.normal.textColor);
            }
        }

        /// <summary>
        /// Adds the necessary components to a new scene.
        /// </summary>
        private void SetupScene()
        {
            // Setup the camera.
            var camera = Camera.main;
            var cameraControllerAdded = false;
            if (camera != null) {
                var cameraGameObject = camera.gameObject;
                if (cameraGameObject.GetComponent<CameraController>() == null) {
                    cameraGameObject.AddComponent<Opsive.ThirdPersonController.Wrappers.CameraController>();
                    cameraControllerAdded = true;
                }
            }

            // Create the "Game" components if it doesn't already exists.
            GameObject gameGameObject;
            if (GameObject.FindObjectOfType<Scheduler>() == null) {
                gameGameObject = new GameObject("Game");
            } else {
                gameGameObject = GameObject.FindObjectOfType<Scheduler>().gameObject;
            }

            AddComponent(gameGameObject, typeof(Opsive.ThirdPersonController.Scheduler), typeof(Opsive.ThirdPersonController.Wrappers.Scheduler));
            AddComponent(gameGameObject, typeof(Opsive.ThirdPersonController.ObjectPool), typeof(Opsive.ThirdPersonController.Wrappers.ObjectPool));
            AddComponent(gameGameObject, typeof(Opsive.ThirdPersonController.EventHandler), typeof(Opsive.ThirdPersonController.Wrappers.EventHandler));
            AddComponent(gameGameObject, typeof(Opsive.ThirdPersonController.DecalManager), typeof(Opsive.ThirdPersonController.Wrappers.DecalManager));
            AddComponent(gameGameObject, typeof(Opsive.ThirdPersonController.LayerManager), typeof(Opsive.ThirdPersonController.Wrappers.LayerManager));
            AddComponent(gameGameObject, typeof(Opsive.ThirdPersonController.ObjectManager), typeof(Opsive.ThirdPersonController.Wrappers.ObjectManager));

            if (cameraControllerAdded) {
                EditorUtility.DisplayDialog("Scene Setup Complete", "The necessary components have been added to your scene. " +
                                                           "Please assign your character to the CameraController component attached to the camera.", "OK");
            } else {
                EditorUtility.DisplayDialog("Scene Setup Complete", "The necessary components have been added to your scene.", "OK");
            }
        }

        /// <summary>
        /// Adds the wrapper component to the specified GameObject if the base component doesn't exist on the GameObject.
        /// </summary>
        /// <param name="gameGameObject">The GameObject to add the wrapper component to.</param>
        /// <param name="component">The base component to check against.</param>
        /// <param name="wrapperComponent">The wrapper component to add.</param>
        private void AddComponent(GameObject gameGameObject, Type component, Type wrapperComponent)
        {
            if (gameGameObject.GetComponent(component) == null) {
                gameGameObject.AddComponent(wrapperComponent);
            }
        }
    }
}