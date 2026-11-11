using System.Collections.Generic;

namespace FPS
{
    public sealed class WeaponStabilityPrediction
    {
        private struct PendingShot
        {
            public ushort sequence;
            public double time;
            public uint seed;
            public WeaponStabilityContext context;
            public bool continuousAuto;
        }

        private readonly List<PendingShot> pending = new();
        public WeaponStability State { get; private set; }
        public int PendingCount => pending.Count;

        public float Fire(RecoilPattern profile, float interval, ushort sequence, double time,
            WeaponStabilityContext context, uint seed, bool predict, bool continuousAuto = false)
        {
            if (predict) pending.Add(new PendingShot { sequence = sequence, time = time, context = context,
                seed = seed, continuousAuto = continuousAuto });
            var state = State;
            double shotTime = WeaponFireClock.ShotTime(time, state.hasShot ? state.lastShotTime + interval : 0,
                interval, continuousAuto);
            float spread = state.Fire(profile, shotTime, interval, context, seed);
            State = state;
            return spread;
        }

        public void Reconcile(RecoilPattern profile, float interval, WeaponStability authoritative,
            bool hasResolvedSequence, ushort resolvedSequence)
        {
            if (hasResolvedSequence)
                pending.RemoveAll(s => !NetworkSequence.IsNewer(s.sequence, resolvedSequence));
            var state = authoritative;
            foreach (var shot in pending)
            {
                double time = WeaponFireClock.ShotTime(shot.time,
                    state.hasShot ? state.lastShotTime + interval : 0, interval, shot.continuousAuto);
                state.Fire(profile, time, interval, shot.context, shot.seed);
            }
            State = state;
        }

        public void ClearPending() => pending.Clear();
    }
}
