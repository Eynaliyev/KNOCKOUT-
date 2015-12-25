using UnityEngine;
#if ENABLE_MULTIPLAYER
using UnityEngine.Networking;
#endif
using System;
using System.Collections.Generic;

namespace Opsive.ThirdPersonController
{
    /// <summary>
    /// It is relatively expensive to instantiate new objects so reuse the objects when possible by placing them in a pool.
    /// </summary>
#if ENABLE_MULTIPLAYER
    public class ObjectPool : NetworkBehaviour
#else
    public class ObjectPool : MonoBehaviour
#endif
    {
        // Static variables
        private static ObjectPool s_Instance;
        private static ObjectPool Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!m_Initialized) {
                    Debug.LogWarning("Warning: ObjectPool is null. A GameObject has been created with the component automatically added. Please run Scene Setup from the Start Window.");
                    s_Instance = new GameObject("ObjectPool").AddComponent<ObjectPool>();
                }
#endif
                return s_Instance;
            }
        }

        // Internal variables
#if UNITY_EDITOR
        private static bool m_Initialized;
#endif
        private Dictionary<int, Stack<GameObject>> m_GameObjectPool = new Dictionary<int, Stack<GameObject>>();
        private Dictionary<int, int> m_InstantiatedGameObjects = new Dictionary<int, int>();
        private Dictionary<Type, object> m_GenericPool = new Dictionary<Type, object>();
#if ENABLE_MULTIPLAYER
        private HashSet<GameObject> m_SpawnedGameObjects = new HashSet<GameObject>();
#endif

        /// <summary>
        /// Assign the static variables and register for any events that the pool should be aware of.
        /// </summary>
        private void OnEnable()
        {
            s_Instance = this;
#if UNITY_EDITOR
            m_Initialized = true;
#endif

#if ENABLE_MULTIPLAYER
            EventHandler.RegisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
#endif
        }

        /// <summary>
        /// Unregister for any events that the pool was registered for.
        /// </summary>
        private void OnDisable()
        {
#if ENABLE_MULTIPLAYER
            EventHandler.UnregisterEvent<NetworkConnection>("OnNetworkServerReady", OnServerReady);
#endif
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// The client has joined a network game. Register for the object pool callbacks so the object pool can be synchronized with the current game state.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            NetworkClient.allClients[0].RegisterHandler(NetworkEventManager.NetworkMessages.MSG_POOLED_OBJECT, AddPooledObject);
        }

        /// <summary>
        /// Message which indicates an object is in the pool.
        /// </summary>
        private class PooledMessage : MessageBase
        {
            // Internal variables
            private GameObject m_PooledObject;
            private int m_OriginalInstanceID;

            // Exposed properties
            public GameObject PooledObject { get { return m_PooledObject; } set { m_PooledObject = value; } }
            public int OriginalInstanceID { get { return m_OriginalInstanceID; } set { m_OriginalInstanceID = value; } }

            /// <summary>
            /// Populate a message object from a NetworkReader stream.
            /// </summary>
            /// <param name="reader">Stream to read from.</param>
            public override void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);

                m_PooledObject = reader.ReadGameObject();
                m_OriginalInstanceID = reader.ReadInt32();
            }

            /// <summary>
            /// Populate a NetworkWriter stream from a message object.
            /// </summary>
            /// <param name="writer">Stream to write to</param>
            public override void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);

                writer.Write(m_PooledObject);
                writer.Write(m_OriginalInstanceID);
            }
        }

        /// <summary>
        /// A new client has just joined the server. Send that client the current pooled items.
        /// </summary>
        /// <param name="netConn">The client connection.</param>
        private void OnServerReady(NetworkConnection netConn)
        {
            var pooledMsg = Get<PooledMessage>();
            foreach (var pool in m_GameObjectPool) {
                foreach (var pooledObject in pool.Value) {
                    // Don't send the object if it isn't in the SpawnedGameObjects set. Not all GameObjects are spawned on the server.
                    if (m_SpawnedGameObjects.Contains(pooledObject)) {
                        pooledMsg.PooledObject = pooledObject;
                        pooledMsg.OriginalInstanceID = pool.Key;
                        NetworkServer.SendToClient(netConn.connectionId, NetworkEventManager.NetworkMessages.MSG_POOLED_OBJECT, pooledMsg);
                    }
                }
            }
            Return(pooledMsg);
        }

        /// <summary>
        /// An object is on the server pool. Add it to the client pool as well by destroying it.
        /// </summary>
        /// <param name="netMsg">The message being sent.</param>
        private void AddPooledObject(NetworkMessage netMsg)
        {
            var pooledMessage = netMsg.ReadMessage<PooledMessage>();
            DestroyLocal(pooledMessage.PooledObject, pooledMessage.OriginalInstanceID);
        }
