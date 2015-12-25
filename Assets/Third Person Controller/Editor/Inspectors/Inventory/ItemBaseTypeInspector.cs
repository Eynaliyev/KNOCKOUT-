using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Shows a custom inspector for ItemBaseType.
    /// </summary>
    [CustomEditor(typeof(ItemBaseType))]
    public class ItemBaseTypeInspector : InspectorBase
    {
        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (target == null || target.Equals(null) || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show the ID field.
            serializedObject.Update();

            // The ID field cannot be edited and is shown for information purposes only. If the ID is -1 then assign a new random id.
            GUI.enabled = false;
            var id = PropertyFromName(serializedObject, "m_ID");
            if (id.intValue == -1) {
                Random.seed = System.Environment.TickCount;
                (target as ItemBaseType).ID = Random.Range(0, int.MaxValue);
                EditorUtility.SetDirty(target);
            }
            EditorGUILayout.PropertyField(id);
            GUI.enabled = true;
        }
    }
}