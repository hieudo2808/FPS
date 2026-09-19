using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public readonly struct SpecialEscapePlan
    {
        public SpecialEscapePlan(Vector3 destination, bool fullyHidden, float exposureScore, float pathLength)
        {
            Destination = destination;
            FullyHidden = fullyHidden;
            ExposureScore = exposureScore;
            PathLength = pathLength;
        }

        public Vector3 Destination { get; }
        public bool FullyHidden { get; }
        public float ExposureScore { get; }
        public float PathLength { get; }
    }

    /// <summary>
    /// Small allocation-bounded planner shared by special infected that need to break team LOS.
    /// It is intentionally not a global service: each brain owns one planner and its reusable path.
    /// </summary>
    public sealed class SpecialEscapePlanner
    {
        public const int MaximumCandidates = 12;
        public const float PathSampleSpacing = 2f;

        private NavMeshPath reusablePath;

        public bool TryPlan(
            NavMeshAgent agent,
            Vector3 origin,
            Vector3 preferredDirection,
            IReadOnlyList<PlayerProfile> observers,
            float retreatDistance,
            LayerMask visibilityMask,
            Vector3 recentDestination,
            out SpecialEscapePlan plan)
        {
            plan = default;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return false;

            reusablePath ??= new NavMeshPath();

            Vector3 preferred = Vector3.ProjectOnPlane(preferredDirection, Vector3.up);
            if (preferred.sqrMagnitude < 0.0001f)
                preferred = -Vector3.ProjectOnPlane(agent.transform.forward, Vector3.up);
            if (preferred.sqrMagnitude < 0.0001f)
                preferred = Vector3.forward;
            preferred.Normalize();

            bool found = false;
            bool foundHidden = false;
            float bestScore = float.PositiveInfinity;
            float distance = Mathf.Max(2f, retreatDistance);

            for (int i = 0; i < MaximumCandidates; i++)
            {
                int signedStep = i == 0 ? 0 : ((i + 1) / 2) * (i % 2 == 1 ? 1 : -1);
                float angle = signedStep * 30f;
                float radius = distance * (i >= 8 ? 0.75f : 1f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * preferred;
                Vector3 rawCandidate = origin + direction * radius;

                if (!NavMesh.SamplePosition(rawCandidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue;
                if (!agent.CalculatePath(hit.position, reusablePath)
                    || reusablePath.status != NavMeshPathStatus.PathComplete
                    || reusablePath.corners == null
                    || reusablePath.corners.Length < 2)
                {
                    continue;
                }

                bool endpointVisible = IsVisibleToAnyLivingPlayer(hit.position + Vector3.up * 0.9f, observers, visibilityMask);
                if (endpointVisible)
                    continue;

                EvaluatePath(reusablePath.corners, observers, visibilityMask, out float exposedLength, out float pathLength);
                bool fullyHidden = exposedLength <= 0.01f;

                float timeToCoverPenalty = exposedLength * 12f;
                float pathPenalty = pathLength * 0.15f;
                float recentPenalty = recentDestination != Vector3.zero
                    ? Mathf.Clamp01(1f - Vector3.Distance(recentDestination, hit.position) / 4f) * 20f
                    : 0f;
                float deadEndPenalty = CalculateDeadEndPenalty(reusablePath.corners, preferred);
                float score = timeToCoverPenalty + pathPenalty + recentPenalty + deadEndPenalty;

                if (foundHidden && !fullyHidden)
                    continue;
                if ((!foundHidden && fullyHidden) || score < bestScore)
                {
                    found = true;
                    foundHidden = fullyHidden;
                    bestScore = score;
                    plan = new SpecialEscapePlan(hit.position, fullyHidden, exposedLength, pathLength);
                }
            }

            return found;
        }

        public static bool IsVisibleToAnyLivingPlayer(
            Vector3 worldPoint,
            IReadOnlyList<PlayerProfile> observers,
            LayerMask visibilityMask)
        {
            if (observers == null)
                return false;

            for (int i = 0; i < observers.Count; i++)
            {
                PlayerProfile profile = observers[i];
                if (!IsLivingObserver(profile))
                    continue;
                if (IsVisibleToObserver(worldPoint, profile, visibilityMask))
                    return true;
            }

            return false;
        }

        private static bool IsVisibleToObserver(Vector3 worldPoint, PlayerProfile profile, LayerMask visibilityMask)
        {
            Transform cameraTransform = profile.cameraTransform;
            Camera camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            Vector3 eye = cameraTransform != null
                ? cameraTransform.position
                : profile.playerTransform.position + Vector3.up * 1.55f;

            bool insideView;
            if (camera != null && camera.isActiveAndEnabled)
            {
                Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
                insideView = viewport.z > 0f
                    && viewport.x >= 0f && viewport.x <= 1f
                    && viewport.y >= 0f && viewport.y <= 1f;
            }
            else
            {
                Vector3 toPoint = worldPoint - eye;
                Vector3 look = profile.lookDirection.sqrMagnitude > 0.001f
                    ? profile.lookDirection.normalized
                    : profile.playerTransform.forward;
                insideView = toPoint.sqrMagnitude > 0.001f
                    && Vector3.Dot(look, toPoint.normalized) >= Mathf.Cos(50f * Mathf.Deg2Rad);
            }

            if (!insideView)
                return false;

            Vector3 ray = worldPoint - eye;
            float distance = ray.magnitude;
            if (distance <= 0.001f)
                return true;

            if (!Physics.Raycast(
                    eye,
                    ray / distance,
                    out RaycastHit hit,
                    distance,
                    visibilityMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // A collider touching the sampled endpoint is the observed character, not cover.
            return hit.distance >= distance - 0.25f;
        }

        private static bool IsLivingObserver(PlayerProfile profile)
        {
            if (profile?.playerTransform == null || !profile.playerTransform.gameObject.activeInHierarchy)
                return false;

            PlayerHealth health = profile.cachedHealth;
            return health == null || (!health.IsDead && health.LifeState == PlayerLifeState.Alive);
        }

        private static void EvaluatePath(
            Vector3[] corners,
            IReadOnlyList<PlayerProfile> observers,
            LayerMask visibilityMask,
            out float exposedLength,
            out float pathLength)
        {
            exposedLength = 0f;
            pathLength = 0f;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 start = corners[i - 1];
                Vector3 end = corners[i];
                float segmentLength = Vector3.Distance(start, end);
                pathLength += segmentLength;
                int samples = Mathf.Max(1, Mathf.CeilToInt(segmentLength / PathSampleSpacing));
                float sampleLength = segmentLength / samples;

                for (int sample = 1; sample <= samples; sample++)
                {
                    Vector3 point = Vector3.Lerp(start, end, sample / (float)samples) + Vector3.up * 0.9f;
                    if (IsVisibleToAnyLivingPlayer(point, observers, visibilityMask))
                        exposedLength += sampleLength;
                }
            }
        }

        private static float CalculateDeadEndPenalty(Vector3[] corners, Vector3 preferredDirection)
        {
            if (corners.Length < 2)
                return 50f;

            Vector3 finalDirection = Vector3.ProjectOnPlane(
                corners[corners.Length - 1] - corners[corners.Length - 2],
                Vector3.up).normalized;
            if (finalDirection.sqrMagnitude < 0.001f)
                return 10f;

            float alignment = Vector3.Dot(finalDirection, preferredDirection);
            return alignment < -0.25f ? 15f : 0f;
        }
    }
}
