using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class NetworkSpawnManager : NetworkBehaviour
    {
        public static NetworkSpawnManager Instance { get; private set; }

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        private int nextSpawnIndex = 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.Singleton.SceneManager.OnSceneEvent += HandleSceneEvent;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                TeleportPlayerToSpawnPoint(client.PlayerObject);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= HandleSceneEvent;
            }
        }

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete)
            {
                ulong clientId = sceneEvent.ClientId;
                
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
                {
                    TeleportPlayerToSpawnPoint(client.PlayerObject);
                }
            }
        }

        private void TeleportPlayerToSpawnPoint(NetworkObject playerObj)
        {
            if (playerObj == null || spawnPoints.Length == 0) return;

            int index = nextSpawnIndex % spawnPoints.Length;
            nextSpawnIndex++;

            Transform selectedSpawn = spawnPoints[index];

            var characterController = playerObj.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            playerObj.transform.position = selectedSpawn.position;
            playerObj.transform.rotation = selectedSpawn.rotation;

            if (characterController != null) characterController.enabled = true;

            Debug.Log($"[SpawnManager] Đã đưa Player {playerObj.OwnerClientId} tới vị trí {selectedSpawn.name}");
        }
    }
}