using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public class InfluenceMapManager : MonoBehaviour
    {
        public static InfluenceMapManager Instance { get; private set; }
        
        [Header("Grid Settings")]
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        [SerializeField] private Vector2 mapSize = new Vector2(100f, 130f);
        [SerializeField] private float cellSize = 5f;
        
        [Header("Influence Settings")]
        [SerializeField] private float playerInfluenceRadius = 15f;
        [SerializeField] private float fovInfluenceAngle = 90f;
        [SerializeField] private float fovInfluenceRadius = 20f;

        [Header("Fair Spawn Constraints")]
        [SerializeField] private float minSpawnDistanceFromPlayer = 28f;
        [SerializeField] private float maxSpawnDistanceFromPlayer = 95f;
        [SerializeField] private float avoidVisibleSpawnDistance = 45f;
        [SerializeField] private float visibleDotThreshold = 0.15f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        
        [SerializeField] private float updateInterval = 0.1f;
        private float lastUpdateTime;

        private float[,] influenceGrid;
        private int gridWidth;
        private int gridHeight;
        private List<Vector3> cachedNavMeshPoints;
        private readonly List<Vector3> bestSpawnCandidates = new List<Vector3>();
        private readonly List<Vector3> fairPressureSpawnCandidates = new List<Vector3>();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            InitializeGrid();
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = Time.time;
                UpdateInfluenceMap();
            }
        }

        private void InitializeGrid()
        {
            gridWidth = Mathf.CeilToInt(mapSize.x / cellSize);
            gridHeight = Mathf.CeilToInt(mapSize.y / cellSize);
            influenceGrid = new float[gridWidth, gridHeight];
            StartCoroutine(BakeNavMeshCache());
        }

        /// <summary>
        /// Goi mot lan tai Start() de cache truoc toan bo cac o grid co vi tri hop le tren NavMesh.
        /// Tranh goi NavMesh.SamplePosition() dong trong GetBestSpawnPosition().
        /// Su dung Coroutine de khong block Main Thread.
        /// </summary>
        private System.Collections.IEnumerator BakeNavMeshCache()
        {
            cachedNavMeshPoints = new List<Vector3>();
            int iterations = 0;
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3 pos = GetCellWorldPosition(x, y);
                    if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    {
                        // Luu kem toa do grid de tra cuu influence score nhanh
                        cachedNavMeshPoints.Add(hit.position);
                    }
                    
                    iterations++;
                    if (iterations >= 50)
                    {
                        iterations = 0;
                        yield return null; // wait for next frame
                    }
                }
            }
        }

        private void UpdateInfluenceMap()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    influenceGrid[x, y] = 0f;
                }
            }
            
            if (PlayerProfiler.Instance == null) return;
            
            foreach (var profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile.playerTransform == null) continue;
                ApplyPlayerInfluence(profile);
            }
        }

        private void ApplyPlayerInfluence(PlayerProfile profile)
        {
            Vector3 playerPos = profile.playerTransform.position;
            Vector3 lookDir = profile.lookDirection;
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3 cellCenter = GetCellWorldPosition(x, y);
                    float distance = Vector3.Distance(playerPos, cellCenter);
                    
                    if (distance > fovInfluenceRadius * 1.5f) continue;
                    
                    float influence = 0f;
                    
                    if (distance < playerInfluenceRadius)
                    {
                        influence -= 100f * (1f - distance / playerInfluenceRadius);
                    }
                    
                    Vector3 toCell = (cellCenter - playerPos).normalized;
                    float angle = Vector3.Angle(lookDir, toCell);
                    
                    if (angle < fovInfluenceAngle / 2f && distance < fovInfluenceRadius)
                    {
                        influence -= 50f * (1f - angle / (fovInfluenceAngle / 2f));
                    }
                    
                    influenceGrid[x, y] += influence;
                }
            }
            
            if (profile.isIsolated)
            {
                Vector2Int cell = WorldToGrid(playerPos);
                ApplyBonus(cell.x, cell.y, 5, 20f);
            }
        }

        private void ApplyBonus(int centerX, int centerY, int radius, float bonus)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    
                    if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= radius)
                        {
                            influenceGrid[x, y] += bonus * (1f - dist / radius);
                        }
                    }
                }
            }
        }

        public Vector3 GetBestSpawnPosition()
        {
            return TryGetBestSpawnPosition(out Vector3 position) ? position : Vector3.zero;
        }

        public bool TryGetBestSpawnPosition(out Vector3 position)
        {
            position = Vector3.zero;

            // Fallback: neu cache chua duoc bake (e.g. goi truoc Start)
            if (cachedNavMeshPoints == null || cachedNavMeshPoints.Count == 0)
                return false;

            float bestScore = float.MinValue;
            bestSpawnCandidates.Clear();

            foreach (Vector3 navPos in cachedNavMeshPoints)
            {
                if (!IsFairSpawnPoint(navPos))
                {
                    continue;
                }

                // Doi chieu vi tri cache voi o grid tuong ung de lay influence score
                Vector2Int cell = WorldToGrid(navPos);
                float score = influenceGrid[cell.x, cell.y];

                if (score > bestScore - 10f)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSpawnCandidates.Clear();
                    }
                    bestSpawnCandidates.Add(navPos);
                }
            }

            if (bestSpawnCandidates.Count > 0)
            {
                position = bestSpawnCandidates[Random.Range(0, bestSpawnCandidates.Count)];
                return true;
            }

            return false;
        }

        public bool IsFairSpawnPoint(Vector3 position)
        {
            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
            {
                return true;
            }

            if (position == Vector3.zero)
            {
                return false;
            }

            bool withinUsefulRange = false;

            foreach (var profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile.playerTransform == null) continue;

                Vector3 toPos = position - profile.playerTransform.position;
                float distance = toPos.magnitude;

                if (distance < minSpawnDistanceFromPlayer)
                {
                    return false;
                }

                if (distance <= maxSpawnDistanceFromPlayer)
                {
                    withinUsefulRange = true;
                }

                Vector3 direction = distance > 0.001f ? toPos / distance : Vector3.zero;
                float dot = Vector3.Dot(profile.lookDirection, direction);
                if (dot > visibleDotThreshold && distance < avoidVisibleSpawnDistance)
                {
                    return false;
                }
            }

            return withinUsefulRange;
        }

        public bool TryGetFairPressurePositionNearPlayer(int playerIndex, out Vector3 position)
        {
            position = Vector3.zero;
            var profile = PlayerProfiler.Instance?.GetProfile(playerIndex);
            if (profile?.playerTransform == null)
                return TryGetBestSpawnPosition(out position);

            Vector3 playerPos = profile.playerTransform.position;
            Vector3 lookDir = profile.lookDirection;
            if (lookDir.sqrMagnitude < 0.001f)
                lookDir = profile.playerTransform.forward;
            float minPressureDistance = minSpawnDistanceFromPlayer + 2f;
            float maxPressureDistance = Mathf.Min(maxSpawnDistanceFromPlayer, minPressureDistance + 20f);

            fairPressureSpawnCandidates.Clear();

            if (cachedNavMeshPoints != null)
            {
                float minSqr = minPressureDistance * minPressureDistance;
                float maxSqr = maxPressureDistance * maxPressureDistance;

                foreach (Vector3 navPos in cachedNavMeshPoints)
                {
                    float distanceSqr = (navPos - playerPos).sqrMagnitude;
                    if (distanceSqr < minSqr || distanceSqr > maxSqr)
                        continue;

                    if (IsFairSpawnPoint(navPos))
                        fairPressureSpawnCandidates.Add(navPos);
                }
            }

            for (int i = 0; i < 12; i++)
            {
                float angle = (i * 30f - 180f) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(minPressureDistance, maxPressureDistance, (i % 3) / 2f);
                Vector3 offset = new Vector3(
                    Mathf.Sin(angle) * radius,
                    0f,
                    Mathf.Cos(angle) * radius
                );

                offset = Quaternion.LookRotation(lookDir) * offset;
                Vector3 testPos = playerPos + offset;

                if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    if (IsFairSpawnPoint(hit.position))
                        fairPressureSpawnCandidates.Add(hit.position);
                }
            }

            if (fairPressureSpawnCandidates.Count > 0)
            {
                position = fairPressureSpawnCandidates[Random.Range(0, fairPressureSpawnCandidates.Count)];
                return true;
            }

            return TryGetBestSpawnPosition(out position);
        }

        private Vector3 GetCellWorldPosition(int x, int y)
        {
            return new Vector3(
                mapCenter.x - mapSize.x / 2f + (x + 0.5f) * cellSize,
                mapCenter.y,
                mapCenter.z - mapSize.y / 2f + (y + 0.5f) * cellSize
            );
        }

        private Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - mapCenter.x + mapSize.x / 2f) / cellSize);
            int y = Mathf.FloorToInt((worldPos.z - mapCenter.z + mapSize.y / 2f) / cellSize);
            return new Vector2Int(
                Mathf.Clamp(x, 0, gridWidth - 1),
                Mathf.Clamp(y, 0, gridHeight - 1)
            );
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || influenceGrid == null) return;
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Vector3 pos = GetCellWorldPosition(x, y);
                    float influence = influenceGrid[x, y];
                    
                    if (influence > 0)
                        Gizmos.color = new Color(0, 1, 0, 0.3f);
                    else if (influence < -50)
                        Gizmos.color = new Color(1, 0, 0, 0.3f);
                    else
                        continue;
                    
                    Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.9f);
                }
            }
        }
    }
}
