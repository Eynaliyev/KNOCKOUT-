using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for Inventory.
    /// </summary>
    [CustomEditor(typeof(Inventory))]
    public class InventoryInspector : InspectorBase
    {
        // Inventory
        [SerializeField] private static bool m_DefaultLoadoutFoldout = true;

        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var inventory = target as Inventory;
            if (inventory == null || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_UnlimitedAmmo"));
            EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_UnequippedItemType"));

            var dropItemsOnDeath = PropertyFromName(serializedObject, "m_DropItems");
            EditorGUILayout.PropertyField(dropItemsOnDeath);
            if (dropItemsOnDeath.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_DroppedItemsParent"));
                EditorGUI.indentLevel--;
            }

            if ((m_DefaultLoadoutFoldout = EditorGUILayout.Foldout(m_DefaultLoadoutFoldout, "Default Loadout", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                var defaultLoadoutProperty = PropertyFromName(serializedObject, "m_DefaultLoadout");
                if (defaultLoadoutProperty.arraySize > 0) {
                    ItemAmountInspector.DrawItemPickup(defaultLoadoutProperty);
                } else {
                    EditorGUILayout.HelpBox("Add a default loadout item to allow the character to spawn with the specified item.", MessageType.Info);
                }

                InspectorUtility.DrawAddRemoveArrayButtons(defaultLoadoutProperty);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(inventory, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(inventory);
            }
        }
    }
}