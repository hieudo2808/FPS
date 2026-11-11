using System;
using System.Collections.Generic;

namespace FPS
{
    public readonly struct ActorPressureSnapshot
    {
        public readonly ulong PlayerId;
        public readonly float Health01, Damage3Seconds, Intensity, ExposureSeconds;
        public readonly int ThreatCount;
        public readonly bool Protect, RecoverySafe;
        public ActorPressureSnapshot(ulong playerId, float health01, float damage3Seconds, int threatCount,
            float intensity, float exposureSeconds, bool protect)
        {
            PlayerId = playerId; Health01 = health01; Damage3Seconds = damage3Seconds;
            ThreatCount = threatCount; Intensity = intensity; ExposureSeconds = exposureSeconds;
            Protect = protect; RecoverySafe = !protect && intensity <= 35 && damage3Seconds <= .05f;
        }
    }

    /// <summary>Server-time pressure proxy, independent of Unity. Damage entries allocate
    /// only on damage; regular 10 Hz sampling reuses each actor's existing buffers.</summary>
    public sealed class DirectorPressure
    {
        private readonly struct Damage
        {
            public readonly double Time;
            public readonly float Normalized;
            public Damage(double time, float normalized) { Time = time; Normalized = normalized; }
        }
        private sealed class Actor
        {
            public readonly Queue<Damage> RecentDamage = new Queue<Damage>(16);
            public double LastDown = double.NegativeInfinity, LastShot = double.NegativeInfinity;
            public double LastSample = double.NaN;
            public float Intensity, Exposure;
        }
        private readonly Dictionary<ulong, Actor> actors = new Dictionary<ulong, Actor>();
        private Actor Get(ulong id)
        {
            if (!actors.TryGetValue(id, out Actor actor)) actors.Add(id, actor = new Actor());
            return actor;
        }
        public void RecordDamage(ulong id, double time, float actualDamage, float maxHealth)
        {
            if (!double.IsFinite(time) || !float.IsFinite(actualDamage) || !float.IsFinite(maxHealth)
                || actualDamage <= 0 || maxHealth <= 0) return;
            Get(id).RecentDamage.Enqueue(new Damage(time, actualDamage / maxHealth));
        }
        public void RecordShot(ulong id, double time) => Get(id).LastShot = time;
        public void RecordDown(ulong id, double time) => Get(id).LastDown = time;

        public ActorPressureSnapshot Sample(ulong id, double time, float health01, int threats,
            bool downed, bool participating, bool countExposure = true)
        {
            Actor actor = Get(id);
            float dt = double.IsNaN(actor.LastSample) ? 0 : (float)Math.Max(0, time - actor.LastSample);
            actor.LastSample = time;
            while (actor.RecentDamage.Count > 0 && time - actor.RecentDamage.Peek().Time > 3)
                actor.RecentDamage.Dequeue();
            float d3 = 0;
            foreach (Damage damage in actor.RecentDamage) d3 += damage.Normalized;
            float h = float.IsFinite(health01) ? Math.Clamp(health01, 0, 1) : 0;
            int n = Math.Clamp(threats, 0, 4);
            bool combat = n > 0 || d3 > 0 || time - actor.LastShot <= 3;
            // A pause/disconnect cannot be credited as combat exposure on resumption.
            float exposureDelta = participating && countExposure && combat && dt <= .3f ? dt : 0;
            actor.Exposure += exposureDelta;
            float raw = Math.Clamp(100 * Math.Min(d3, 1) + 10 * n + 30 * (1 - h), 0, 100);
            actor.Intensity = participating ? Math.Max(raw, actor.Intensity - 10 * dt) : 0;
            bool protect = participating && ((h <= .20f && (n > 0 || d3 > 0))
                || (h <= .60f && d3 >= .30f) || downed
                || time - actor.LastDown <= 20 || actor.Intensity >= 80);
            return new ActorPressureSnapshot(id, h, d3, n, actor.Intensity, exposureDelta, protect);
        }
        public void Reset() => actors.Clear();
    }

    /// <summary>Archived before counters are reset; consumers cannot mutate the sample array.</summary>
    public sealed class DirectorEvaluationRecord
    {
        public string RunId { get; }
        public int AttemptId { get; }
        public int EncounterId { get; }
        public int BoundaryTick { get; }
        public DirectorDecisionReason Reason { get; }
        public IReadOnlyList<PlayerPerformanceSample> Samples { get; }
        public DynamicDifficultyEvaluation Evaluation { get; }
        public float MultiplierBefore { get; }
        public bool Applied { get; }
        public DynamicDifficultyPolicy Policy { get; }
        public string HoldReason { get; }
        public DirectorEvaluationRecord(string runId, int attemptId, int encounterId, int boundaryTick,
            DirectorDecisionReason reason, List<PlayerPerformanceSample> samples,
            DynamicDifficultyEvaluation evaluation, float multiplierBefore, bool applied,
            DynamicDifficultyPolicy? policy = null)
        {
            RunId = runId; AttemptId = attemptId; EncounterId = encounterId; BoundaryTick = boundaryTick;
            Reason = reason; Samples = samples.AsReadOnly(); Evaluation = evaluation;
            MultiplierBefore = multiplierBefore; Applied = applied;
            Policy = policy ?? DynamicDifficultyPolicy.Default;
            HoldReason = !evaluation.HasEvidence ? "insufficient-evidence"
                : !applied ? "observe-only" : !evaluation.Updated ? "target-or-tier-limit" : "";
        }
    }
}
