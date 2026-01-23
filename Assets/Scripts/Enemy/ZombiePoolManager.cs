using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
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
            
            poolContainer = new GameObject("ZombiePool").transform;
            poolContainer.SetParent(transform);
        }

        public void InitializePool(GameObject prefab, int size = -1)
        {
            if (prefab == null) return;
            
            string key = prefab.name;
            if (pools.ContainsKey(key)) return;
            
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

        public GameObject GetZombie(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            
            string key = prefab.name;
            
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
                zombie = CreatePooledObject(prefabLookup[key]);
                if (showDebugLogs)
                    Debug.Log($"[ZombiePool] Auto-expanded pool for '{key}'");
            }
            else
            {
                Debug.LogWarning($"[ZombiePool] Pool empty for '{key}'");
                return null;
            }
            
            ResetZombie(zombie, position, rotation);
            zombie.SetActive(true);
            
            return zombie;
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
            string name = zombie.name;
            
            if (name.Contains("(Clone)"))
            foreach (var key in pools.Keys) {
                if (name.StartsWith(key))
                    return key;
            }
            
            return null;
        }

        private void ResetZombie(GameObject zombie, Vector3 position, Quaternion rotation)
        {
            zombie.transform.position = position;
            zombie.transform.rotation = rotation;
            zombie.transform.SetParent(null);
            
            NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                
                zombie.transform.position = position;
                
                agent.enabled = true;
            }
            
            Collider col = zombie.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
            
            EnemyHealth health = zombie.GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.ResetHealth();
            }
            
            EnemyAI ai = zombie.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.ResetAI();
            }
        }

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
