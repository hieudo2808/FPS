using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class NetworkSpawnManager : NetworkBehaviour
    {
        public static NetworkSpawnManager Instance { get; private set; }

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private LayerMask enemySafetyMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask playerSafetyMask = Physics.DefaultRaycastLayers;

        private int nextSpawnIndex;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public bool TryGetNextSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            return TryGetSpawnPose(out position, out rotation, new SpawnRequest(0, 0f, 0f, true));
        }

        public bool TryGetSpawnPose(out Vector3 position, out Quaternion rotation, SpawnRequest request)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (spawnPoints == null || spawnPoints.Length == 0)
                return false;

            if (TryGetSafeSpawnPose(out position, out rotation, request))
                return true;

            if (!request.allowFallback)
                return false;

            Transform selectedSpawn = GetNextSpawnPoint();
            position = selectedSpawn.position;
            rotation = selectedSpawn.rotation;
            return true;
        }

        private bool TryGetSafeSpawnPose(out Vector3 position, out Quaternion rotation, SpawnRequest request)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            int startIndex = spawnPoints.Length > 0
                ? Random.Range(0, spawnPoints.Length)
                : 0;

            for (int offset = 0; offset < spawnPoints.Length; offset++)
            {
                Transform candidate = spawnPoints[(startIndex + offset) % spawnPoints.Length];
                if (candidate == null)
                    continue;

                if (!IsSpawnPointSafe(candidate.position, request))
                    continue;

                position = candidate.position;
                rotation = candidate.rotation;
                return true;
            }

            return false;
        }

        private Transform GetNextSpawnPoint()
        {
            int index = nextSpawnIndex % spawnPoints.Length;
            nextSpawnIndex++;
            return spawnPoints[index];
        }

        private bool IsSpawnPointSafe(Vector3 position, SpawnRequest request)
        {
            if (request.avoidEnemiesRadius > 0f
                && Physics.CheckSphere(position, request.avoidEnemiesRadius, enemySafetyMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (request.avoidPlayersRadius > 0f
                && Physics.CheckSphere(position, request.avoidPlayersRadius, playerSafetyMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return true;
        }
    }
}
