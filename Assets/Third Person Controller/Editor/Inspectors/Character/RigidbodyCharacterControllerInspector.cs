using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Reflection;
using Opsive.ThirdPersonController.Abilities;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for RigidbodyCharacterController.
    /// </summary>
    [CustomEditor(typeof(RigidbodyCharacterController))]
    public class RigidbodyCharacterControllerInspector : InspectorBase
    {
        private static string s_MultiplayerSymbol = "ENABLE_MULTIPLAYER";

        // RigidbodyCharacterController
        [SerializeField] private static bool m_MovementFoldout = true;
        [SerializeField] private static bool m_RestrictionsFoldout = true;
        [SerializeField] private static bool m_ItemFoldout = true;
        [SerializeField] private static bool m_PhysicsFoldout = true;
        [SerializeField] private static bool m_StickinessFoldout = true;
        [SerializeField] private static bool m_AbilityFoldout = true;
        private Dictionary<int, SerializedObject> m_SerializedObjectMap = new Dictionary<int, SerializedObject>();
        private static List<Type> m_AbilityTypes = new List<Type>();
        private Dictionary<Type, bool> m_AbilityIsUniqueMap = new Dictionary<Type, bool>();
        private Texture2D m_UpArrow;
        private Texture2D m_DownArrow;
        private GUIStyle m_UpArrowStyle;
        private GUIStyle m_DownArrowStyle;

        /// <summary>
        /// Initializes the InspectorUtility and searches for ability types.
        /// </summary>
        private void OnEnable()
        {
            // Search through all of the assemblies to find any types that derive from Ability.
            m_AbilityTypes.Clear();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; ++i) {
                var assemblyTypes = assemblies[i].GetTypes();
                for (int j = 0; j < assemblyTypes.Length; ++j) {
                    // Ignore the Third Person Controller base classes.
                    if (typeof(Ability).IsAssignableFrom(assemblyTypes[j]) && (assemblyTypes[j].Namespace == null || !assemblyTypes[j].Namespace.Equals("Opsive.ThirdPersonController.Abilities"))) {
                        m_AbilityTypes.Add(assemblyTypes[j]);
                    }
                }
            }
            m_UpArrow = Resources.Load<Texture2D>(string.Format("Icons/{0}UpArrow", (EditorGUIUtility.isProSkin ? "Dark" : "Light")));
            m_DownArrow = Resources.Load<Texture2D>(string.Format("Icons/{0}DownArrow", (EditorGUIUtility.isProSkin ? "Dark" : "Light")));
        }

        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var controller = target as RigidbodyCharacterController;
            if (controller == null || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            if (m_UpArrowStyle == null) {
                m_UpArrowStyle = new GUIStyle(EditorStyles.miniButtonRight);
                var padding = m_UpArrowStyle.padding;
                padding.top = 5;
                m_UpArrowStyle.padding = padding;

                m_DownArrowStyle = new GUIStyle(EditorStyles.miniButtonLeft);
                padding = m_DownArrowStyle.padding;
                padding.top = 5;
                m_DownArrowStyle.padding = padding;
            }

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            // Ensure the correct multiplayer symbol is defined.
#if !(UNITY_4_6 || UNITY_5_0)
            var hasNetworkIdentity = (target as MonoBehaviour).GetComponent<UnityEngine.Networking.NetworkIdentity>() != null;
            var showNetworkToggle = false;
            var removeSymbol = false;
#if ENABLE_MULTIPLAYER
            if (!hasNetworkIdentity) {
                EditorGUILayout.HelpBox("ENABLE_MULTIPLAYER is defined but no NetworkIdentity can be found. This symbol needs to be removed.", MessageType.Error);
                showNetworkToggle = true;
                removeSymbol = true;
            }
#else
            if (hasNetworkIdentity) {
                EditorGUILayout.HelpBox("A NetworkIdentity was found but ENABLE_MULTIPLAYER is not defined. This symbol needs to be defined.", MessageType.Error);
                showNetworkToggle = true;
            }
#endif
            if (!EditorApplication.isCompiling && showNetworkToggle && GUILayout.Button((removeSymbol ? "Remove" : "Add") + " Multiplayer Symbol")) {
                ToggleMultiplayerSymbol();
            }
#endif

            var movementType = PropertyFromName(serializedObject, "m_MovementType");
            EditorGUILayout.PropertyField(movementType);

            if ((m_MovementFoldout = EditorGUILayout.Foldout(m_MovementFoldout, "Movement Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                var useRootMotion = PropertyFromName(serializedObject, "m_UseRootMotion");
                EditorGUILayout.PropertyField(useRootMotion);
                if (!useRootMotion.boolValue) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_GroundSpeed"));
                } else {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_RootMotionSpeedMultiplier"));
                }
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_GroundDampening"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_AirSpeed"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_AirDampening"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_RotationSpeed"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_FastRotationSpeed"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MaxStepHeight"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_StepOffset"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_StepSpeed"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_SlopeLimit"));
                EditorGUI.indentLevel--;
            }

            if ((m_PhysicsFoldout = EditorGUILayout.Foldout(m_PhysicsFoldout, "Physics Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_SkinWidth"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_GroundedFrictionMaterial"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_StepFrictionMaterial"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_SlopeFrictionMaterial"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_AirFrictionMaterial"));
                EditorGUI.indentLevel--;
            }

            if ((m_StickinessFoldout = EditorGUILayout.Foldout(m_StickinessFoldout, "Stickiness Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_GroundStickiness"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_SkinMovingPlatformStickiness"));
                EditorGUI.indentLevel--;
            }

            if ((m_RestrictionsFoldout = EditorGUILayout.Foldout(m_RestrictionsFoldout, "Constraint Options", InspectorUtility.BoldFoldout))) {
                var movementRestriction = PropertyFromName(serializedObject, "m_MovementConstraint");
                EditorGUILayout.PropertyField(movementRestriction);
                if ((RigidbodyCharacterController.MovementConstraint)movementRestriction.enumValueIndex == RigidbodyCharacterController.MovementConstraint.RestrictX ||
                    (RigidbodyCharacterController.MovementConstraint)movementRestriction.enumValueIndex == RigidbodyCharacterController.MovementConstraint.RestrictXZ) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MinXPosition"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MaxXPosition"));
                }
                if ((RigidbodyCharacterController.MovementConstraint)movementRestriction.enumValueIndex == RigidbodyCharacterController.MovementConstraint.RestrictZ ||
                    (RigidbodyCharacterController.MovementConstraint)movementRestriction.enumValueIndex == RigidbodyCharacterController.MovementConstraint.RestrictXZ) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MinZPosition"));
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MaxZPosition"));
                }
            }

            if ((m_ItemFoldout = EditorGUILayout.Foldout(m_ItemFoldout, "Item Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                // Explicitly update always aim so the property updates the Animator Monitor.
                var alwaysAim = EditorGUILayout.Toggle("Always Aim", controller.AlwaysAim);
                if (controller.AlwaysAim != alwaysAim) {
                    controller.AlwaysAim = alwaysAim;
                }
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ItemForciblyUseDuration"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DualWieldItemForciblyUseDuration"));
                if (movementType.enumValueIndex != (int)RigidbodyCharacterController.MovementType.Combat) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ItemUseRotationThreshold"));
                }
                EditorGUI.indentLevel--;
            }

            if ((m_AbilityFoldout = EditorGUILayout.Foldout(m_AbilityFoldout, "Abilities", InspectorUtility.BoldFoldout))) {
                var abilities = PropertyFromName(serializedObject, "m_Abilities");
                var abilitySet = new HashSet<Type>();
                // Draw the active abilities first.
                for (int i = 0; i < abilities.arraySize; ++i) {
                    if (abilities.GetArrayElementAtIndex(i).objectReferenceValue == null) {
                        continue;
                    }
                    var ability = abilities.GetArrayElementAtIndex(i).objectReferenceValue as Ability;
                    var type = ability.GetType();
                    OnAbilityDraw(controller, type, type.Name, ability, abilities, i);
                    abilitySet.Add(type);
                }
                // Then the non-active abilities.
                for (int i = 0; i < m_AbilityTypes.Count; ++i) {
                    if (!abilitySet.Contains(m_AbilityTypes[i]) || !IsUniqueAbility(m_AbilityTypes[i])) {
                        OnAbilityDraw(controller, m_AbilityTypes[i], m_AbilityTypes[i].Name, null, abilities, -1);
                    }
                }
            }

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(controller, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }
        }

        /// <summary>
        /// Abilities are unique if only one of that ability type can be added.
        /// </summary>
        /// <param name="abilityType">The type of ability.</param>
        /// <returns>Is the ability unique?</returns>
        private bool IsUniqueAbility(Type abilityType)
        {
            bool isUnique;
            if (m_AbilityIsUniqueMap.TryGetValue(abilityType, out isUnique)) {
                return isUnique;
            }

            // Non-unique abilities will have the IsUniqueAbility method. Search through the base classes to determine if this method exists.
            // If the method does not exist then the ability is unique.
            isUnique = true;
            var type = abilityType;
            while (!type.Equals(typeof(object))) {
                var isUniqueMethod = type.GetMethod("IsUniqueAbility", BindingFlags.Public | BindingFlags.Static);
                if (isUniqueMethod != null) {
                    isUnique = (bool)isUniqueMethod.Invoke(null, null);
                    m_AbilityIsUniqueMap.Add(abilityType, isUnique);
                    return isUnique;
                }
                type = type.BaseType;
            }
            m_AbilityIsUniqueMap.Add(abilityType, isUnique);
            return isUnique;
        }

        /// <summary>
        /// Draw the ability inspector. If the ability is added or removed this method will add or remove the corresponding ability component.
        /// </summary>
        /// <param name="controller">The parent controller component.</param>
        /// <param name="label">The name of the ability.</param>
        /// <param name="ability">The instance of the ability (can be null).</param>
        /// <param name="abilities">The SerializedProperty array that contains all of the active abilities.</param>
        private void OnAbilityDraw(RigidbodyCharacterController controller, Type type, string label, Ability ability, SerializedProperty abilities, int index)
        {
            var prevHasAbility = ability != null;
            EditorGUILayout.BeginHorizontal();
            // Indicate if the ability is active within the inspector.
            if (prevHasAbility && ability.IsActive) {
                label += " (Active)";
            }
            // Show a toggle to enable or disable the ability.
            var hasAbility = EditorGUILayout.Toggle(label, prevHasAbility);
            EditorGUILayout.EndHorizontal();
            if (prevHasAbility != hasAbility) {
                if (hasAbility) {
                    ability = Undo.AddComponent(controller.gameObject, type) as Ability;
                    ability.Index = abilities.arraySize;
                    ability.hideFlags = HideFlags.HideInInspector;
                    abilities.InsertArrayElementAtIndex(abilities.arraySize);
                    abilities.GetArrayElementAtIndex(abilities.arraySize - 1).objectReferenceValue = ability;
                } else {
                    // The reference value must be null in order for the element to be removed from the SerializedProperty array.
                    abilities.GetArrayElementAtIndex(index).objectReferenceValue = null;
                    abilities.DeleteArrayElementAtIndex(index);
                    Undo.DestroyObjectImmediate(ability);
                }
                // Return early - the view will be updated in the next repaint.
                return;
            }

            if (hasAbility) {
                if (ability.hideFlags != HideFlags.HideInInspector) {
                    ability.hideFlags = HideFlags.HideInInspector;
                } 
                SerializedObject abilitySerializedObject;
                if (!m_SerializedObjectMap.TryGetValue(ability.GetInstanceID(), out abilitySerializedObject) || abilitySerializedObject.targetObject == null) {
                    abilitySerializedObject = new SerializedObject(ability);
                    m_SerializedObjectMap.Remove(ability.GetInstanceID());
                    m_SerializedObjectMap.Add(ability.GetInstanceID(), abilitySerializedObject);
                }
                abilitySerializedObject.Update();
                EditorGUI.BeginChangeCheck();
                EditorGUI.indentLevel++;
                var property = abilitySerializedObject.GetIterator();
                property.NextVisible(true);
                do {
                    EditorGUILayout.PropertyField(property);
                } while (property.NextVisible(false));
                EditorGUI.indentLevel--;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.enabled = index < abilities.arraySize - 1;
                if (GUILayout.Button(m_DownArrow, m_DownArrowStyle, GUILayout.Width(20))) {
                    abilities.MoveArrayElement(index, index + 1);
                }
                GUI.enabled = index > 0;
                if (GUILayout.Button(m_UpArrow, m_UpArrowStyle, GUILayout.Width(20))) {
                    abilities.MoveArrayElement(index, index - 1);
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck()) {
                    // The ability order may have changed. Ensure the indexes are up to date.
                    for (int i = abilities.arraySize - 1; i > -1; --i) {
                        if ((abilities.GetArrayElementAtIndex(i).objectReferenceValue as Ability) == null) {
                            abilities.DeleteArrayElementAtIndex(i);
                            continue;
                        }
                        (abilities.GetArrayElementAtIndex(i).objectReferenceValue as Ability).Index = i;
                    }
                    Undo.RecordObject(ability, "Inspector");
                    abilitySerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(ability);
                }
            }
        }

        /// <summary>
        /// Toggles the platform dependent multiplayer compiler symbole.
        /// </summary>
        public static void ToggleMultiplayerSymbol()
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (symbols.Contains(s_MultiplayerSymbol + ";")) {
                symbols = symbols.Replace(s_MultiplayerSymbol + ";", "");
            } else if (symbols.Contains(s_MultiplayerSymbol)) {
                symbols = symbols.Replace(s_MultiplayerSymbol, "");
            } else {
                symbols += (s_MultiplayerSymbol + ";");
            }
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
        }
    }
}