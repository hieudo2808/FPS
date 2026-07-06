using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class NetworkSpawnManager : NetworkBehaviour
    {
        public static NetworkSpawnManager Instance { get; private set; }

        [Header("Spawn Points")]
        [SerializeField] private Transform[] spawnPoints;

        private int nextSpawnIndex;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public bool TryGetNextSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (spawnPoints == null || spawnPoints.Length == 0)
                return false;

            int index = nextSpawnIndex % spawnPoints.Length;
            nextSpawnIndex++;

            Transform selectedSpawn = spawnPoints[index];
            position = selectedSpawn.position;
            rotation = selectedSpawn.rotation;
            return true;
        }
    }
}
