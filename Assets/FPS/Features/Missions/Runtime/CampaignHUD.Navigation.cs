using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

namespace FPS
{
    public sealed partial class CampaignHUD
    {
        public CampaignObjectiveId TrackedObjectiveId { get; private set; }
        public event Action TrackingChanged;
        public bool HasMissionTracker => tracker != null && tracker.gameObject.activeInHierarchy;
        private Text navigation;
        private FactoryBranchHud powerHud, logisticsHud;
        private TMP_Text locationLabel, actionLabel, branchCounts;
        private const float TrackerHeight = 172, TrackerReducedHeight = 88;
        private const float RouteRefreshSeconds = .5f, CornerHeight = .6f, CornerMatch = .35f;
        private const float EdgePadding = 24, LocalHudMove = 60, MarkerPulseMax = 1.12f;
        private const float MarkerWidth = 144, LabelOffset = 48, LabelHeight = 56;
        private RectTransform tracker, marker;
        private RectTransform routeIcon;
        private NavMeshPath guidancePath;
        private Vector3[] routeCorners = Array.Empty<Vector3>();
        private int nextRouteCorner = 1;
        private readonly List<CampaignObjectiveId> trackableIds = new();
        private uint observedRevision = uint.MaxValue;
        private CampaignMissionController trackingCampaign;
        private (PlayerHealth owner, CampaignMissionController campaign, CampaignChapter chapter, CampaignPhase phase,
            CampaignObjectiveId objective, Component target, ulong tankId) navigationKey;
        private float nextNavigationUpdate, insertionRouteReadyAt, nextCueUntil;
        private Vector3 routeAnchor, routeDestination;
        private Vector2 lastBearing;
        private bool markerAtEdge, routeUnavailable;
        private readonly Rect[] markerObstacles = new Rect[5];
        private readonly Vector3[] hudCorners = new Vector3[4];

        public bool TryTrackObjective(CampaignObjectiveId id, bool allowLocked = false)
        {
            if (Campaign == null || (!CampaignObjectiveSelection.CanTrack(Campaign.State, id)
                && (!allowLocked || !CampaignObjectiveSelection.CanInspect(Campaign.State, id)))
                || Campaign.Current?.objectives == null
                || !Campaign.Current.objectives.Any(o => o != null && o.objectiveId == id && (o.interactionBody != null || !o.documentOnly))) return false;
            SetTracked(id);
            return true;
        }

        private void SetTracked(CampaignObjectiveId id)
        {
            if (TrackedObjectiveId == id) return;
            TrackedObjectiveId = id;
            nextThreatCheck = 0;
            ResetNavigation();
            RefreshHintTarget();
            TrackingChanged?.Invoke();
            RefreshTracker();
        }

