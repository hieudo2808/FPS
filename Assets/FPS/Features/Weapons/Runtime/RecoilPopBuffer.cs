using System;
using FPS.RecoilPort;
using UnityEngine;

namespace FPS
{
    public sealed class RecoilPopBuffer
    {
        struct PopEvent { public double time; public float yawSign; public bool active; }
        readonly PopEvent[] events;
        readonly RecoilPattern profile;
        readonly float[] pitchCriticalTimes, yawCriticalTimes;
        int next;
        double previousSample = double.NegativeInfinity;
        public int Capacity => events.Length;

        public RecoilPopBuffer(RecoilPattern profile, float minFireInterval)
        {
            profile.ValidateCurves();
            this.profile = profile;
            events = new PopEvent[profile.PopCapacity(minFireInterval)];
            pitchCriticalTimes = CriticalTimes(profile.cameraPopPitchCurve);
            yawCriticalTimes = CriticalTimes(profile.cameraPopYawCurve);
        }

        static float[] CriticalTimes(UnrealRecoilCurve.Key[] curve) => curve == null || curve.Length == 0
            ? Array.Empty<float>() : UnrealRecoilCurve.CriticalTimes(curve);

        public void Add(double now, uint seed)
        {
            if (events.Length == 0) return;
            events[next] = new PopEvent { time = now, yawSign = WeaponBallistics.CosmeticYawSign(seed), active = true };
            next = (next + 1) % events.Length;
        }

        public Vector2 Sample(double now)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < events.Length; i++)
            {
                ref PopEvent entry = ref events[i];
                if (!entry.active) continue;
                float age = (float)(now - entry.time);
                if (age < 0) continue;
                if (age >= profile.CameraPopDuration) { entry.active = false; continue; }
                float from = (float)Math.Max(0, previousSample - entry.time);
                sum.x += Excursion(profile.cameraPopPitchCurve, pitchCriticalTimes, from, age);
                sum.y += Excursion(profile.cameraPopYawCurve, yawCriticalTimes, from, age) * entry.yawSign;
            }
            previousSample = now;
            return Vector2.Scale(sum, profile.cameraPopScale);
        }

        static float Excursion(UnrealRecoilCurve.Key[] curve, float[] criticalTimes, float from, float to)
        {
            if (curve == null || curve.Length == 0) return 0f;
            float selected = UnrealRecoilCurve.Evaluate(curve, to), selectedTime = to;
            for (int i = 0; i + 1 < curve.Length; i++)
            {
                if (curve[i].mode == 0 && to > curve[i].time && from < curve[i + 1].time)
                    Select(curve[i].value, Mathf.Min(to, curve[i + 1].time));
            }
            foreach (float time in criticalTimes)
                if (time > from && time <= to) Select(UnrealRecoilCurve.Evaluate(curve, time), time);
            return selected;

            void Select(float value, float time)
            {
                if (Mathf.Abs(value) > Mathf.Abs(selected)
                    || (Mathf.Abs(value) == Mathf.Abs(selected) && time >= selectedTime))
                { selected = value; selectedTime = time; }
            }
        }

        public void Clear()
        {
            Array.Clear(events, 0, events.Length);
            next = 0;
            previousSample = double.NegativeInfinity;
        }
    }
}
