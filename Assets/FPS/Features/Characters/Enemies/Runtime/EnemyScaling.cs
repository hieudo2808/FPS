using System;
using UnityEngine;

namespace FPS
{
    [Serializable]
    public struct PlayerCountCurve
    {
        [SerializeField] private float onePlayer;
        [SerializeField] private float twoPlayers;
        [SerializeField] private float threePlayers;
        [SerializeField] private float fourPlayers;

        public PlayerCountCurve(float onePlayer, float twoPlayers, float threePlayers, float fourPlayers)
        {
            this.onePlayer = onePlayer;
            this.twoPlayers = twoPlayers;
            this.threePlayers = threePlayers;
            this.fourPlayers = fourPlayers;
        }

        public float Evaluate(int playerCount)
        {
            return Mathf.Max(0f, playerCount switch
            {
                <= 1 => onePlayer,
                2 => twoPlayers,
                3 => threePlayers,
                _ => fourPlayers
            });
        }
    }

    public readonly struct EnemyScalingSnapshot
    {
        public EnemyScalingSnapshot(
            int playerCount,
            float maxHealth,
            float damageAndStatusMultiplier,
            float controlResistanceMultiplier,
            float abilityCooldownMultiplier,
            float directorSpawnPressureMultiplier)
        {
            PlayerCount = EnemyScalingResolver.ClampPlayerCount(playerCount);
            MaxHealth = Mathf.Max(1f, maxHealth);
            DamageAndStatusMultiplier = Mathf.Max(0f, damageAndStatusMultiplier);
            ControlResistanceMultiplier = Mathf.Max(0.01f, controlResistanceMultiplier);
            AbilityCooldownMultiplier = Mathf.Max(0.01f, abilityCooldownMultiplier);
            DirectorSpawnPressureMultiplier = Mathf.Max(0.01f, directorSpawnPressureMultiplier);
        }

        public int PlayerCount { get; }
        public float MaxHealth { get; }
        public float DamageAndStatusMultiplier { get; }
        public float ControlResistanceMultiplier { get; }
        public float AbilityCooldownMultiplier { get; }
        public float DirectorSpawnPressureMultiplier { get; }
    }

    /// <summary>
    /// Stateless balance math. Runtime components own when a player-count snapshot is captured.
    /// </summary>
    public static class EnemyScalingResolver
    {
        private static readonly PlayerCountCurve DefaultCommonHealth = new(1f, 1.35f, 1.70f, 2.05f);
        private static readonly PlayerCountCurve DefaultDamage = new(1f, 1.08f, 1.15f, 1.20f);
        private static readonly PlayerCountCurve DefaultControlResistance = new(1f, 1.15f, 1.30f, 1.45f);
        private static readonly PlayerCountCurve DefaultAbilityCooldown = new(1f, 0.95f, 0.90f, 0.85f);
        private static readonly PlayerCountCurve DefaultSpawnPressure = new(1f, 1.20f, 1.40f, 1.60f);
        private static readonly PlayerCountCurve DefaultScreamerHealth = new(150f, 200f, 250f, 300f);
        private static readonly PlayerCountCurve DefaultInfectorHealth = new(500f, 750f, 975f, 1200f);
        private static readonly PlayerCountCurve DefaultTankHealth = new(2500f, 5000f, 7500f, 10000f);

        public static int ClampPlayerCount(int playerCount) => Mathf.Clamp(playerCount, 1, 4);

        public static float GetCommonHealthMultiplier(int playerCount, EnemyScalingProfile profile = null)
        {
            return profile != null
                ? profile.GetCommonHealthMultiplier(ClampPlayerCount(playerCount))
                : DefaultCommonHealth.Evaluate(ClampPlayerCount(playerCount));
        }

        public static float GetDamageAndStatusMultiplier(int playerCount, EnemyScalingProfile profile = null)
        {
            return profile != null
                ? profile.GetDamageAndStatusMultiplier(ClampPlayerCount(playerCount))
                : DefaultDamage.Evaluate(ClampPlayerCount(playerCount));
        }

        public static float GetControlResistanceMultiplier(int playerCount, EnemyScalingProfile profile = null)
        {
            return profile != null
                ? profile.GetControlResistanceMultiplier(ClampPlayerCount(playerCount))
                : DefaultControlResistance.Evaluate(ClampPlayerCount(playerCount));
        }

        public static float GetAbilityCooldownMultiplier(int playerCount, EnemyScalingProfile profile = null)
        {
            return profile != null
                ? profile.GetAbilityCooldownMultiplier(ClampPlayerCount(playerCount))
                : DefaultAbilityCooldown.Evaluate(ClampPlayerCount(playerCount));
        }

        public static float GetDirectorSpawnPressureMultiplier(int playerCount, EnemyScalingProfile profile = null)
        {
            return profile != null
                ? profile.GetDirectorSpawnPressureMultiplier(ClampPlayerCount(playerCount))
                : DefaultSpawnPressure.Evaluate(ClampPlayerCount(playerCount));
        }

        public static float GetSpecialHealth(
            SpecialType type,
            int playerCount,
            float fallbackAuthoredHealth,
            EnemyScalingProfile profile = null)
        {
            int count = ClampPlayerCount(playerCount);
            if (profile != null)
                return profile.GetSpecialHealth(type, count, fallbackAuthoredHealth);

            return type switch
            {
                SpecialType.Screamer => DefaultScreamerHealth.Evaluate(count),
                SpecialType.Infector => DefaultInfectorHealth.Evaluate(count),
                SpecialType.Tank => DefaultTankHealth.Evaluate(count),
                _ => Mathf.Max(1f, fallbackAuthoredHealth * DefaultCommonHealth.Evaluate(count))
            };
        }

        public static EnemyScalingSnapshot ResolveSpecial(
            SpecialType type,
            int playerCount,
            float authoredHealth,
            float healthDifficultyMultiplier,
            float damageDifficultyMultiplier,
            EnemyScalingProfile profile = null)
        {
            int count = ClampPlayerCount(playerCount);
            float health = GetSpecialHealth(type, count, authoredHealth, profile)
                * Mathf.Max(0.01f, healthDifficultyMultiplier);
            float damage = GetDamageAndStatusMultiplier(count, profile)
                * Mathf.Max(0f, damageDifficultyMultiplier);

            return new EnemyScalingSnapshot(
                count,
                health,
                damage,
                GetControlResistanceMultiplier(count, profile),
                GetAbilityCooldownMultiplier(count, profile),
                GetDirectorSpawnPressureMultiplier(count, profile));
        }
    }
}
