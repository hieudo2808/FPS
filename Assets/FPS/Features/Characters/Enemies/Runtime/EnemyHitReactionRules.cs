using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Server rules for how a burst of confirmed hits affects an enemy. Pure so the
    /// thresholds can be tested without a scene. Everything that is not Flinch changes
    /// gameplay and only applies to enemies driven by the common melee brain.
    /// </summary>
    public static class EnemyHitReactionRules
    {
        /// <summary>Hits closer together than this are merged into one presented reaction.</summary>
        public const float PresentInterval = 0.1f;
        /// <summary>After a Stumble or Interrupt, further hits only flinch for this long.</summary>
        public const float GameplayCooldown = 1.5f;
        public const float StumbleSeconds = 0.4f;
        public const float InterruptSeconds = 0.5f;
        /// <summary>A burst worth this share of max health is a heavy hit.</summary>
        public const float HeavyFraction = 0.25f;
        /// <summary>A headshot this heavy is enough to break an attack.</summary>
        public const float HeavyHeadshotFraction = 0.15f;

        /// <summary>0..1 visual strength. Reaches 1 at the heavy-hit threshold.</summary>
        public static float Strength(float burstDamage, float maxHealth)
        {
            if (burstDamage <= 0f) return 0f;
            if (maxHealth <= 0f) return 1f;
            return Mathf.Clamp01(burstDamage / (maxHealth * HeavyFraction));
        }

        public static bool IsHeavy(float burstDamage, float maxHealth, bool headshot)
        {
            if (burstDamage <= 0f) return false;
            if (maxHealth <= 0f) return true;
            // Tolerance so a hit of exactly the threshold counts despite float rounding of the product.
            const float epsilon = 0.001f;
            return burstDamage + epsilon >= maxHealth * HeavyFraction
                || headshot && burstDamage + epsilon >= maxHealth * HeavyHeadshotFraction;
        }

        /// <param name="gameplayAllowed">The enemy uses the common melee brain and is not in a special action.</param>
        /// <param name="attackWindingUp">A melee swing has started but has not dealt its damage yet.</param>
        /// <param name="secondsSinceGameplayReaction">Time since the last Stumble/Interrupt; negative when there was none.</param>
        public static EnemyHitReactionKind Resolve(
            HitboxZone zone,
            bool headshot,
            float burstDamage,
            float maxHealth,
            bool alive,
            bool gameplayAllowed,
            bool attackWindingUp,
            float secondsSinceGameplayReaction)
        {
            if (!alive || burstDamage <= 0f) return EnemyHitReactionKind.None;
            bool cooling = secondsSinceGameplayReaction >= 0f && secondsSinceGameplayReaction < GameplayCooldown;
            if (!gameplayAllowed || cooling) return EnemyHitReactionKind.Flinch;

            bool heavy = IsHeavy(burstDamage, maxHealth, headshot);
            if (heavy && attackWindingUp) return EnemyHitReactionKind.Interrupt;
            // A swing that is already committed is not cancelled by light leg hits.
            if (attackWindingUp) return EnemyHitReactionKind.Flinch;
            return heavy || zone == HitboxZone.Leg ? EnemyHitReactionKind.Stumble : EnemyHitReactionKind.Flinch;
        }

        /// <summary>Damage types that come from a body impact and deserve a reaction.</summary>
        public static bool Reacts(DamageType type) =>
            type == DamageType.Bullet || type == DamageType.Explosion;

        public static byte QuantizeStrength(float strength01) => (byte)Mathf.RoundToInt(Mathf.Clamp01(strength01) * 255f);

        /// <summary>Bullet travel direction as a yaw relative to the enemy's facing, 256 steps.</summary>
        public static byte QuantizeYaw(Vector3 localDirection)
        {
            float degrees = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
            return (byte)(Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 360f * 256f) & 0xFF);
        }

        public static Vector3 DequantizeYaw(byte yaw)
        {
            float radians = yaw / 256f * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        public static sbyte QuantizeCentimetres(float metres) => (sbyte)Mathf.Clamp(Mathf.RoundToInt(metres * 100f), -127, 127);
    }
}
