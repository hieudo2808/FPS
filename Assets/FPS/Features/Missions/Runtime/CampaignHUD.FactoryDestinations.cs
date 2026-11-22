using TMPro;
using UnityEngine;

namespace FPS
{
    public sealed partial class CampaignHUD
    {
        private const float BranchLabelWidth = 128;
        private readonly Rect[] factoryTextObstacles = new Rect[4];

        private sealed class FactoryBranchHud
        {
            public RectTransform row, marker;
            public TMP_Text name, count, step, label;
            public CampaignInteractable target;
        }

        private static bool FactoryDestinationsActive(CampaignState state) => state.chapter == CampaignChapter.Factory
            && state.phase == CampaignPhase.Exploring && !(CampaignRules.UtilitiesReady(state) && CampaignRules.LogisticsReady(state));

        private FactoryBranchHud BuildFactoryBranch(Transform safe, string name)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(tracker, false);
            row.anchorMin = Vector2.up;
            row.anchorMax = Vector2.one;
            var heading = DeviceText(row, name + "Name", Vector2.up, Vector2.one, Theme.Small, FontStyles.Bold);
            var count = DeviceText(row, name + "Count", Vector2.up, Vector2.one, Theme.Small, FontStyles.Normal);
            heading.rectTransform.offsetMin = count.rectTransform.offsetMin = new Vector2(0, -30);
            count.alignment = TextAlignmentOptions.TopRight;
            count.color = Theme.Muted;
            var step = DeviceText(row, name + "Step", Vector2.zero, Vector2.one, Theme.Body, FontStyles.Normal);
            step.rectTransform.offsetMax = new Vector2(0, -30);

            var marker = new GameObject("Factory" + name + "Marker", typeof(RectTransform)).GetComponent<RectTransform>();
            marker.SetParent(safe, false);
            HudLayout.Corner(marker, new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 28));
            BuildNavigationDot(marker);
            var label = DeviceText(marker, "BranchLabel", Vector2.one * .5f, Vector2.one * .5f, Theme.Small, FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            row.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            heading.text = name;
            return new FactoryBranchHud { row = row, name = heading, count = count, step = step, marker = marker, label = label };
        }

        private void UpdateFactoryTracker(CampaignState state, float captionSize)
        {
            locationLabel.text = CampaignRules.ChapterName(state.chapter);
            status.text = CampaignRules.ChapterGoal(state.chapter);
            status.fontSize = Theme.Body;
            status.alignment = TextAlignmentOptions.TopLeft;
            status.rectTransform.offsetMin = new Vector2(24, 0);
            status.rectTransform.offsetMax = new Vector2(-24, -40);
            locationLabel.gameObject.SetActive(true);
            actionLabel.gameObject.SetActive(false);
            branchCounts.gameObject.SetActive(false);
            float top = 44 + Mathf.Ceil(status.GetPreferredValues(status.text, 373, 0).y);
            top = UpdateFactoryRow(powerHud, state, CampaignFactoryBranch.Power, 3, captionSize, top);
            top = UpdateFactoryRow(logisticsHud, state, CampaignFactoryBranch.Logistics, 2, captionSize, top);
            tracker.sizeDelta = new Vector2(421, top + 26);
        }

        private float UpdateFactoryRow(FactoryBranchHud hud, CampaignState state, CampaignFactoryBranch branch, int total,
            float captionSize, float top)
        {
            var next = CampaignRules.FactoryNext(state, branch);
            hud.target = next == CampaignObjectiveId.None ? null : Campaign.Objective(next);
            int progress = CampaignRules.FactoryProgress(state, branch);
            hud.name.fontSize = hud.count.fontSize = captionSize;
            hud.count.text = $"{progress}/{total}";
            hud.step.fontSize = Theme.Body;
            hud.step.text = progress == total ? "COMPLETE" : ObjectiveAction(next);
            float height = 30 + Mathf.Ceil(hud.step.GetPreferredValues(hud.step.text, 373, 0).y);
            hud.row.offsetMin = new Vector2(24, -top - height);
            hud.row.offsetMax = new Vector2(-24, -top);
            return top + height + 8;
        }

        private void HideFactoryMarkers()
        {
            if (powerHud == null) return;
            powerHud.marker.gameObject.SetActive(false);
            logisticsHud.marker.gameObject.SetActive(false);
        }

