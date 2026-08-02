using System;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Authoring override for a static difficulty tier. An absent override keeps the
    /// existing code-defined values, which is the compatibility path for current scenes.
    /// </summary>
    [CreateAssetMenu(menuName = "FPS/AI/Static Difficulty Profile")]
    public sealed class StaticDifficultyProfileAsset : ScriptableObject
    {
        [SerializeField] private DifficultyLevel difficulty = DifficultyLevel.Medium;
        [SerializeField] private DifficultyStats stats = new DifficultyStats
        {
            hpMultiplier = 1f,
            damageMultiplier = 1f,
            speedMultiplier = 1f,
            maxConcurrentAttackers = 3,
            spawnIntervalMultiplier = 1f,
            maxAliveMultiplier = 1f,
            specialSpawnChance = 0.15f,
            enableRubberBanding = false
        };

        public DifficultyLevel Difficulty => difficulty;
        public DifficultyStats Stats => stats;
    }

    public static class StaticDifficultyProfiles
    {
        public static DifficultyStats Get(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy:
                    return new DifficultyStats
                    {
                        hpMultiplier = 0.5f,
                        damageMultiplier = 0.5f,
                        speedMultiplier = 0.8f,
                        maxConcurrentAttackers = 2,
                        spawnIntervalMultiplier = 1.25f,
                        maxAliveMultiplier = 0.7f,
                        specialSpawnChance = 0.05f,
                        enableRubberBanding = false
                    };
                case DifficultyLevel.Hard:
                    return new DifficultyStats
                    {
                        hpMultiplier = 1.5f,
                        damageMultiplier = 1.5f,
                        speedMultiplier = 1.2f,
                        maxConcurrentAttackers = 4,
                        spawnIntervalMultiplier = 0.75f,
                        maxAliveMultiplier = 1.25f,
                        specialSpawnChance = 0.2f,
                        enableRubberBanding = true
                    };
                case DifficultyLevel.Pandemonium:
                    return new DifficultyStats
                    {
                        hpMultiplier = 3f,
                        damageMultiplier = 2f,
                        speedMultiplier = 1.5f,
                        maxConcurrentAttackers = 6,
                        spawnIntervalMultiplier = 0.5f,
                        maxAliveMultiplier = 1.75f,
                        specialSpawnChance = 0.35f,
                        enableRubberBanding = true
                    };
                default:
                    return new DifficultyStats
                    {
                        hpMultiplier = 1f,
                        damageMultiplier = 1f,
                        speedMultiplier = 1f,
                        maxConcurrentAttackers = 3,
                        spawnIntervalMultiplier = 1f,
                        maxAliveMultiplier = 1f,
                        specialSpawnChance = 0.15f,
                        enableRubberBanding = false
                    };
            }
        }

        public static bool TryGetOverride(
            StaticDifficultyProfileAsset[] overrides,
            DifficultyLevel level,
            out DifficultyStats stats)
        {
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Length; i++)
                {
                    StaticDifficultyProfileAsset profile = overrides[i];
                    if (profile != null && profile.Difficulty == level)
                    {
                        stats = profile.Stats;
                        return true;
                    }
                }
            }

            stats = default;
            return false;
        }
    }
}
