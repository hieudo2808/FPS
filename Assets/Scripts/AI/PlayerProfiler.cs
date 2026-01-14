using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    [System.Serializable]
    public class PlayerProfile
    {
        // Player reference
        public Transform playerTransform;
        public int playerIndex;

        public Transform cameraTransform;
        
        // Position tracking
        public List<Vector3> positionHistory = new List<Vector3>();
        public Vector3 mostFrequentPosition;
        public bool isCamping;
        public float campingDuration;
        
        // Combat stats
        public int totalKills;
        public int headshotKills;
        public float headshotRatio;
        public float avgKillDistance;
        public float avgReactionTime;
        
        // Current state
        public float currentHealth;
        public float currentAmmoPercent;
        public bool isReloading;
        public bool isMoving;
        public Vector3 lookDirection;
        
        // Team metrics
        public float distanceToNearestAlly;
        public bool isIsolated;
    }

    public class PlayerProfiler : MonoBehaviour
    {
        public static PlayerProfiler Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private float positionUpdateInterval = 0.5f;
        [SerializeField] private int positionHistorySize = 60;
        [SerializeField] private float campingRadius = 5f;
        [SerializeField] private float campingTimeThreshold = 10f;
        [SerializeField] private float isolationDistance = 15f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        
        private List<PlayerProfile> playerProfiles = new List<PlayerProfile>();
        private float lastPositionUpdate;
        
        public List<PlayerProfile> AllProfiles => playerProfiles;
        public int PlayerCount => playerProfiles.Count;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindAllPlayers();
        }

        private void Update()
        {
            // Update position tracking
            if (Time.time - lastPositionUpdate >= positionUpdateInterval)
            {
                UpdatePositionTracking();
                lastPositionUpdate = Time.time;
            }
            
            // Update current state
            UpdateCurrentState();
            
            // Update team metrics
            if (playerProfiles.Count > 1)
            {
                UpdateTeamMetrics();
            }
        }

        private void FindAllPlayers()
        {
            playerProfiles.Clear();
            
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                PlayerProfile profile = new PlayerProfile
                {
                    playerTransform = players[i].transform,
                    playerIndex = i
                };

                if (Camera.main != null) 
                    profile.cameraTransform = Camera.main.transform;

                playerProfiles.Add(profile);
                
                if (showDebugLogs)
                    Debug.Log($"[PlayerProfiler] Found player {i}: {players[i].name}");
            }
        }

        private void UpdatePositionTracking()
        {
            foreach (var profile in playerProfiles)
            {
                if (profile.playerTransform == null) continue;
                
                // Add current position to history
                profile.positionHistory.Add(profile.playerTransform.position);
                
                // Limit history size
                while (profile.positionHistory.Count > positionHistorySize)
                {
                    profile.positionHistory.RemoveAt(0);
                }
                
                // Check camping
                UpdateCampingStatus(profile);
            }
        }

        private void UpdateCampingStatus(PlayerProfile profile)
        {
            if (profile.positionHistory.Count < 20) return;
            
            // Get last 20 positions (10 seconds worth)
            int checkCount = Mathf.Min(20, profile.positionHistory.Count);
            Vector3 avgPosition = Vector3.zero;
            
            for (int i = profile.positionHistory.Count - checkCount; i < profile.positionHistory.Count; i++)
            {
                avgPosition += profile.positionHistory[i];
            }
            avgPosition /= checkCount;
            
            // Check max deviation
            float maxDeviation = 0f;
            for (int i = profile.positionHistory.Count - checkCount; i < profile.positionHistory.Count; i++)
            {
                float dist = Vector3.Distance(profile.positionHistory[i], avgPosition);
                maxDeviation = Mathf.Max(maxDeviation, dist);
            }
            
            // Camping if stayed within radius
            bool wasCamping = profile.isCamping;
            profile.isCamping = maxDeviation < campingRadius;
            
            if (profile.isCamping)
            {
                profile.campingDuration += positionUpdateInterval;
                profile.mostFrequentPosition = avgPosition;
            }
            else
            {
                profile.campingDuration = 0f;
            }
            
            if (showDebugLogs && profile.isCamping && !wasCamping)
                Debug.Log($"[PlayerProfiler] Player {profile.playerIndex} started camping");
        }

        private void UpdateCurrentState()
        {
            foreach (var profile in playerProfiles)
            {
                if (profile.playerTransform == null) continue;
                
                // Health
                PlayerHealth health = profile.playerTransform.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    profile.currentHealth = health.CurrentHealth;
                }
                
                // Look direction (Camera)
                if (profile.cameraTransform != null)
                {
                    profile.lookDirection = profile.cameraTransform.forward;
                }
                
                // Movement check
                if (profile.positionHistory.Count >= 2)
                {
                    Vector3 lastPos = profile.positionHistory[profile.positionHistory.Count - 1];
                    Vector3 prevPos = profile.positionHistory[profile.positionHistory.Count - 2];
                    profile.isMoving = Vector3.Distance(lastPos, prevPos) > 0.1f;
                }
            }
        }

        private void UpdateTeamMetrics()
        {
            foreach (var profile in playerProfiles)
            {
                if (profile.playerTransform == null) continue;
                
                float nearestDist = float.MaxValue;
                
                foreach (var other in playerProfiles)
                {
                    if (other == profile || other.playerTransform == null) continue;
                    
                    float dist = Vector3.Distance(
                        profile.playerTransform.position,
                        other.playerTransform.position
                    );
                    nearestDist = Mathf.Min(nearestDist, dist);
                }
                
                profile.distanceToNearestAlly = nearestDist;
                profile.isIsolated = nearestDist > isolationDistance;
            }
        }

        // Called by zombie when killed
        public void ReportKill(int playerIndex, Vector3 zombiePosition, bool wasHeadshot, float reactionTime)
        {
            if (playerIndex < 0 || playerIndex >= playerProfiles.Count) return;
            
            var profile = playerProfiles[playerIndex];
            profile.totalKills++;
            
            if (wasHeadshot)
                profile.headshotKills++;
            
            profile.headshotRatio = (float)profile.headshotKills / profile.totalKills;
            
            float distance = Vector3.Distance(profile.playerTransform.position, zombiePosition);
            profile.avgKillDistance = (profile.avgKillDistance * (profile.totalKills - 1) + distance) / profile.totalKills;
            profile.avgReactionTime = (profile.avgReactionTime * (profile.totalKills - 1) + reactionTime) / profile.totalKills;
            
            if (showDebugLogs)
                Debug.Log($"[PlayerProfiler] Player {playerIndex} kill #{profile.totalKills}, Headshot: {wasHeadshot}");
        }

        public PlayerProfile GetProfile(int index)
        {
            if (index >= 0 && index < playerProfiles.Count)
                return playerProfiles[index];
            return null;
        }

        public PlayerProfile GetMostVulnerable()
        {
            PlayerProfile mostVulnerable = null;
            float highestScore = 0f;
            
            foreach (var profile in playerProfiles)
            {
                float score = (100f - profile.currentHealth) * 0.3f
                            + (profile.isIsolated ? 30f : 0f)
                            + (profile.isReloading ? 25f : 0f)
                            + (profile.currentAmmoPercent < 0.2f ? 15f : 0f);
                
                if (score > highestScore)
                {
                    highestScore = score;
                    mostVulnerable = profile;
                }
            }
            
            return mostVulnerable;
        }

        public PlayerProfile GetCarryPlayer()
        {
            PlayerProfile carry = null;
            float highestThreat = 0f;
            
            foreach (var profile in playerProfiles)
            {
                float threat = profile.totalKills * 0.4f
                             + profile.headshotRatio * 30f
                             + (profile.avgReactionTime > 0 ? (1f / profile.avgReactionTime) * 10f : 0f);
                
                if (threat > highestThreat)
                {
                    highestThreat = threat;
                    carry = profile;
                }
            }
            
            return carry;
        }
    }
}
