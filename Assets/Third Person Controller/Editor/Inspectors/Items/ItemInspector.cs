using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// Base class for a custom Item inspector.
    /// </summary>
    public abstract class ItemInspector : InspectorBase
    {
        // Item
        private GameObject m_AssignTo;
        private enum HandAssignment { Left, Right }
        private HandAssignment m_HandAssignment = HandAssignment.Right;

        [SerializeField] private static bool m_CharacterAnimatorFoldout = true;
        [SerializeField] private static bool m_UIFoldout = true;
        [SerializeField] private static bool m_IKFoldout = true;
        [SerializeField] private static bool m_CharacterDeathFoldout = true;

        /// <summary>
        /// Draws the custom inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            var item = target as MonoBehaviour;
            if (target == null || target.Equals(null) || serializedObject == null)
                return; // How'd this happen?

            base.OnInspectorGUI();

            // Show all of the fields.
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            // Allow the user to assign the item if it isn't already assigned
            if (item.transform.parent == null) {
                m_AssignTo = EditorGUILayout.ObjectField("Assign To", m_AssignTo, typeof(GameObject), true) as GameObject;
                if (m_AssignTo != null) {
                    m_HandAssignment = (HandAssignment)EditorGUILayout.EnumPopup("Hand", m_HandAssignment);
                }

                GUI.enabled = m_AssignTo != null;
                if (GUILayout.Button("Assign")) {
                    AssignItem(item.gameObject);
                }
                GUI.enabled = true;
            }

            var itemType = PropertyFromName(serializedObject, "m_ItemType");
            EditorGUILayout.PropertyField(itemType);
            if (itemType.objectReferenceValue == null) {
                EditorGUILayout.HelpBox("This field is required. The Inventory uses the Item Type to determine the type of weapon.", MessageType.Error);
            }

            var itemName = PropertyFromName(serializedObject, "m_ItemName");
            EditorGUILayout.PropertyField(itemName);
            if (string.IsNullOrEmpty(itemName.stringValue)) {
                EditorGUILayout.HelpBox("The Item Name specifies the name of the Animator substate machine. It should not be empty unless you only have one item type.", MessageType.Warning);
            }

            if ((m_CharacterAnimatorFoldout = EditorGUILayout.Foldout(m_CharacterAnimatorFoldout, "Character Animator Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_IdleState"), true);
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_MovementState"), true);
                if (target is IUsableItem) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_AimStates"), true);
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_UseStates"), true);
                }
                if (target is IReloadableItem) {
                    EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ReloadState"), true);
                }
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_EquipState"), true);
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_UnequipState"), true);
                EditorGUI.indentLevel--;
            }

            if ((m_UIFoldout = EditorGUILayout.Foldout(m_UIFoldout, "UI Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ItemSprite"), true);
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_RightItemSprite"), true);
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_CrosshairsSprite"), true);
                EditorGUI.indentLevel--;
            }

            if ((m_IKFoldout = EditorGUILayout.Foldout(m_IKFoldout, "IK Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_TwoHandedItem"), true);
                EditorGUI.indentLevel--;
            }

            if ((m_CharacterDeathFoldout = EditorGUILayout.Foldout(m_CharacterDeathFoldout, "Character Death Options", InspectorUtility.BoldFoldout))) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(PropertyFromName(serializedObject, "m_ItemPickup"), true);
                EditorGUI.indentLevel--;
            }

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(item, "Inspector");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(item);
            }
        }

        /// <summary>
        /// Assigns the item as a child to the specified hand Transform.
        /// </summary>
        /// <param name="itemGameObject">The Item GameObject</param>
        private void AssignItem(GameObject itemGameObject)
        {
            Animator animator = null;
            if ((animator = m_AssignTo.GetComponent<Animator>()) == null) {
                EditorUtility.DisplayDialog("Unable to Assign", "Unable to assign the item. Ensure the Assign To GameObject contains an Animator component.", "Okay");
            } else {
                var item = GameObject.Instantiate(itemGameObject) as GameObject;
                var handTransform = animator.GetBoneTransform(m_HandAssignment == HandAssignment.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                item.transform.parent = handTransform.GetComponentInChildren<ItemPlacement>().transform;
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                Selection.activeGameObject = item;
            }
        }
    }
}