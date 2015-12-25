using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for Health.
    /// </summary>
    [CustomEditor(typeof(Health))]
    public class HealthInspector : InspectorBase
    {
        private static bool m_ShieldFoldout = true;
        private static bool m_DeathFoldout = true;

        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var health = target as Health;
            if (health == null || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_Invincible"));
            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MaxHealth"));
            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_TimeInvincibleAfterSpawn"));
            if ((m_ShieldFoldout = EditorGUILayout.Foldout(m_ShieldFoldout, "Shield Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MaxShield"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ShieldRegenerativeInitialWait"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ShieldRegenerativeAmount"));
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ShieldRegenerativeWait"));
                EditorGUI.indentLevel--;
            }

            if ((m_DeathFoldout = EditorGUILayout.Foldout(m_DeathFoldout, "Death Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_SpawnedObjectsOnDeath"), true);
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DestroyedObjectsOnDeath"), true);
                var deactivateOnDeath = PropertyFromName(serializedObject, "m_DeactivateOnDeath");
                EditorGUILayout.PropertyField(deactivateOnDeath);
                if (deactivateOnDeath.boolValue) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DeactivateOnDeathDelay"));
                }
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(health, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(health);
            }
        }
    }
}