#endif

        /// <summary>
        /// Instantiate a new GameObject. Use the object pool if a previously used GameObject is located in the pool, otherwise instaniate a new GameObject.
        /// </summary>
        /// <param name="original">The original GameObject to pooled a copy of.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <returns>The pooled/instantiated GameObject.</returns>
        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation)
        {
            return Instantiate(original, position, rotation, null);
        }

        /// <summary>
        /// Spawn a new GameObject on the server and persist to the clients. Use the object pool if a previously used GameObject is located in the pool, otherwise instaniate a new GameObject.
        /// </summary>
        /// <param name="original">The original GameObject to pooled a copy of.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <returns>The pooled/instantiated GameObject.</returns>
        public static GameObject Spawn(GameObject original, Vector3 position, Quaternion rotation)
        {
            return Spawn(original, position, rotation, null);
        }

        /// <summary>
        /// Instantiate a new GameObject. Use the object pool if a previously used GameObject is located in the pool, otherwise instaniate a new GameObject.
        /// </summary>
        /// <param name="original">The original GameObject to pooled a copy of.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <param name="parent">The parent to assign to the pooled GameObject.</param>
        /// <returns>The pooled/instantiated GameObject.</returns>
        public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            return Instance.InstantiateInternal(original, position, rotation, parent, false);
        }

        /// <summary>
        /// Spawn a new GameObject on the server and persist to the clients. Use the object pool if a previously used GameObject is located in the pool, otherwise instaniate a new GameObject.
        /// </summary>
        /// <param name="original">The original GameObject to pooled a copy of.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <param name="parent">The parent to assign to the pooled GameObject.</param>
        /// <returns>The pooled/instantiated GameObject.</returns>
        public static GameObject Spawn(GameObject original, Vector3 position, Quaternion rotation, Transform parent)
        {
            return Instance.InstantiateInternal(original, position, rotation, parent, true);
        }

        /// <summary>
        /// Internal method to instantiate a new GameObject. Use the object pool if a previously used GameObject is located in the pool, otherwise instaniate a new GameObject.
        /// </summary>
        /// <param name="original">The original GameObject to pooled a copy of.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <param name="parent">The parent to assign to the pooled GameObject.</param>
        /// <param name="networkSpawn">Should the object be spawned on the server and persisted across clients?</param>
        /// <returns>The pooled/instantiated GameObject.</returns>
        private GameObject InstantiateInternal(GameObject original, Vector3 position, Quaternion rotation, Transform parent, bool networkSpawn)
        {
            GameObject instantiatedObject = null;
            var originalInstanceID = original.GetInstanceID();
            if ((instantiatedObject = InstantiateInternal(originalInstanceID, position, rotation, parent, networkSpawn)) != null) {
                return instantiatedObject;
            }

            instantiatedObject = (GameObject)GameObject.Instantiate(original, position, rotation);
            instantiatedObject.transform.parent = parent;
            // Map the newly instantiated instance ID to the original instance ID so when the object is returned it knows what pool to go to.
            m_InstantiatedGameObjects.Add(instantiatedObject.GetInstanceID(), originalInstanceID);
#if ENABLE_MULTIPLAYER
            if (networkSpawn && isServer) {
                NetworkServer.Spawn(instantiatedObject);
                m_SpawnedGameObjects.Add(instantiatedObject);
                if (!isClient) {
                    RpcSpawnedObject(instantiatedObject.GetInstanceID(), originalInstanceID);
                }
            }
#endif
            return instantiatedObject;
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// An object has been spawned on the server. Add the instance id of the spawned object to the instantiated GameObjects dictionary on the client.
        /// This will allow the object to be returned to the correct pool.
        /// </summary>
        /// <param name="spawnedInstanceID">The instance id of the spawned object.</param>
        /// <param name="originalInstanceID">The original instance id of the GameObject spawned.</param>
        [ClientRpc]
        private void RpcSpawnedObject(int spawnedInstanceID, int originalInstanceID)
        {
            m_InstantiatedGameObjects.Add(spawnedInstanceID, originalInstanceID);
        }
#endif

        /// <summary>
        /// An object is trying to be popped from the object pool. Return the pooled object if it exists otherwise null meaning one needs to be insantiated.
        /// </summary>
        /// <param name="originalInstanceID">The instance id of the GameObject trying to be popped from the pool.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <param name="parent">The parent to assign to the pooled GameObject.</param>
        /// <param name="networkSpawn">Should the object be spawned on the server and persisted across clients?</param>
        /// <returns>The pooled GameObject.</returns>
        private GameObject InstantiateInternal(int originalInstanceID, Vector3 position, Quaternion rotation, Transform parent, bool networkSpawn)
        {
            GameObject instantiatedObject = null;
            Stack<GameObject> pool;
            if (m_GameObjectPool.TryGetValue(originalInstanceID, out pool)) {
                if (pool.Count > 0) {
#if ENABLE_MULTIPLAYER
                    if (networkSpawn && isServer && !isClient) {
                        RpcInstantiateInternal(originalInstanceID, position, rotation, parent != null ? parent.gameObject : null);
                    }
#endif
                    instantiatedObject = pool.Pop();
                    instantiatedObject.transform.position = position;
                    instantiatedObject.transform.rotation = rotation;
                    instantiatedObject.transform.parent = parent;
                    instantiatedObject.SetActive(true);
                    // Map the newly instantiated instance ID to the original instance ID so when the object is returned it knows what pool to go to.
                    m_InstantiatedGameObjects.Add(instantiatedObject.GetInstanceID(), originalInstanceID);
                    return instantiatedObject;
                }
            }
            return instantiatedObject;
        }
        
#if ENABLE_MULTIPLAYER
        /// <summary>
        /// An object has been popped from the pool on the server. Pop it on the client as well.
        /// </summary>
        /// <param name="originalInstanceID">The instance id of the GameObject being popped from the pool.</param>
        /// <param name="position">The position of the pooled GameObject.</param>
        /// <param name="rotation">The rotation of the pooled Gameobject.</param>
        /// <param name="parent">The parent to assign to the pooled GameObject.</param>
        [ClientRpc]
        private void RpcInstantiateInternal(int originalInstanceID, Vector3 position, Quaternion rotation, GameObject parent)
        {
            InstantiateInternal(originalInstanceID, position, rotation, parent != null ? parent.transform : null, false);
        }
#endif

        /// <summary>
        /// Return if the object was instantiated with the ObjectPool.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to check to see if it was instantiated with the ObjectPool.</param>
        /// <returns>True if the object was instantiated with the ObjectPool.</returns>
        public static bool SpawnedWithPool(GameObject instantiatedObject)
        {
            return Instance.SpawnedWithPoolInternal(instantiatedObject);
        }

        /// <summary>
        /// Internal method to return if the object was instantiated with the ObjectPool.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to check to see if it was instantiated with the ObjectPool.</param>
        /// <returns>True if the object was instantiated with the ObjectPool.</returns>
        private bool SpawnedWithPoolInternal(GameObject instantiatedObject)
        {
            return m_InstantiatedGameObjects.ContainsKey(instantiatedObject.GetInstanceID());
        }

        /// <summary>
        /// Return the instance ID of the prefab used to spawn the instantiated object.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to get the original instance ID</param>
        /// <returns>The original instance ID</returns>
        public static int OriginalInstanceID(GameObject instantiatedObject)
        {
            return Instance.OriginalInstanceIDInternal(instantiatedObject);
        }

        /// <summary>
        /// Internal method to return the instance ID of the prefab used to spawn the instantiated object.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to get the original instance ID</param>
        /// <returns>The original instance ID</returns>
        private int OriginalInstanceIDInternal(GameObject instantiatedObject)
        {
            var instantiatedInstanceID = instantiatedObject.GetInstanceID();
            var originalInstanceID = -1;
            if (!m_InstantiatedGameObjects.TryGetValue(instantiatedInstanceID, out originalInstanceID)) {
                Debug.LogError("Unable to get the original instance ID of " + instantiatedObject + ": has the object already been placed in the ObjectPool?");
                return -1;
            }
            return originalInstanceID;
        }

        /// <summary>
        /// Return the specified GameObject back to the ObjectPool.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to return to the pool.</param>
        public static void Destroy(GameObject instantiatedObject)
        {
            // Objects may be wanting to be destroyed as the game is stopping but the ObjectPool has already been destroyed. Ensure the ObjectPool is still valid.
            if (Instance == null) {
                return;
            }
            Instance.DestroyInternal(instantiatedObject);
        }

        /// <summary>
        /// Internal method to return the specified GameObject back to the ObjectPool. Call the corresponding server or client method.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to return to the pool.</param>
        private void DestroyInternal(GameObject instantiatedObject)
        {
            var instantiatedInstanceID = instantiatedObject.GetInstanceID();
            var originalInstanceID = -1;
            if (!m_InstantiatedGameObjects.TryGetValue(instantiatedInstanceID, out originalInstanceID)) { 
                Debug.LogError("Unable to pool " + instantiatedObject + " (instance " + instantiatedInstanceID + "): the GameObject was not instantiated with ObjectPool.Instantiate " + Time.time);
                return;
            }

            // Map the instantiated instance ID back to the orignal instance ID so the GameObject can be returned to the correct pool.
            m_InstantiatedGameObjects.Remove(instantiatedInstanceID);

#if ENABLE_MULTIPLAYER
            // Tell the clients to remove the object as well.
            if (isServer && m_SpawnedGameObjects.Contains(instantiatedObject)) {
                RpcDestroy(instantiatedObject, originalInstanceID);
                return;
            }
#endif

            DestroyLocal(instantiatedObject, originalInstanceID);
        }

#if ENABLE_MULTIPLAYER
        /// <summary>
        /// Return the specified GameObject back to the ObjectPool on the client.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to return to the pool.</param>
        /// <param name="originalInstanceID">The instance ID of the original GameObject.</param>
        [ClientRpc]
        private void RpcDestroy(GameObject instantiatedObject, int originalInstanceID)
        {
            DestroyLocal(instantiatedObject, originalInstanceID);
        }
#endif

        /// <summary>
        /// Return the specified GameObject back to the ObjectPool.
        /// </summary>
        /// <param name="instantiatedObject">The GameObject to return to the pool.</param>
        /// <param name="originalInstanceID">The instance ID of the original GameObject.</param>
        private void DestroyLocal(GameObject instantiatedObject, int originalInstanceID)
        {
            // This GameObject may have a collider and that collider may be ignoring the collision with other colliders. Revert this setting because the object is going
            // back into the pool.
            Collider instantiatedObjectCollider;
            if ((instantiatedObjectCollider = instantiatedObject.GetComponent<Collider>()) != null) {
                LayerManager.RevertCollision(instantiatedObjectCollider);
            }
            instantiatedObject.SetActive(false);
            instantiatedObject.transform.parent = transform;

            Stack<GameObject> pool;
            if (m_GameObjectPool.TryGetValue(originalInstanceID, out pool)) {
                pool.Push(instantiatedObject);
            } else {
                // The pool for this GameObject type doesn't exist yet so it has to be created.
                pool = new Stack<GameObject>();
                pool.Push(instantiatedObject);
                m_GameObjectPool.Add(originalInstanceID, pool);
            }
        }

        /// <summary>
        /// Get a pooled object of the specified type using a generic ObjectPool.
        /// </summary>
        /// <typeparam name="T">The type of object to get.</typeparam>
        /// <returns>A pooled object of type T.</returns>
        public static T Get<T>()
        {
            return Instance.GetInternal<T>();
        }

        /// <summary>
        /// Internal method to get a pooled object of the specified type using a generic ObjectPool.
        /// </summary>
        /// <typeparam name="T">The type of object to get.</typeparam>
        /// <returns>A pooled object of type T.</returns>
        private T GetInternal<T>()
        {
            object value;
            if (m_GenericPool.TryGetValue(typeof(T), out value)) {
                var pooledObjects = value as List<T>;
                if (pooledObjects.Count > 0) {
                    var obj = pooledObjects[0];
                    pooledObjects.RemoveAt(0);
                    return obj;
                }
            }
            return Activator.CreateInstance<T>();
        }

        /// <summary>
        /// Return the object back to the generic object pool.
        /// </summary>
        /// <typeparam name="T">The type of object to return.</typeparam>
        /// <param name="obj">The object to return.</param>
        public static void Return<T>(T obj)
        {
            Instance.ReturnInternal<T>(obj);
        }

        /// <summary>
        /// Internal method to return the object back to the generic object pool.
        /// </summary>
        /// <typeparam name="T">The type of object to return.</typeparam>
        /// <param name="obj">The object to return.</param>
        private void ReturnInternal<T>(T obj)
        {
            object value;
            if (m_GenericPool.TryGetValue(typeof(T), out value)) {
                var pooledObjects = value as List<T>;
                pooledObjects.Add(obj);
            } else {
                var pooledObjects = new List<T>();
                pooledObjects.Add(obj);
                m_GenericPool.Add(typeof(T), pooledObjects);
            }
        }
    }
}