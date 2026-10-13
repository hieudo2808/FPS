using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    /// <summary>Validates the actual walkable floor, chapter ownership and body clearance.</summary>
    public static class CampaignPlacement
    {
        public static bool TryFloor(CampaignChapterRoot chapter, Vector3 position, out Vector3 floor)
        {
            floor = position;
            if (chapter == null || !float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)
                || !NavMesh.SamplePosition(position, out var hit, .65f, NavMesh.AllAreas)
                || Mathf.Abs(position.y - hit.position.y) > .55f) return false;
            if (!Physics.Raycast(hit.position + Vector3.up * .4f, Vector3.down, out var ground, .85f, ~0, QueryTriggerInteraction.Ignore)) return false;
            var owner = ground.collider.GetComponentInParent<CampaignChapterRoot>();
            if (owner != chapter)
            {
                // The service road belongs to the outgoing Factory chapter until the interlock commits.
                bool road = false;
                for (Transform t = ground.transform; t != null; t = t.parent)
                    if (t.name == "FactoryAsylumConnection") { road = true; break; }
                if (chapter.chapter != CampaignChapter.Factory || !road) return false;
            }
            floor = hit.position;
            return true;
        }

        public static bool Clear(Vector3 floor, ulong ignoredPlayerId = 0)
        {
            foreach (var collider in Physics.OverlapCapsule(floor + Vector3.up * .57f,
                floor + Vector3.up * 1.55f, .5f, ~0, QueryTriggerInteraction.Ignore))
            {
                var player = collider.GetComponentInParent<PlayerHealth>();
                if (player != null && player.StablePlayerId.Value == ignoredPlayerId) continue;
                return false;
            }
            return true;
        }

        public static bool CompletePath(Vector3 source, Vector3 destination, out float distance)
        {
            distance = 0;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(source, destination, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) distance += Vector3.Distance(corners[i - 1], corners[i]);
            return true;
        }

        public static bool TryReconnectPosition(CampaignChapterRoot chapter, Vector3 requested, ulong playerId, out Vector3 result)
        {
            result = requested;
            if (chapter == null || chapter.recoveries == null) return false;
            // A small search stays on the saved floor. Never use a large 3D sample through stacked maps.
            for (int ring = 0; ring <= 3; ring++)
                for (int i = 0; i < (ring == 0 ? 1 : 8); i++)
                {
                    float angle = i * Mathf.PI / 4;
                    Vector3 candidate = requested + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * .6f);
                    if (!TryFloor(chapter, candidate, out var floor) || !Clear(floor, playerId)) continue;
                    bool connected = false;
                    foreach (var point in chapter.recoveries)
                        if (point != null && TryFloor(chapter, point.transform.position, out var start)
                            && CompletePath(start, floor, out _)) { connected = true; break; }
                    if (!connected) continue;
                    result = floor + Vector3.up * .12f;
                    return true;
                }
            return false;
        }
    }
}
