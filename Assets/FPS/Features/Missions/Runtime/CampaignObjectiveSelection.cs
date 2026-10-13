using System;
using System.Collections.Generic;

namespace FPS
{
    /// <summary>Local presentation selection; never completes or changes an authoritative objective.</summary>
    public static class CampaignObjectiveSelection
    {
        public static bool CanTrack(CampaignState state, CampaignObjectiveId id) => state != null
            && state.phase == CampaignPhase.Exploring && id != CampaignObjectiveId.None
            && CampaignRules.CanUse(state, id) == CampaignResult.Accepted;

        public static CampaignObjectiveId Resolve(CampaignState state, IReadOnlyList<CampaignObjectiveId> orderedIds,
            CampaignObjectiveId current, Func<CampaignObjectiveId, float?> routeLength)
        {
            if (CanTrack(state, current) && Contains(orderedIds, current)) return current;
            var fallback = CampaignObjectiveId.None;
            var nearest = CampaignObjectiveId.None;
            float shortest = float.PositiveInfinity;
            foreach (var id in orderedIds)
            {
                if (!CanTrack(state, id)) continue;
                if (fallback == CampaignObjectiveId.None) fallback = id;
                float? distance = routeLength?.Invoke(id);
                if (!distance.HasValue || float.IsNaN(distance.Value) || distance.Value < 0 || distance.Value >= shortest) continue;
                nearest = id; shortest = distance.Value;
            }
            return nearest != CampaignObjectiveId.None ? nearest : fallback;
        }

        private static bool Contains(IReadOnlyList<CampaignObjectiveId> ids, CampaignObjectiveId id)
        { for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true; return false; }
    }
}
