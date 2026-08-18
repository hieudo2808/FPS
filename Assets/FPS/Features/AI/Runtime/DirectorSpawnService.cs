using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class DirectorSpawnService : MonoBehaviour
    {
        public static DirectorSpawnService Instance { get; private set; }

        [Header("Distance and visibility")]
        [SerializeField, Min(1f)] private float minimumPlayerDistance = 28f;
        [SerializeField, Min(1f)] private float maximumPlayerDistance = 95f;
        [SerializeField, Range(20f, 160f)] private float fallbackHorizontalFov = 100f;
        [SerializeField] private LayerMask visibilityMask = Physics.DefaultRaycastLayers;

        [Header("Navigation")]
        [SerializeField, Min(0.25f)] private float navMeshSampleRadius = 2f;
        [SerializeField, Min(0.25f)] private float groundProbeDistance = 3f;

        [Header("Debug")]
        [SerializeField] private bool showRejectedAnchors;

        private readonly List<DirectorSpawnAnchor> anchors = new(72);
        private readonly List<int> validAnchorIndices = new(72);
        private NavMeshPath reusablePath;

        private void Awake()
        {
            reusablePath = new NavMeshPath();
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            RefreshAnchors();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RefreshAnchors()
        {
            anchors.Clear();
            GetComponentsInChildren(true, anchors);
        }

        public bool TryGetSpawnPosition(DirectorSpawnAnchorType requestedTypes, out Vector3 position)
        {
            position = Vector3.zero;
            if (anchors.Count == 0)
                RefreshAnchors();
            if (anchors.Count == 0 || IsDirectorSuppressed())
                return false;

            requestedTypes = ResolveMissionSpawnTypes(requestedTypes);
            validAnchorIndices.Clear();
            float totalWeight = 0f;

            for (int i = 0; i < anchors.Count; i++)
            {
                DirectorSpawnAnchor anchor = anchors[i];
                if (anchor == null || !anchor.isActiveAndEnabled)
                    continue;
                if ((anchor.AnchorTypes & requestedTypes) == 0)
                    continue;
                if (!IsAnchorValid(anchor))
                    continue;

                validAnchorIndices.Add(i);
                totalWeight += anchor.SelectionWeight;
            }

            if (validAnchorIndices.Count == 0)
                return false;

            float roll = Random.value * totalWeight;
            for (int i = 0; i < validAnchorIndices.Count; i++)
            {
                DirectorSpawnAnchor anchor = anchors[validAnchorIndices[i]];
                roll -= anchor.SelectionWeight;
                if (roll > 0f && i < validAnchorIndices.Count - 1)
                    continue;

                if (NavMesh.SamplePosition(anchor.SpawnPosition, out NavMeshHit hit,
                        navMeshSampleRadius, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }

            return false;
        }

        public bool IsAnchorValid(DirectorSpawnAnchor anchor)
        {
            if (anchor == null || anchor.Zone == null || !anchor.Zone.AllowsSpawning)
                return false;

            Vector3 candidate = anchor.SpawnPosition;
            if (!anchor.Zone.Contains(candidate))
                return false;
            if (!HasGround(candidate))
                return false;

            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
                return NavMesh.SamplePosition(candidate, out _, navMeshSampleRadius, NavMesh.AllAreas);

            bool inUsefulRange = false;
            bool hasCompletePath = false;

            foreach (PlayerProfile profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile?.playerTransform == null)
                    continue;

                Vector3 playerPosition = profile.playerTransform.position;
                Vector3 toCandidate = candidate - playerPosition;
                float distance = toCandidate.magnitude;
                if (distance < minimumPlayerDistance)
                    return Reject(anchor, "too close");
                if (distance <= maximumPlayerDistance)
                    inUsefulRange = true;

                if (IsInsidePlayerFrustum(profile, candidate, distance))
                    return Reject(anchor, "frustum");
                if (HasPhysicalLineOfSight(playerPosition, candidate))
                    return Reject(anchor, "line of sight");

                if (!hasCompletePath && HasCompletePath(candidate, playerPosition))
                    hasCompletePath = true;
            }

            return inUsefulRange && hasCompletePath;
        }

        private bool HasGround(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 1.25f;
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                groundProbeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.normal.y >= 0.55f;
        }

        private bool IsInsidePlayerFrustum(PlayerProfile profile, Vector3 candidate, float distance)
        {
            Camera playerCamera = profile.playerTransform.GetComponentInChildren<Camera>();
            if (playerCamera != null && playerCamera.isActiveAndEnabled)
            {
                Vector3 viewport = playerCamera.WorldToViewportPoint(candidate + Vector3.up);
                return viewport.z > 0f
                    && viewport.x >= 0f && viewport.x <= 1f
                    && viewport.y >= 0f && viewport.y <= 1f;
            }

            Vector3 lookDirection = profile.lookDirection.sqrMagnitude > 0.001f
                ? profile.lookDirection.normalized
                : profile.playerTransform.forward;
            Vector3 direction = (candidate - profile.playerTransform.position).normalized;
            float verticalAllowance = Mathf.Lerp(0.4f, 0.12f, Mathf.InverseLerp(1f, maximumPlayerDistance, distance));
            return Vector3.Dot(lookDirection, direction) >= Mathf.Cos(fallbackHorizontalFov * 0.5f * Mathf.Deg2Rad)
                && Mathf.Abs(direction.y) <= verticalAllowance;
        }

        private bool HasPhysicalLineOfSight(Vector3 playerPosition, Vector3 candidate)
        {
            Vector3 origin = playerPosition + Vector3.up * 1.55f;
            Vector3 destination = candidate + Vector3.up * 0.9f;
            return !Physics.Linecast(origin, destination, visibilityMask, QueryTriggerInteraction.Ignore);
        }

        private bool HasCompletePath(Vector3 candidate, Vector3 playerPosition)
        {
            reusablePath ??= new NavMeshPath();
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit start, navMeshSampleRadius, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(playerPosition, out NavMeshHit end, 3f, NavMesh.AllAreas))
            {
                return false;
            }

            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, reusablePath)
                && reusablePath.status == NavMeshPathStatus.PathComplete;
        }

        private static DirectorSpawnAnchorType ResolveMissionSpawnTypes(DirectorSpawnAnchorType requested)
        {
            FactoryMissionState state = FactoryMissionController.Instance != null
                ? FactoryMissionController.Instance.State
                : FactoryMissionState.BranchesActive;

            if (state == FactoryMissionState.ExtractionActive)
                return requested | DirectorSpawnAnchorType.Horde | DirectorSpawnAnchorType.Finale;

            if (AIDirector.Instance != null)
                return requested | AIDirector.Instance.CurrentSpawnAnchorTypes;

            return requested;
        }

        private static bool IsDirectorSuppressed()
        {
            if (FactoryMissionController.Instance == null)
                return false;

            FactoryMissionState state = FactoryMissionController.Instance.State;
            return state is FactoryMissionState.Insertion
                or FactoryMissionState.Completed
                or FactoryMissionState.Failed;
        }

        private bool Reject(DirectorSpawnAnchor anchor, string reason)
        {
            if (showRejectedAnchors && Debug.isDebugBuild)
                GameLog.Info(() => $"[DirectorSpawn] Rejected {anchor.name}: {reason}");
            return false;
        }
    }
}
