using System;
using System.Collections.Generic;

namespace FPS
{
    /// <summary>
    /// Server-only encounter accumulator. It converts authoritative tick snapshots into
    /// normalized per-player samples; it does not decide difficulty or spawn anything.
    /// </summary>
    public sealed class AdaptiveDifficultyMetricsCollector
    {
        private sealed class Counter
        {
            public int ShotsFired;
            public int ShotsHit;
            public int HeadshotHits;
            public int Kills;
            public int Headshots;
            public int Downed;
            public float DamageTaken;
        }

        private readonly Dictionary<ulong, Counter> counters = new Dictionary<ulong, Counter>();

        public int PlayerCount => counters.Count;

        public void Record(ServerTelemetrySnapshot snapshot)
        {
            if (!snapshot.PlayerId.IsValid)
                return;

            if (!counters.TryGetValue(snapshot.PlayerId.Value, out Counter counter))
            {
                counter = new Counter();
                counters.Add(snapshot.PlayerId.Value, counter);
            }

            counter.ShotsFired += Math.Max(0, snapshot.ShotsFired);
            counter.ShotsHit += Math.Max(0, snapshot.ShotsHit);
            counter.HeadshotHits += Math.Max(0, snapshot.HeadshotHitCount);
            counter.Kills += Math.Max(0, snapshot.KillCount);
            counter.Headshots += Math.Max(0, snapshot.HeadshotCount);
            counter.Downed += Math.Max(0, snapshot.DownedCount);
            counter.DamageTaken += Math.Max(0f, snapshot.DamageTaken);
        }

        public List<PlayerPerformanceSample> BuildSamples()
        {
            var samples = new List<PlayerPerformanceSample>(counters.Count);
            foreach (KeyValuePair<ulong, Counter> entry in counters)
            {
                Counter counter = entry.Value;
                float headshotRatio = counter.ShotsHit > 0
                    ? (float)counter.HeadshotHits / counter.ShotsHit
                    : counter.Kills > 0
                        ? (float)counter.Headshots / counter.Kills
                        : 0f;
                float damageTakenNorm = counter.DamageTaken / Math.Max(100f, counter.Kills * 100f);
                float downedCountNorm = counter.Downed / 3f;
                float ammoEfficiency = counter.ShotsFired > 0
                    ? (float)counter.Kills / counter.ShotsFired
                    : 0f;

                samples.Add(PlayerPerformanceSample.Create(
                    entry.Key,
                    headshotRatio,
                    Clamp01(damageTakenNorm),
                    Clamp01(downedCountNorm),
                    Clamp01(ammoEfficiency),
                    counter.ShotsFired,
                    counter.Kills,
                    counter.Downed));
            }

            return samples;
        }

        public void ResetEncounter()
        {
            counters.Clear();
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
