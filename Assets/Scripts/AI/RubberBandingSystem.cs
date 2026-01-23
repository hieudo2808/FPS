using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace FPS
{
    public class RubberBandingSystem : Singleton<RubberBandingSystem>
    {
        [Header("Teleport Settings")]
        [SerializeField] private float maxDistanceFromPlayer = 50f;
        [SerializeField] private float teleportCheckInterval = 2f;
        [SerializeField] private float minTimeBeforeTeleport = 10f;

        [Header("Catch-up Speed Settings")]
        [SerializeField] private float catchUpSpeedMultiplier = 1.5f;
        [SerializeField] private float behindPlayerDotThreshold = -0.5f;
        [SerializeField] private float maxSpeedBoost = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private Dictionary<EnemyAI, ZombieTrackingData> trackedZombies = new Dictionary<EnemyAI, ZombieTrackingData>();
        private float lastTeleportCheck;

        private class ZombieTrackingData
        {
            public float timeOutOfRange;
            public float originalSpeed;
            public bool isSpeedBoosted;
        }

        private void Update()
        {
            if (Time.time - lastTeleportCheck >= teleportCheckInterval)
            {
                lastTeleportCheck = Time.time;
                CheckAndTeleportDistantZombies();
            }

            UpdateCatchUpSpeed();
        }

        public void RegisterZombie(EnemyAI zombie)
        {
            if (zombie == null || trackedZombies.ContainsKey(zombie)) return;

            var agent = zombie.GetComponent<NavMeshAgent>();
            trackedZombies[zombie] = new ZombieTrackingData
            {
                timeOutOfRange = 0f,
                originalSpeed = agent != null ? agent.speed : 5f,
                isSpeedBoosted = false
            };
        }

        public void UnregisterZombie(EnemyAI zombie)
        {
            trackedZombies.Remove(zombie);
        }

        private void CheckAndTeleportDistantZombies()
        {
            if (PlayerProfiler.Instance == null) return;

            var nearestPlayer = GetNearestPlayer();
            if (nearestPlayer == null) return;

            List<EnemyAI> toRemove = new List<EnemyAI>();

            foreach (var kvp in trackedZombies)
            {
                EnemyAI zombie = kvp.Key;
                ZombieTrackingData data = kvp.Value;

                if (zombie == null)
                {
                    toRemove.Add(zombie);
                    continue;
                }

                float distance = Vector3.Distance(zombie.transform.position, nearestPlayer.position);

                if (distance > maxDistanceFromPlayer)
                {
                    if (!IsInAnyPlayerView(zombie.transform.position))
                    {
                        data.timeOutOfRange += teleportCheckInterval;

                        if (data.timeOutOfRange >= minTimeBeforeTeleport)
                        {
                            TeleportZombie(zombie);
                            data.timeOutOfRange = 0f;
                        }
                    }
                    else
                    {
                        data.timeOutOfRange = 0f;
                    }
                }
                else
                {
                    data.timeOutOfRange = 0f;
                }
            }

            foreach (var zombie in toRemove)
            {
                trackedZombies.Remove(zombie);
            }
        }

        private void TeleportZombie(EnemyAI zombie)
        {
            Vector3 newPos = GetTeleportPosition();
            if (newPos == Vector3.zero) return;

            var agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(newPos);

                if (showDebugLogs)
                    Debug.Log($"[RubberBanding] Teleported zombie to {newPos}");
            }
        }

        private Vector3 GetTeleportPosition()
        {
            if (InfluenceMapManager.Instance != null)
            {
                return InfluenceMapManager.Instance.GetBestSpawnPosition();
            }

            var player = GetNearestPlayer();
            if (player == null) return Vector3.zero;

            Vector3 behindPlayer = player.position - player.forward * 15f;
            
            if (NavMesh.SamplePosition(behindPlayer, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            return Vector3.zero;
        }

        private void UpdateCatchUpSpeed()
        {
            var nearestPlayer = GetNearestPlayer();
            if (nearestPlayer == null) return;

            foreach (var kvp in trackedZombies)
            {
                EnemyAI zombie = kvp.Key;
                ZombieTrackingData data = kvp.Value;

                if (zombie == null) continue;

                var agent = zombie.GetComponent<NavMeshAgent>();
                if (agent == null) continue;

                Vector3 toZombie = (zombie.transform.position - nearestPlayer.position).normalized;
                float dot = Vector3.Dot(nearestPlayer.forward, toZombie);

                if (dot < behindPlayerDotThreshold && !IsInAnyPlayerView(zombie.transform.position))
                {
                    if (!data.isSpeedBoosted)
                    {
                        float distance = Vector3.Distance(zombie.transform.position, nearestPlayer.position);
                        float speedMultiplier = 1f + (distance / 100f);
                        speedMultiplier = Mathf.Min(speedMultiplier, maxSpeedBoost);

                        agent.speed = data.originalSpeed * speedMultiplier;
                        data.isSpeedBoosted = true;

                        if (showDebugLogs)
                            Debug.Log($"[RubberBanding] Speed boost: {speedMultiplier:F2}x");
                    }
                }
                else
                {
                    if (data.isSpeedBoosted)
                    {
                        agent.speed = data.originalSpeed;
                        data.isSpeedBoosted = false;
                    }
                }
            }
        }

        private bool IsInAnyPlayerView(Vector3 position)
        {
            if (PlayerProfiler.Instance == null) return false;

            foreach (var profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile.playerTransform == null) continue;

                Vector3 toPos = (position - profile.playerTransform.position).normalized;
                float dot = Vector3.Dot(profile.lookDirection, toPos);

                if (dot > 0f)
                {
                    float dist = Vector3.Distance(position, profile.playerTransform.position);
                    if (dist < 40f)
                        return true;
                }
            }

            return false;
        }

        private Transform GetNearestPlayer()
        {
            if (PlayerProfiler.Instance == null) return null;

            Transform nearest = null;
            float minDist = float.MaxValue;

            foreach (var profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile.playerTransform == null) continue;
                
                return profile.playerTransform;
            }

            return nearest;
        }
    }
}
