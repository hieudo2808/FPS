using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace FPS
{
    public sealed partial class CampaignHUD
    {
        public CampaignObjectiveId TrackedObjectiveId { get; private set; }
        public event Action TrackingChanged;
        public bool HasMissionTracker => tracker != null && tracker.gameObject.activeInHierarchy;
        private Text locationLabel, actionLabel, navigation;
        private RectTransform tracker, marker;
        private Image destinationIcon;
        private RectTransform routeIcon;
        private NavMeshPath guidancePath;
        private Vector3[] routeCorners = Array.Empty<Vector3>();
        private readonly List<CampaignObjectiveId> trackableIds = new();
        private uint observedRevision = uint.MaxValue;
        private CampaignMissionController trackingCampaign;
        private float nextNavigationUpdate, routeDistance;
        private Vector3 routeTarget;
        private bool routeComplete, hasRouteTarget, routeComputed;
        private readonly Rect[] markerObstacles = new Rect[3];

        public bool TryTrackObjective(CampaignObjectiveId id)
        {
            if (Campaign == null || !CampaignObjectiveSelection.CanTrack(Campaign.State, id)
                || Campaign.Current?.objectives == null
                || !Campaign.Current.objectives.Any(o => o != null && !o.documentOnly && o.objectiveId == id)) return false;
            SetTracked(id);
            return true;
        }

        private void SetTracked(CampaignObjectiveId id)
        {
            if (TrackedObjectiveId == id) return;
            TrackedObjectiveId = id;
            nextNavigationUpdate = 0; routeComputed = false;
            TrackingChanged?.Invoke();
        }

        private void ResolveTracking(PlayerHealth owner)
        {
            if (Campaign == null || owner == null) return;
            if (trackingCampaign == Campaign && observedRevision == Campaign.State.revision) return;
            trackingCampaign = Campaign; observedRevision = Campaign.State.revision;
            nextNavigationUpdate = 0; routeComputed = false;
            trackableIds.Clear();
            if (Campaign.Current?.objectives != null)
                foreach (var item in Campaign.Current.objectives.Where(o => o != null && !o.documentOnly)
                    .OrderBy(o => CatalogOrder(o.objectiveId)))
                    if (!trackableIds.Contains(item.objectiveId)) trackableIds.Add(item.objectiveId);
            SetTracked(CampaignObjectiveSelection.Resolve(Campaign.State, trackableIds, TrackedObjectiveId, id =>
            {
                var item = Campaign.Objective(id);
                return item != null && TryGuidancePath(owner.transform.position, item.Point, item) ? routeDistance : (float?)null;
            }));
        }

        private int CatalogOrder(CampaignObjectiveId id)
        {
            var definitions = Campaign.ContentCatalog?.objectives;
            if (definitions != null)
                for (int i = 0; i < definitions.Length; i++) if (definitions[i]?.id == id) return i;
            return 1000 + (int)id;
        }

        private void BuildNavigation(Transform safe)
        {
            tracker = Box(safe, "MissionTracker", Vector2.zero, Vector2.one);
            HudSkin.Apply(tracker.GetComponent<Image>(), "Panel");
            HudLayout.Corner(tracker, new Vector2(0, 1), new Vector2(28, -28), new Vector2(440, 192));
            locationLabel = Label(tracker, "Location", Vector2.up, Vector2.one, 24);
            locationLabel.rectTransform.offsetMin = new Vector2(24, -44);
            locationLabel.rectTransform.offsetMax = new Vector2(-24, -16);
            locationLabel.color = HudSkin.Accent;
            status = Label(tracker, "TrackedObjective", Vector2.zero, Vector2.one, 28);
            status.rectTransform.offsetMin = new Vector2(24, 80);
            status.rectTransform.offsetMax = new Vector2(-24, -48);
            status.fontStyle = FontStyle.Bold;
            actionLabel = Label(tracker, "CurrentAction", Vector2.up, Vector2.one, 24);
            actionLabel.rectTransform.offsetMin = new Vector2(24, -176);
            actionLabel.rectTransform.offsetMax = new Vector2(-24, -120);
            marker = Box(safe, "NavigationMarker", Vector2.zero, Vector2.one);
            marker.GetComponent<Image>().color = Color.clear;
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(.5f, .5f);
            marker.sizeDelta = new Vector2(228, 110);
            var icon = Box(marker, "Destination", Vector2.zero, Vector2.one);
            icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
            icon.sizeDelta = new Vector2(28, 28);
            destinationIcon = icon.GetComponent<Image>(); HudSkin.Apply(destinationIcon, "Mission", HudSkin.Accent);
            routeIcon = Box(marker, "RouteCorner", Vector2.zero, Vector2.one);
            routeIcon.GetComponent<Image>().color = Color.clear;
            routeIcon.anchorMin = routeIcon.anchorMax = new Vector2(.5f, .5f); routeIcon.sizeDelta = new Vector2(34, 24);
            var arrow = Box(routeIcon, "Arrow", Vector2.zero, Vector2.one);
            arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, .5f); arrow.sizeDelta = new Vector2(32, 24);
            arrow.localRotation = Quaternion.Euler(0, 0, 90);
            HudSkin.Apply(arrow.GetComponent<Image>(), "Arrow", HudSkin.Accent);
            navigation = Label(marker, "Distance", new Vector2(.025f, -.20f), new Vector2(.975f, .35f), 24);
            navigation.alignment = TextAnchor.MiddleCenter;
            marker.gameObject.SetActive(false);
        }

        private void UpdateTracker(PlayerHealth owner)
        {
            bool visible = HudLayout.GameplayVisible && owner != null && owner.CanUseCombat && !panel.gameObject.activeSelf;
            tracker.gameObject.SetActive(visible);
            if (!visible) return;
            var s = Campaign.State;
            locationLabel.text = s.chapter switch { CampaignChapter.Factory => "FACTORY", CampaignChapter.Asylum => "AN LAC", _ => "B2 / LABORATORY" };
            var item = s.phase == CampaignPhase.Exploring ? Campaign.Objective(TrackedObjectiveId) : null;
            status.text = s.phase switch
            {
                CampaignPhase.Exploring => item != null ? item.Title : "Recover the T9-17 evidence",
                CampaignPhase.Preparing => s.tankStage == CampaignTankStage.Active ? "Evade the Tank" : "Prepare for extraction",
                CampaignPhase.Encounter => "Hold the extraction route",
                CampaignPhase.AwaitingParty => "Board with the squad",
                CampaignPhase.Transitioning => "Hold formation",
                CampaignPhase.Insertion => "Reach the facility",
                CampaignPhase.Completed => "Campaign complete",
                _ => "Mission failed"
            };
            bool factoryTank = s.phase == CampaignPhase.Exploring && s.chapter == CampaignChapter.Factory
                && CampaignRules.UtilitiesReady(s) && CampaignRules.LogisticsReady(s) && !s.factoryTankDefeated
                && !s.Has(CampaignObjectiveId.FactoryCase);
            if (factoryTank) status.text = "Eliminate the Tank";
            actionLabel.text = s.phase switch
            {
                CampaignPhase.Exploring => ObjectiveAction(TrackedObjectiveId) + (hasRouteTarget && routeComputed && !routeComplete ? "\nRoute unavailable" : ""),
                CampaignPhase.Preparing => $"READY {Campaign.ReadyCount}/{Campaign.ActivePlayerCount} / Gather at the panel",
                CampaignPhase.Encounter => $"{Mathf.CeilToInt(Campaign.Remaining)}s remaining / Defend the approach",
                CampaignPhase.AwaitingParty => "Enter the boarding area / Wait for the squad",
                CampaignPhase.Transitioning => "Wait for the receiving door",
                _ => ""
            };
            if (factoryTank) actionLabel.text = "Regroup in open space / Protect the evidence case";
            bool reduced = HudLayout.SecondaryReduced;
            if (reduced && s.phase == CampaignPhase.Encounter)
                status.text = $"EXTRACTION / {Mathf.CeilToInt(Campaign.Remaining)}s";
            else if (reduced && s.phase == CampaignPhase.Preparing)
                status.text = s.tankStage == CampaignTankStage.Active ? "EVADE TANK / GATHER" : $"READY {Campaign.ReadyCount}/{Campaign.ActivePlayerCount}";
            tracker.sizeDelta = new Vector2(440, reduced ? 72 : 192);
            locationLabel.gameObject.SetActive(!reduced); actionLabel.gameObject.SetActive(!reduced);
            status.rectTransform.offsetMin = new Vector2(24, reduced ? 16 : 80);
            status.rectTransform.offsetMax = new Vector2(-24, reduced ? -16 : -48);
            status.alignment = reduced ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
        }

        public static string ObjectiveAction(CampaignObjectiveId id) => id switch
        {
            CampaignObjectiveId.FactoryIsolate => "Inspect the electrical isolation panel",
            CampaignObjectiveId.FactoryBackup => "Configure the cold-store backup",
            CampaignObjectiveId.FactoryGenerator => "Restore cold-store power",
            CampaignObjectiveId.FactoryManifest => "Read the shipping manifest",
            CampaignObjectiveId.FactoryShipping => "Cross-check the shipping record",
            CampaignObjectiveId.FactoryCase => "Secure the evidence case",
            CampaignObjectiveId.FactoryRoute => "Open the service route",
            CampaignObjectiveId.AsylumAccess => "Read the security desk log",
            CampaignObjectiveId.AsylumFuse => "Recover the service fuse",
            CampaignObjectiveId.AsylumInstall => "Install the fuse",
            CampaignObjectiveId.AsylumPower => "Power the lower service panel",
            CampaignObjectiveId.AsylumPatient => "Inspect the upper records",
            CampaignObjectiveId.AsylumTransfer => "Cross-check the mortuary record",
            CampaignObjectiveId.AsylumLift => "Scan the card at the freight lift",
            CampaignObjectiveId.LabTrace => "Read the laboratory trace record",
            CampaignObjectiveId.LabIndex => "Inspect the laboratory index",
            CampaignObjectiveId.LabPower => "Allocate power at the control panel",
            CampaignObjectiveId.LabArchive => "Cross-check the archive record",
            CampaignObjectiveId.LabCase => "Secure the laboratory evidence case",
            CampaignObjectiveId.LabTransmit => "Transmit evidence and call the lift",
            _ => "Check the mission list"
        };

        private void UpdateNavigation(PlayerHealth owner)
        {
            ResolveTracking(owner);
            marker.gameObject.SetActive(false);
            var phase = Campaign.State.phase;
            if (!HudLayout.GameplayVisible || panel.gameObject.activeSelf || !owner.CanUseCombat
                || phase is not (CampaignPhase.Exploring or CampaignPhase.Preparing or CampaignPhase.AwaitingParty)) return;
            var item = phase == CampaignPhase.Exploring ? Campaign.Objective(TrackedObjectiveId) : null;
            var volume = phase == CampaignPhase.Preparing ? Campaign.Current?.preparationArea : Campaign.Current?.boardingArea;
            hasRouteTarget = phase == CampaignPhase.Exploring ? item != null : volume != null;
            if (!hasRouteTarget) return;
            routeTarget = item != null ? item.Point : volume.bounds.center - Vector3.up * (volume.bounds.extents.y - .1f);
            if (Time.unscaledTime >= nextNavigationUpdate)
            {
                nextNavigationUpdate = Time.unscaledTime + 1f;
                routeComplete = TryGuidancePath(owner.transform.position, routeTarget, item);
                routeCorners = routeComplete ? guidancePath.corners : Array.Empty<Vector3>();
                routeComputed = true;
            }
            var camera = owner.GetComponentInChildren<Camera>();
            if (camera == null) return;
            if (item != null && item.InReach(owner, Campaign.settings.interactionRange)) return;
            if (item == null && volume.bounds.Contains(owner.transform.position)) return;
            bool targetVisible = TargetVisible(camera.transform.position, routeTarget, item);
            Vector3 point = routeTarget;
            bool corner = routeComplete && !targetVisible;
            if (corner)
            {
                foreach (var candidate in routeCorners)
                    if ((candidate - owner.transform.position).sqrMagnitude >= 2.25f)
                    { point = candidate + Vector3.up * .65f; break; }
            }
            Vector3 projected = camera.WorldToScreenPoint(point);
            float scale = canvas.scaleFactor;
            var bounds = Screen.safeArea;
            float horizontalInset = (marker.rect.width * .5f + HudLayout.Margin) * scale;
            bounds.xMin += horizontalInset; bounds.xMax -= horizontalInset;
            bounds.yMin += (marker.rect.height * .7f + HudLayout.Margin) * scale;
            bounds.yMax -= (marker.rect.height * .5f + HudLayout.Margin) * scale;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            var screenCenter = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var position = HudMarkerProjection.Project(projected, screenCenter, bounds, out bool edge);
            if (edge)
            {
                var left = Screen.safeArea;
                left.xMax = left.xMin + 400 * scale; left.yMax = left.yMin + 650 * scale;
                var right = Screen.safeArea;
                right.xMin = right.xMax - 470 * scale; right.yMax = right.yMin + 390 * scale;
                var top = Screen.safeArea;
                top.xMax = top.xMin + 500 * scale; top.yMin = top.yMax - 220 * scale;
                markerObstacles[0] = left; markerObstacles[1] = right; markerObstacles[2] = top;
                position = HudMarkerProjection.AvoidHud(position, bounds, markerObstacles, 60 * scale);
            }
            var root = (RectTransform)marker.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, position, null, out var local);
            marker.anchoredPosition = local;
            bool nearReticle = !edge && (position - screenCenter).sqrMagnitude < 48 * 48;
            destinationIcon.gameObject.SetActive(!edge && !corner && !nearReticle);
            routeIcon.gameObject.SetActive((edge || corner) && !nearReticle);
            routeIcon.localRotation = edge ? Quaternion.Euler(0, 0, Mathf.Atan2(position.y - screenCenter.y, position.x - screenCenter.x) * Mathf.Rad2Deg - 90) : Quaternion.identity;
            if (nearReticle) marker.anchoredPosition += new Vector2(160, 0);
            string distance = routeComplete ? $"{Mathf.CeilToInt(routeDistance)}m ROUTE" : $"{Mathf.CeilToInt(Vector3.Distance(owner.transform.position, routeTarget))}m TO TARGET";
            float height = routeTarget.y - owner.transform.position.y;
            navigation.text = distance + (height >= 2.5f ? "\nHIGHER" : height <= -2.5f ? "\nLOWER" : "");
            navigation.color = routeComplete ? TacticalUiTheme.Text : TacticalUiTheme.Muted;
            marker.gameObject.SetActive(true);
        }

        private static bool TargetVisible(Vector3 from, Vector3 to, CampaignInteractable item)
        {
            if (!Physics.Linecast(from, to, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return item != null && item.OwnsCollider(hit.collider);
        }

        private bool TryGuidancePath(Vector3 from, Vector3 to, CampaignInteractable item = null)
        {
            guidancePath ??= new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var start, .8f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(to, out var end, 2f, NavMesh.AllAreas)
                // An interaction point may be above the floor. Sampling must not move up to a different storey.
                || end.position.y > to.y + .3f || to.y - end.position.y > 2f
                || !TargetVisible(end.position + Vector3.up * 1.55f, to, item)
                || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, guidancePath)
                || guidancePath.status != NavMeshPathStatus.PathComplete) return false;
            var corners = guidancePath.corners;
            if (corners.Length == 0) return false;
            routeDistance = 0;
            for (int i = 1; i < corners.Length; i++) routeDistance += Vector3.Distance(corners[i - 1], corners[i]);
            return true;
        }
    }
}
