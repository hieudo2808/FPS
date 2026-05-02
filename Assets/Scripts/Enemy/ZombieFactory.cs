using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class ZombieFactory : SceneSingleton<ZombieFactory>
    {
        [Header("Pooling")]
        [SerializeField] private bool usePooling = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        public GameObject SpawnZombie(Vector3 position, Quaternion rotation,
            float hpModifier = 1f, float speedModifier = 1f, float damageModifier = 1f)
        {
            if (IsClientOnly())
            {
                Debug.LogWarning("[ZombieFactory] Client tried to spawn zombie locally. Spawn request ignored because zombies must be server-authoritative.");
                return null;
            }

            ZombieData data = ZombieRegistry.Instance?.GetRandomZombie();
            if (data == null || data.prefab == null)
            {
                Debug.LogError("[ZombieFactory] No zombie prefab available!");
                return null;
            }

            bool canUsePooling = usePooling && !IsNetworkSession() && ZombiePoolManager.Instance != null;

            GameObject zombie = canUsePooling
                ? ZombiePoolManager.Instance.GetZombie(data.prefab, position, rotation)
                : Instantiate(data.prefab, position, rotation);

            if (zombie == null) return null;

            // Network session: Spawn trước khi set stats, nếu không IsServer = false → RPC crash
            if (IsNetworkSession())
            {
                var netObj = zombie.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                {
                    // Validate: zombie PHẢI có NetworkTransform để sync vị trí về client
                    if (zombie.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
                    {
                        zombie.AddComponent<Unity.Netcode.Components.NetworkTransform>();
                        Debug.LogWarning($"[ZombieFactory] '{zombie.name}' thiếu NetworkTransform! Đã tự thêm runtime. " +
                            "HÃY THÊM VÀO PREFAB trong Inspector để fix vĩnh viễn.");
                    }

                    netObj.Spawn(true);
                }
            }

            float playerScale = GetPlayerCountMultiplier();

            EnemyHealth health = zombie.GetComponent<EnemyHealth>();
            if (health != null)
            {
                float finalHP = data.baseHP * hpModifier * playerScale;
                health.SetMaxHealth(finalHP);
            }

            EnemyAI ai = zombie.GetComponent<EnemyAI>();
            if (ai != null)
            {
                float finalSpeed = data.baseSpeed * speedModifier;
                float finalDamage = data.baseDamage * damageModifier * playerScale;
                ai.SetStats(finalSpeed, finalDamage, data.attackRate);

                RubberBandingSystem.Instance?.RegisterZombie(ai);
            }

            if (showDebugLogs)
            {
                Debug.Log($"[ZombieFactory] Spawned {data.displayName} at {position}. " +
                    $"HP: {data.baseHP * hpModifier * playerScale:F0}, " +
                    $"Speed: {data.baseSpeed * speedModifier:F1}, " +
                    $"Pooled: {canUsePooling}, Networked: {IsNetworkSession()}");
            }

            return zombie;
        }

        public GameObject SpawnZombieAtRandomPoint(float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            Vector3 pos = ZombieRegistry.Instance.GetSpawnPosition();
            return SpawnZombie(pos, Quaternion.identity, hpMod, speedMod, damageMod);
        }

        public GameObject SpawnZombieAtSmartPosition(float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            Vector3 pos;

            if (InfluenceMapManager.Instance != null)
            {
                pos = InfluenceMapManager.Instance.GetBestSpawnPosition();

                if (pos == Vector3.zero)
                {
                    pos = ZombieRegistry.Instance.GetSpawnPosition();
                }
            }
            else
            {
                pos = ZombieRegistry.Instance.GetSpawnPosition();
            }

            return SpawnZombie(pos, Quaternion.identity, hpMod, speedMod, damageMod);
        }

        public GameObject SpawnZombieBehindPlayer(int playerIndex, float hpMod = 1f, float speedMod = 1f, float damageMod = 1f)
        {
            Vector3 pos;

            if (InfluenceMapManager.Instance != null)
            {
                pos = InfluenceMapManager.Instance.GetSpawnPositionNearPlayer(playerIndex, behindOnly: true);
            }
            else
            {
                pos = ZombieRegistry.Instance.GetSpawnPosition();
            }

            return SpawnZombie(pos, Quaternion.identity, hpMod, speedMod, damageMod);
        }

        private float GetPlayerCountMultiplier()
        {
            int playerCount = 1;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                playerCount = Mathf.Max(1, NetworkManager.Singleton.ConnectedClientsIds.Count);
            }
            else if (PlayerProfiler.Instance != null)
            {
                playerCount = Mathf.Max(1, PlayerProfiler.Instance.PlayerCount);
            }

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
