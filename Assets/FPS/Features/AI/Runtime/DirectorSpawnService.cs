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
            if (CampaignMissionController.Instance?.Current != null)
            {
                var chapter = CampaignMissionController.Instance.Current;
                SetCampaignAnchors(chapter.anchors, chapter.spawnMinimumDistance, chapter.spawnMaximumDistance);
                return;
            }
            anchors.Clear();
            GetComponentsInChildren(true, anchors);
        }

        public void SetCampaignAnchors(DirectorSpawnAnchor[] values, float minimum, float maximum)
        {
            anchors.Clear();
            if (values != null) anchors.AddRange(values);
            minimumPlayerDistance = minimum;
            maximumPlayerDistance = maximum;
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
            if (CampaignMissionController.Instance != null && !CampaignMissionController.Instance.AllowsAnchor(anchor)) return false;
            if (anchor == null || anchor.Zone == null || !anchor.Zone.AllowsSpawning)
                return false;

            Vector3 candidate = anchor.SpawnPosition;
            if (!anchor.Zone.Contains(candidate))
                return false;
            if (!HasGround(candidate))
                return false;

            var campaign = CampaignMissionController.Instance;
            if (campaign != null)
            {
                bool reachable = false;
                foreach (var player in campaign.Players)
                {
                    if (player.LifeState != PlayerLifeState.Alive) continue;
                    float distance = Vector3.Distance(candidate, player.transform.position);
                    if (distance < minimumPlayerDistance || HasPhysicalLineOfSight(player.transform.position, candidate))
                        return false;
                    if (distance <= maximumPlayerDistance && HasCompletePath(candidate, player.transform.position))
                        reachable = true;
                }
                return reachable && CapsuleClear(candidate, .5f, 2f);
            }

            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
                return NavMesh.SamplePosition(candidate, out _, navMeshSampleRadius, NavMesh.AllAreas);

            bool inUsefulRange = false;
            bool hasCompletePath = false;

            foreach (PlayerProfile profile in PlayerProfiler.Instance.AllProfiles)
            {
                if (profile?.playerTransform == null)
                    continue;
                if (profile.cachedHealth != null && profile.cachedHealth.LifeState != PlayerLifeState.Alive)
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

            // Sampling must not jump to another storey of the asylum.
            if (Mathf.Abs(start.position.y - candidate.y) > .75f || Mathf.Abs(end.position.y - playerPosition.y) > 1f)
                return false;
            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, reusablePath)
                && reusablePath.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>Validate the chosen special's body on a complete route before taking it from the pool.</summary>
        public bool ValidateCampaignSpecialPosition(Vector3 position, bool tank)
        {
            var campaign = CampaignMissionController.Instance;
            if (campaign == null) return true;
            bool validAnchor = false;
            foreach (var anchor in anchors)
                if (anchor != null && (anchor.SpawnPosition - position).sqrMagnitude < 1f && IsAnchorValid(anchor))
                { validAnchor = true; break; }
            if (!validAnchor) return false;
            float radius = tank ? .9f : .5f, height = tank ? 2.8f : 2f;
            if (!CapsuleClear(position, radius, height)) return false;
            foreach (var player in campaign.Players)
            {
                if (player.LifeState == PlayerLifeState.Alive && player.IsInputReady
                    && HasCampaignSpecialRoute(position, player.transform.position, tank)) return true;
            }
            return false;
        }

        public bool HasCampaignSpecialRoute(Vector3 position, Vector3 target, bool tank)
        {
            if (!HasCompletePath(position, target)) return false;
            float radius = tank ? .9f : .5f, height = tank ? 2.8f : 2f;
            // The path runs on a NavMesh baked for a narrower agent, so it legitimately hugs walls at that agent's
            // radius. Sweeping the full Tank width along it rejects every route with a corner or gateway; the Tank
            // itself follows these paths. Keep its full height for headroom, and its full body at the spawn spot.
            var navigation = NavMesh.GetSettingsByIndex(0);
            float corridor = tank ? Mathf.Min(radius, navigation.agentRadius - .05f) : radius;
            // Stairs: corners sit at the foot and head of a flight, so a straight line between them cuts through
            // the treads. Follow the walkable surface and ignore anything the agent is allowed to step over.
            float clearance = tank ? navigation.agentClimb : .08f;
            var corners = reusablePath.corners;
            for (int segment = 1; segment < corners.Length; segment++)
            {
                Vector3 a = corners[segment - 1], b = corners[segment];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .45f));
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 point = Vector3.Lerp(a, b, (float)step / steps);
                    // Stop outside the target's own body; the attack closes this gap.
                    if (Vector3.Distance(point, target) < radius + 1.5f) continue;
                    if (tank && NavMesh.SamplePosition(point, out var surface, 1f, NavMesh.AllAreas)) point = surface.position;
                    if (Physics.CheckCapsule(point + Vector3.up * (corridor + clearance), point + Vector3.up * (height - corridor + .08f),
                            corridor, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
                }
            }
            return true;
        }

        /// <summary>Tank staging must have standing room, not merely a point on a narrow NavMesh corridor.</summary>
        public static bool HasTankArenaClearance(Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out var center, .5f, NavMesh.AllAreas)
                || Mathf.Abs(center.position.y - position.y) > .25f
                || !CapsuleClear(center.position, .9f, 2.8f)) return false;
            for (int side = 0; side < 4; side++)
            {
                Vector3 direction = side switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.left, _ => Vector3.right };
                Vector3 edge = center.position + direction * 1.5f;
                if (!NavMesh.SamplePosition(edge, out var sampled, .3f, NavMesh.AllAreas)
                    || Mathf.Abs(sampled.position.y - center.position.y) > .25f
                    || NavMesh.Raycast(center.position, sampled.position, out _, NavMesh.AllAreas)
                    || !CapsuleClear(sampled.position, .9f, 2.8f)) return false;
            }
            return true;
        }

        private readonly List<(DirectorSpawnAnchor anchor, float distance)> tankAnchorOrder = new(72);
        private const float RelaxedTankMinimumDistance = 10f;
        private static readonly float[] RelaxedTankRings = { 30f, 22f, 15f, RelaxedTankMinimumDistance };

        /// <summary>Physical validity only: room for the Tank body and a walkable route to a living operator.</summary>
        public bool IsRelaxedTankPositionValid(Vector3 position)
        {
            var campaign = CampaignMissionController.Instance;
            if (campaign == null || !CapsuleClear(position, .9f, 2.8f)) return false;
            bool route = false;
            foreach (var player in campaign.Players)
            {
                if (player.LifeState != PlayerLifeState.Alive || !player.IsInputReady) continue;
                if (Vector3.Distance(position, player.transform.position) < RelaxedTankMinimumDistance - .5f) return false;
                route |= HasCampaignSpecialRoute(position, player.transform.position, true);
            }
            return route;
        }

        // Used only after the strict search has failed for a full timeout. Tries authored anchors first, then
        // rings around each living operator, preferring a spot nobody can see.
        private bool TryGetRelaxedTankPosition(out Vector3 position)
        {
            position = default;
            var campaign = CampaignMissionController.Instance;
            if (campaign == null) return false;
            bool found = false;
            foreach (var anchor in anchors)
                if (anchor != null && anchor.isActiveAndEnabled && campaign.AllowsAnchor(anchor)
                    && ConsiderRelaxedTankPosition(campaign, anchor.SpawnPosition, ref position, ref found)) return true;
            foreach (var player in campaign.Players)
            {
                if (player.LifeState != PlayerLifeState.Alive || !player.IsInputReady) continue;
                foreach (float ring in RelaxedTankRings)
                    for (int step = 0; step < 12; step++)
                    {
                        Vector3 offset = Quaternion.Euler(0, step * 30f, 0) * Vector3.forward * ring;
                        if (ConsiderRelaxedTankPosition(campaign, player.transform.position + offset, ref position, ref found)) return true;
                    }
            }
            return found;
        }

        // Returns true for an unseen valid spot (search ends); the first seen valid spot is kept as the fallback result.
        private bool ConsiderRelaxedTankPosition(CampaignMissionController campaign, Vector3 candidate, ref Vector3 result, ref bool found)
        {
            if (!NavMesh.SamplePosition(candidate, out var hit, navMeshSampleRadius, NavMesh.AllAreas)
                || Mathf.Abs(hit.position.y - candidate.y) > 1.5f || !IsRelaxedTankPositionValid(hit.position)) return false;
            foreach (var player in campaign.Players)
                if (player.LifeState == PlayerLifeState.Alive && HasPhysicalLineOfSight(player.transform.position, hit.position))
                {
                    if (!found) { result = hit.position; found = true; }
                    return false;
                }
            result = hit.position; found = true;
            return true;
        }

        public bool TryGetCampaignTankPosition(out Vector3 position, bool fallback = false, bool relaxed = false)
        {
            position = default;
            if (relaxed) return TryGetRelaxedTankPosition(out position);
            // This is also allowed during Asylum preparation, where the ordinary Director is suppressed.
            // Anchors authored for specials are the intended staging spots, so they are tried before the general ones.
            // Within each group the nearest valid anchor wins; the fallback order tries the farthest first instead.
            var campaign = CampaignMissionController.Instance;
            tankAnchorOrder.Clear();
            foreach (var candidate in anchors)
            {
                if (candidate == null) continue;
                float nearest = float.MaxValue;
                if (campaign != null)
                    foreach (var player in campaign.Players)
                        if (player.LifeState == PlayerLifeState.Alive)
                            nearest = Mathf.Min(nearest, (player.transform.position - candidate.SpawnPosition).sqrMagnitude);
                tankAnchorOrder.Add((candidate, nearest));
            }
            tankAnchorOrder.Sort((left, right) => left.distance.CompareTo(right.distance));
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < tankAnchorOrder.Count; i++)
                {
                    var anchor = tankAnchorOrder[fallback ? tankAnchorOrder.Count - 1 - i : i].anchor;
                    if (!anchor.isActiveAndEnabled
                        || ((anchor.AnchorTypes & DirectorSpawnAnchorType.Special) != 0) != (pass == 0) || !IsAnchorValid(anchor)) continue;
                    if (!NavMesh.SamplePosition(anchor.SpawnPosition, out var hit, .5f, NavMesh.AllAreas)
                        || !HasTankArenaClearance(hit.position) || !ValidateCampaignSpecialPosition(hit.position, true)) continue;
                    position = hit.position;
                    return true;
                }
            return false;
        }

        private static bool CapsuleClear(Vector3 feet, float radius, float height) =>
            !Physics.CheckCapsule(feet + Vector3.up * (radius + .08f), feet + Vector3.up * (height - radius + .08f),
                radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private static DirectorSpawnAnchorType ResolveMissionSpawnTypes(DirectorSpawnAnchorType requested)
        {
            if (CampaignMissionController.Instance != null)
                return CampaignMissionController.Instance.IsFinale ? requested | DirectorSpawnAnchorType.Horde | DirectorSpawnAnchorType.Finale
                    : requested | DirectorSpawnAnchorType.Common;
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
            if (CampaignMissionController.Instance != null) return CampaignMissionController.Instance.SuppressSpawns;
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
