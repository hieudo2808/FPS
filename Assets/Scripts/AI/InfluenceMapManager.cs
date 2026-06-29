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
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        
        [SerializeField] private float updateInterval = 0.1f;
        private float lastUpdateTime;

        private float[,] influenceGrid;
        private int gridWidth;
        private int gridHeight;
        private List<Vector3> cachedNavMeshPoints;

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
            BakeNavMeshCache();
        }

        /// <summary>
        /// Goi mot lan tai Start() de cache truoc toan bo cac o grid co vi tri hop le tren NavMesh.
        /// Tranh goi NavMesh.SamplePosition() dong trong GetBestSpawnPosition().
        /// </summary>
        private void BakeNavMeshCache()
        {
            cachedNavMeshPoints = new List<Vector3>();
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
                    
                    if (angle > 120f && distance > 10f && distance < 30f)
                    {
                        influence += 30f;
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
            // Fallback: neu cache chua duoc bake (e.g. goi truoc Start)
            if (cachedNavMeshPoints == null || cachedNavMeshPoints.Count == 0)
                return Vector3.zero;

            float bestScore = float.MinValue;
            List<Vector3> candidates = new List<Vector3>();

            foreach (Vector3 navPos in cachedNavMeshPoints)
            {
                // Doi chieu vi tri cache voi o grid tuong ung de lay influence score
                Vector2Int cell = WorldToGrid(navPos);
                float score = influenceGrid[cell.x, cell.y];

                if (score > bestScore - 10f)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        candidates.Clear();
                    }
                    candidates.Add(navPos);
                }
            }

            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)];

            return Vector3.zero;
        }

        public Vector3 GetSpawnPositionNearPlayer(int playerIndex, bool behindOnly = false)
        {
            var profile = PlayerProfiler.Instance?.GetProfile(playerIndex);
            if (profile?.playerTransform == null)
                return GetBestSpawnPosition();
            
            Vector3 playerPos = profile.playerTransform.position;
            Vector3 lookDir = profile.lookDirection;
            
            List<Vector3> candidates = new List<Vector3>();
            
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f - 180f) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Sin(angle) * 20f,
                    0f,
                    Mathf.Cos(angle) * 20f
                );
                
                offset = Quaternion.LookRotation(lookDir) * offset;
                Vector3 testPos = playerPos + offset;
                
                if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    Vector3 toPos = (hit.position - playerPos).normalized;
                    float dot = Vector3.Dot(lookDir, toPos);
                    
                    if (!behindOnly || dot < -0.3f)
                    {
                        candidates.Add(hit.position);
                    }
                }
            }
            
            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)];
            
            return GetBestSpawnPosition();
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
