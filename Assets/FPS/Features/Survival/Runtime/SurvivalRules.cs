using UnityEngine;

namespace FPS
{
    public static class SurvivalRules
    {
        public static bool IsNewer(uint sequence, uint previous) => unchecked((int)(sequence - previous)) > 0;
        public static int Capacity(PickupType kind) => kind switch
        {
            PickupType.FragGrenade or PickupType.IncendiaryGrenade => 3,
            PickupType.Medkit or PickupType.Antidote => 2,
            _ => 0
        };
        public static bool CanAdd(PickupType type, int count, int amount) => count >= 0 && amount > 0 && amount <= Capacity(type) - count;
        public static float FragDamage(float distance) => distance > 4f || !float.IsFinite(distance) ? 0f : Mathf.Lerp(100f, 25f, Mathf.Clamp01(distance / 4f));
        public static float UseDuration(ConsumableKind kind, bool self) => kind == ConsumableKind.Medkit ? (self ? 4f : 2.5f) : (self ? 5f : 3f);
        public static bool ValidThrowDirection(Vector3 direction, Vector3 forward)
        {
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z) || direction.sqrMagnitude < .5f || direction.sqrMagnitude > 1.5f) return false;
            // Allow near-vertical pitch. Yaw must agree with the server movement heading.
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
            return planar.sqrMagnitude < .02f || Vector3.Dot(planar.normalized, Vector3.ProjectOnPlane(forward, Vector3.up).normalized) >= -.25f;
        }
    }
}
