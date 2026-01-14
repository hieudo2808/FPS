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
        
        private float[,] influenceGrid;
        private int gridWidth;
        private int gridHeight;

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
            UpdateInfluenceMap();
        }

        private void InitializeGrid()
        {
            gridWidth = Mathf.CeilToInt(mapSize.x / cellSize);
            gridHeight = Mathf.CeilToInt(mapSize.y / cellSize);
            influenceGrid = new float[gridWidth, gridHeight];
        }

        private void UpdateInfluenceMap()
        {
            // Reset grid
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    influenceGrid[x, y] = 0f;
                }
            }
            
            if (PlayerProfiler.Instance == null) return;
            
            // Apply influence from each player
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
                    
                    // Skip if too far
                    if (distance > fovInfluenceRadius * 1.5f) continue;
                    
                    // Base influence (negative = dangerous to spawn)
                    float influence = 0f;
                    
                    // Close to player = very dangerous
                    if (distance < playerInfluenceRadius)
                    {
                        influence -= 100f * (1f - distance / playerInfluenceRadius);
                    }
                    
                    // In FOV = dangerous
                    Vector3 toCell = (cellCenter - playerPos).normalized;
                    float angle = Vector3.Angle(lookDir, toCell);
                    
                    if (angle < fovInfluenceAngle / 2f && distance < fovInfluenceRadius)
                    {
                        influence -= 50f * (1f - angle / (fovInfluenceAngle / 2f));
                    }
                    
                    // Behind player = good spawn spot (blind spot)
                    if (angle > 120f && distance > 10f && distance < 30f)
                    {
                        influence += 30f;
                    }
                    
                    influenceGrid[x, y] += influence;
                }
            }
            
            // Bonus for isolated players
            if (profile.isIsolated)
            {
                Vector2Int cell = WorldToGrid(playerPos);
                ApplyBonus(cell.x, cell.y, 5, 20f); // Radius 5 cells, +20 bonus
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
            float bestScore = float.MinValue;
            Vector3 bestPos = Vector3.zero;
            List<Vector3> candidates = new List<Vector3>();
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    float score = influenceGrid[x, y];
                    
                    if (score > bestScore - 10f) // Top candidates
                    {
                        Vector3 pos = GetCellWorldPosition(x, y);
                        
                        // Verify NavMesh
                        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                        {
                            if (score > bestScore)
                            {
                                bestScore = score;
                                candidates.Clear();
                            }
                            candidates.Add(hit.position);
                        }
                    }
                }
            }
            
            // Random from top candidates for variety
            if (candidates.Count > 0)
            {
                bestPos = candidates[Random.Range(0, candidates.Count)];
            }
            
            return bestPos;
        }

        public Vector3 GetSpawnPositionNearPlayer(int playerIndex, bool behindOnly = false)
        {
            var profile = PlayerProfiler.Instance?.GetProfile(playerIndex);
            if (profile?.playerTransform == null)
                return GetBestSpawnPosition();
            
            Vector3 playerPos = profile.playerTransform.position;
            Vector3 lookDir = profile.lookDirection;
            
            // Generate positions behind player
            List<Vector3> candidates = new List<Vector3>();
            
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f - 180f) * Mathf.Deg2Rad; // Behind to sides
                Vector3 offset = new Vector3(
                    Mathf.Sin(angle) * 20f,
                    0f,
                    Mathf.Cos(angle) * 20f
                );
                
                // Rotate by player's look direction
                offset = Quaternion.LookRotation(lookDir) * offset;
                Vector3 testPos = playerPos + offset;
                
                if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    // Check if behind player
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
