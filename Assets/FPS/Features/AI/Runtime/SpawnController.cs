using System;

namespace FPS
{
    public readonly struct SpawnDecision
    {
        public readonly bool CanSpawn;
        public readonly bool SpawnSpecial;
        public readonly int MaxAlive;
        public readonly float IntervalSeconds;
        public readonly float SpecialChance;

        public SpawnDecision(
            bool canSpawn,
            bool spawnSpecial,
            int maxAlive,
            float intervalSeconds,
            float specialChance)
        {
            CanSpawn = canSpawn;
            SpawnSpecial = spawnSpecial;
            MaxAlive = maxAlive;
            IntervalSeconds = intervalSeconds;
            SpecialChance = specialChance;
        }
    }

    /// <summary>
    /// Pure final-spawn calculation. Director owns phase/gating; this class only combines
    /// the director base value with static and adaptive modifiers before a runtime factory
    /// performs the actual server-authoritative spawn.
    /// </summary>
    public sealed class SpawnController
    {
        public SpawnDecision Decide(
            DirectorDecision director,
            DifficultyStats staticStats,
            float dynamicMultiplier,
            int currentAlive,
            int baseMaxAlive,
            int playerCount,
            float baseInterval,
            float minimumInterval,
            bool specialEnabled,
            float random01)
        {
            float adaptiveMultiplier = Math.Max(0.01f, dynamicMultiplier);
            float phaseMultiplier = Math.Max(0f, director.SpawnRateMultiplier);
            int safePlayerCount = Math.Max(1, playerCount);
            float playerScale = 1f + (safePlayerCount - 1) * 0.3f;

            float interval = baseInterval * staticStats.spawnIntervalMultiplier;
            if (phaseMultiplier > 0f)
                interval /= phaseMultiplier;
            interval /= playerScale;
            interval /= adaptiveMultiplier;
            interval = Math.Max(minimumInterval, interval);

            int maxAlive = Math.Max(1, (int)Math.Round(
                baseMaxAlive * staticStats.maxAliveMultiplier * adaptiveMultiplier));
            bool canSpawn = director.Phase != DirectorPhase.Relax && currentAlive < maxAlive;
            float specialChance = Clamp01(staticStats.specialSpawnChance * adaptiveMultiplier);
            bool spawnSpecial = canSpawn
                && specialEnabled
                && director.SpecialGateOpen
                && Clamp01(random01) < specialChance;

            return new SpawnDecision(
                canSpawn,
                spawnSpecial,
                maxAlive,
                interval,
                specialChance);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
