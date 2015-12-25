using UnityEngine;
using UnityEditor;

namespace Opsive.ThirdPersonController.Editor
{
    /// <summary>
    /// A wizard that will build a new Item.
    /// </summary>
    public class ItemBuilder : EditorWindow
    {
        // Window properties
        private Vector2 m_ScrollPosition;
        private GUIStyle m_HeaderLabelStyle;

        private ItemBaseType m_ItemType;
        private GameObject m_Base;
        private string m_ItemName;
        private enum ItemTypes { Shootable, Melee, Throwable, Static };
        private ItemTypes m_Type = ItemTypes.Shootable;
        private GameObject m_AssignTo;
        private bool m_AddToDefaultLoadout;
        private enum HandAssignment { Left, Right }
        private HandAssignment m_HandAssignment = HandAssignment.Right;

        [MenuItem("Tools/Third Person Controller/Item Builder", false, 13)]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<ItemBuilder>(true, "Item Builder");
            window.minSize = new Vector2(520, 150);
            DontDestroyOnLoad(window);
        }

        /// <summary>
        /// Initializes the GUIStyle used by the header.
        /// </summary>
        private void OnEnable()
        {
            if (m_HeaderLabelStyle == null) {
                m_HeaderLabelStyle = new GUIStyle(EditorStyles.label);
                m_HeaderLabelStyle.wordWrap = true;
            }
        }

        /// <summary>
        /// Shows the Item Builder.
        /// </summary>
        private void OnGUI()
        {
            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);

            ShowHeaderGUI();
            var canBuild = ShowIntroGUI();

            GUILayout.EndScrollView();

