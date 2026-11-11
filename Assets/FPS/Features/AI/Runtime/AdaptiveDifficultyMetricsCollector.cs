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
            public float NormalizedDamage;
            public float Exposure;
        }

        private Dictionary<ulong, Counter> counters = new Dictionary<ulong, Counter>();
        private Dictionary<ulong, Counter> nextCounters = new Dictionary<ulong, Counter>();
        public int? PendingBoundaryTick { get; private set; }
        // Sealed snapshots are ordered by server tick. Keep this watermark across
        // encounter resets so a replay cannot become evidence in the next window.
        private readonly Dictionary<ulong, int> lastRecordedTick = new Dictionary<ulong, int>();
        private int discardedThroughTick = int.MinValue;

        public int PlayerCount => counters.Count;

        private Counter Get(ulong id, int tick)
        {
            var window = PendingBoundaryTick.HasValue && tick > PendingBoundaryTick.Value ? nextCounters : counters;
            if (!window.TryGetValue(id, out Counter counter)) window.Add(id, counter = new Counter());
            return counter;
        }

        public void Record(ServerTelemetrySnapshot snapshot, float maxHealth = 100)
        {
            if (!snapshot.PlayerId.IsValid || snapshot.ServerTick <= discardedThroughTick)
                return;
            if (lastRecordedTick.TryGetValue(snapshot.PlayerId.Value, out int lastTick)
                && snapshot.ServerTick <= lastTick)
                return;
            lastRecordedTick[snapshot.PlayerId.Value] = snapshot.ServerTick;

            Counter counter = Get(snapshot.PlayerId.Value, snapshot.ServerTick);

            counter.ShotsFired += Math.Max(0, snapshot.ShotsFired);
            counter.ShotsHit += Math.Max(0, snapshot.ShotsHit);
            counter.HeadshotHits += Math.Max(0, snapshot.HeadshotHitCount);
            counter.Kills += Math.Max(0, snapshot.KillCount);
            counter.Headshots += Math.Max(0, snapshot.HeadshotCount);
            counter.Downed += Math.Max(0, snapshot.DownedCount);
            float damage = float.IsFinite(snapshot.DamageTaken) ? Math.Max(0f, snapshot.DamageTaken) : 0;
            counter.DamageTaken += damage;
            counter.NormalizedDamage += damage / (float.IsFinite(maxHealth) ? Math.Max(1, maxHealth) : 100);
        }

        public void RecordExposure(ulong id, int serverTick, float elapsedSeconds)
        {
            if (elapsedSeconds > 0 && float.IsFinite(elapsedSeconds)) Get(id, serverTick).Exposure += elapsedSeconds;
        }

        public bool RequestBoundary(int tick)
        {
            if (PendingBoundaryTick.HasValue) return false;
            PendingBoundaryTick = tick;
            return true;
        }

        public void CompleteBoundary()
        {
            if (!PendingBoundaryTick.HasValue) return;
            counters.Clear();
            var old = counters; counters = nextCounters; nextCounters = old;
            PendingBoundaryTick = null;
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
                    counter.Downed, counter.Exposure, counter.NormalizedDamage));
            }

            return samples;
        }

        public void ResetEncounter(int discardThroughTick = int.MinValue)
        {
            counters.Clear();
            nextCounters.Clear();
            PendingBoundaryTick = null;
            discardedThroughTick = Math.Max(discardedThroughTick, discardThroughTick);
        }

        public void ResetSession()
        {
            ResetEncounter();
            lastRecordedTick.Clear();
            discardedThroughTick = int.MinValue;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
