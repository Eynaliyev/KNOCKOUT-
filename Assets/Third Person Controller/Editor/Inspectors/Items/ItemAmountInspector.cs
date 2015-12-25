using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for Inventory.ItemAmount.
    /// </summary>
    public static class ItemAmountInspector
    {
        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        /// <param name="itemProperty">The ItemAmount property to draw.</param>
        public static void DrawItemPickup(SerializedProperty itemProperty)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Type", GUILayout.MinWidth(80));
            EditorGUILayout.LabelField("Amount", GUILayout.Width(70));
            EditorGUILayout.LabelField("Infinity", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < itemProperty.arraySize; ++i) {
                var loadout = itemProperty.GetArrayElementAtIndex(i);
                var itemType = loadout.FindPropertyRelative("m_ItemType");
                var amount = loadout.FindPropertyRelative("m_Amount");
                EditorGUILayout.BeginHorizontal();
                itemType.objectReferenceValue = EditorGUILayout.ObjectField(itemType.objectReferenceValue, typeof(ItemBaseType), true, GUILayout.MinWidth(80)) as ItemBaseType;
                // DualWieldItemTypes cannot be directly picked up.
                if (itemType.objectReferenceValue is DualWieldItemType) {
                    itemType.objectReferenceValue = null;
                }
                var prevInfinity = (amount.intValue == int.MaxValue);
                GUI.enabled = !prevInfinity;
                if (itemType.objectReferenceValue is PrimaryItemType) {
                    amount.intValue = Mathf.Min(EditorGUILayout.IntField(Mathf.Max(amount.intValue, 1), GUILayout.Width(70)), 2);
                } else {
                    amount.intValue = EditorGUILayout.IntField(amount.intValue, GUILayout.Width(70));
                }

                GUI.enabled = !(itemType.objectReferenceValue is PrimaryItemType);
                var infinity = EditorGUILayout.Toggle(prevInfinity, GUILayout.Width(70));
                if (prevInfinity != infinity) {
                    if (infinity) {
                        amount.intValue = int.MaxValue;
                    } else {
                        amount.intValue = 1;
                    }
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
                if (itemType.objectReferenceValue == null) {
                    EditorGUILayout.HelpBox("An item type must be specified.", MessageType.Error);
                }
            }
        }
    }
}