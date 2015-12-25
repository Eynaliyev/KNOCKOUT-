using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for ItemPickup.
    /// </summary>
    [CustomEditor(typeof(ItemPickup))]
    public class ItemPickupInspector : PickupObjectInspector
    {
        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var itemPickup = target as ItemPickup;
            if (itemPickup == null || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            var itemListProperty = PropertyFromName(serializedObject, "m_ItemList");
            if (itemListProperty.arraySize > 0) {
                ItemAmountInspector.DrawItemPickup(itemListProperty);
            } else {
                EditorGUILayout.HelpBox("Add an item type to allow the character to pickup the specified item.", MessageType.Info);
            }

            InspectorUtility.DrawAddRemoveArrayButtons(itemListProperty);

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(itemPickup, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(itemPickup);
            }
        }
    }
}