        private void ResolveTracking(PlayerHealth owner)
        {
            if (Campaign == null || owner == null) return;
            if (trackingCampaign == Campaign && observedRevision == Campaign.State.revision) return;
            trackingCampaign = Campaign; observedRevision = Campaign.State.revision;
            trackableIds.Clear();
            if (Campaign.Current?.objectives != null)
                foreach (var item in Campaign.Current.objectives.Where(o => o != null && (o.interactionBody != null || !o.documentOnly))
                    .OrderBy(o => CatalogOrder(o.objectiveId)))
                    if (!trackableIds.Contains(item.objectiveId)) trackableIds.Add(item.objectiveId);
            SetTracked(CampaignObjectiveSelection.Resolve(Campaign.State, trackableIds, TrackedObjectiveId, id =>
            {
                var item = Campaign.Objective(id);
                if (item == null) return null;
                return !FactoryDestinationsActive(Campaign.State)
                    && TryGuidancePath(PlayerFeet(owner), item.TargetPosition, item, out var corners)
                    ? RouteLength(corners[0], corners, 1) : Vector3.Distance(owner.transform.position, item.TargetPosition);
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
            SelectedUiTheme.Surface(tracker.GetComponent<Image>(), Theme.Tracker);
            HudLayout.Corner(tracker, new Vector2(0, 1), Vector2.zero, new Vector2(421, TrackerHeight));
            locationLabel = DeviceText(tracker, "Location", Vector2.up, Vector2.one, 24, FontStyles.Normal);
            locationLabel.rectTransform.offsetMin = new Vector2(24, -36);
            locationLabel.rectTransform.offsetMax = new Vector2(-24, -12);
            locationLabel.color = Theme.Muted; locationLabel.fontSize = Theme.Small;
            status = DeviceText(tracker, "TrackedObjective", Vector2.zero, Vector2.one, 24, FontStyles.Bold);
            status.rectTransform.offsetMin = new Vector2(32, 84);
            status.rectTransform.offsetMax = new Vector2(-32, -64);
            actionLabel = DeviceText(tracker, "CurrentAction", Vector2.zero, Vector2.one, 24, FontStyles.Normal);
            actionLabel.rectTransform.offsetMin = new Vector2(32, -172);
            actionLabel.rectTransform.offsetMax = new Vector2(-32, -116);
            branchCounts = DeviceText(tracker, "BranchProgress", Vector2.zero, Vector2.one, 24, FontStyles.Normal);
            branchCounts.fontSize = Theme.Small;
            branchCounts.alignment = TextAlignmentOptions.TopRight;
            branchCounts.rectTransform.offsetMin = new Vector2(24, 12);
            branchCounts.rectTransform.offsetMax = new Vector2(-68, -76);
            marker = Box(safe, "NavigationMarker", Vector2.zero, Vector2.one);
            marker.GetComponent<Image>().color = Color.clear;
            HudLayout.Corner(marker, new Vector2(.5f, .5f), Vector2.zero, new Vector2(MarkerWidth, 128));
            BuildNavigationDot(marker);
            routeIcon = Box(marker, "RouteCorner", Vector2.zero, Vector2.one);
            routeIcon.GetComponent<Image>().color = Color.clear;
            routeIcon.anchorMin = routeIcon.anchorMax = new Vector2(.5f, .5f); routeIcon.sizeDelta = new Vector2(34, 24);
            var arrow = Box(routeIcon, "Arrow", Vector2.zero, Vector2.one);
            HudLayout.Corner(arrow, new Vector2(.5f, .5f), new Vector2(0, 28), new Vector2(24, 18));
            HudSkin.Apply(arrow.GetComponent<Image>(), "FPSSample/GoalMarker", Theme.Text);
            navigation = Label(marker, "Distance", Vector2.zero, Vector2.one, 24);
            HudLayout.Corner(navigation.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -LabelOffset), new Vector2(MarkerWidth, LabelHeight));
            navigation.alignment = TextAnchor.MiddleCenter;
            navigation.resizeTextForBestFit = true;
            navigation.resizeTextMinSize = 16;
            navigation.resizeTextMaxSize = 24;
            marker.gameObject.SetActive(false);
            powerHud = BuildFactoryBranch(safe, "Power");
            logisticsHud = BuildFactoryBranch(safe, "Logistics");
        }

        private void BuildNavigationDot(RectTransform parent)
        {
            var icon = Box(parent, "Destination", Vector2.zero, Vector2.one);
            HudLayout.Corner(icon, new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 28));
            HudSkin.Apply(icon.GetComponent<Image>(), "FPSSample/GoalMarkerCentre", Theme.Text);
        }

        private void UpdateTracker(PlayerHealth owner)
        {
            bool visible = HudLayout.GameplayVisible && owner != null && owner.CanUseCombat && !panel.gameObject.activeSelf;
            tracker.gameObject.SetActive(visible);
            if (!visible) return;
            var s = Campaign.State;
            bool factoryDestinations = FactoryDestinationsActive(s);
            powerHud.row.gameObject.SetActive(factoryDestinations);
            logisticsHud.row.gameObject.SetActive(factoryDestinations);
            float captionSize = Theme.Small * canvas.scaleFactor >= TacticalUiTheme.MinimumReadableTextPixels ? Theme.Small : Theme.Body;
            locationLabel.fontSize = captionSize;
            if (factoryDestinations)
            {
                UpdateFactoryTracker(s, captionSize);
                return;
            }
            // The chapter goal stays on screen for the whole chapter; the step below it changes.
            string goal = "<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Text) + ">" + CampaignRules.ChapterGoal(s.chapter) + "</color>";
            locationLabel.text = CampaignRules.ChapterName(s.chapter) + " — " + goal;
            var trackedTitle = s.phase == CampaignPhase.Exploring ? ObjectiveAction(TrackedObjectiveId) : "";
            status.text = s.phase switch
            {
                CampaignPhase.Exploring => trackedTitle.Length != 0 ? CampaignTextHighlight.Format(trackedTitle, Theme) : "Recover the T9-17 case",
                CampaignPhase.Preparing => s.tankStage == CampaignTankStage.Active ? CampaignTextHighlight.Format("Evade the Tank", Theme) : "Prepare for extraction",
                CampaignPhase.Encounter => "Hold the extraction route",
                CampaignPhase.AwaitingParty => "Board with the squad",
                CampaignPhase.Transitioning => "Hold formation",
                CampaignPhase.Insertion => "Reach the facility",
                CampaignPhase.Completed => "Campaign complete",
                _ => "Mission failed"
            };
            bool factoryTank = CampaignRules.TankIsNext(s);
            if (factoryTank) status.text = CampaignTextHighlight.Format(s.tankStage == CampaignTankStage.Active ? "Eliminate the Tank"
                : "Tank incoming", Theme);
            actionLabel.fontSize = branchCounts.fontSize = captionSize;
            // Small screens use the larger caption size; the goal matters more than the place name when both cannot fit.
            // Below the reference resolution the typography pass enlarges captions, so only the goal is sure to fit.
            if (canvas.scaleFactor < .99f || captionSize != Theme.Small
                || locationLabel.GetPreferredValues(locationLabel.text, 10000, 1000).x > 373) locationLabel.text = goal;
            actionLabel.lineSpacing = branchCounts.lineSpacing = captionSize == Theme.Small ? 5 : 3;
            actionLabel.text = s.phase switch
            {
                CampaignPhase.Preparing => CampaignTextHighlight.Format(CampaignRules.PartyGateText(s.partyGate, true), Theme),
                CampaignPhase.Encounter => $"{Mathf.CeilToInt(Campaign.Remaining)}s remaining / Defend the approach",
                CampaignPhase.AwaitingParty => CampaignTextHighlight.Format(CampaignRules.PartyGateText(s.partyGate, false), Theme),
                CampaignPhase.Transitioning => "Wait for the receiving door",
                _ => ""
            };
            if (factoryTank) actionLabel.text = CampaignTextHighlight.Format(CampaignRules.TankStatus(s), Theme);
            bool factoryBranches = s.phase == CampaignPhase.Exploring && s.chapter == CampaignChapter.Factory;
            if (factoryBranches)
            {
                if (!factoryTank) actionLabel.text = "Power\nLogistics";
                branchCounts.text = $"{CampaignRules.FactoryProgress(s, CampaignFactoryBranch.Power)}/3\n"
                    + $"{CampaignRules.FactoryProgress(s, CampaignFactoryBranch.Logistics)}/2";
            }
            bool reduced = HudLayout.SecondaryReduced;
            if (reduced && s.phase == CampaignPhase.Encounter)
                status.text = $"EXTRACTION / {Mathf.CeilToInt(Campaign.Remaining)}s";
            else if (reduced && s.phase == CampaignPhase.Preparing)
                status.text = s.tankStage == CampaignTankStage.Active ? "EVADE TANK / GATHER" : $"READY {Campaign.ReadyCount}/{Campaign.ActivePlayerCount}";
            tracker.sizeDelta = new Vector2(421, reduced ? TrackerReducedHeight : TrackerHeight);
            tracker.anchoredPosition = Vector2.zero;
            locationLabel.gameObject.SetActive(!reduced); actionLabel.gameObject.SetActive(!reduced);
            branchCounts.gameObject.SetActive(!reduced && factoryBranches);
            status.rectTransform.offsetMin = new Vector2(24, reduced ? 24 : 100);
            status.rectTransform.offsetMax = new Vector2(-24, reduced ? -24 : -40);
            status.fontSize = Theme.Body;
            bool longTitle = status.GetPreferredValues(status.text, 10000, 1000).x > 373;
            float extra = !reduced && longTitle && captionSize == Theme.Body ? 30 : 0;
            if (longTitle && captionSize == Theme.Small) status.fontSize = Theme.Small;
            if (routeUnavailable)
            {
                status.text += $"\n<size={Mathf.CeilToInt(captionSize)}><color=#{ColorUtility.ToHtmlStringRGB(Theme.Muted)}>ROUTE UNAVAILABLE</color></size>";
                extra += Mathf.Ceil(captionSize + 6);
            }
            tracker.sizeDelta = new Vector2(421, (reduced ? TrackerReducedHeight : TrackerHeight) + extra);
            status.alignment = reduced ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
            actionLabel.rectTransform.offsetMin = new Vector2(24, 12);
            actionLabel.rectTransform.offsetMax = new Vector2(-68, -76 - extra);
            branchCounts.rectTransform.offsetMax = new Vector2(-68, -76 - extra);
        }

        // The catalog is the only source of objective names.
        public static string ObjectiveAction(CampaignObjectiveId id)
            => CampaignMissionController.Instance?.ContentCatalog?.FindObjective(id)?.title ?? string.Empty;

        private string NavigationAction() => CampaignRules.TankIsNext(Campaign.State) ? CampaignRules.TankStatus(Campaign.State)
            : Campaign.State.phase == CampaignPhase.Preparing ? CampaignRules.PartyGateText(Campaign.State.partyGate, true)
            : Campaign.State.phase == CampaignPhase.AwaitingParty ? CampaignRules.PartyGateText(Campaign.State.partyGate, false)
            : ObjectiveAction(TrackedObjectiveId);

        private void SetRouteUnavailable(bool unavailable)
        {
            if (routeUnavailable == unavailable) return;
            routeUnavailable = unavailable;
            RefreshTracker();
        }

        private void ResetNavigation()
        {
            navigationKey = default;
            routeCorners = Array.Empty<Vector3>();
            nextNavigationUpdate = 0;
            ResetMarkerBearing();
            if (marker != null) marker.gameObject.SetActive(false);
            HideFactoryMarkers();
            SetRouteUnavailable(false);
        }

        private void ResetMarkerBearing()
        {
            markerAtEdge = false;
            lastBearing = Vector2.zero;
        }

        private void UpdateNavigation(PlayerHealth owner)
        {
            var campaign = Campaign;
            if (campaign == null || !campaign.IsSpawned || owner == null || !owner.CanUseCombat
                || !HudLayout.GameplayVisible || panel.gameObject.activeSelf)
            { ResetNavigation(); return; }
            var state = campaign.State;
            var phase = state.phase;
            if (phase is not (CampaignPhase.Exploring or CampaignPhase.Preparing or CampaignPhase.AwaitingParty))
            { ResetNavigation(); return; }
            var camera = owner.GetComponent<MouseMovement>()?.BodyCam;
            if (camera == null || !camera.isActiveAndEnabled)
            { ResetNavigation(); return; }

            if (FactoryDestinationsActive(state))
            {
                if (navigationKey.owner != null || routeUnavailable) ResetNavigation();
                DrawFactoryDestinations(camera, PlayerFeet(owner));
                return;
            }
            HideFactoryMarkers();

            Component target;
            Vector3 destination = default;
            ulong tankId = 0;
            if (CampaignRules.TankIsNext(state))
            {
                if (state.tankStage != CampaignTankStage.Active) { ResetNavigation(); return; }
                target = campaign.TryGetTankTarget(out var tank) ? tank : null;
                if (tank != null) { destination = tank.transform.position; tankId = tank.NetworkObjectId; }
            }
            else if (phase == CampaignPhase.Exploring)
            {
                if (TrackedObjectiveId == CampaignObjectiveId.None) { ResetNavigation(); return; }
                var item = campaign.Objective(TrackedObjectiveId);
                target = item;
                if (item != null) destination = item.TargetPosition;
            }
            else
            {
                var volume = phase == CampaignPhase.Preparing ? campaign.Current?.preparationArea : campaign.Current?.boardingArea;
                target = volume;
                if (volume != null) destination = new Vector3(volume.bounds.center.x, volume.bounds.min.y + .1f, volume.bounds.center.z);
            }

            var key = (owner, campaign, state.chapter, phase, TrackedObjectiveId, target, tankId);
            if (!navigationKey.Equals(key))
            {
                ResetNavigation();
                navigationKey = key;
            }
            if (target == null)
            { SetRouteUnavailable(true); return; }

            // The gate's carve can outlive the door opening by a frame, even if the tracked branch changes.
            if (state.chapter == CampaignChapter.Factory && phase == CampaignPhase.Exploring && routeCorners.Length == 0)
            {
                if (campaign.doors?.Any(door => door != null && door.isActiveAndEnabled && door.rule == CampaignDoorRule.AfterInsertion
                    && door.leaf != null && door.speed > 0 && door.navigationBlocker != null && door.navigationBlocker.isActiveAndEnabled) == true)
                    insertionRouteReadyAt = Time.unscaledTime + RouteRefreshSeconds;
                if (Time.unscaledTime < insertionRouteReadyAt) return;
            }

            Vector3 feet = PlayerFeet(owner);
            if (Time.unscaledTime >= nextNavigationUpdate)
            {
                nextNavigationUpdate = Time.unscaledTime + RouteRefreshSeconds;
                RefreshRoute(feet, destination, target);
            }
            else if (IsRouteCorner && NearCorner(feet, routeCorners[nextRouteCorner]) && TrySampleFeet(feet, out var start))
            {
                int next = AdvanceCorner(routeCorners, nextRouteCorner, feet, start.position);
                if (next != nextRouteCorner)
                {
                    nextRouteCorner = next;
                    routeAnchor = IsRouteCorner ? routeCorners[next] : routeDestination;
                    ResetMarkerBearing();
                }
            }
            if (routeCorners.Length == 0) { marker.gameObject.SetActive(false); return; }
            DrawNavigation(camera, feet);
        }

        private bool IsRouteCorner => nextRouteCorner < routeCorners.Length - 1;

        private void RefreshRoute(Vector3 feet, Vector3 destination, Component target)
        {
            bool hadRoute = routeCorners.Length != 0;
            bool hadCorner = IsRouteCorner;
            if (!TryGuidancePath(feet, destination, target, out var corners))
            {
                routeCorners = Array.Empty<Vector3>();
                SetRouteUnavailable(true);
                return;
            }

            int next = AdvanceCorner(corners, Mathf.Min(1, corners.Length - 1), feet, corners[0]);
            bool isCorner = next < corners.Length - 1;
            bool sameCorner = hadCorner && isCorner && (corners[next] - routeAnchor).sqrMagnitude <= CornerMatch * CornerMatch
                && (corners[next] == routeAnchor || !NavMesh.Raycast(corners[0], routeAnchor, out _, NavMesh.AllAreas));
            routeCorners = corners;
            routeDestination = destination;
            nextRouteCorner = next;
            if (!sameCorner) routeAnchor = isCorner ? corners[next] : routeDestination;
            if (!hadRoute || hadCorner != isCorner || (isCorner && !sameCorner)) ResetMarkerBearing();
            SetRouteUnavailable(false);
        }

        private static int AdvanceCorner(Vector3[] corners, int index, Vector3 feet, Vector3 navPosition)
        {
            while (index < corners.Length - 1 && NearCorner(feet, corners[index])
                && !NavMesh.Raycast(navPosition, corners[index + 1], out _, NavMesh.AllAreas)) index++;
            return index;
        }

        private static bool NearCorner(Vector3 feet, Vector3 corner)
        {
            Vector3 delta = corner - feet;
            return Mathf.Abs(delta.y) <= CornerHeight && delta.x * delta.x + delta.z * delta.z <= 1f;
        }

        private void DrawNavigation(Camera camera, Vector3 feet)
        {
            float scale = canvas.scaleFactor;
            float pulse = nextCueUntil > Time.unscaledTime ? 1f + .12f * Mathf.Sin(Time.unscaledTime * 18f) : 1f;
            marker.localScale = Vector3.one * pulse;
            var safe = Screen.safeArea;
            var viewport = camera.pixelRect;
            const float iconExtent = 39;
            float xInset = (MarkerWidth * .5f * MarkerPulseMax + HudLayout.Margin) * scale;
            float topInset = (iconExtent * MarkerPulseMax + HudLayout.Margin) * scale;
            float bottomInset = ((LabelOffset + LabelHeight * .5f) * MarkerPulseMax + HudLayout.Margin) * scale;
            var bounds = Rect.MinMaxRect(Mathf.Max(safe.xMin, viewport.xMin) + xInset, Mathf.Max(safe.yMin, viewport.yMin) + bottomInset,
                Mathf.Min(safe.xMax, viewport.xMax) - xInset, Mathf.Min(safe.yMax, viewport.yMax) - topInset);
            if (bounds.width <= 0 || bounds.height <= 0) { marker.gameObject.SetActive(false); return; }

            Vector3 point = IsRouteCorner ? routeAnchor + Vector3.up * .65f : routeAnchor;
            var position = HudMarkerProjection.Project(camera, point, bounds, EdgePadding * scale,
                ref markerAtEdge, ref lastBearing, out var bearing);
            FillMarkerObstacles(safe, viewport.center, scale);

            // On the edge, slide along that side without making the arrow point at a HUD-induced offset.
            var slideBounds = bounds;
            if (markerAtEdge)
            {
                float xEdge = Mathf.Min(position.x - bounds.xMin, bounds.xMax - position.x);
                float yEdge = Mathf.Min(position.y - bounds.yMin, bounds.yMax - position.y);
                if (xEdge < yEdge) { slideBounds.xMin = position.x; slideBounds.xMax = position.x; }
                else { slideBounds.yMin = position.y; slideBounds.yMax = position.y; }
            }
            float icons = (markerAtEdge ? iconExtent : 15f) * MarkerPulseMax * scale;
            float width = MarkerWidth * .5f * MarkerPulseMax * scale;
            float depth = (LabelOffset + LabelHeight * .5f) * MarkerPulseMax * scale;
            float localMove = LocalHudMove * scale;
            var footprint = Rect.MinMaxRect(-width, -depth, width, icons);
            bool showDistance = HudMarkerProjection.TryAvoidHud(position, slideBounds, markerObstacles, footprint, localMove, out var placement);
            if (!showDistance && markerAtEdge && position.y <= bounds.yMin + .01f)
            {
                // Lift the group over the lower HUD without changing its horizontal bearing.
                var column = Rect.MinMaxRect(position.x, bounds.yMin, position.x, bounds.yMax);
                showDistance = HudMarkerProjection.TryAvoidHud(position, column, markerObstacles, footprint, float.PositiveInfinity, out placement);
            }
            var iconBounds = Rect.MinMaxRect(-icons, -icons, icons, icons);
            if (!showDistance && !HudMarkerProjection.TryAvoidHud(position, slideBounds, markerObstacles,
                iconBounds, markerAtEdge ? float.PositiveInfinity : localMove, out placement)
                && (!markerAtEdge || !HudMarkerProjection.TryAvoidHud(position, bounds, markerObstacles,
                    iconBounds, float.PositiveInfinity, out placement)))
            { marker.gameObject.SetActive(false); return; }

            var root = (RectTransform)marker.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, placement, null, out var local);
            marker.anchoredPosition = local;
            routeIcon.gameObject.SetActive(markerAtEdge);
            if (markerAtEdge)
                routeIcon.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(bearing.y, bearing.x) * Mathf.Rad2Deg - 90);
            navigation.gameObject.SetActive(showDistance);
            if (showDistance)
            {
                float height = routeAnchor.y - feet.y;
                navigation.text = $"{Mathf.CeilToInt(RouteLength(feet, routeCorners, nextRouteCorner))} m"
                    + (height >= 2.5f ? "\nHIGHER" : height <= -2.5f ? "\nLOWER" : "");
            }
            marker.gameObject.SetActive(true);
        }

