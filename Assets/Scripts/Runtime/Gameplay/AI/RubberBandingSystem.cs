using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public class RubberBandingSystem : SceneSingleton<RubberBandingSystem>
    {
        [Header("Teleport Settings")]
        [SerializeField] private float maxDistanceFromPlayer = 50f;
        [SerializeField] private float teleportCheckInterval = 2f;
        [SerializeField] private float minTimeBeforeTeleport = 10f;

        [Header("Catch-up Speed Settings")]
        [SerializeField] private float catchUpSpeedMultiplier = 1.5f;
        [SerializeField] private float behindPlayerDotThreshold = -0.5f;
        [SerializeField] private float maxSpeedBoost = 1.5f;

        [Header("Feature Flags")]
        public bool isEnabled = true;

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
            if (!isEnabled) return;
            if (Time.time - lastTeleportCheck >= teleportCheckInterval)
            {
                lastTeleportCheck = Time.time;
                CheckAndTeleportDistantZombies();
            }
        }

        private void OnEnable()
        {
            StartCoroutine(CatchUpSpeedLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator CatchUpSpeedLoop()
        {
            var wait = new WaitForSeconds(0.1f);
            while (true)
            {
                UpdateCatchUpSpeed();
                yield return wait;
            }
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

            var health = zombie.GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.OnDeathServer += () => UnregisterZombie(zombie);
            }
        }

        public void UnregisterZombie(EnemyAI zombie)
        {
            trackedZombies.Remove(zombie);
        }

        private void CheckAndTeleportDistantZombies()
        {
            if (PlayerProfiler.Instance == null) return;

            Transform nearestPlayer = GetNearestPlayerToCenter();
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
                trackedZombies.Remove(zombie);
        }

        private void TeleportZombie(EnemyAI zombie)
        {
            Vector3 newPos = GetTeleportPosition();
            if (newPos == Vector3.zero) return;

            var agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(newPos);
            }
        }

        private Vector3 GetTeleportPosition()
        {
            if (InfluenceMapManager.Instance != null)
            {
                return InfluenceMapManager.Instance.TryGetBestSpawnPosition(out Vector3 smartPosition)
                    ? smartPosition
                    : Vector3.zero;
            }

            bool hasProfiledPlayers = PlayerProfiler.Instance != null && PlayerProfiler.Instance.PlayerCount > 0;
            if (hasProfiledPlayers)
                return Vector3.zero;

            if (ZombieRegistry.Instance != null &&
                ZombieRegistry.Instance.TryGetSpawnPosition(out Vector3 registryPosition))
            {
                return registryPosition;
            }

            return Vector3.zero;
        }

        private void UpdateCatchUpSpeed()
        {
            if (!isEnabled) return;
            Transform nearestPlayer = GetNearestPlayerToCenter();
            if (nearestPlayer == null) return;

            foreach (var kvp in trackedZombies)
            {
                EnemyAI zombie = kvp.Key;
                ZombieTrackingData data = kvp.Value;

                if (zombie == null) continue;

                var agent = zombie.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.enabled) continue;

                Vector3 toZombie = (zombie.transform.position - nearestPlayer.position).normalized;
                float dot = Vector3.Dot(nearestPlayer.forward, toZombie);

                if (dot < behindPlayerDotThreshold && !IsInAnyPlayerView(zombie.transform.position))
                {
                    if (!data.isSpeedBoosted)
                    {
                        float distance = Vector3.Distance(zombie.transform.position, nearestPlayer.position);
                        float speedMultiplier = Mathf.Min((1f + distance / 100f) * catchUpSpeedMultiplier, maxSpeedBoost);

                        agent.speed = data.originalSpeed * speedMultiplier;
                        data.isSpeedBoosted = true;
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
                    if (dist < 40f) return true;
                }
            }

            return false;
        }

        private Transform GetNearestPlayerToCenter()
        {
            if (PlayerProfiler.Instance == null) return null;

            Transform nearest = null;
            float minDist = float.MaxValue;

            Vector3 reference = TeamAnalyzer.Instance != null
                ? TeamAnalyzer.Instance.TeamCentroid
                : Vector3.zero;

            foreach (var profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile.playerTransform == null) continue;

                float dist = Vector3.Distance(profile.playerTransform.position, reference);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = profile.playerTransform;
                }
            }

            return nearest;
        }

        public Transform GetNearestPlayerTo(Vector3 position)
        {
            var profile = PlayerProfiler.Instance?.GetNearest(position);
            return profile?.playerTransform;
        }

#if UNITY_INCLUDE_TESTS
        public readonly struct TestSnapshot
        {
            public readonly int trackedZombieCount;

            public TestSnapshot(int trackedZombieCount)
            {
                this.trackedZombieCount = trackedZombieCount;
            }
        }

        public TestSnapshot CaptureTestSnapshot()
        {
            return new TestSnapshot(trackedZombies.Count);
        }
#endif
    }
}
