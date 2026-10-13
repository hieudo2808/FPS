using UnityEngine;

namespace FPS
{
    /// <summary>Screen-space marker math, including behind-camera projection.</summary>
    public static class HudMarkerProjection
    {
        public static Vector2 Project(Vector3 projected, Vector2 screenCenter, Rect bounds, out bool edge)
        {
            var point = new Vector2(projected.x, projected.y);
            edge = projected.z <= 0 || !bounds.Contains(point);
            if (!edge) return point;
            var direction = point - screenCenter;
            if (projected.z <= 0) direction = -direction;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
            float x = direction.x > 0 ? bounds.xMax - screenCenter.x : screenCenter.x - bounds.xMin;
            float y = direction.y > 0 ? bounds.yMax - screenCenter.y : screenCenter.y - bounds.yMin;
            float t = Mathf.Min(Mathf.Abs(direction.x) > .0001f ? x / Mathf.Abs(direction.x) : float.PositiveInfinity,
                Mathf.Abs(direction.y) > .0001f ? y / Mathf.Abs(direction.y) : float.PositiveInfinity);
            return screenCenter + direction * t;
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

        public static Vector2 AvoidHud(Vector2 point, Rect bounds, Rect[] occupied, float clearance)
        {
            if (Clear(point)) return point;
            Vector2 best = point;
            float distance = float.PositiveInfinity;
            Test(new Vector2(bounds.xMin, bounds.yMin)); Test(new Vector2(bounds.xMin, bounds.yMax));
            Test(new Vector2(bounds.xMax, bounds.yMin)); Test(new Vector2(bounds.xMax, bounds.yMax));
            foreach (var area in occupied)
            {
                for (int i = 0; i < 2; i++)
                {
                    float y = Mathf.Clamp(i == 0 ? area.yMin - clearance - 1 : area.yMax + clearance + 1, bounds.yMin, bounds.yMax);
                    Test(new Vector2(bounds.xMin, y)); Test(new Vector2(bounds.xMax, y));
                    float x = Mathf.Clamp(i == 0 ? area.xMin - clearance - 1 : area.xMax + clearance + 1, bounds.xMin, bounds.xMax);
                    Test(new Vector2(x, bounds.yMin)); Test(new Vector2(x, bounds.yMax));
                }
            }
            return best;

            bool Clear(Vector2 candidate)
            {
                foreach (var area in occupied)
                    if (candidate.x >= area.xMin - clearance && candidate.x <= area.xMax + clearance
                        && candidate.y >= area.yMin - clearance && candidate.y <= area.yMax + clearance) return false;
                return true;
            }
            void Test(Vector2 candidate)
            {
                float squared = (candidate - point).sqrMagnitude;
                if (squared >= distance || !Clear(candidate)) return;
                best = candidate; distance = squared;
            }
        }
    }
}
