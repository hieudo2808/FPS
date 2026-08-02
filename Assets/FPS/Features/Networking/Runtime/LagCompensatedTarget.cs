using System;
using UnityEngine;

namespace FPS
{
    public struct HitboxSegmentSnapshot
    {
        public HitboxSegment segment;
        public IDamageable damageTarget;
        public Bounds bounds;
        public int layer;
        public HitboxZone zone;
        public float damageMultiplier;
    }

    public sealed class HitboxSnapshot
    {
        public double time;
        public int count;
        public HitboxSegmentSnapshot[] segments;
    }

    public class LagCompensatedTarget : MonoBehaviour
    {
        [SerializeField] private HitboxSegment[] hitboxSegments;
        [SerializeField] private bool autoRefreshSegments = true;

        private HitboxSnapshot[] history;
        private int writeIndex;
        private int sampleCount;
        private double nextSampleTime;

        public int SnapshotCount => sampleCount;
        public int HistoryCapacity => history != null ? history.Length : 0;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            LagCompensationManager.RegisterTarget(this);
        }

        private void OnDisable()
        {
            LagCompensationManager.UnregisterTarget(this);
        }

        private void Update()
        {
            if (!LagCompensationManager.ShouldSampleServerHistory())
                return;

            double now = LagCompensationManager.GetServerTime();
            if (now < nextSampleTime)
                return;

            Sample(now);
            nextSampleTime = now + (1.0 / NetworkGameplayPolicy.HitboxHistoryHz);
        }

        public void RefreshSegments()
        {
            hitboxSegments = GetComponentsInChildren<HitboxSegment>(includeInactive: false);
            AllocateHistory();
        }

        public void SampleForTests(double time)
        {
            // EditMode không gọi OnEnable, nên test seam phải tự đăng ký với manager.
            EnsureInitialized();
            LagCompensationManager.RegisterTarget(this);
            Sample(time);
        }

        public bool TryGetSnapshotAt(double targetTime, out HitboxSnapshot snapshot)
        {
            snapshot = null;
            if (history == null || sampleCount == 0)
                return false;

            double bestDelta = double.MaxValue;
            for (int i = 0; i < sampleCount; i++)
            {
                HitboxSnapshot candidate = history[i];
                if (candidate == null || candidate.count == 0)
                    continue;

                double delta = Math.Abs(candidate.time - targetTime);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    snapshot = candidate;
                }
            }

            return snapshot != null;
        }

        private void EnsureInitialized()
        {
            if (autoRefreshSegments && (hitboxSegments == null || hitboxSegments.Length == 0))
                hitboxSegments = GetComponentsInChildren<HitboxSegment>(includeInactive: false);

            if (history == null)
                AllocateHistory();
        }

        private void AllocateHistory()
        {
            int segmentCount = hitboxSegments != null ? hitboxSegments.Length : 0;
            int capacity = Mathf.Max(2, Mathf.CeilToInt(
                NetworkGameplayPolicy.HitboxHistorySeconds * NetworkGameplayPolicy.HitboxHistoryHz) + 2);

            history = new HitboxSnapshot[capacity];
            for (int i = 0; i < capacity; i++)
            {
                history[i] = new HitboxSnapshot
                {
                    segments = new HitboxSegmentSnapshot[Mathf.Max(1, segmentCount)]
                };
            }

            writeIndex = 0;
            sampleCount = 0;
        }

        private void Sample(double time)
        {
            if (hitboxSegments == null || hitboxSegments.Length == 0)
                return;

            HitboxSnapshot snapshot = history[writeIndex];
            snapshot.time = time;
            snapshot.count = 0;

            for (int i = 0; i < hitboxSegments.Length; i++)
            {
                HitboxSegment segment = hitboxSegments[i];
                if (segment == null || !segment.isActiveAndEnabled)
                    continue;

                Collider collider = segment.GetComponent<Collider>();
                if (collider == null || !collider.enabled)
                    continue;

                int slot = snapshot.count++;
                if (slot >= snapshot.segments.Length)
                    break;

                snapshot.segments[slot] = new HitboxSegmentSnapshot
                {
                    segment = segment,
                    damageTarget = segment.DamageTarget,
                    bounds = collider.bounds,
                    layer = collider.gameObject.layer,
                    zone = segment.Zone,
                    damageMultiplier = segment.DamageMultiplier
                };
            }

            writeIndex = (writeIndex + 1) % history.Length;
            sampleCount = Mathf.Min(sampleCount + 1, history.Length);
        }
    }
}