        private void DrawFactoryDestinations(Camera camera, Vector3 feet)
        {
            var safe = Screen.safeArea;
            var viewport = camera.pixelRect;
            float scale = canvas.scaleFactor;
            var bounds = Rect.MinMaxRect(Mathf.Max(safe.xMin, viewport.xMin), Mathf.Max(safe.yMin, viewport.yMin),
                Mathf.Min(safe.xMax, viewport.xMax), Mathf.Min(safe.yMax, viewport.yMax));
            FillMarkerObstacles(safe, viewport.center, scale);
            factoryTextObstacles[0] = VisibleTextBounds(subtitle);
            factoryTextObstacles[1] = VisibleTextBounds(progress);
            factoryTextObstacles[2] = VisibleTextBounds(feedback);
            factoryTextObstacles[3] = ScreenBounds(HUDManager.Instance?.VisibleInteractionPrompt);
            bool showPower = ProjectFactoryDestination(powerHud, camera, bounds, scale, out var power);
            bool showLogistics = ProjectFactoryDestination(logisticsHud, camera, bounds, scale, out var logistics);
            float fontSize = Theme.Small * scale >= TacticalUiTheme.MinimumReadableTextPixels ? Theme.Small : Theme.Body;
            string powerHint = showPower ? HeightHint(powerHud.target.TargetPosition.y - feet.y) : "";
            string logisticsHint = showLogistics ? HeightHint(logisticsHud.target.TargetPosition.y - feet.y) : "";
            var powerLabel = showPower ? FactoryLabelRect(power, power.x, powerHint, bounds, fontSize, scale) : default;
            var logisticsLabel = showLogistics ? FactoryLabelRect(logistics, logistics.x, logisticsHint, bounds, fontSize, scale) : default;
            float width = BranchLabelWidth * scale, gap = 8 * scale;
            if (showPower && showLogistics && bounds.width >= 2 * (width + gap)
                && (powerLabel.Overlaps(logisticsLabel) || powerLabel.Overlaps(DotBounds(logistics, scale))
                    || logisticsLabel.Overlaps(DotBounds(power, scale))))
            {
                float divider = Mathf.Clamp((power.x + logistics.x) * .5f, bounds.xMin + width + gap, bounds.xMax - width - gap);
                powerLabel = FactoryLabelRect(power, divider - width * .5f - gap, powerHint, bounds, fontSize, scale, DotBounds(logistics, scale));
                logisticsLabel = FactoryLabelRect(logistics, divider + width * .5f + gap, logisticsHint, bounds, fontSize, scale, DotBounds(power, scale));
            }
            if (showPower) DrawFactoryLabel(powerHud, "POWER" + powerHint, fontSize, power, powerLabel, scale);
            if (showLogistics) DrawFactoryLabel(logisticsHud, "LOGISTICS" + logisticsHint, fontSize, logistics, logisticsLabel, scale);
        }

        private Rect VisibleTextBounds(UnityEngine.UI.Text text)
            => text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text) ? ScreenBounds(text.rectTransform) : default;

        private Rect ScreenBounds(RectTransform rect)
        {
            if (rect == null) return default;
            rect.GetWorldCorners(hudCorners);
            return Rect.MinMaxRect(hudCorners[0].x, hudCorners[0].y, hudCorners[2].x, hudCorners[2].y);
        }

        private static Rect DotBounds(Vector2 point, float scale)
            => new Rect(point - Vector2.one * (14 * scale), Vector2.one * (28 * scale));

        private bool ProjectFactoryDestination(FactoryBranchHud hud, Camera camera, Rect bounds, float scale, out Vector2 point)
        {
            point = default;
            if (hud.target == null || !hud.target.isActiveAndEnabled)
            { hud.marker.gameObject.SetActive(false); return false; }
            Vector3 projected = camera.WorldToScreenPoint(hud.target.TargetPosition);
            point = projected;
            bool visible = projected.z >= camera.nearClipPlane && FitsFactoryHud(DotBounds(point, scale), bounds);
            hud.marker.gameObject.SetActive(visible);
            if (!visible) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hud.marker.parent, point, null, out var local);
            hud.marker.anchoredPosition = local;
            return true;
        }

        private static string HeightHint(float height) => height >= 2.5f ? "\nHIGHER" : height <= -2.5f ? "\nLOWER" : "";

        private Rect FactoryLabelRect(Vector2 point, float x, string hint, Rect bounds, float fontSize, float scale, Rect otherDot = default)
        {
            float width = BranchLabelWidth * scale;
            if (width > bounds.width) return default;
            float height = (hint.Length == 0 ? 1 : 2) * (fontSize + 5) * scale;
            var rect = new Rect(Mathf.Clamp(x - width * .5f, bounds.xMin, bounds.xMax - width), point.y - 18 * scale - height, width, height);
            if (!FitsFactoryHud(rect, bounds) || rect.Overlaps(otherDot)) rect.y = point.y + 18 * scale;
            return FitsFactoryHud(rect, bounds) && !rect.Overlaps(otherDot) ? rect : default;
        }

        private static void DrawFactoryLabel(FactoryBranchHud hud, string text, float fontSize, Vector2 point, Rect rect, float scale)
        {
            hud.label.gameObject.SetActive(rect.width > 0);
            if (rect.width <= 0) return;
            hud.label.text = text;
            hud.label.fontSize = fontSize;
            hud.label.rectTransform.sizeDelta = rect.size / scale;
            hud.label.rectTransform.anchoredPosition = (rect.center - point) / scale;
        }

        private bool FitsFactoryHud(Rect footprint, Rect bounds)
        {
            if (footprint.xMin < bounds.xMin || footprint.xMax > bounds.xMax
                || footprint.yMin < bounds.yMin || footprint.yMax > bounds.yMax) return false;
            // Keep the side panels and tracker; the centre only blocks occupied text bands.
            for (int i = 0; i < 3; i++)
                if (footprint.Overlaps(markerObstacles[i])) return false;
            foreach (var text in factoryTextObstacles)
                if (text.width > 0 && footprint.Overlaps(text)) return false;
            return true;
        }
    }
}