            GUILayout.Space(3);
            GUILayout.BeginHorizontal();
            GUI.enabled = canBuild;
            if (GUILayout.Button(m_AssignTo == null ? "Build" : "Assign")) {
                BuildOrAssignItem();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Shows the current section's header.
        /// </summary>
        private void ShowHeaderGUI()
        {
            var description = "This builder will create a new Item of the specified type.";
            EditorGUILayout.LabelField(description, m_HeaderLabelStyle);
            GUILayout.Space(5);
        }

        /// <summary>
        /// Shows the initial Item options.
        /// </summary>
        private bool ShowIntroGUI()
        {
            var canContinue = true;
            m_ItemType = EditorGUILayout.ObjectField("Item Type", m_ItemType, typeof(ItemBaseType), false) as ItemBaseType;
            if (canContinue && m_ItemType == null) {
                EditorGUILayout.HelpBox("This field is required. The Inventory uses the Item Type to determine the type of weapon ", MessageType.Error);
                canContinue = false;
            }

            m_Base = EditorGUILayout.ObjectField("Base", m_Base, typeof(GameObject), true) as GameObject;
            if (canContinue && m_Base == null) {
                EditorGUILayout.HelpBox("This field is required. If the item uses a model then that model should be the base object. If an item does not use a model (such as a grenade), " +
                                         "then an empty GameObject can be used.", MessageType.Error);
                canContinue = false;
            } else if (canContinue && PrefabUtility.GetPrefabType(m_Base) == PrefabType.Prefab) {
                EditorGUILayout.HelpBox("Please drag your item into the scene. The Item Builder cannot add components to prefabs.",
                                    MessageType.Error);
                canContinue = false;
            }

            m_ItemName = EditorGUILayout.TextField("Item Name", m_ItemName);
            if (string.IsNullOrEmpty(m_ItemName)) {
                EditorGUILayout.HelpBox("The Item Name specifies the name of the Animator substate machine. It should not be empty unless you only have one item type.", MessageType.Warning);
            }

            m_AssignTo = EditorGUILayout.ObjectField("Assign To", m_AssignTo, typeof(GameObject), true) as GameObject;
            if (m_AssignTo == null) {
                EditorGUILayout.HelpBox("When the Item is built it can be assigned to an existing character. If this value is not specified the Item can be later assigned.", MessageType.Info);
            } else {
                EditorGUI.indentLevel++;
                m_HandAssignment = (HandAssignment)EditorGUILayout.EnumPopup("Hand", m_HandAssignment);
                m_AddToDefaultLoadout = EditorGUILayout.Toggle("Add to Default Loadout", m_AddToDefaultLoadout);
                EditorGUI.indentLevel--;
            }
            m_Type = (ItemTypes)EditorGUILayout.EnumPopup("Type", m_Type);

            return canContinue;
        }

        /// <summary>
        /// Builds or assigns a new Item.
        /// </summary>
        private void BuildOrAssignItem()
        {
            // If assign to is null than don't change the item transform's parent, otherwise assign it as a child to the specified hand Transform.
            if (m_AssignTo == null) {
                BuildItem();
                Selection.activeGameObject = m_Base;
                Close();
            } else {
                Animator animator = null;
                if ((animator = m_AssignTo.GetComponent<Animator>()) == null) {
                    EditorUtility.DisplayDialog("Unable to Assign", "Unable to assign the item. Ensure the Assign To GameObject contains an Animator component.", "Okay");
                } else {
                    if (m_AddToDefaultLoadout) {
                        var inventory = m_AssignTo.transform.GetComponentInParent<Inventory>();
                        if (inventory == null) {
                            EditorUtility.DisplayDialog("Unable to Add to Default Loadout", "Unable to add the ItemType to the Default Loadout. Ensure the Assign To GameObject contains an Inventory component.", "Okay");
                            return;
                        }
                        var defaultLoadout = inventory.DefaultLoadout;
                        if (defaultLoadout == null) {
                            defaultLoadout = new Inventory.ItemAmount[0];
                        }
                        var canAdd = true;
                        for (int i = 0; i < defaultLoadout.Length; ++i) {
                            // Don't duplicate the ItemType within the loadout.
                            if (defaultLoadout[i].ItemType.Equals(m_ItemType)) {
                                canAdd = false;
                                break;
                            }
                        }
                        if (canAdd) {
                            var newLoadout = new Inventory.ItemAmount[defaultLoadout.Length + 1];
                            defaultLoadout.CopyTo(newLoadout, 0);
                            newLoadout[newLoadout.Length - 1] = new Inventory.ItemAmount(m_ItemType, 1);
                            inventory.DefaultLoadout = newLoadout;
                        }
                    }
                    BuildItem();
                    var handTransform = animator.GetBoneTransform(m_HandAssignment == HandAssignment.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    m_Base.transform.parent = handTransform.GetComponentInChildren<ItemPlacement>().transform;
                    m_Base.transform.localPosition = Vector3.zero;
                    m_Base.transform.localRotation = Quaternion.identity;
                    Selection.activeGameObject = m_Base;
                    Close();
                }
            }
        }

        /// <summary>
        /// Builds a new Item.
        /// </summary>
        private void BuildItem()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m_Base))) {
                var name = m_Base.name;
                m_Base = GameObject.Instantiate(m_Base) as GameObject;
                m_Base.name = name;
            }
            switch (m_Type) {
                case ItemTypes.Shootable:
                    BuildShootableItem();
                    break;
                case ItemTypes.Melee:
                    BuildMeleeItem();
                    break;
                case ItemTypes.Throwable:
                    BuildThrowableItem();
                    break;
                case ItemTypes.Static:
                    BuildStaticItem();
                    break;
            }
            var item = m_Base.GetComponent<Item>();
            item.ItemName = m_ItemName;
            var audioSource = m_Base.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        /// <summary>
        /// Adds the ShootableWeapon component to the Item.
        /// </summary>
        private void BuildShootableItem()
        {
            var itemTransform = m_Base.transform;
            var shootableWeapon = m_Base.AddComponent<Opsive.ThirdPersonController.Wrappers.ShootableWeapon>();

            shootableWeapon.ItemType = m_ItemType;
            shootableWeapon.FirePoint = CreateChildObject(itemTransform, "Fire Point", Vector3.zero);

            var sphereCollider = shootableWeapon.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.1f;
            sphereCollider.enabled = false;
        }

        /// <summary>
        /// Create a new GameObject relative to the parent with the specified name and offset.
        /// </summary>
        private Transform CreateChildObject(Transform parent, string name, Vector3 offset)
        {
            var child = new GameObject(name).transform;
            child.parent = parent;
            child.localPosition = offset;
            return child;
        }

        /// <summary>
        /// Adds the MeleeWeapon component to the Item.
        /// </summary>
        /// <param name="item"></param>
        private void BuildMeleeItem()
        {
            var meleeWeapon = m_Base.AddComponent<Opsive.ThirdPersonController.Wrappers.MeleeWeapon>();
            meleeWeapon.ItemType = m_ItemType;

            var sphereCollider = meleeWeapon.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.1f;
            sphereCollider.enabled = false;
        }

        /// <summary>
        /// Adds the ThrowableItem component to the Item.
        /// </summary>
        /// <param name="item"></param>
        private void BuildThrowableItem()
        {
            var throwableItem = m_Base.AddComponent<Opsive.ThirdPersonController.Wrappers.ThrowableItem>();
            throwableItem.ItemType = m_ItemType;
        }

        /// <summary>
        /// Adds the StaticItem component to the Item.
        /// </summary>
        private void BuildStaticItem()
        {
            var staticItem = m_Base.AddComponent<Opsive.ThirdPersonController.Wrappers.StaticItem>();
            staticItem.ItemType = m_ItemType;
        }
    }
}