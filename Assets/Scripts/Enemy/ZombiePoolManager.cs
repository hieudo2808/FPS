using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    /// <summary>
    /// ZombiePoolManager - Object pooling for multiple zombie types
    /// Supports spawning, recycling, and resetting zombies
    /// </summary>
    public class ZombiePoolManager : Singleton<ZombiePoolManager>
    {
        [Header("Pool Settings")]
        [SerializeField] private int poolSizePerType = 20;
        [SerializeField] private bool autoExpand = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        
        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();
        private Transform poolContainer;

        protected override void Awake()
        {
            base.Awake();
            
            // Create container for pooled objects
            poolContainer = new GameObject("ZombiePool").transform;
            poolContainer.SetParent(transform);
        }

        /// <summary>
        /// Initialize pool for a specific zombie prefab
        /// Called by ZombieRegistry during setup
        /// </summary>
        public void InitializePool(GameObject prefab, int size = -1)
        {
            if (prefab == null) return;
            
            string key = prefab.name;
            if (pools.ContainsKey(key)) return; // Already initialized
            
            int poolSize = size > 0 ? size : poolSizePerType;
            Queue<GameObject> pool = new Queue<GameObject>();
            
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = CreatePooledObject(prefab);
                pool.Enqueue(obj);
            }
            
            pools[key] = pool;
            prefabLookup[key] = prefab;
            
            if (showDebugLogs)
                Debug.Log($"[ZombiePool] Initialized pool for '{key}' with {poolSize} objects");
        }

        private GameObject CreatePooledObject(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, poolContainer);
            obj.SetActive(false);
            return obj;
        }

        /// <summary>
        /// Get a zombie from pool
        /// </summary>
        public GameObject GetZombie(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            
            string key = prefab.name;
            
            // Initialize pool if not exists
            if (!pools.ContainsKey(key))
            {
                InitializePool(prefab);
            }
            
            Queue<GameObject> pool = pools[key];
            GameObject zombie;
            
            if (pool.Count > 0)
            {
                zombie = pool.Dequeue();
            }
            else if (autoExpand)
            {
                // Auto expand pool
                zombie = CreatePooledObject(prefabLookup[key]);
                if (showDebugLogs)
                    Debug.Log($"[ZombiePool] Auto-expanded pool for '{key}'");
            }
            else
            {
                Debug.LogWarning($"[ZombiePool] Pool empty for '{key}'");
                return null;
            }
            
            // Reset and activate
            ResetZombie(zombie, position, rotation);
            zombie.SetActive(true);
            
            return zombie;
        }

        /// <summary>
        /// Return zombie to pool
        /// </summary>
        public void ReturnZombie(GameObject zombie)
        {
            if (zombie == null) return;
            
            // Find which pool this belongs to
            string key = GetPoolKey(zombie);
            if (string.IsNullOrEmpty(key))
            {
                // Unknown zombie, just destroy
                Destroy(zombie);
                return;
            }
            
            zombie.SetActive(false);
            zombie.transform.SetParent(poolContainer);
            
            if (pools.ContainsKey(key))
            {
                pools[key].Enqueue(zombie);
                
                if (showDebugLogs)
                    Debug.Log($"[ZombiePool] Returned zombie to pool '{key}'");
            }
        }

        private string GetPoolKey(GameObject zombie)
        {
            // Extract original prefab name (remove "(Clone)" and instance numbers)
            string name = zombie.name;
            
            // Remove "(Clone)" suffix
            if (name.Contains("(Clone)"))
                name = name.Replace("(Clone)", "").Trim();
                
            // Check if this key exists
            foreach (var key in pools.Keys)
            {
                if (name.StartsWith(key))
                    return key;
            }
            
            return null;
        }

        private void ResetZombie(GameObject zombie, Vector3 position, Quaternion rotation)
        {
            // Reset transform first
            zombie.transform.position = position;
            zombie.transform.rotation = rotation;
            zombie.transform.SetParent(null);
            
            // Reset NavMeshAgent properly
            NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                // Disable first
                agent.enabled = false;
                
                // Set position while disabled
                zombie.transform.position = position;
                
                // Enable and warp
                agent.enabled = true;
                agent.Warp(position);
            }
            
            // Re-enable collider BEFORE activating
            Collider col = zombie.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
            
            // Reset EnemyHealth
            EnemyHealth health = zombie.GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.ResetHealth();
            }
            
            // Reset EnemyAI (doesn't touch NavMeshAgent - we handle it here)
            EnemyAI ai = zombie.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.ResetAI();
            }
        }

        /// <summary>
        /// Get pool statistics
        /// </summary>
        public int GetPoolCount(string prefabName)
        {
            if (pools.ContainsKey(prefabName))
                return pools[prefabName].Count;
            return 0;
        }

        public int GetTotalPooled()
        {
            int total = 0;
            foreach (var pool in pools.Values)
                total += pool.Count;
            return total;
        }
    }
}
