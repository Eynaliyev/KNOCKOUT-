using UnityEngine;
#if !(UNITY_4_6 || UNITY_5_0)
using UnityEngine.Networking;
#endif

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Builds the Third Person Controller character. This component allows the character to be built at runtime.
    /// </summary>
    public class CharacterBuilder : MonoBehaviour
    {
        // Internal variables
        private static GameObject m_Character;
        private static bool m_AIAgent;
#if !(UNITY_4_6 || UNITY_5_0)
        private static bool m_IsNetworked;
#endif
        private static RigidbodyCharacterController.MovementType m_MovementType = RigidbodyCharacterController.MovementType.Combat;
        private static bool m_AddIK = true;

        /// <summary>
        /// Builds the Third Person Controller character.
        /// </summary>
        public static void BuildCharacter(GameObject character, bool aiAgent, bool isNetworked, RigidbodyCharacterController.MovementType movementType, bool addIK)
        {
            // Set the internal variables.
            m_Character = character;
            m_AIAgent = aiAgent;
#if !(UNITY_4_6 || UNITY_5_0)
            m_IsNetworked = isNetworked;
#endif
            m_MovementType = movementType;
            m_AddIK = addIK;

            // Build the character.
            BuildStandardComponents();
            for (int i = 0; i < 2; ++i) {
                var animator = m_Character.GetComponent<Animator>();
                BuildItemHands(animator.GetBoneTransform(i == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand));
            }
#if !(UNITY_4_6 || UNITY_5_0)
            if (m_IsNetworked) {
                BuildNetwork();
            }
#endif
        }

        /// <summary>
        /// Adds the standard components to the character. These components do not have any custom settings associated with them.
        /// </summary>
        /// <param name="character"></param>
        private static void BuildStandardComponents()
        {
            if (m_Character.GetComponent<Animator>() == null) {
                m_Character.AddComponent<Animator>();
            }
            var animator = m_Character.GetComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
            var shooterController = true;
#if !UNITY_4_6
            if (m_MovementType == RigidbodyCharacterController.MovementType.Adventure) {
                shooterController = false;
            }
#endif
            if (shooterController) {
                animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animator/Shooter");
            } else {
                animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animator/Adventure");
            }
            if (animator.avatar == null) {
                Debug.LogError("Error: The Animator Avatar on " + m_Character + " is not assigned. Please assign an avatar within the inspector.");
            }
            CapsuleCollider capsuleCollider;
            if ((capsuleCollider = m_Character.GetComponent<CapsuleCollider>()) == null) {
                capsuleCollider = m_Character.AddComponent<CapsuleCollider>();
            }
            capsuleCollider.center = new Vector3(0, 0.9f, 0);
            capsuleCollider.radius = 0.3f;
            capsuleCollider.height = 1.8f;
            Rigidbody rigidbody;
            if ((rigidbody = m_Character.GetComponent<Rigidbody>()) == null) {
                rigidbody = m_Character.AddComponent<Rigidbody>();
            }
            rigidbody.angularDrag = 999;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;

            if (m_AIAgent) {
                m_Character.layer = LayerManager.Default;
            } else {
                // An human-controller character needs to be able to handle input.
                m_Character.AddComponent<Opsive.ThirdPersonController.Input.Wrappers.UnityInput>();
                m_Character.tag = "Player";
                m_Character.layer = LayerManager.Player;

                // A blank SkinnedMeshRenderer is added to prevent the Transforms from going crazy when all of the renderers are disabled. This renderer will always be enabled.
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) {
                    var existingRenderer = head.gameObject.GetComponent<Renderer>();
                    if (existingRenderer != null) {
                        DestroyImmediate(existingRenderer, true);
                    }
                    var renderer = head.gameObject.AddComponent<SkinnedMeshRenderer>();
#if UNITY_4_6
                    renderer.castShadows = false;
#else
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
#endif
                    renderer.useLightProbes = false;
                    renderer.receiveShadows = false;
                    renderer.updateWhenOffscreen = true;
                    renderer.materials = new Material[] { };
                    var localBounds = renderer.localBounds;
                    localBounds.extents = Vector3.one;
                    renderer.localBounds = localBounds;
                }
            }

            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.AnimatorMonitor>();
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.ControllerHandler>();
            RigidbodyCharacterController controller;
            // CharacterHandler requires a RigidbodyCharacterController so the component may already be added.
            if ((controller = m_Character.GetComponent<Opsive.ThirdPersonController.Wrappers.RigidbodyCharacterController>()) == null) {
                controller = m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.RigidbodyCharacterController>();
            } 
            controller.Movement = m_MovementType;
            controller.GroundedFrictionMaterial = Resources.Load<PhysicMaterial>("Physic Materials/MaxFriction");
            controller.StepFrictionMaterial = controller.SlopeFrictionMaterial = controller.AirFrictionMaterial = Resources.Load<PhysicMaterial>("Physic Materials/Frictionless");

            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.Inventory>();

            // Add a trigger and audio source to the feet for footsteps.
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (leftFoot != null) {
                leftFoot.gameObject.AddComponent<AudioSource>();
                leftFoot.gameObject.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterFootTrigger>();
                var footTrigger = leftFoot.gameObject.AddComponent<SphereCollider>();
                footTrigger.isTrigger = true;
                footTrigger.radius = 0.18f;
                var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                rightFoot.gameObject.AddComponent<AudioSource>();
                rightFoot.gameObject.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterFootTrigger>();
                footTrigger = rightFoot.gameObject.AddComponent<SphereCollider>();
                footTrigger.isTrigger = true;
                footTrigger.radius = 0.18f;
                var footsteps = m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterFootsteps>();
                footsteps.Feet = new GameObject[] { leftFoot.gameObject, rightFoot.gameObject };
            }

            var childTransforms = m_Character.GetComponentsInChildren<Transform>();
            for (int i = 0; i < childTransforms.Length; ++i) {
                if (childTransforms[i].gameObject.Equals(m_Character)) {
                    continue;
                }
                childTransforms[i].gameObject.layer = LayerManager.IgnoreRaycast;
            }
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.ItemHandler>();
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.InventoryHandler>();
            if (m_AddIK) {
                m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterIK>();
            }

            // Collider for the item
            var sphereCollider = m_Character.AddComponent<SphereCollider>();
            sphereCollider.enabled = false;
            sphereCollider.radius = 0.001f;
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.ItemColliderPositioner>();

            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterHealth>();
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.CharacterRespawner>();
            if (m_MovementType == RigidbodyCharacterController.MovementType.PointClick) {
                m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.PointClickControllerHandler>();
            }
        }

        /// <summary>
        /// Adds the GameObject that items will be placed under.
        /// </summary>
        /// <param name="handTransform">The parent transform.</param>
        private static void BuildItemHands(Transform handTransform)
        {
            var items = new GameObject("Items");
            items.AddComponent<Opsive.ThirdPersonController.Wrappers.ItemPlacement>();
            items.transform.parent = handTransform;
            items.transform.localPosition = Vector3.zero;
            items.transform.localRotation = Quaternion.identity;
        }

#if !(UNITY_4_6 || UNITY_5_0)
        /// <summary>
        /// Adds the network components to the character.
        /// </summary>
        private static void BuildNetwork()
        {
            m_Character.AddComponent<Opsive.ThirdPersonController.Wrappers.NetworkMonitor>();
            var networkAnimator = m_Character.AddComponent<NetworkAnimator>();
            networkAnimator.animator = m_Character.GetComponent<Animator>();
            var networkTransform = m_Character.AddComponent<NetworkTransform>();
            networkTransform.transformSyncMode = NetworkTransform.TransformSyncMode.SyncTransform;
        }
#endif
    }
}