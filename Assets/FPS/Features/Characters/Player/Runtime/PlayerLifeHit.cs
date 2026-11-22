using UnityEngine;

namespace FPS
{
    public enum PlayerHitSide : byte { Front, Back, Left, Right }
    public enum PlayerHitRegion : byte { Body, Head, Leg }

    /// <summary>
    /// The hit that put a player down or killed them, packed into one replicated byte so every peer picks
    /// the same 3P Death clip: region (2 bits), side the hit came from (2 bits), heavy (1 bit), variant (3 bits).
    /// </summary>
    public readonly struct PlayerLifeHit
    {
        public readonly PlayerHitRegion region;
        public readonly PlayerHitSide side;
        public readonly bool heavy;
        public readonly byte variant;

        public PlayerLifeHit(PlayerHitRegion region, PlayerHitSide side, bool heavy, byte variant)
        {
            this.region = region;
            this.side = side;
            this.heavy = heavy;
            this.variant = (byte)(variant & 7);
        }

        public byte Pack() => (byte)((byte)region | (byte)side << 2 | (heavy ? 1 : 0) << 4 | variant << 5);

        public static PlayerLifeHit Unpack(byte value) => new(
            (PlayerHitRegion)Mathf.Min(value & 3, 2), (PlayerHitSide)(value >> 2 & 3), (value >> 4 & 1) != 0, (byte)(value >> 5));

        /// <summary>Damage at or above this is a large hit (the LG clips).</summary>
        public const float HeavyDamage = 30f;

        /// <summary>Which side of the victim the attack came from, relative to where the victim faces.</summary>
        public static PlayerHitSide SideOf(Vector3 victimPosition, Vector3 victimForward, Vector3? source)
        {
            if (source is not Vector3 from) return PlayerHitSide.Front;
            Vector3 toSource = Vector3.ProjectOnPlane(from - victimPosition, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(victimForward, Vector3.up);
            if (toSource.sqrMagnitude < 1e-4f || forward.sqrMagnitude < 1e-4f) return PlayerHitSide.Front;
            float angle = Vector3.SignedAngle(forward, toSource, Vector3.up);
            if (Mathf.Abs(angle) <= 45f) return PlayerHitSide.Front;
            if (Mathf.Abs(angle) >= 135f) return PlayerHitSide.Back;
            return angle > 0f ? PlayerHitSide.Right : PlayerHitSide.Left;
        }

        public static PlayerHitRegion RegionOf(HitboxZone zone) => zone switch
        {
            HitboxZone.Head => PlayerHitRegion.Head,
            HitboxZone.Leg => PlayerHitRegion.Leg,
            _ => PlayerHitRegion.Body
        };

        public static PlayerLifeHit From(Vector3 victimPosition, Vector3 victimForward, Vector3? source, HitboxZone zone,
            float damage, DamageType type, int seed) => new(
            RegionOf(zone),
            SideOf(victimPosition, victimForward, source),
            damage >= HeavyDamage || type == DamageType.Explosion,
            (byte)(seed & 7));
    }
}
