using System;
using System.Collections.Generic;

namespace FPS.RecoilPort
{
    public static class UnrealRecoilCurve
    {
        [Serializable]
        public struct Key
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
            public int mode; // 0 constant, 1 linear, 2 cubic; describes the outgoing segment.
        }

        public static void Validate(Key[] keys)
        {
            if (keys == null || keys.Length == 0)
                throw new ArgumentException("A curve needs at least one key.", nameof(keys));
            for (int i = 0; i < keys.Length; i++)
            {
                Key k = keys[i];
                if (!Finite(k.time) || !Finite(k.value) || !Finite(k.inTangent)
                    || !Finite(k.outTangent) || k.mode < 0 || k.mode > 2
                    || (i > 0 && k.time <= keys[i - 1].time))
                    throw new ArgumentException("Invalid or unsorted curve keys.", nameof(keys));
            }
        }

        public static float Evaluate(Key[] keys, float x)
        {
            if (keys == null || keys.Length == 0)
                throw new ArgumentException("A curve needs at least one key.", nameof(keys));
            if (!Finite(x))
                throw new ArgumentOutOfRangeException(nameof(x));
            // shortcut: unweighted tangents and clamped extrapolation only; extend before importing other curve types.
            if (x <= keys[0].time) return keys[0].value;
            if (x >= keys[keys.Length - 1].time) return keys[keys.Length - 1].value;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                Key a = keys[i];
                Key b = keys[i + 1];
                if (x >= b.time) continue;
                float dt = b.time - a.time;
                float u = (x - a.time) / dt;
                switch (a.mode)
                {
                    case 0: return a.value;
                    case 1: return a.value + u * (b.value - a.value);
                    case 2:
                        float u2 = u * u;
                        float u3 = u2 * u;
                        return (2f*u3 - 3f*u2 + 1f)*a.value
                            + (u3 - 2f*u2 + u)*dt*a.outTangent
                            + (-2f*u3 + 3f*u2)*b.value
                            + (u3 - u2)*dt*b.inTangent;
                    default: throw new ArgumentException("Unsupported curve mode.", nameof(keys));
                }
            }
            throw new InvalidOperationException("Validate keys before evaluating a curve.");
        }

        // Keys and stationary points are sufficient to bound every supported segment.
        public static float[] CriticalTimes(Key[] keys)
        {
            Validate(keys);
            var times = new List<float>(keys.Length * 3);
            for (int i = 0; i < keys.Length; i++)
            {
                times.Add(keys[i].time);
                if (i + 1 == keys.Length || keys[i].mode != 2) continue;
                Key left = keys[i], right = keys[i + 1];
                double dt = (double)right.time - left.time;
                double m0 = dt * left.outTangent, m1 = dt * right.inTangent;
                double a = 3 * (2 * ((double)left.value - right.value) + m0 + m1);
                double b = 2 * (3 * ((double)right.value - left.value) - 2 * m0 - m1);
                double c = m0;
                if (a == 0)
                {
                    if (b != 0) AddRoot(-c / b);
                }
                else
                {
                    double discriminant = b * b - 4 * a * c;
                    if (discriminant >= 0)
                    {
                        double q = -.5 * (b + (b < 0 ? -1 : 1) * Math.Sqrt(discriminant));
                        AddRoot(q / a);
                        if (q != 0) AddRoot(c / q);
                    }
                }
                void AddRoot(double u)
                {
                    if (u > 0 && u < 1) times.Add((float)(left.time + dt * u));
                }
            }
            times.Sort();
            return times.ToArray();
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
