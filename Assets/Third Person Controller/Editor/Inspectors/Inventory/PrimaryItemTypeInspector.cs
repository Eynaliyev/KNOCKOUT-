using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for PrimaryItemType.
    /// </summary>
    [CustomEditor(typeof(PrimaryItemType))]
    public class PrimaryItemTypeInspector : ItemBaseTypeInspector
    {
        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var primaryItemType = target as PrimaryItemType;
            if (primaryItemType == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            var includedConsumableItem = primaryItemType.ConsumableItem;
            includedConsumableItem.ItemType = EditorGUILayout.ObjectField("Consumable Item", includedConsumableItem.ItemType, typeof(ConsumableItemType), false) as ConsumableItemType;
            if (includedConsumableItem.ItemType != null) {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                var prevInfinity = (includedConsumableItem.Capacity == int.MaxValue);
                GUI.enabled = !prevInfinity;
                includedConsumableItem.Capacity = EditorGUILayout.IntField("Capacity", primaryItemType.ConsumableItem.Capacity);
                GUI.enabled = true;
                var infinity = EditorGUILayout.ToggleLeft("Infinity", prevInfinity, GUILayout.Width(70));
                if (prevInfinity != infinity) {
                    if (infinity) {
                        includedConsumableItem.Capacity = int.MaxValue;
                    } else {
                        includedConsumableItem.Capacity = 1;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DualWieldItems"), true);

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(primaryItemType, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(primaryItemType);
            }
        }
    }
}