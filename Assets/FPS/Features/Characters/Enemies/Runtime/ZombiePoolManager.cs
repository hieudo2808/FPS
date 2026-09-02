using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace FPS
{
    public class ZombiePoolManager : SceneSingleton<ZombiePoolManager>
    {
        [Header("Pool Settings")]
        [SerializeField] private int poolSizePerType = 20;
        [SerializeField] private bool autoExpand = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();
        private Dictionary<string, ZombieNetworkPoolHandler> handlers = new Dictionary<string, ZombieNetworkPoolHandler>();

        public int ConfiguredPoolSizePerType => poolSizePerType;
        public bool AutoExpand => autoExpand;

        protected override void Awake()
        {
            base.Awake();
        }

        public void InitializePool(GameObject prefab, int size = -1)
        {
            if (prefab == null) return;

            string key = GetPrefabKey(prefab.name);
            if (pools.ContainsKey(key)) return;

            int poolSize = size > 0 ? size : poolSizePerType;
            var pool = new Queue<GameObject>();

            for (int i = 0; i < poolSize; i++)
                pool.Enqueue(CreatePooledObject(prefab));

            pools[key] = pool;
            prefabLookup[key] = prefab;

            RegisterNetworkHandler(prefab, key);

            if (showDebugLogs)
                GameLog.Info(() => $"[ZombiePool] Initialized pool '{key}' with {poolSize} objects");
        }

        private void RegisterNetworkHandler(GameObject prefab, string key)
        {
            if (NetworkManager.Singleton == null) return;
            if (handlers.ContainsKey(key)) return;

            var handler = new ZombieNetworkPoolHandler(prefab, this);
            NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, handler);
            handlers[key] = handler;
        }

        protected override void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                foreach (var kvp in handlers)
                {
                    if (prefabLookup.TryGetValue(kvp.Key, out var prefab))
                        NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
                }
            }

            base.OnDestroy();
        }

        private GameObject CreatePooledObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab);
            NetworkObject networkObject = obj.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
                networkObject.Despawn(false);
            obj.SetActive(false);

            return obj;
        }

        public GameObject GetZombie(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            string key = GetPrefabKey(prefab.name);

            if (!pools.ContainsKey(key))
                InitializePool(prefab);

            GameObject zombie = DequeueOrExpand(key, prefab);
            if (zombie == null) return null;

            zombie.SetActive(true);
            if (!ResetZombie(zombie, position, rotation))
            {
                ReturnToPoolInternal(zombie);
                return null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsConnectedClient)
            {
                var netObj = zombie.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                    netObj.Spawn(true);
            }

            if (showDebugLogs)
                GameLog.Info(() => $"[ZombiePool] Got '{key}' from pool");

            return zombie;
        }

        public GameObject GetFromPoolOnly(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            string key = GetPrefabKey(prefab.name);

            if (!pools.ContainsKey(key))
                InitializePool(prefab);

            GameObject zombie = DequeueOrExpand(key, prefab);
            if (zombie == null) return null;

            zombie.SetActive(true);
            if (!ResetZombie(zombie, position, rotation))
            {
                ReturnToPoolInternal(zombie);
                return null;
            }

            return zombie;
        }

        private GameObject DequeueOrExpand(string key, GameObject prefab)
        {
            var pool = pools[key];

            if (pool.Count > 0)
                return pool.Dequeue();

            if (autoExpand)
            {
                if (showDebugLogs)
                    GameLog.Info(() => $"[ZombiePool] Auto-expanded pool '{key}'");
                return CreatePooledObject(prefabLookup[key]);
            }

            GameLog.Warning(() => $"[ZombiePool] Pool empty for '{key}' and autoExpand is off");
            return null;
        }

        public void ReturnZombie(GameObject zombie)
        {
            if (zombie == null) return;

            string key = GetPoolKey(zombie);
            if (string.IsNullOrEmpty(key))
            {
                Destroy(zombie);
                return;
            }

            if (NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsServer
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsConnectedClient
                && NetworkManager.Singleton.NetworkConfig != null
                && NetworkManager.Singleton.NetworkConfig.NetworkTransport != null)
            {
                var netObj = zombie.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(false);
                }
            }

            ReturnToPoolInternal(zombie);
        }

        public void ReturnToPoolInternal(GameObject zombie)
        {
            if (zombie == null) return;

            NetworkObject networkObject = zombie.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                try
                {
                    networkObject.Despawn(false);
                }
                catch (System.Exception ex)
                {
                    // A teardown race or a synthetic editor test object can
                    // lack the full NGO spawn context. Pooling still owns the
                    // GameObject, so continue deactivation instead of letting
                    // a failed network despawn leak the zombie.
                    GameLog.Warning(() => $"[ZombiePool] Despawn skipped for '{zombie.name}': {ex.GetType().Name}");
                }
            }

            string key = GetPoolKey(zombie);
            if (string.IsNullOrEmpty(key))
            {
                Destroy(zombie);
                return;
            }

            zombie.SetActive(false);

            if (pools.ContainsKey(key))
            {
                pools[key].Enqueue(zombie);

                if (showDebugLogs)
                    GameLog.Info(() => $"[ZombiePool] Returned '{key}' to pool");
            }
        }

        private bool ResetZombie(GameObject zombie, Vector3 position, Quaternion rotation)
        {
            NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
            NavMeshHit hit = default;

            if (agent != null && !NavMesh.SamplePosition(position, out hit, 2f, NavMesh.AllAreas))
            {
                GameLog.Warning(() => $"[ZombiePool] NavMesh sample failed for '{zombie.name}' at {position}");
                return false;
            }

            zombie.transform.position = agent != null ? hit.position : position;
            zombie.transform.rotation = rotation;

            if (agent != null)
            {
                agent.enabled = false;
                zombie.transform.position = hit.position;
                agent.enabled = true;

                if (!agent.isOnNavMesh)
                {
                    GameLog.Warning(() => $"[ZombiePool] Agent could not bind to NavMesh after sample for '{zombie.name}'");
                    agent.enabled = false;
                    return false;
                }

                agent.Warp(hit.position);
            }

            Collider col = zombie.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;

            foreach (var resettable in zombie.GetComponents<IPoolResettable>())
                resettable.ResetForPool();

            return true;
        }

        private static string GetPrefabKey(string name)
        {
            return name.Replace("(Clone)", "").Trim();
        }

        private string GetPoolKey(GameObject zombie)
        {
            string cleaned = GetPrefabKey(zombie.name);

            foreach (var key in pools.Keys)
                if (cleaned == key || cleaned.StartsWith(key))
                    return key;

            return null;
        }

        public int GetPoolCount(string prefabName)
        {
            string key = GetPrefabKey(prefabName);
            return pools.ContainsKey(key) ? pools[key].Count : 0;
        }

        public int GetTotalPooled()
        {
            int total = 0;
            foreach (var pool in pools.Values)
                total += pool.Count;
            return total;
        }

        public bool HasPoolFor(GameObject prefab)
        {
            if (prefab == null)
                return false;

            return pools.ContainsKey(GetPrefabKey(prefab.name));
        }
    }
}
