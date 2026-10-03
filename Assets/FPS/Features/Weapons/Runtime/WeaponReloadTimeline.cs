using System;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// A reload's immutable server clock. Clients can sample opening, each insert,
    /// and closing even when updates are delayed or the reload is observed late.
    /// Ammo is still committed exclusively by WeaponServerState.
    /// </summary>
    [Serializable]
    public struct WeaponReloadTimeline : INetworkSerializable, IEquatable<WeaponReloadTimeline>
    {
        public double startedAt;
        public float timingMultiplier;
        public int rounds;

        public bool IsValid => timingMultiplier >= 1f && timingMultiplier <= 3f
            && !double.IsNaN(startedAt) && !double.IsInfinity(startedAt);

        public static WeaponReloadTimeline Begin(double now, float multiplier, int rounds) => new()
        {
            startedAt = now,
            timingMultiplier = Mathf.Clamp(multiplier, 1f, 3f),
            rounds = Mathf.Max(0, rounds)
        };

        public float NormalizedTime(WeaponData data, double now, bool thirdPerson)
        {
            if (!IsValid || data == null) return 0f;
            double elapsed = Math.Max(0d, now - startedAt) / timingMultiplier;
            if (data.reloadMode != ReloadMode.PerShell)
                return Mathf.Clamp01((float)(elapsed / Math.Max(.0001, data.ReloadAnimationDuration)));

            float start = data.PerShellOpeningDuration / Mathf.Max(.0001f, data.ReloadAnimationDuration);
            float end = (data.PerShellOpeningDuration + data.PerShellInterval)
                / Mathf.Max(.0001f, data.ReloadAnimationDuration);
            if (thirdPerson)
            {
                start = data.thirdPersonReloadInsertRange.x;
                end = data.thirdPersonReloadInsertRange.y;
            }
            start = Mathf.Clamp(start, 0f, .999f);
            end = Mathf.Clamp(end, start + .0001f, 1f);
            double opening = data.PerShellOpeningDuration;
            double interval = Math.Max(.0001, data.PerShellInterval);
            double insertsEnd = opening + Math.Max(0, rounds) * interval;
            if (elapsed < opening)
                return start * (float)(elapsed / Math.Max(.0001, opening));
            if (elapsed < insertsEnd)
                return Mathf.Lerp(start, end, (float)(((elapsed - opening) % interval) / interval));
            return Mathf.Lerp(end, 1f, Mathf.Clamp01((float)((elapsed - insertsEnd)
                / Math.Max(.0001, data.ReloadAnimationDuration
                    - data.PerShellOpeningDuration - data.PerShellInterval))));
        }

        public double GameplayCompleteTime(WeaponData data) => startedAt + timingMultiplier
            * Duration(data, false);

        public double PresentationCompleteTime(WeaponData data) => startedAt + timingMultiplier
            * Duration(data, true);

        private float Duration(WeaponData data, bool presentation)
        {
            if (data == null) return 0f;
            float duration = presentation ? data.ReloadAnimationDuration : data.ReloadDuration;
            if (data.reloadMode == ReloadMode.PerShell)
                duration += (Mathf.Max(1, rounds) - 1) * data.PerShellInterval;
            return duration;
        }

        // A replicated reload end before its gameplay deadline is a cancellation.
        // A natural end can retain the visual tail without retaining the ammo lock.
        public bool ShouldContinueAfterGameplay(WeaponData data, double now) => IsValid
            && now + .0001d >= GameplayCompleteTime(data)
            && now < PresentationCompleteTime(data);

        public bool Equals(WeaponReloadTimeline other) => startedAt.Equals(other.startedAt)
            && timingMultiplier.Equals(other.timingMultiplier) && rounds == other.rounds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref startedAt);
            serializer.SerializeValue(ref timingMultiplier);
            serializer.SerializeValue(ref rounds);
        }
    }
}
