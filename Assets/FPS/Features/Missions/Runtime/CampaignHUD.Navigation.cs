using System;
using System.Collections.Generic;
using System.Linq;
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
        private Text navigation, branchArrow;
        private TMP_Text locationLabel, actionLabel, branchCounts;
        private const float TrackerHeight = 172, TrackerReducedHeight = 88;
        private RectTransform tracker, marker;
        private Image destinationIcon;
        private RectTransform routeIcon;
        private NavMeshPath guidancePath;
        private Vector3[] routeCorners = Array.Empty<Vector3>();
        private int nextRouteCorner = 1;
        private readonly List<CampaignObjectiveId> trackableIds = new();
        private uint observedRevision = uint.MaxValue;
        private CampaignMissionController trackingCampaign;
        private float nextNavigationUpdate, routeDistance;
        private Vector3 routeTarget;
        private bool routeComplete, hasRouteTarget, routeComputed;
        private float nextCueUntil;
        private readonly Rect[] markerObstacles = new Rect[4];
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

        public void CycleTrackedBranch()
        {
            if (Campaign == null || Campaign.State.chapter != CampaignChapter.Factory || Campaign.State.phase != CampaignPhase.Exploring) return;
            var target = CampaignObjectiveSelection.CycleFactoryBranch(Campaign.State, TrackedObjectiveId);
            if (target != CampaignObjectiveId.None) SetTracked(target);
        }

        private void SetTracked(CampaignObjectiveId id)
        {
            if (TrackedObjectiveId == id) return;
            TrackedObjectiveId = id;
            nextThreatCheck = 0;
            nextNavigationUpdate = 0; routeComputed = false;
            RefreshHintTarget();
            TrackingChanged?.Invoke();
            RefreshTracker();
        }

        private void ResolveTracking(PlayerHealth owner)
        {
            if (Campaign == null || owner == null) return;
            if (trackingCampaign == Campaign && observedRevision == Campaign.State.revision) return;
            trackingCampaign = Campaign; observedRevision = Campaign.State.revision;
            nextNavigationUpdate = 0; routeComputed = false;
            trackableIds.Clear();
            if (Campaign.Current?.objectives != null)
                foreach (var item in Campaign.Current.objectives.Where(o => o != null && (o.interactionBody != null || !o.documentOnly))
                    .OrderBy(o => CatalogOrder(o.objectiveId)))
                    if (!trackableIds.Contains(item.objectiveId)) trackableIds.Add(item.objectiveId);
            SetTracked(CampaignObjectiveSelection.Resolve(Campaign.State, trackableIds, TrackedObjectiveId, id =>
            {
                var item = Campaign.Objective(id);
                return item == null ? null : TryGuidancePath(owner.transform.position, item.TargetPosition, item)
                    ? routeDistance : Vector3.Distance(owner.transform.position, item.TargetPosition);
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
            branchArrow = Label(tracker, "BranchArrow", Vector2.up, Vector2.up, 24);
            branchArrow.rectTransform.pivot = Vector2.up;
            branchArrow.rectTransform.sizeDelta = new Vector2(24, 40);
            branchArrow.verticalOverflow = VerticalWrapMode.Overflow;
            branchArrow.text = "▶";
            marker = Box(safe, "NavigationMarker", Vector2.zero, Vector2.one);
            marker.GetComponent<Image>().color = Color.clear;
            marker.anchorMin = marker.anchorMax = marker.pivot = new Vector2(.5f, .5f);
            marker.sizeDelta = new Vector2(360, 128);
            var icon = Box(marker, "Destination", Vector2.zero, Vector2.one);
            icon.anchorMin = icon.anchorMax = new Vector2(.5f, .5f);
            icon.sizeDelta = new Vector2(28, 28);
            destinationIcon = icon.GetComponent<Image>(); HudSkin.Apply(destinationIcon, "FPSSample/GoalMarkerCentre", Theme.Text);
            routeIcon = Box(marker, "RouteCorner", Vector2.zero, Vector2.one);
            routeIcon.GetComponent<Image>().color = Color.clear;
            routeIcon.anchorMin = routeIcon.anchorMax = new Vector2(.5f, .5f); routeIcon.sizeDelta = new Vector2(34, 24);
            var arrow = Box(routeIcon, "Arrow", Vector2.zero, Vector2.one);
            arrow.anchorMin = arrow.anchorMax = new Vector2(.5f, .5f); arrow.sizeDelta = new Vector2(32, 24);
            arrow.localRotation = Quaternion.identity;
            HudSkin.Apply(arrow.GetComponent<Image>(), "FPSSample/GoalMarker", Theme.Text);
            navigation = Label(marker, "Distance", new Vector2(.025f, -.20f), new Vector2(.975f, .35f), 24);
            navigation.alignment = TextAnchor.MiddleCenter;
            navigation.resizeTextForBestFit = true;
            navigation.resizeTextMinSize = 16;
            navigation.resizeTextMaxSize = 24;
            navigation.horizontalOverflow = HorizontalWrapMode.Overflow;
            marker.gameObject.SetActive(false);
        }

        private void UpdateTracker(PlayerHealth owner)
        {
            bool visible = HudLayout.GameplayVisible && owner != null && owner.CanUseCombat && !panel.gameObject.activeSelf;
            tracker.gameObject.SetActive(visible);
            if (!visible) return;
            var s = Campaign.State;
            // The chapter goal stays on screen for the whole chapter; the step below it changes.
            string goal = "<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Text) + ">" + CampaignRules.ChapterGoal(s.chapter) + "</color>";
            locationLabel.text = CampaignRules.ChapterName(s.chapter) + " — " + goal;
            var item = s.phase == CampaignPhase.Exploring ? Campaign.Objective(TrackedObjectiveId) : null;
            status.text = s.phase switch
            {
                CampaignPhase.Exploring => item != null ? ObjectiveAction(item.objectiveId) : "Recover the evidence case",
                CampaignPhase.Preparing => s.tankStage == CampaignTankStage.Active ? "Evade the Tank" : "Prepare for extraction",
                CampaignPhase.Encounter => "Hold the extraction route",
                CampaignPhase.AwaitingParty => "Board with the squad",
                CampaignPhase.Transitioning => "Hold formation",
                CampaignPhase.Insertion => "Reach the facility",
                CampaignPhase.Completed => "Campaign complete",
                _ => "Mission failed"
            };
            bool factoryTank = CampaignRules.TankIsNext(s);
            if (factoryTank) status.text = s.tankStage == CampaignTankStage.Active ? "Eliminate the Tank"
                : "Tank incoming";
            float captionSize = Theme.Small * canvas.scaleFactor >= TacticalUiTheme.MinimumReadableTextPixels ? Theme.Small : Theme.Body;
            actionLabel.fontSize = branchCounts.fontSize = locationLabel.fontSize = captionSize;
            // Small screens use the larger caption size; the goal matters more than the place name when both cannot fit.
            // Below the reference resolution the typography pass enlarges captions, so only the goal is sure to fit.
            if (canvas.scaleFactor < .99f || captionSize != Theme.Small
                || locationLabel.GetPreferredValues(locationLabel.text, 10000, 1000).x > 373) locationLabel.text = goal;
            actionLabel.lineSpacing = branchCounts.lineSpacing = captionSize == Theme.Small ? 5 : 3;
            actionLabel.text = s.phase switch
            {
                CampaignPhase.Preparing => CampaignRules.PartyGateText(s.partyGate, true),
                CampaignPhase.Encounter => $"{Mathf.CeilToInt(Campaign.Remaining)}s remaining / Defend the approach",
                CampaignPhase.AwaitingParty => CampaignRules.PartyGateText(s.partyGate, false),
                CampaignPhase.Transitioning => "Wait for the receiving door",
                _ => ""
            };
            if (factoryTank) actionLabel.text = CampaignRules.TankStatus(s);
            bool factoryBranches = s.phase == CampaignPhase.Exploring && s.chapter == CampaignChapter.Factory;
            if (factoryBranches)
            {
                string branches = FactoryBranchProgress();
                if (!factoryTank)
                {
                    // The switch key sits on the branch being followed instead of taking a row of its own.
                    string[] lines = branches.Replace("▶", "  ").Split('\n');
                    int active = branches.StartsWith("▶") ? 0 : branches.Contains("\n▶") ? 1 : -1;
                    if (active >= 0 && CampaignRules.CanSwitchFactoryBranch(s))
                        lines[active] += "   <color=#" + ColorUtility.ToHtmlStringRGB(Theme.Muted) + ">[" + SurvivalHotbar.Key("Track", "T") + "] SWITCH</color>";
                    actionLabel.text = string.Join("\n", lines);
                }
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
            branchArrow.gameObject.SetActive(!reduced && factoryBranches && branchArrow.text.Length > 0);
            status.rectTransform.offsetMin = new Vector2(24, reduced ? 24 : 100);
            status.rectTransform.offsetMax = new Vector2(-24, reduced ? -24 : -40);
            status.fontSize = Theme.Body;
            bool longTitle = status.GetPreferredValues(status.text, 10000, 1000).x > 373;
            float extra = !reduced && longTitle && captionSize == Theme.Body ? 30 : 0;
            if (longTitle && captionSize == Theme.Small) status.fontSize = Theme.Small;
            tracker.sizeDelta = new Vector2(421, (reduced ? TrackerReducedHeight : TrackerHeight) + extra);
            status.alignment = reduced ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
            actionLabel.rectTransform.offsetMin = new Vector2(24, 12);
            actionLabel.rectTransform.offsetMax = new Vector2(-68, -76 - extra);
            branchCounts.rectTransform.offsetMax = new Vector2(-68, -76 - extra);
            if (extra > 0) branchArrow.rectTransform.anchoredPosition += new Vector2(0, -extra);
        }

        // The catalog is the only source of objective names.
        public static string ObjectiveAction(CampaignObjectiveId id)
            => CampaignMissionController.Instance?.ContentCatalog?.FindObjective(id)?.title ?? string.Empty;

        private string FactoryBranchProgress()
        {
            var s = Campaign.State;
            int power = CampaignRules.FactoryProgress(s, CampaignFactoryBranch.Power);
            int logistics = CampaignRules.FactoryProgress(s, CampaignFactoryBranch.Logistics);
            branchCounts.text = $"{power}/3\n{logistics}/2";
            string branches = FormatFactoryBranches(TrackedObjectiveId);
            int active = branches.IndexOf('▶');
            branchArrow.fontSize = Mathf.RoundToInt(actionLabel.fontSize);
            branchArrow.text = active < 0 ? "" : "→";
            branchArrow.rectTransform.anchoredPosition = new Vector2(24, active == 0 ? -75 : -102);
            return branches;
        }

        private static string FormatFactoryBranches(CampaignObjectiveId tracked)
        {
            bool powerActive = tracked is CampaignObjectiveId.FactoryIsolate or CampaignObjectiveId.FactoryBackup or CampaignObjectiveId.FactoryGenerator;
            bool logisticsActive = tracked is CampaignObjectiveId.FactoryManifest or CampaignObjectiveId.FactoryShipping;
            return $"{(powerActive ? "▶" : "  ")}  Power\n{(logisticsActive ? "▶" : "  ")}  Logistics";
        }

        private string NavigationAction() => CampaignRules.TankIsNext(Campaign.State) ? CampaignRules.TankStatus(Campaign.State)
            : Campaign.State.phase == CampaignPhase.Preparing ? CampaignRules.PartyGateText(Campaign.State.partyGate, true)
            : Campaign.State.phase == CampaignPhase.AwaitingParty ? CampaignRules.PartyGateText(Campaign.State.partyGate, false)
            : ObjectiveAction(TrackedObjectiveId);

        private void UpdateNavigation(PlayerHealth owner)
        {
            var phase = Campaign.State.phase;
            if (!HudLayout.GameplayVisible || panel.gameObject.activeSelf || !owner.CanUseCombat
                || phase is not (CampaignPhase.Exploring or CampaignPhase.Preparing or CampaignPhase.AwaitingParty))
            { marker.gameObject.SetActive(false); return; }
            var item = phase == CampaignPhase.Exploring && !CampaignRules.TankIsNext(Campaign.State) ? Campaign.Objective(TrackedObjectiveId) : null;
            var volume = phase == CampaignPhase.Preparing ? Campaign.Current?.preparationArea : Campaign.Current?.boardingArea;
            bool tankTarget = CampaignRules.TankIsNext(Campaign.State) && Campaign.State.tankStage == CampaignTankStage.Active;
            hasRouteTarget = phase == CampaignPhase.Exploring ? item != null || tankTarget : volume != null;
            if (!hasRouteTarget) { marker.gameObject.SetActive(false); return; }
            Vector3 tankPosition = default;
            bool routeToTank = tankTarget && Campaign.TryGetTankTarget(out tankPosition);
            if (routeToTank)
                routeTarget = tankPosition;
            else if (item != null) routeTarget = item.TargetPosition;
            else if (volume != null) routeTarget = volume.bounds.center - Vector3.up * (volume.bounds.extents.y - .1f);
            else { hasRouteTarget = false; marker.gameObject.SetActive(false); return; }
            if (Time.unscaledTime >= nextNavigationUpdate)
            {
                nextNavigationUpdate = Time.unscaledTime + 1f;
                routeComplete = TryGuidancePath(owner.transform.position, routeTarget, item);
                routeCorners = routeComplete ? guidancePath.corners : Array.Empty<Vector3>();
                nextRouteCorner = 1;
                routeComputed = true;
            }
            var camera = owner.GetComponentInChildren<Camera>();
            if (camera == null)
            { marker.gameObject.SetActive(false); return; }
            Vector3 point = routeTarget;
            bool corner = false;
            if (routeComplete)
            {
                // The first corner is the sampled start. Follow intermediate turns consistently, independent of camera occlusion.
                while (nextRouteCorner < routeCorners.Length - 1
                    && Vector3.ProjectOnPlane(routeCorners[nextRouteCorner] - owner.transform.position, Vector3.up).sqrMagnitude < 2.25f)
                    nextRouteCorner++;
                if (nextRouteCorner < routeCorners.Length - 1)
                { point = routeCorners[nextRouteCorner] + Vector3.up * .65f; corner = true; }
            }
            float scale = canvas.scaleFactor;
            float pulse = nextCueUntil > Time.unscaledTime ? 1f + .12f * Mathf.Sin(Time.unscaledTime * 18f) : 1f;
            marker.localScale = Vector3.one * pulse;
            var bounds = Screen.safeArea;
            float horizontalInset = (marker.rect.width * .5f * pulse + HudLayout.Margin) * scale;
            bounds.xMin += horizontalInset; bounds.xMax -= horizontalInset;
            bounds.yMin += (marker.rect.height * .7f * pulse + HudLayout.Margin) * scale;
            bounds.yMax -= (marker.rect.height * .5f * pulse + HudLayout.Margin) * scale;
            if (bounds.width <= 0 || bounds.height <= 0) { marker.gameObject.SetActive(false); return; }
            var screenCenter = camera.pixelRect.center;
            var position = HudMarkerProjection.Project(camera, point, bounds, out bool edge);
            var bearing = position - screenCenter;
            bool nearReticle = !edge && (position - screenCenter).sqrMagnitude < 48 * 48;
            var central = Screen.safeArea;
            central.yMin += HudLayout.SubtitleBottom * scale;
            feedback.rectTransform.GetWorldCorners(hudCorners);
            central.xMin = hudCorners[0].x; central.xMax = hudCorners[2].x;
            central.yMax = hudCorners[2].y;
            // Avoid the full distance-label rectangle, rather than only its marker anchor.
            float labelHalfWidth = Mathf.Max(0, marker.rect.width * .5f * pulse - 60) * scale;
            float labelDepth = Mathf.Max(0, marker.rect.height * .7f * pulse - 60) * scale;
            central.xMin -= labelHalfWidth; central.xMax += labelHalfWidth;
            central.yMin -= labelDepth; central.yMax += labelDepth;
            var centralWithMargin = new Rect(central.xMin - 60 * scale, central.yMin - 60 * scale,
                central.width + 120 * scale, central.height + 120 * scale);
            if (!nearReticle && (edge || centralWithMargin.Contains(position)))
            {
                var left = Screen.safeArea;
                left.xMax = left.xMin + 400 * scale; left.yMax = left.yMin + 650 * scale;
                var right = Screen.safeArea;
                right.xMin = right.xMax - (HudLayout.CombatWidth + 50) * scale; right.yMax = right.yMin + 650 * scale;
                tracker.GetWorldCorners(hudCorners);
                var top = Rect.MinMaxRect(hudCorners[0].x, hudCorners[0].y, hudCorners[2].x, hudCorners[2].y);
                markerObstacles[0] = left; markerObstacles[1] = right; markerObstacles[2] = top;
                markerObstacles[3] = central;
                position = HudMarkerProjection.AvoidHud(position, bounds, markerObstacles, 60 * scale);
            }
            var root = (RectTransform)marker.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, position, null, out var local);
            marker.anchoredPosition = local;
            navigation.rectTransform.anchorMin = new Vector2(.025f, nearReticle ? .65f : -.20f);
            navigation.rectTransform.anchorMax = new Vector2(.975f, nearReticle ? 1.2f : .35f);
            destinationIcon.gameObject.SetActive(!edge && !corner && !nearReticle);
            routeIcon.gameObject.SetActive((edge || corner || !routeComplete) && !nearReticle);
            routeIcon.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(bearing.y, bearing.x) * Mathf.Rad2Deg - 90);
            string distance = routeComplete ? $"{Mathf.CeilToInt(routeDistance)} m" : $"{Mathf.CeilToInt(Vector3.Distance(owner.transform.position, routeTarget))} m TO TARGET";
            float height = routeTarget.y - owner.transform.position.y;
            navigation.text = distance + (height >= 2.5f ? "\nHIGHER" : height <= -2.5f ? "\nLOWER" : "");
            navigation.color = routeComplete ? Theme.Text : Theme.Muted;
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