        private void FillMarkerObstacles(Rect safe, Vector2 center, float scale)
        {
            float leftHeight = Campaign.InsertionPartySize > 1 ? 650 : 320;
            markerObstacles[0] = new Rect(safe.xMin, safe.yMin, 400 * scale, leftHeight * scale);
            float rightWidth = (HudLayout.CombatWidth + 50) * scale;
            markerObstacles[1] = new Rect(safe.xMax - rightWidth, safe.yMin, rightWidth, 240 * scale);
            tracker.GetWorldCorners(hudCorners);
            markerObstacles[2] = Rect.MinMaxRect(hudCorners[0].x, hudCorners[0].y, hudCorners[2].x, hudCorners[2].y);
            feedback.rectTransform.GetWorldCorners(hudCorners);
            markerObstacles[3] = Rect.MinMaxRect(hudCorners[0].x, safe.yMin + HudLayout.SubtitleBottom * scale, hudCorners[2].x, hudCorners[2].y);
            markerObstacles[4] = new Rect(center - Vector2.one * (20 * scale), Vector2.one * (40 * scale));
        }

        private static Vector3 PlayerFeet(PlayerHealth owner)
        {
            var controller = owner.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled) return owner.transform.position;
            var bounds = controller.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static bool TrySampleFeet(Vector3 feet, out NavMeshHit start)
            => NavMesh.SamplePosition(feet, out start, .8f, NavMesh.AllAreas) && Mathf.Abs(start.position.y - feet.y) <= CornerHeight;

