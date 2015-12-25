using UnityEngine;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// Any weapon that uses melee to damage the target. This includes a knife, baseball bat, mace, etc.
    /// </summary>
    public class MeleeWeapon : Weapon
    {
        [Tooltip("The number of melee attacks per second")]
        [SerializeField] private float m_AttackRate = 2;
        [Tooltip("The maximum distance that an object can be for it to be attacked")]
        [SerializeField] private float m_AttackDistance = 1;
        [Tooltip("The radius of the SphereCast when determining if a target was hit")]
        [SerializeField] private float m_AttackRadius = 1;
        [Tooltip("The layers that the melee attack can hit")]
        [SerializeField] private LayerMask m_AttackLayer;

        [Tooltip("Optionally specify a sound that should randomly play when the weapon is attacked")]
        [SerializeField] private AudioClip[] m_AttackSound;
        [Tooltip("If Attack Sound is specified, play the sound after the specified delay")]
        [SerializeField] private float m_AttackSoundDelay;

        [Tooltip("Optionally specify an event to send to the object hit on damage")]
        [SerializeField] private string m_DamageEvent;
        [Tooltip("The amount of damage done to the object hit")]
        [SerializeField] private float m_DamageAmount = 10;
        [Tooltip("How much force is applied to the object hit")]
        [SerializeField] private float m_ImpactForce = 5;
        [Tooltip("Optionally specify any default dust that should appear on at the location of the object hit. This is only used if no per-object dust is setup in the ObjectManager")]
        [SerializeField] private GameObject m_DefaultDust;
        [Tooltip("Optionally specify a default impact sound that should play at the point of the object hit. This is only used if no per-object sound is setup in the ObjectManager")]
        [SerializeField] private AudioClip m_DefaultImpactSound;

        // SharedFields
#if !ENABLE_MULTIPLAYER
        private SharedMethod<bool> m_IsAI = null;
#endif
        private SharedMethod<bool, Vector3> m_TargetLookPosition = null;

        // Internal variables
        private float m_AttackDelay;
        private float m_LastAttackTime;
        private RaycastHit m_RaycastHit;
        private bool m_InUse;

        // Component references
        private AudioSource m_AudioSource;
        private Transform m_CharacterTransform;
        private CapsuleCollider m_CharacterCapsuleCollider;

        /// <summary>
        /// Cache the component references and initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_AudioSource = GetComponent<AudioSource>();

            m_AttackDelay = 1.0f / m_AttackRate;
            m_LastAttackTime = -m_AttackRate;
        }

        /// <summary>
        /// Register for any events that the weapon should be aware of.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            EventHandler.RegisterEvent<Transform, Vector3, Vector3>(gameObject, "OnItemAddMeleeEffects", AddMeleeEffects);
            EventHandler.RegisterEvent(gameObject, "OnItemAddAttackEffects", AddAttackEffects);
            
            // Init may not have been called from the inventory so the character GameObject may not have been assigned yet.
            if (m_Character != null) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }
        }

        /// <summary>
        /// Unregister for any events that the weapon was registered for.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            // The aim and use states should begin fresh.
            m_AimStates.ResetNextState();
            m_UseStates.ResetNextState();

            EventHandler.UnregisterEvent<Transform, Vector3, Vector3>(gameObject, "OnItemAddMeleeEffects", AddMeleeEffects);
            EventHandler.UnregisterEvent(gameObject, "OnItemAddAttackEffects", AddAttackEffects);
            // The character may be null if Init hasn't been called yet.
            if (m_Character != null) {
                EventHandler.UnregisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }
        }

        /// <summary>
        /// Initializes the item after it has been added to the Inventory.
        /// </summary>
        public override void Init(Inventory inventory)
        {
            base.Init(inventory);

            // Register for character events if the GameObject is active. OnEnable normally registers for these callbacks but in this case OnEnable has already occurred.
            if (gameObject.activeSelf) {
                EventHandler.RegisterEvent<bool>(m_Character, "OnControllerAim", OnAim);
            }
        }

        /// <summary>
        /// Initializes all of the SharedFields.
        /// </summary>
        protected override void Start()
        {
            base.Start();

            SharedManager.InitializeSharedFields(m_Character, this);
            // An AI Agent does not need to communicate with the camera. Do not initialze the SharedFields on the network to prevent non-local characters from
            // using the main camera to determine their look direction. TargetLookPosition has been implemented by the NetworkMonitor component.
#if !ENABLE_MULTIPLAYER
            if (!m_IsAI.Invoke()) {
                SharedManager.InitializeSharedFields(Utility.FindCamera().gameObject, this);
            }
#endif
            m_CharacterTransform = m_Character.transform;
            m_CharacterCapsuleCollider = m_Character.GetComponent<CapsuleCollider>();
        }

        /// <summary>
        /// Returns the maximum distance that the item can be used.
        /// </summary>
        /// <returns>The maximum distance that the item can be used.</returns>
        public override float MaxUseDistance()
        {
            return m_AttackDistance;
        }

        /// <summary>
        /// Try to attack. The weapon may not be able to attack if the last attack was too recent.
        /// <returns>True if the item was used.</returns>
        /// </summary>
        public override bool TryUse()
        {
            if (!m_InUse && m_LastAttackTime + m_AttackDelay < Time.time) {
                m_LastAttackTime = Time.time;
                m_InUse = true;
                // Add any melee starting effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
                m_NetworkMonitor.ExecuteItemEvent(m_ItemType.ID, "OnItemAddAttackEffects");
#else
                AddAttackEffects();
#endif
                EventHandler.ExecuteEvent(m_Character, "OnItemUse", m_ItemType is PrimaryItemType);
                return true;
            }
            return false;
        }

        /// <summary>
        /// The melee weapon has attacked, add any effects.
        /// </summary>
        private void AddAttackEffects()
        {
            // Play a attack sound.
            if (m_AttackSound != null && m_AttackSound.Length > 0) {
                m_AudioSource.clip = m_AttackSound[Random.Range(0, m_AttackSound.Length - 1)];
                if (m_AttackSoundDelay > 0) {
                    m_AudioSource.PlayDelayed(m_AttackSoundDelay);
                } else {
                    m_AudioSource.Play();
                }
            }
        }

        /// <summary>
        /// Is the melee weapon currently being used?
        /// </summary>
        /// <returns>True if the weapon is in use.</returns>
        public override bool InUse()
        {
            return m_InUse;
        }

        /// <summary>
        /// The melee weapon has been used. Damage the object hit.
        /// </summary>
        public override void Used()
        {
#if ENABLE_MULTIPLAYER
            // The server will control the raycast logic.
            if (!m_IsServer.Invoke()) {
                return;
            }
#endif
            // Disable the character's collider so the SphereCast doesn't hit the character.
            m_CharacterCapsuleCollider.enabled = false;
            var attackDirection = AttackDirection();
            if (Physics.SphereCast(m_CharacterTransform.position - attackDirection * m_AttackRadius, m_AttackRadius, attackDirection, out m_RaycastHit, m_AttackDistance + m_AttackRadius, m_AttackLayer.value)) {
                // Execute any custom events.
                if (!string.IsNullOrEmpty(m_DamageEvent)) {
                    EventHandler.ExecuteEvent(m_RaycastHit.collider.gameObject, m_DamageEvent, m_DamageAmount, m_RaycastHit.point, m_RaycastHit.normal * -m_ImpactForce);
                }
                Health hitHealth;
                // If the Health component exists it will apply a force to the rigidbody in addition to deducting the health. Otherwise just apply the force to the rigidbody. 
                if ((hitHealth = m_RaycastHit.transform.GetComponentInParent<Health>()) != null) {
                    hitHealth.Damage(m_DamageAmount, m_RaycastHit.point, m_RaycastHit.normal * -m_ImpactForce);
                } else if (m_ImpactForce > 0 && m_RaycastHit.rigidbody != null && !m_RaycastHit.rigidbody.isKinematic) {
                    m_RaycastHit.rigidbody.AddForceAtPosition(m_RaycastHit.normal * -m_ImpactForce, m_RaycastHit.point);
                }

                // Add any melee effects. These effects do not need to be added on the server.
#if ENABLE_MULTIPLAYER
                m_NetworkMonitor.ExecuteItemEvent(m_ItemType.ID, "OnItemAddMeleeEffects", m_RaycastHit.transform.gameObject, m_RaycastHit.point, m_RaycastHit.normal);
#else
                AddMeleeEffects(m_RaycastHit.transform, m_RaycastHit.point, m_RaycastHit.normal);
#endif
            }

            m_CharacterCapsuleCollider.enabled = true;
            m_InUse = false;
            m_AimStates.NextState();
            m_UseStates.NextState();
            EventHandler.ExecuteEvent(m_Character, "OnItemStopUse");
        }

        /// <summary>
        /// The melee hit an object, add any melee effects.
        /// </summary>
        /// <param name="hitTransform">The transform that was hit.</param>
        /// <param name="hitPoint">The hit point.</param>
        /// <param name="hitNormal">The normal of the transform at the hit point.</param>
        private void AddMeleeEffects(Transform hitTransform, Vector3 hitPoint, Vector3 hitNormal)
        {
            // Spawn a dust particle effect at the hit point.
            var dust = ObjectManager.ObjectForItem(hitTransform.tag, m_ItemType, ObjectManager.ObjectCategory.Dust) as GameObject;
            if (dust == null) {
                dust = m_DefaultDust;
            }
            if (dust != null) {
                ObjectPool.Instantiate(dust, hitPoint, dust.transform.rotation * Quaternion.LookRotation(hitNormal));
            }

            // Play a sound at the hit point.
            var audioClip = ObjectManager.ObjectForItem(hitTransform.tag, m_ItemType, ObjectManager.ObjectCategory.Audio) as AudioClip;
            if (audioClip == null) {
                audioClip = m_DefaultImpactSound;
            }
            if (audioClip != null) {
                AudioSource.PlayClipAtPoint(audioClip, hitPoint);
            }
        }

        /// <summary>
        /// Determines the direction to attack based on the camera's look position and a random spread.
        /// </summary>
        /// <returns>The direction to attack.</returns>
        private Vector3 AttackDirection()
        {
            // If TargetLookPosition is null then use the forward direction. It may be null if the AI agent doesn't have the AIAgent component attached.
            if (m_TargetLookPosition == null) {
                return m_CharacterTransform.forward;
            }
            return (m_TargetLookPosition.Invoke(true) - m_CharacterTransform.position).normalized;
        }
        
        /// <summary>
        /// Callback from the controller when the item is aimed or no longer aimed.
        /// <param name="aim">Is the controller aiming?</param>
        /// </summary>
        private void OnAim(bool aim)
        {
            // When the character is no longer aiming reset the aim/use states so they will begin fresh.
            if (!aim) {
                m_AimStates.ResetNextState();
                m_UseStates.ResetNextState();
            }
        }
    }
}