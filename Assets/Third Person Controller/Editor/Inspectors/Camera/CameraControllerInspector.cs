using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for CameraController.
    /// </summary>
    [CustomEditor(typeof(CameraController))]
    public class CameraControllerInspector : InspectorBase
    {
        // CameraController
        private static bool m_CharacterFoldout = true;
        private static bool m_LookFoldout = true;
        private static bool m_MovementFoldout = true;
        private static bool m_ZoomMovementFoldout = true;
        private static bool m_ScopeMovementFoldout = true;
        private static bool m_PhysicsFoldout = true;
        private static bool m_RecoilFoldout = true;
        private static bool m_DeathOrbitFoldout = true;

        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var cameraController = target as CameraController;
            if (cameraController == null || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ViewMode"));

            if ((m_CharacterFoldout = EditorGUILayout.Foldout(m_CharacterFoldout, "Character Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                var initCharacterOnStartProperty = PropertyFromName(serializedObject, "m_InitCharacterOnStart");
                EditorGUILayout.PropertyField(initCharacterOnStartProperty);

                if (initCharacterOnStartProperty.boolValue) {
                    var characterProperty = PropertyFromName(serializedObject, "m_Character");
                    characterProperty.objectReferenceValue = EditorGUILayout.ObjectField("Character", characterProperty.objectReferenceValue, typeof(GameObject), true, GUILayout.MinWidth(80)) as GameObject;
                    if (characterProperty.objectReferenceValue == null) {
                        EditorGUILayout.HelpBox("This field is required. The character specifies the GameObject that the CameraController should interact with.", MessageType.Error);
                    } else {
                        if ((characterProperty.objectReferenceValue as GameObject).GetComponent<Opsive.ThirdPersonController.Input.PlayerInput>() == null) {
                            EditorGUILayout.HelpBox("The Camera Controller component cannot reference an AI Agent. Ensure the Camera Controller is referencing a player controlled character.", MessageType.Error);
                        }
                    }
                }

                var autoAnchorProperty = PropertyFromName(serializedObject, "m_AutoAnchor");
                EditorGUILayout.PropertyField(autoAnchorProperty);
                if (autoAnchorProperty.boolValue) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_AutoAnchorBone"));
                } else {
                    var anchorProperty = PropertyFromName(serializedObject, "m_Anchor");
                    anchorProperty.objectReferenceValue = EditorGUILayout.ObjectField("Anchor", anchorProperty.objectReferenceValue, typeof(Transform), true, GUILayout.MinWidth(80)) as Transform;
                    if (anchorProperty.objectReferenceValue == null) {
                        EditorGUILayout.HelpBox("The anchor specifies the Transform that the camera should follow. If null it will use the Character's Transform.", MessageType.Info);
                    }
                }
                EditorGUI.indentLevel--;
            }

            if (cameraController.ViewMode != CameraMonitor.CameraViewMode.Pseudo3D && (m_LookFoldout = EditorGUILayout.Foldout(m_LookFoldout, "Look Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;

                var minPitchProperty = PropertyFromName(serializedObject, "m_MinPitchLimit");
                var maxPitchProperty = PropertyFromName(serializedObject, "m_MaxPitchLimit");
                var minValue = Mathf.Round(minPitchProperty.floatValue * 100f) / 100f;
                var maxValue = Mathf.Round(maxPitchProperty.floatValue * 100f) / 100f;
                InspectorUtility.DrawMinMaxLabeledFloatSlider("Pitch Limit", ref minValue, ref maxValue, 
                    (cameraController.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || cameraController.ViewMode == CameraMonitor.CameraViewMode.RPG) ? -90 : 0, 90);
                minPitchProperty.floatValue = minValue;
                maxPitchProperty.floatValue = maxValue;

                // Cover yaw limits are only applicable to the third person view.
                if (cameraController.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || cameraController.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                    var minYawProperty = PropertyFromName(serializedObject, "m_MinYawLimit");
                    var maxYawProperty = PropertyFromName(serializedObject, "m_MaxYawLimit");
                    minValue = Mathf.Round(minYawProperty.floatValue * 100f) / 100f;
                    maxValue = Mathf.Round(maxYawProperty.floatValue * 100f) / 100f;
                    InspectorUtility.DrawMinMaxLabeledFloatSlider("Cover Yaw Limit", ref minValue, ref maxValue, -180, 180);
                    minYawProperty.floatValue = minValue;
                    maxYawProperty.floatValue = maxValue;
                }
                EditorGUI.indentLevel--;
            }

            if ((m_MovementFoldout = EditorGUILayout.Foldout(m_MovementFoldout, "Movement Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MoveSmoothing"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_CameraOffset"), true);
                // The following properties are only applicable to the third person view.
                if (cameraController.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || cameraController.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_NormalFOV"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_TurnSmoothing"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_TurnSpeed"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_CanTurnInAir"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_StepZoomSensitivity"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MinStepZoom"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MaxStepZoom"));
                } else {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_RotationSpeed"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ViewDistance"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ViewStep"));
                    if (cameraController.ViewMode == CameraMonitor.CameraViewMode.Pseudo3D) {
                        EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_LookDirection"));
                    }
                }
                EditorGUI.indentLevel--;
            }

            // The following properties are only applicable to the third person view.
            if (cameraController.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || cameraController.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                if ((m_ZoomMovementFoldout = EditorGUILayout.Foldout(m_ZoomMovementFoldout, "Zoom Movement Options", InspectorUtility.BoldFoldout))) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_AllowZoom"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ZoomTurnSmoothing"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ZoomCameraOffset"), true);
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ZoomFOV"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_FOVSpeed"));
                    EditorGUI.indentLevel--;
                }

                if ((m_ScopeMovementFoldout = EditorGUILayout.Foldout(m_ScopeMovementFoldout, "Scope Movement Options", InspectorUtility.BoldFoldout))) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ScopeTurnSmoothing"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ScopeCameraOffset"), true);
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ScopeFOV"));
                    EditorGUI.indentLevel--;
                }
            }

            if (cameraController.ViewMode != CameraMonitor.CameraViewMode.Pseudo3D && (m_PhysicsFoldout = EditorGUILayout.Foldout(m_PhysicsFoldout, "Physics Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                if (cameraController.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || cameraController.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DisableRendererDistance"));
                }
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_CollisionRadius"));
                EditorGUI.indentLevel--;
            }

            // The following properties are only applicable to the third person view.
            if (cameraController.ViewMode == CameraMonitor.CameraViewMode.ThirdPerson || cameraController.ViewMode == CameraMonitor.CameraViewMode.RPG) {
                if ((m_RecoilFoldout = EditorGUILayout.Foldout(m_RecoilFoldout, "Recoil Options", InspectorUtility.BoldFoldout))) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_RecoilSpring"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_RecoilDampening"));
                    EditorGUI.indentLevel--;
                }

                if ((m_DeathOrbitFoldout = EditorGUILayout.Foldout(m_DeathOrbitFoldout, "Death Orbit Options", InspectorUtility.BoldFoldout))) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DeathAnchor"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_UseDeathOrbit"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DeathRotationSpeed"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DeathOrbitMoveSpeed"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DeathOrbitDistance"));
                    EditorGUI.indentLevel--;
                }
            } else {
                if (PropertyFromName(serializedObject, "m_UseDeathOrbit").boolValue) {
                    PropertyFromName(serializedObject, "m_UseDeathOrbit").boolValue = false;
                    GUI.changed = true;
                }
            }

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(cameraController, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(cameraController);
            }
        }
    }
}