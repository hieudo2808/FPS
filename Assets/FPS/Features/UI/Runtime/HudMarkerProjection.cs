using UnityEngine;

namespace FPS
{
    /// <summary>Screen-space marker math, including behind-camera projection.</summary>
    public static class HudMarkerProjection
    {
        public static Vector2 Project(Camera camera, Vector3 worldPoint, Rect bounds, float reentryPadding,
            ref bool edge, ref Vector2 lastBearing, out Vector2 bearing)
        {
            var projected = camera.WorldToScreenPoint(worldPoint);
            var point = new Vector2(projected.x, projected.y);
            var screenCenter = camera.pixelRect.center;
            bool inFront = projected.z > camera.nearClipPlane;
            var direction = point - screenCenter;
            if (!inFront)
            {
                var delta = worldPoint - camera.transform.position;
                delta.y = 0;
                var right = camera.transform.right;
                float rear = -Vector3.Dot(Vector3.Cross(right, Vector3.up), delta);
                if (rear > 0)
                {
                    // A point behind the player needs a turn, even when it is lower than the camera.
                    float side = Vector3.Dot(right, delta);
                    if (Mathf.Abs(side) <= rear * .035f) side = lastBearing.x;
                    direction = side < 0 ? Vector2.left : Vector2.right;
                }
                else
                {
                    var view = camera.worldToCameraMatrix.MultiplyPoint3x4(worldPoint);
                    var clip = camera.projectionMatrix * new Vector4(view.x, view.y, view.z, 1);
                    direction = new Vector2(clip.x * camera.pixelWidth, clip.y * camera.pixelHeight);
                }
            }
            if (direction.sqrMagnitude > .0001f) lastBearing = direction.normalized;
            bearing = lastBearing.sqrMagnitude > 0 ? lastBearing : Vector2.right;

            float padding = edge ? Mathf.Min(reentryPadding, Mathf.Min(bounds.width, bounds.height) * .25f) : 0;
            edge = !inFront || point.x < bounds.xMin + padding || point.x > bounds.xMax - padding
                || point.y < bounds.yMin + padding || point.y > bounds.yMax - padding;
            if (!edge) return point;

            if (!bounds.Contains(screenCenter)) screenCenter = bounds.center;
            float x = bearing.x > 0 ? bounds.xMax - screenCenter.x : bounds.xMin - screenCenter.x;
            float y = bearing.y > 0 ? bounds.yMax - screenCenter.y : bounds.yMin - screenCenter.y;
            float t = Mathf.Min(Mathf.Abs(bearing.x) > .0001f ? x / bearing.x : float.PositiveInfinity,
                Mathf.Abs(bearing.y) > .0001f ? y / bearing.y : float.PositiveInfinity);
            return new Vector2(Mathf.Clamp(screenCenter.x + bearing.x * t, bounds.xMin, bounds.xMax),
                Mathf.Clamp(screenCenter.y + bearing.y * t, bounds.yMin, bounds.yMax));
        }

        public static Vector2 AvoidCornerHud(Vector2 point, Rect bounds, Rect occupied, float clearance)
        {
            occupied.xMin -= clearance; occupied.xMax += clearance;
            occupied.yMin -= clearance; occupied.yMax += clearance;
            if (!occupied.Contains(point)) return point;
            if (Mathf.Abs(point.x - bounds.xMin) < 1f || Mathf.Abs(point.x - bounds.xMax) < 1f)
                point.y = occupied.center.y < bounds.center.y ? occupied.yMax : occupied.yMin;
            else
                point.x = occupied.center.x < bounds.center.x ? occupied.xMax : occupied.xMin;
            point.x = Mathf.Clamp(point.x, bounds.xMin, bounds.xMax);
            point.y = Mathf.Clamp(point.y, bounds.yMin, bounds.yMax);
            return point;
        }

        public static bool TryAvoidHud(Vector2 point, Rect bounds, Rect[] occupied, Rect footprint, float maxMove, out Vector2 placement)
        {
            placement = point;
            if (Clear(point)) return true;
            Vector2 best = point;
            float distance = maxMove * maxMove;
            bool found = false;
            foreach (var area in occupied)
            {
                float left = area.xMin - footprint.xMax - 1, right = area.xMax - footprint.xMin + 1;
                float bottom = area.yMin - footprint.yMax - 1, top = area.yMax - footprint.yMin + 1;
                Test(point.x, top); Test(point.x, bottom); Test(left, point.y); Test(right, point.y);
                Test(left, top); Test(right, top); Test(left, bottom); Test(right, bottom);
            }
            Test(bounds.xMin, bounds.yMin); Test(bounds.xMin, bounds.yMax);
            Test(bounds.xMax, bounds.yMin); Test(bounds.xMax, bounds.yMax);
            placement = best;
            return found;

            bool Clear(Vector2 candidate)
            {
                foreach (var area in occupied)
                    if (candidate.x + footprint.xMax > area.xMin && candidate.x + footprint.xMin < area.xMax
                        && candidate.y + footprint.yMax > area.yMin && candidate.y + footprint.yMin < area.yMax) return false;
                return true;
            }
            void Test(float x, float y)
            {
                var candidate = new Vector2(Mathf.Clamp(x, bounds.xMin, bounds.xMax), Mathf.Clamp(y, bounds.yMin, bounds.yMax));
                float squared = (candidate - point).sqrMagnitude;
                if (squared > distance || (found && squared == distance) || !Clear(candidate)) return;
                best = candidate; distance = squared; found = true;
            }
        }
    }
}
