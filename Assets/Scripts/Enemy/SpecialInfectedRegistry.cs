using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    [System.Serializable]
    public class SpecialInfectedData
    {
        public string displayName;
        public SpecialType type;
        public GameObject prefab;
        public bool isImplemented = false;
        public bool allowedInSolo = true;
        public float spawnWeight = 5f;
        public float cooldown = 60f;
        
        [HideInInspector]
        public float lastSpawnTime = -999f;
        
        public bool IsReady => Time.time - lastSpawnTime >= cooldown;
    }

    public class SpecialInfectedRegistry : MonoBehaviour
    {
        public static SpecialInfectedRegistry Instance { get; private set; }
        
        [Header("Special Infected Types")]
        [SerializeField] private List<SpecialInfectedData> specialTypes = new List<SpecialInfectedData>();
        
        [Header("Spawn Settings")]
        [SerializeField] private float minTimeBetweenSpecials = 30f;
        [SerializeField] private int maxSpecialsAlive = 2;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        private float lastSpecialSpawnTime;
        private List<GameObject> aliveSpecials = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeDefaultTypes();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeDefaultTypes()
        {
            if (specialTypes.Count == 0)
            {
                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Stalker",
                    type = SpecialType.Stalker,
                    prefab = null,
                    isImplemented = false,
                    allowedInSolo = true,
                    cooldown = 45f
                });
                
                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Screamer",
                    type = SpecialType.Screamer,
                    prefab = null,
                    isImplemented = false,
                    allowedInSolo = true,
                    cooldown = 60f
                });
                
                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Spitter",
                    type = SpecialType.Spitter,
                    prefab = null,
                    isImplemented = false,
                    allowedInSolo = true,
                    cooldown = 40f
                });
                
                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Tank",
                    type = SpecialType.Tank,
                    prefab = null,
                    isImplemented = false,
                    allowedInSolo = true,
                    cooldown = 120f
                });
            }
        }

        private void Update()
        {
            aliveSpecials.RemoveAll(s => s == null);
        }

        public bool CanSpawnSpecial()
        {
            if (Time.time - lastSpecialSpawnTime < minTimeBetweenSpecials)
                return false;
            
            if (aliveSpecials.Count >= maxSpecialsAlive)
                return false;
            
            return HasImplementedSpecial();
        }

        public bool HasImplementedSpecial()
        {
            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            
            foreach (var special in specialTypes)
            {
                if (!special.isImplemented || special.prefab == null)
                    continue;
                
                if (playerCount == 1 && !special.allowedInSolo)
                    continue;
                
                if (special.IsReady)
                    return true;
            }
            
            return false;
        }

        public SpecialInfectedData GetRandomSpecial()
        {
            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            List<SpecialInfectedData> available = new List<SpecialInfectedData>();
            float totalWeight = 0f;
            
            foreach (var special in specialTypes)
            {
                if (!special.isImplemented || special.prefab == null)
                    continue;
                
                if (playerCount == 1 && !special.allowedInSolo)
                    continue;
                
                if (!special.IsReady)
                    continue;
                
                available.Add(special);
                totalWeight += special.spawnWeight;
            }
            
            if (available.Count == 0)
                return null;
            
            float random = Random.Range(0, totalWeight);
            float cumulative = 0f;
            
            foreach (var special in available)
            {
                cumulative += special.spawnWeight;
                if (random < cumulative)
                    return special;
            }
            
            return available[0];
        }

        public GameObject SpawnSpecial(Vector3 position)
        {
            var data = GetRandomSpecial();
            if (data == null)
            {
                if (showDebugLogs)
                    Debug.Log("[SpecialRegistry] No special available to spawn");
                return null;
            }
            
            GameObject special = Instantiate(data.prefab, position, Quaternion.identity);
            data.lastSpawnTime = Time.time;
            lastSpecialSpawnTime = Time.time;
            
            aliveSpecials.Add(special);
            
            if (showDebugLogs)
                Debug.Log($"[SpecialRegistry] Spawned {data.displayName} at {position}");
            
            return special;
        }

        public int AliveSpecialCount => aliveSpecials.Count;
        
        public void RegisterSpecialPrefab(SpecialType type, GameObject prefab)
        {
            foreach (var special in specialTypes)
            {
                if (special.type == type)
                {
                    special.prefab = prefab;
                    special.isImplemented = true;
                    Debug.Log($"[SpecialRegistry] Registered {type} prefab");
                    return;
                }
            }
        }
    }
}
