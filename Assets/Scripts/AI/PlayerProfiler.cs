using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [System.Serializable]
    public class PlayerProfile
    {
        public Transform playerTransform;
        public int playerIndex;
        public ulong clientId;

        public Transform cameraTransform;

        public List<Vector3> positionHistory = new List<Vector3>();
        public Vector3 mostFrequentPosition;
        public bool isCamping;
        public float campingDuration;

        public int totalKills;
        public int headshotKills;
        public float headshotRatio;
        public float avgKillDistance;
        public float avgReactionTime;

        public float currentHealth;
        public float currentAmmoPercent;
        public bool isReloading;
        public bool isMoving;
        public Vector3 lookDirection;

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

        private readonly List<PlayerProfile> playerProfiles = new List<PlayerProfile>();
        private readonly Dictionary<ulong, PlayerProfile> profilesByClientId = new Dictionary<ulong, PlayerProfile>();
        private float lastPositionUpdate;
        private float lastPlayerCheck;

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
            RefreshPlayers();
        }

        private void Update()
        {
            if (Time.time - lastPlayerCheck > 1f)
            {
                lastPlayerCheck = Time.time;
                RefreshPlayers();
            }

            if (Time.time - lastPositionUpdate >= positionUpdateInterval)
            {
                UpdatePositionTracking();
                lastPositionUpdate = Time.time;
            }

            UpdateCurrentState();

            if (playerProfiles.Count > 1)
            {
                UpdateTeamMetrics();
            }
        }

        private void RefreshPlayers()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                RefreshNetworkPlayers();
                return;
            }

            RefreshTaggedPlayers();
        }

        private void RefreshNetworkPlayers()
        {
            List<PlayerProfile> nextProfiles = new List<PlayerProfile>();

            int index = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList.OrderBy(c => c.ClientId))
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null || !playerObject.IsSpawned) continue;
                if (!playerObject.gameObject.activeInHierarchy) continue;

                PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
                if (playerHealth != null && playerHealth.IsDead) continue;

                if (!profilesByClientId.TryGetValue(client.ClientId, out PlayerProfile profile))
                {
                    profile = new PlayerProfile();
                    profilesByClientId[client.ClientId] = profile;
                }

                profile.clientId = client.ClientId;
                profile.playerTransform = playerObject.transform;
                profile.playerIndex = index++;
                profile.cameraTransform = FindCameraTransform(playerObject.gameObject);

                nextProfiles.Add(profile);
            }

            playerProfiles.Clear();
            playerProfiles.AddRange(nextProfiles);

            CleanupStaleProfiles(nextProfiles);

            if (showDebugLogs)
            {
                Debug.Log($"[PlayerProfiler] Refreshed {playerProfiles.Count} network players on authoritative side");
            }
        }

        private void RefreshTaggedPlayers()
        {
            playerProfiles.Clear();
            profilesByClientId.Clear();

            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            int index = 0;
            foreach (GameObject player in players)
            {
                if (player == null || !player.activeInHierarchy) continue;

                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null && playerHealth.IsDead) continue;

                PlayerProfile profile = new PlayerProfile
                {
                    playerTransform = player.transform,
                    playerIndex = index++,
                    cameraTransform = FindCameraTransform(player)
                };

                playerProfiles.Add(profile);
            }
        }

        private void CleanupStaleProfiles(List<PlayerProfile> activeProfiles)
        {
            HashSet<ulong> activeClientIds = new HashSet<ulong>(activeProfiles.Select(profile => profile.clientId));
            List<ulong> staleClientIds = new List<ulong>();

            foreach (ulong clientId in profilesByClientId.Keys)
            {
                if (!activeClientIds.Contains(clientId))
                {
                    staleClientIds.Add(clientId);
                }
            }

            foreach (ulong clientId in staleClientIds)
            {
                profilesByClientId.Remove(clientId);
            }
        }

        private static Transform FindCameraTransform(GameObject player)
        {
            Camera playerCamera = player.GetComponentInChildren<Camera>(true);
            return playerCamera != null ? playerCamera.transform : null;
        }

        private void UpdatePositionTracking()
        {
            foreach (var profile in playerProfiles)
            {
                if (!IsProfileValid(profile)) continue;

                profile.positionHistory.Add(profile.playerTransform.position);

                while (profile.positionHistory.Count > positionHistorySize)
                {
                    profile.positionHistory.RemoveAt(0);
                }

                UpdateCampingStatus(profile);
            }
        }

        private void UpdateCampingStatus(PlayerProfile profile)
        {
            if (profile.positionHistory.Count < 20) return;

            int checkCount = Mathf.Min(20, profile.positionHistory.Count);
            Vector3 avgPosition = Vector3.zero;

            for (int i = profile.positionHistory.Count - checkCount; i < profile.positionHistory.Count; i++)
            {
                avgPosition += profile.positionHistory[i];
            }
            avgPosition /= checkCount;

            float maxDeviation = 0f;
            for (int i = profile.positionHistory.Count - checkCount; i < profile.positionHistory.Count; i++)
            {
                float dist = Vector3.Distance(profile.positionHistory[i], avgPosition);
                maxDeviation = Mathf.Max(maxDeviation, dist);
            }

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

            if (showDebugLogs && profile.isCamping && !wasCamping && profile.campingDuration >= campingTimeThreshold)
            {
                Debug.Log($"[PlayerProfiler] Player {profile.playerIndex} started camping");
            }
        }

        private void UpdateCurrentState()
        {
            foreach (var profile in playerProfiles)
            {
                if (!IsProfileValid(profile)) continue;

                PlayerHealth health = profile.playerTransform.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    profile.currentHealth = health.CurrentHealth;
                }

                if (profile.cameraTransform != null)
                {
                    profile.lookDirection = profile.cameraTransform.forward;
                }
                else
                {
                    profile.lookDirection = profile.playerTransform.forward;
                }

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
                if (!IsProfileValid(profile)) continue;

                float nearestDist = float.MaxValue;

                foreach (var other in playerProfiles)
                {
                    if (other == profile || !IsProfileValid(other)) continue;

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

        public void ReportKill(int playerIndex, Vector3 zombiePosition, bool wasHeadshot, float reactionTime)
        {
            if (playerIndex < 0 || playerIndex >= playerProfiles.Count) return;

            var profile = playerProfiles[playerIndex];
            if (!IsProfileValid(profile)) return;

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
                if (!IsProfileValid(profile)) continue;

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
                if (!IsProfileValid(profile)) continue;

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

        public PlayerProfile GetClosestPlayer(Vector3 position)
        {
            PlayerProfile closest = null;
            float minDist = float.MaxValue;

            foreach (var profile in playerProfiles)
            {
                if (!IsProfileValid(profile)) continue;

                float dist = (profile.playerTransform.position - position).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = profile;
                }
            }
            return closest;
        }

        private static bool IsProfileValid(PlayerProfile profile)
        {
            if (profile == null || profile.playerTransform == null) return false;
            if (!profile.playerTransform.gameObject.activeInHierarchy) return false;

            PlayerHealth health = profile.playerTransform.GetComponent<PlayerHealth>();
            return health == null || !health.IsDead;
        }
    }
}
