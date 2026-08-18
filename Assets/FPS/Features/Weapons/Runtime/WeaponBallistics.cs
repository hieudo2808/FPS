using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Allocation-free deterministic spread and range falloff helpers used by
    /// the authoritative fire path. A shot sequence always produces the same
    /// pellet directions on host and dedicated server.
    /// </summary>
    public static class WeaponBallistics
    {
        public static uint BuildShotSeed(ulong ownerClientId, ushort shotSequence, byte weaponSlot)
        {
            uint low = (uint)ownerClientId;
            uint high = (uint)(ownerClientId >> 32);
            return Hash(low ^ RotateLeft(high, 13) ^ ((uint)shotSequence << 8) ^ weaponSlot);
        }

        public static Vector3 GetProjectileDirection(
            Vector3 forward,
            float coneHalfAngleDegrees,
            uint shotSeed,
            int projectileIndex)
        {
            Vector3 normalizedForward = forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : Vector3.forward;
            float angle = Mathf.Max(0f, coneHalfAngleDegrees);
            if (angle <= 0.0001f)
                return normalizedForward;

            uint indexSeed = Hash(shotSeed ^ unchecked((uint)projectileIndex * 0x9E3779B9u));
            float radius = Mathf.Sqrt(ToUnitFloat(Hash(indexSeed ^ 0xA511E9B3u)));
            float theta = ToUnitFloat(Hash(indexSeed ^ 0x63D83595u)) * Mathf.PI * 2f;
            float diskX = Mathf.Cos(theta) * radius;
            float diskY = Mathf.Sin(theta) * radius;

            Vector3 referenceUp = Mathf.Abs(Vector3.Dot(normalizedForward, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            Vector3 right = Vector3.Cross(referenceUp, normalizedForward).normalized;
            Vector3 up = Vector3.Cross(normalizedForward, right).normalized;
            float coneRadius = Mathf.Tan(angle * Mathf.Deg2Rad);
            return (normalizedForward + right * (diskX * coneRadius) + up * (diskY * coneRadius)).normalized;
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        private static float ToUnitFloat(uint value)
        {
            // Use 24 significant bits so conversion is identical on all targets.
            return (value & 0x00FFFFFFu) / 16777216f;
        }
    }
}
