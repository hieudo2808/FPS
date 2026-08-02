using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    [System.Serializable]
    public enum SpecialImplementationState
    {
        FrameworkOnly,
        Playable
    }

    [System.Serializable]
    public class SpecialInfectedData
    {
        public string displayName;
        public SpecialType type;
        public GameObject prefab;
        public SpecialImplementationState implementationState = SpecialImplementationState.FrameworkOnly;
        // Legacy serialized scenes may still carry this; runtime spawn eligibility uses implementationState only.
        [HideInInspector]
        public bool isImplemented = false;
        public bool allowedInSolo = true;
        public float spawnWeight = 5f;
        public float cooldown = 60f;

        [HideInInspector]
        public float lastSpawnTime = -999f;

        public bool IsReady => Time.time - lastSpawnTime >= cooldown;
        public bool IsPlayable => implementationState == SpecialImplementationState.Playable;
    }

    public class SpecialInfectedRegistry : MonoBehaviour
    {
        public static SpecialInfectedRegistry Instance { get; private set; }
        public event System.Action<GameObject> OnSpecialSpawned;

        [Header("Special Infected Types")]
        [SerializeField] private List<SpecialInfectedData> specialTypes = new List<SpecialInfectedData>();

        [Header("Spawn Settings")]
        [SerializeField] private float minTimeBetweenSpecials = 30f;
        [SerializeField] private int maxSpecialsAlive = 2;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private float lastSpecialSpawnTime;
        private List<GameObject> aliveSpecials = new List<GameObject>();
        private float lastAliveCleanupTime = -999f;
        [SerializeField] private float aliveCleanupInterval = 0.5f;

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
                    implementationState = SpecialImplementationState.FrameworkOnly,
                    allowedInSolo = true,
                    cooldown = 45f
                });

                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Screamer",
                    type = SpecialType.Screamer,
                    prefab = null,
                    implementationState = SpecialImplementationState.FrameworkOnly,
                    allowedInSolo = true,
                    cooldown = 60f
                });

                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Spitter",
                    type = SpecialType.Spitter,
                    prefab = null,
                    implementationState = SpecialImplementationState.FrameworkOnly,
                    allowedInSolo = true,
                    cooldown = 40f
                });

                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Tank",
                    type = SpecialType.Tank,
                    prefab = null,
                    implementationState = SpecialImplementationState.FrameworkOnly,
                    allowedInSolo = true,
                    cooldown = 120f
                });
            }
        }

        private void Update()
        {
            if (Time.time - lastAliveCleanupTime < aliveCleanupInterval)
                return;

            lastAliveCleanupTime = Time.time;
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
                if (!IsPlayableSpecial(special))
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
                if (!IsPlayableSpecial(special))
                    continue;
                
                if (playerCount == 1 && !special.allowedInSolo)
                    continue;
                
                if (!special.IsReady)
                    continue;

                if (!PassesSpawnRules(special))
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
                    GameLog.Info("[SpecialRegistry] No special available to spawn");
                return null;
            }
            
            GameObject special = Instantiate(data.prefab, position, Quaternion.identity);
            data.lastSpawnTime = Time.time;
            lastSpecialSpawnTime = Time.time;
            
            aliveSpecials.Add(special);
            OnSpecialSpawned?.Invoke(special);
            
            if (showDebugLogs)
                GameLog.Info(() => $"[SpecialRegistry] Spawned {data.displayName} at {position}");
            
            return special;
        }

        public int AliveSpecialCount => aliveSpecials.Count;

        private bool PassesSpawnRules(SpecialInfectedData special)
        {
            SpecialInfectedBase specialBrain = special.prefab != null
                ? special.prefab.GetComponent<SpecialInfectedBase>()
                : null;

            if (specialBrain == null || PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
                return true;

            foreach (var profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile?.playerTransform == null) continue;
                if (specialBrain.ShouldSpawn(profile))
                    return true;
            }

            return false;
        }

        private static bool IsPlayableSpecial(SpecialInfectedData special)
        {
            return special != null
                && special.IsPlayable
                && special.prefab != null
                && special.type == SpecialType.Screamer;
        }

        public void RegisterSpecialPrefab(SpecialType type, GameObject prefab)
        {
            foreach (var special in specialTypes)
            {
                if (special.type == type)
                {
                    special.prefab = prefab;
                    special.implementationState = SpecialImplementationState.FrameworkOnly;
                    special.isImplemented = false;
                    GameLog.Info(() => $"[SpecialRegistry] Registered {type} prefab");
                    return;
                }
            }
        }

        public bool RegisterPlayableSpecialPrefab(SpecialType type, GameObject prefab)
        {
            if (!CanPromoteToPlayable(type, prefab))
                return false;

            foreach (var special in specialTypes)
            {
                if (special.type == type)
                {
                    special.prefab = prefab;
                    special.implementationState = SpecialImplementationState.Playable;
                    special.isImplemented = true;
                    GameLog.Info(() => $"[SpecialRegistry] Registered playable {type} prefab");
                    return true;
                }
            }

            return false;
        }

        private static bool CanPromoteToPlayable(SpecialType type, GameObject prefab)
        {
            if (prefab == null)
                return false;

            return type == SpecialType.Screamer && prefab.GetComponent<SI_Screamer>() != null;
        }
    }
}
