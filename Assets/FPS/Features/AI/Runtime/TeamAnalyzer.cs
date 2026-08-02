using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public enum TeamFormation { SOLO, GROUPED, SPLIT, PAIRED, MIXED }
    public enum PlayerRole { CARRY, FRONTLINE, SUPPORT, LONE_WOLF }

    public class TeamAnalyzer : MonoBehaviour
    {
        public static TeamAnalyzer Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private float groupedDistance = 10f;
        [SerializeField] private float splitDistance = 20f;
        [SerializeField] private float isolationDistance = 15f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        
        private TeamFormation currentFormation = TeamFormation.SOLO;
        private List<PlayerProfile> isolatedPlayers = new List<PlayerProfile>();
        private int carryPlayerIndex = -1;
        private Vector3 teamCentroid;
        private float teamSpread;
        private int frameCounter;
        
        public TeamFormation Formation => currentFormation;
        public List<PlayerProfile> IsolatedPlayers => isolatedPlayers;
        public int CarryPlayerIndex => carryPlayerIndex;
        public Vector3 TeamCentroid => teamCentroid;
        public float TeamSpread => teamSpread;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Update()
        {
            if (PlayerProfiler.Instance == null) return;
            if (PlayerProfiler.Instance.PlayerCount <= 1)
            {
                currentFormation = TeamFormation.SOLO;
                return;
            }

            frameCounter++;
            if (frameCounter < 10) return;
            frameCounter = 0;

            AnalyzeFormation();
            AnalyzeRoles();
            FindIsolatedPlayers();
            CalculateTeamCentroid();
        }

        private void AnalyzeFormation()
        {
            var profiles = PlayerProfiler.Instance.AllProfiles;
            int playerCount = profiles.Count;
            
            if (playerCount == 1)
            {
                currentFormation = TeamFormation.SOLO;
                return;
            }
            
            float maxDistance = 0f;
            float minDistance = float.MaxValue;
            float totalDistance = 0f;
            int pairCount = 0;
            
            for (int i = 0; i < playerCount; i++)
            {
                for (int j = i + 1; j < playerCount; j++)
                {
                    if (profiles[i].playerTransform == null || profiles[j].playerTransform == null)
                        continue;
                        
                    float dist = Vector3.Distance(
                        profiles[i].playerTransform.position,
                        profiles[j].playerTransform.position
                    );
                    
                    maxDistance = Mathf.Max(maxDistance, dist);
                    minDistance = Mathf.Min(minDistance, dist);
                    totalDistance += dist;
                    pairCount++;
                }
            }
            
            teamSpread = pairCount > 0 ? totalDistance / pairCount : 0f;
            
            if (maxDistance <= groupedDistance)
            {
                currentFormation = TeamFormation.GROUPED;
            }
            else if (minDistance >= splitDistance)
            {
                currentFormation = TeamFormation.SPLIT;
            }
            else if (playerCount == 4 && HasTwoPairs(profiles))
            {
                currentFormation = TeamFormation.PAIRED;
            }
            else
            {
                currentFormation = TeamFormation.MIXED;
            }
            
            if (showDebugLogs)
                GameLog.Info(() => $"[TeamAnalyzer] Formation: {currentFormation}, Spread: {teamSpread:F1}m");
        }

        private bool HasTwoPairs(List<PlayerProfile> profiles)
        {
            int closeCount = 0;
            for (int i = 0; i < profiles.Count; i++)
            {
                for (int j = i + 1; j < profiles.Count; j++)
                {
                    if (profiles[i].playerTransform == null || profiles[j].playerTransform == null)
                        continue;
                        
                    float dist = Vector3.Distance(
                        profiles[i].playerTransform.position,
                        profiles[j].playerTransform.position
                    );
                    if (dist <= groupedDistance)
                        closeCount++;
                }
            }
            return closeCount == 2;
        }

        private void AnalyzeRoles()
        {
            var profiles = PlayerProfiler.Instance.AllProfiles;
            if (profiles.Count == 0) return;
            
            float highestThreat = 0f;
            carryPlayerIndex = 0;
            
            for (int i = 0; i < profiles.Count; i++)
            {
                float threat = profiles[i].totalKills * 0.6f + profiles[i].headshotRatio * 40f;
                if (threat > highestThreat)
                {
                    highestThreat = threat;
                    carryPlayerIndex = i;
                }
            }
        }

        private void FindIsolatedPlayers()
        {
            isolatedPlayers.Clear();
            var profiles = PlayerProfiler.Instance.AllProfiles;
            
            foreach (var profile in profiles)
            {
                if (profile.isIsolated || profile.distanceToNearestAlly > isolationDistance)
                {
                    isolatedPlayers.Add(profile);
                }
            }
        }

        private void CalculateTeamCentroid()
        {
            teamCentroid = PlayerProfiler.Instance.GetCentroid();
        }

        public PlayerRole GetPlayerRole(int playerIndex)
        {
            if (PlayerProfiler.Instance == null) return PlayerRole.SUPPORT;
            
            var profile = PlayerProfiler.Instance.GetProfile(playerIndex);
            if (profile == null) return PlayerRole.SUPPORT;
            
            if (playerIndex == carryPlayerIndex)
                return PlayerRole.CARRY;
            
            if (profile.isIsolated)
                return PlayerRole.LONE_WOLF;
            
            if (profile.totalKills > 10 && profile.avgKillDistance < 15f)
                return PlayerRole.FRONTLINE;
            
            return PlayerRole.SUPPORT;
        }

        public PlayerProfile GetMostIsolatedPlayer()
        {
            if (isolatedPlayers.Count == 0) return null;
            
            PlayerProfile mostIsolated = null;
            float maxDist = 0f;
            
            foreach (var player in isolatedPlayers)
            {
                if (player.distanceToNearestAlly > maxDist)
                {
                    maxDist = player.distanceToNearestAlly;
                    mostIsolated = player;
                }
            }
            
            return mostIsolated;
        }
    }
}
