using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    [System.Serializable]
    public class ZombieData
    {
        public string displayName = "Zombie";
        public GameObject prefab;
        public float baseHP = 100f;
        public float baseSpeed = 3.5f;
        public float baseDamage = 10f;
        public float attackRate = 1.5f;
        [Range(1, 100)]
        public int spawnWeight = 10;
    }

    public class ZombieRegistry : MonoBehaviour
    {
        public static ZombieRegistry Instance { get; private set; }
        
        [Header("Normal Zombies")]
        [SerializeField] private List<ZombieData> zombieTypes = new List<ZombieData>();
        
        [Header("Spawn Points")]
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
        
        private int totalWeight;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                CalculateTotalWeight();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void CalculateTotalWeight()
        {
            totalWeight = 0;
            foreach (var zombie in zombieTypes)
            {
                totalWeight += zombie.spawnWeight;
            }
        }

        public ZombieData GetRandomZombie()
        {
            if (zombieTypes.Count == 0) return null;
            
            int random = Random.Range(0, totalWeight);
            int cumulative = 0;
            
            foreach (var zombie in zombieTypes)
            {
                cumulative += zombie.spawnWeight;
                if (random < cumulative)
                    return zombie;
            }
            
            return zombieTypes[0];
        }

        public Transform GetRandomSpawnPoint()
        {
            if (spawnPoints.Count == 0) return null;
            return spawnPoints[Random.Range(0, spawnPoints.Count)];
        }

        public Vector3 GetSpawnPosition()
        {
            Transform spawnPoint = GetRandomSpawnPoint();
            if (spawnPoint == null) return Vector3.zero;
            
            Vector3 offset = new Vector3(
                Random.Range(-3f, 3f),
                0f,
                Random.Range(-3f, 3f)
            );
            
            return spawnPoint.position + offset;
        }

        public bool TryGetSpawnPosition(out Vector3 position, System.Func<Vector3, bool> validator = null, int maxAttempts = 16)
        {
            position = Vector3.zero;
            if (spawnPoints.Count == 0) return false;

            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 candidate = GetSpawnPosition();
                if (validator == null || validator(candidate))
                {
                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        public int ZombieTypeCount => zombieTypes.Count;
        public int SpawnPointCount => spawnPoints.Count;
        
        public void AddZombieType(ZombieData data)
        {
            zombieTypes.Add(data);
            CalculateTotalWeight();
        }
    }
}
