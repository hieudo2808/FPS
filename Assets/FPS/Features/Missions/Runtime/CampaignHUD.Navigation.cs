using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace FPS
{
    public sealed partial class CampaignHUD
    {
        private Text navigation;
        private NavMeshPath guidancePath;
        private float nextNavigationUpdate, routeDistance;
        private Vector3 nextRouteCorner;
        private string routeName;

        private void UpdateNavigation(PlayerHealth owner)
        {
            var phase = Campaign.State.phase;
            bool visible = !panel.gameObject.activeSelf && owner.CanUseCombat
                && phase is CampaignPhase.Exploring or CampaignPhase.Preparing or CampaignPhase.AwaitingParty;
            navigation.enabled = visible;
            if (!visible) return;
            if (Time.unscaledTime >= nextNavigationUpdate)
            {
                nextNavigationUpdate = Time.unscaledTime + 1;
                routeName = null;
                if (phase == CampaignPhase.Exploring)
                {
                    foreach (var item in Campaign.Current.objectives.Where(i => i != null
                        && CampaignRules.CanUse(Campaign.State, i.objectiveId) == CampaignResult.Accepted)
                        .OrderBy(i => (i.Point - owner.transform.position).sqrMagnitude))
                    {
                        if (!TryGuidancePath(owner.transform.position, item.Point)) continue;
                        routeName = item.displayName;
                        break;
                    }
                }
                else
                {
                    var volume = phase == CampaignPhase.Preparing ? Campaign.Current.preparationArea : Campaign.Current.boardingArea;
                    if (volume != null && TryGuidancePath(owner.transform.position,
                        volume.bounds.center - Vector3.up * (volume.bounds.extents.y - .1f)))
                        routeName = phase == CampaignPhase.Preparing ? "Điểm xác nhận sẵn sàng"
                            : Campaign.State.chapter == CampaignChapter.Asylum ? "Cabin thang B2" : "Khu tập kết";
                }
            }
            if (routeName == null) { navigation.text = ""; return; }
            var camera = owner.GetComponentInChildren<Camera>();
            Vector3 forward = camera != null ? camera.transform.forward : owner.transform.forward;
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(forward, Vector3.up),
                Vector3.ProjectOnPlane(nextRouteCorner - owner.transform.position, Vector3.up), Vector3.up);
            string direction = Mathf.Abs(angle) > 135 ? "Quay lại" : angle > 35 ? "Rẽ phải" : angle < -35 ? "Rẽ trái" : "Đi tiếp";
            navigation.text = routeDistance < 2 ? $"{routeName} · tại đây"
                : $"{direction} · {Mathf.CeilToInt(routeDistance)} m theo lối đi\n{routeName}";
        }

        private bool TryGuidancePath(Vector3 from, Vector3 to)
        {
            // NavMeshPath owns native Unity data; create it on the main thread after Awake.
            guidancePath ??= new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var start, .8f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(to, out var end, 2, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, guidancePath)
                || guidancePath.status != NavMeshPathStatus.PathComplete) return false;
            var corners = guidancePath.corners;
            if (corners.Length == 0) return false;
            routeDistance = 0;
            nextRouteCorner = corners[corners.Length - 1];
            bool foundNext = false;
            for (int i = 1; i < corners.Length; i++)
            {
                routeDistance += Vector3.Distance(corners[i - 1], corners[i]);
                if (foundNext || (corners[i] - from).sqrMagnitude < 1) continue;
                nextRouteCorner = corners[i]; foundNext = true;
            }
            return true;
        }
    }
}
