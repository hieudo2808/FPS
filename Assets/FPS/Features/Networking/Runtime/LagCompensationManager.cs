using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public readonly struct LagCompensatedHit
    {
        public readonly HitboxSegment segment;
        public readonly IDamageable damageTarget;
        public readonly Vector3 point;
        public readonly float distance;
        public readonly HitboxZone zone;
        public readonly float damageMultiplier;

        public LagCompensatedHit(
            HitboxSegment segment,
            IDamageable damageTarget,
            Vector3 point,
            float distance,
            HitboxZone zone,
            float damageMultiplier)
        {
            this.segment = segment;
            this.damageTarget = damageTarget;
            this.point = point;
            this.distance = distance;
            this.zone = zone;
            this.damageMultiplier = damageMultiplier;
        }
    }

    public class LagCompensationManager : NetworkBehaviour
    {
        public static LagCompensationManager Instance { get; private set; }

        private static readonly List<LagCompensatedTarget> Targets = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        public static void RegisterTarget(LagCompensatedTarget target)
        {
            if (target == null || Targets.Contains(target))
                return;

            Targets.Add(target);
        }

        public static void UnregisterTarget(LagCompensatedTarget target)
        {
            Targets.Remove(target);
        }

        public static bool ShouldSampleServerHistory()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            return NetworkManager.Singleton.IsServer;
        }

        public static double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }

        public static double ResolveRewindTime(double serverReceiveTime, double clientShotLocalTime)
        {
            if (clientShotLocalTime <= 0.0)
                return serverReceiveTime;

            double minTime = serverReceiveTime - NetworkHardeningRuntime.Current.MaxRewindSeconds;
            if (clientShotLocalTime < minTime)
                return minTime;

            if (clientShotLocalTime > serverReceiveTime)
                return serverReceiveTime;

            return clientShotLocalTime;
        }

        public static bool TryResolveRewindTime(
            double serverReceiveTime,
            int serverReceiveTick,
            int clientEstimatedServerTick,
            int tickRate,
            double roundTripSeconds,
            out double rewindTime)
        {
            rewindTime = serverReceiveTime;
            if (tickRate <= 0)
                return false;

            int ticksOld = serverReceiveTick - clientEstimatedServerTick;
            NetworkHardeningSettings settings = NetworkHardeningRuntime.Current;
            if (ticksOld < -settings.MaxFutureInputTicks)
                return false;

            if (ticksOld <= 0)
            {
                rewindTime = serverReceiveTime;
                return true;
            }

            double ageSeconds = ticksOld / (double)tickRate;
            double rttAwareLimit = Math.Max(
                Math.Max(0.15, settings.RewindJitterMarginSeconds),
                Math.Max(0.0, roundTripSeconds) * 0.5 + settings.RewindJitterMarginSeconds);
            double allowedRewind = Math.Min(settings.MaxRewindSeconds, rttAwareLimit);
            // The configured rewind window is a hard bound.  A whole-tick
            // tolerance would admit a shot one tick older than maxRewind
            // (for example 16 ticks at 60 Hz ~= 266.7 ms for a 250 ms cap).
            if (ageSeconds > allowedRewind + 1e-9)
                return false;

            rewindTime = serverReceiveTime - Math.Min(ageSeconds, allowedRewind);
            return true;
        }

        public static bool TryRaycast(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            int layerMask,
            double rewindTime,
            float blockingDistance,
            out LagCompensatedHit hit)
        {
            hit = default;
            if (direction.sqrMagnitude <= 0.0001f)
                return false;

            Ray ray = new Ray(origin, direction.normalized);
            float bestDistance = Mathf.Min(maxDistance, blockingDistance);
            bool found = false;

            for (int i = Targets.Count - 1; i >= 0; i--)
            {
                LagCompensatedTarget target = Targets[i];
                if (target == null || !target.isActiveAndEnabled)
                {
                    Targets.RemoveAt(i);
                    continue;
                }

                if (!target.TryGetSnapshotAt(rewindTime, out HitboxSnapshot snapshot))
                    continue;

                for (int s = 0; s < snapshot.count; s++)
                {
                    HitboxSegmentSnapshot segment = snapshot.segments[s];
                    if ((layerMask & (1 << segment.layer)) == 0)
                        continue;

                    if (segment.damageTarget == null || segment.damageTarget.IsDead)
                        continue;

                    if (!segment.bounds.IntersectRay(ray, out float distance))
                        continue;

                    if (distance > bestDistance)
                        continue;

                    bestDistance = distance;
                    found = true;
                    hit = new LagCompensatedHit(
                        segment.segment,
                        segment.damageTarget,
                        ray.GetPoint(distance),
                        distance,
                        segment.zone,
                        segment.damageMultiplier);
                }
            }

            return found;
        }
    }
}
