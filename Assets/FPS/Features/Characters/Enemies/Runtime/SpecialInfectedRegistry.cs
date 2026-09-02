using System.Collections.Generic;
using Unity.Netcode;
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
        private readonly List<PlayerTeamHealthSnapshot> teamHealthSnapshots = new List<PlayerTeamHealthSnapshot>(4);
        private float lastAliveCleanupTime = -999f;
        [SerializeField] private float aliveCleanupInterval = 0.5f;

        private void Awake()
        {
            // Scene-authored registries keep their serialized list. Runtime-created
            // registries (and legacy unit fixtures) still need the canonical entries
            // so explicit promotion APIs can operate without silently doing nothing.
            InitializeDefaultTypes();

            if (Instance == null)
            {
                Instance = this;
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
                    spawnWeight = 2f,
                    cooldown = 120f
                });

                specialTypes.Add(new SpecialInfectedData
                {
                    displayName = "Infector",
                    type = SpecialType.Infector,
                    prefab = null,
                    implementationState = SpecialImplementationState.FrameworkOnly,
                    allowedInSolo = true,
                    spawnWeight = 4f,
                    cooldown = 45f
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

        public bool CanSpawnSpecial(GamePhase phase = GamePhase.PEAK)
        {
            if (Time.time - lastSpecialSpawnTime < minTimeBetweenSpecials)
                return false;

            if (aliveSpecials.Count >= maxSpecialsAlive)
                return false;

            return GetRemainingBudget(phase) > 0 && HasImplementedSpecial(phase);
        }

        public bool HasImplementedSpecial(GamePhase phase = GamePhase.PEAK)
        {
            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            
            foreach (var special in specialTypes)
            {
                if (!IsPlayableSpecial(special))
                    continue;
                
                if (playerCount == 1 && !special.allowedInSolo)
                    continue;
                
                if (special.IsReady)
                {
                    if (GetSpecialCost(special.type) <= GetRemainingBudget(phase)
                        && !HasReachedTypeCap(special.type)
                        && PassesSpawnRules(special))
                        return true;
                }
            }
            
            return false;
        }

        public SpecialInfectedData GetRandomSpecial(GamePhase phase = GamePhase.PEAK)
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

                if (GetSpecialCost(special.type) > GetRemainingBudget(phase)
                    || HasReachedTypeCap(special.type))
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

        public GameObject SpawnSpecial(Vector3 position, GamePhase phase = GamePhase.PEAK)
        {
            var data = GetRandomSpecial(phase);
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

        public static int GetPhaseBudget(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.BUILD => 2,
                GamePhase.PEAK => 4,
                _ => 0
            };
        }

        public static int GetSpecialCost(SpecialType type)
        {
            return type switch
            {
                SpecialType.Screamer => 1,
                SpecialType.Infector => 2,
                SpecialType.Tank => 3,
                _ => 1
            };
        }

        public int GetRemainingBudget(GamePhase phase)
        {
            int spent = 0;
            for (int i = aliveSpecials.Count - 1; i >= 0; i--)
            {
                GameObject special = aliveSpecials[i];
                if (special == null)
                {
                    aliveSpecials.RemoveAt(i);
                    continue;
                }

                SpecialInfectedBase brain = special.GetComponent<SpecialInfectedBase>();
                if (brain != null)
                    spent += GetSpecialCost(brain.Type);
            }
            return Mathf.Max(0, GetPhaseBudget(phase) - spent);
        }

        private bool HasReachedTypeCap(SpecialType type)
        {
            int cap = type == SpecialType.Infector ? 1 : int.MaxValue;
            int count = 0;
            for (int i = 0; i < aliveSpecials.Count; i++)
            {
                SpecialInfectedBase brain = aliveSpecials[i] != null
                    ? aliveSpecials[i].GetComponent<SpecialInfectedBase>()
                    : null;
                if (brain != null && brain.Type == type && ++count >= cap)
                    return true;
            }
            return false;
        }

        private bool PassesSpawnRules(SpecialInfectedData special)
        {
            SpecialInfectedBase specialBrain = special.prefab != null
                ? special.prefab.GetComponent<SpecialInfectedBase>()
                : null;

            if (specialBrain == null)
                return true;

            IReadOnlyList<PlayerProfile> profiles = PlayerProfiler.Instance != null
                ? PlayerProfiler.Instance.AllProfiles
                : null;
            BuildTeamHealthSnapshot(profiles);
            return specialBrain.ShouldSpawnForTeam(profiles, teamHealthSnapshots);
        }

        private void BuildTeamHealthSnapshot(IReadOnlyList<PlayerProfile> profiles)
        {
            teamHealthSnapshots.Clear();
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                foreach (NetworkClient client in manager.ConnectedClientsList)
                {
                    PlayerHealth health = client.PlayerObject != null
                        ? client.PlayerObject.GetComponent<PlayerHealth>()
                        : null;
                    if (health == null)
                    {
                        teamHealthSnapshots.Clear();
                        return;
                    }

                    bool isDownOrDead = health.IsDead || health.LifeState != PlayerLifeState.Alive;
                    teamHealthSnapshots.Add(new PlayerTeamHealthSnapshot(
                        health.CurrentHealth,
                        health.MaxHealth,
                        isDownOrDead));
                }

                return;
            }

            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Count; i++)
            {
                PlayerProfile profile = profiles[i];
                PlayerHealth health = profile?.cachedHealth;
                if (profile?.playerTransform == null || health == null)
                {
                    teamHealthSnapshots.Clear();
                    return;
                }

                bool isDownOrDead = health.IsDead || health.LifeState != PlayerLifeState.Alive;
                teamHealthSnapshots.Add(new PlayerTeamHealthSnapshot(
                    health.CurrentHealth,
                    health.MaxHealth,
                    isDownOrDead));
            }
        }

        private static bool IsPlayableSpecial(SpecialInfectedData special)
        {
            return special != null
                && special.IsPlayable
                && special.prefab != null;
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

            switch (type)
            {
                case SpecialType.Screamer:
                    return prefab.GetComponent<SI_Screamer>() != null;
                case SpecialType.Tank:
                    return prefab.GetComponent<SI_Tank>() != null;
                case SpecialType.Infector:
                    return prefab.GetComponent<SI_Infector>() != null;
                default:
                    return false;
            }
        }
    }
}