        private static bool TargetVisible(Vector3 from, Vector3 to, Component target)
        {
            if (!Physics.Linecast(from, to, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return target is CampaignInteractable item ? item.OwnsCollider(hit.collider)
                : target is NetworkObject && hit.collider.transform.IsChildOf(target.transform);
        }

        private bool TryGuidancePath(Vector3 feet, Vector3 to, Component target, out Vector3[] corners)
        {
            corners = Array.Empty<Vector3>();
            guidancePath ??= new NavMeshPath();
            if (!TrySampleFeet(feet, out var start)
                || !NavMesh.SamplePosition(to, out var end, 2f, NavMesh.AllAreas)
                // An interaction point may be above the floor. Sampling must not move up to a different storey.
                || end.position.y > to.y + .3f || to.y - end.position.y > 2f
                || !TargetVisible(end.position + Vector3.up * 1.55f, to, target)
                || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, guidancePath)
                || guidancePath.status != NavMeshPathStatus.PathComplete) return false;
            corners = guidancePath.corners;
            return corners.Length != 0;
        }

        private static float RouteLength(Vector3 from, Vector3[] corners, int first)
        {
            float length = 0;
            for (int i = first; i < corners.Length; i++)
            {
                length += Vector3.Distance(from, corners[i]);
                from = corners[i];
            }
            return length;
        }
    }
}
