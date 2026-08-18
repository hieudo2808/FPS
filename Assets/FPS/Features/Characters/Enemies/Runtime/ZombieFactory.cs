using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public class ZombieFactory : SceneSingleton<ZombieFactory>
    {
        public event System.Action<GameObject> OnZombieSpawned;

        [Header("Pooling")]
        [SerializeField] private bool usePooling = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        public GameObject SpawnZombie(Vector3 position, Quaternion rotation,
            float hpModifier = 1f, float speedModifier = 1f, float damageModifier = 1f)
        {
            if (IsClientOnly())
            {
                GameLog.Warning("[ZombieFactory] Client tried to spawn zombie. Ignored - server-authoritative only.");
                return null;
            }

            ZombieData data = ZombieRegistry.Instance?.GetRandomZombie();
            if (data == null || data.prefab == null)
            {
                GameLog.Error("[ZombieFactory] No zombie prefab available!");
                return null;
            }

            bool canUsePooling = usePooling && ZombiePoolManager.Instance != null;

            if (!TryGetNavMeshSpawnPosition(position, out Vector3 navMeshPosition))
            {
#if UNITY_EDITOR
                // EditMode tests and prefab validation intentionally run
                // without a baked NavMesh. Keep the production server path
                // strict, but allow editor-only factory setup to exercise the
                // same public spawn contract at the requested position.
                if (!Application.isPlaying || NavMesh.CalculateTriangulation().vertices.Length == 0)
                {
                    navMeshPosition = position;
                }
                else
#endif
                {
                GameLog.Warning(() => $"[ZombieFactory] Skipping zombie spawn because no NavMesh was found near {position}");
                return null;
                }
            }

            GameObject zombie = canUsePooling
                ? ZombiePoolManager.Instance.GetZombie(data.prefab, navMeshPosition, rotation)
                : SpawnDirect(data.prefab, navMeshPosition, rotation);

            if (zombie == null) return null;

            if (!canUsePooling && IsNetworkSession())
            {
                var netObj = zombie.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    netObj.Spawn(true);
                }
            }

            float finalHpMod = hpModifier;
            float finalSpeedMod = speedModifier;
            float finalDamageMod = damageModifier;

            if (DifficultyManager.Instance != null)
            {
                DifficultyStats diffStats = DifficultyManager.Instance.GetCurrentStats();
                finalHpMod *= diffStats.hpMultiplier;
                finalSpeedMod *= diffStats.speedMultiplier;
                finalDamageMod *= diffStats.damageMultiplier;
            }

            ApplyStats(zombie, data, finalHpMod, finalSpeedMod, finalDamageMod);
            OnZombieSpawned?.Invoke(zombie);

            if (showDebugLogs)
            {
                GameLog.Info(() => $"[ZombieFactory] Spawned {data.displayName} at {position}. " +
                    $"HP: {data.baseHP * hpModifier:F0}, " +
                    $"Speed: {data.baseSpeed * speedModifier:F1}, " +
                    $"Pooled: {canUsePooling}, Networked: {IsNetworkSession()}");
            }

            return zombie;
        }

        public GameObject SpawnZombieAtRandomPoint(float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            if (TryGetFairRegistrySpawnPosition(out Vector3 pos))
                return SpawnZombie(pos, Quaternion.identity, hpMod, speedMod, damageMod);

            return null;
        }

        public GameObject SpawnZombieAtSmartPosition(float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            if (DirectorSpawnService.Instance != null)
            {
                DirectorSpawnAnchorType requestedTypes = DirectorSpawnAnchorType.Common;
                if (AIDirector.Instance != null)
                    requestedTypes |= AIDirector.Instance.CurrentSpawnAnchorTypes;

                if (DirectorSpawnService.Instance.TryGetSpawnPosition(requestedTypes, out Vector3 anchorPosition))
                    return SpawnZombie(anchorPosition, Quaternion.identity, hpMod, speedMod, damageMod);
            }

            if (InfluenceMapManager.Instance != null)
            {
                if (InfluenceMapManager.Instance.TryGetBestSpawnPosition(out Vector3 smartPos))
                    return SpawnZombie(smartPos, Quaternion.identity, hpMod, speedMod, damageMod);
            }

            if (ZombieRegistry.Instance != null)
            {
                if (TryGetFairRegistrySpawnPosition(out Vector3 registryPos))
                    return SpawnZombie(registryPos, Quaternion.identity, hpMod, speedMod, damageMod);
            }

            return null;
        }

        public GameObject SpawnZombieAtFairPressurePosition(float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            return SpawnZombieAtSmartPosition(hpMod, speedMod, damageMod);
        }

        public GameObject SpawnZombieAtFairPressurePosition(Vector3 preferredPosition, Quaternion rotation,
            float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            if (IsFairPosition(preferredPosition))
                return SpawnZombie(preferredPosition, rotation, hpMod, speedMod, damageMod);

            return SpawnZombieAtSmartPosition(hpMod, speedMod, damageMod);
        }

        public GameObject SpawnZombieAtFairPressurePosition(int playerIndex, float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            if (InfluenceMapManager.Instance != null &&
                InfluenceMapManager.Instance.TryGetFairPressurePositionNearPlayer(playerIndex, out Vector3 pos))
            {
                return SpawnZombie(pos, Quaternion.identity, hpMod, speedMod, damageMod);
            }

            return SpawnZombieAtSmartPosition(hpMod, speedMod, damageMod);
        }

        private bool TryGetFairRegistrySpawnPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (ZombieRegistry.Instance == null) return false;

            System.Func<Vector3, bool> validator = null;
            if (PlayerProfiler.Instance != null && PlayerProfiler.Instance.PlayerCount > 0)
            {
                if (InfluenceMapManager.Instance == null)
                    return false;

                validator = InfluenceMapManager.Instance.IsFairSpawnPoint;
            }

            return ZombieRegistry.Instance.TryGetSpawnPosition(out position, validator);
        }

        private bool IsFairPosition(Vector3 position)
        {
            if (position == Vector3.zero)
                return PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0;

            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
                return true;

            return InfluenceMapManager.Instance != null && InfluenceMapManager.Instance.IsFairSpawnPoint(position);
        }

        private static bool TryGetNavMeshSpawnPosition(Vector3 position, out Vector3 navMeshPosition)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                navMeshPosition = hit.position;
                return true;
            }

            navMeshPosition = position;
            return false;
        }

        private GameObject SpawnDirect(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Instantiate(prefab, position, rotation);
        }

        private void ApplyStats(GameObject zombie, ZombieData data, float hpMod, float speedMod, float damageMod)
        {
            float playerScale = GetPlayerCountMultiplier();

            EnemyHealth health = zombie.GetComponent<EnemyHealth>();
            if (health != null)
                health.SetMaxHealth(data.baseHP * hpMod * playerScale);

            EnemyAI ai = zombie.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.SetStats(
                    data.baseSpeed * speedMod,
                    data.baseDamage * damageMod * playerScale,
                    data.attackRate
                );

                RubberBandingSystem.Instance?.RegisterZombie(ai);
            }
        }

        private float GetPlayerCountMultiplier()
        {
            int playerCount = 1;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                playerCount = Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count);
            else if (PlayerProfiler.Instance != null)
                playerCount = Mathf.Max(1, PlayerProfiler.Instance.PlayerCount);

            return 1f + (playerCount - 1) * 0.35f;
        }

        private static bool IsNetworkSession()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        }

        private static bool IsClientOnly()
        {
            return NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && NetworkManager.Singleton.IsClient
                && !NetworkManager.Singleton.IsServer;
        }
    }
}
