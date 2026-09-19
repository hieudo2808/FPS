using System;
using UnityEngine;

namespace FPS
{
    public static class SpecialThreatSignal
    {
        public static event Action<Vector3, float> Raised;

        public static void Raise(Vector3 worldPosition, float durationSeconds)
        {
            Raised?.Invoke(worldPosition, Mathf.Max(0.1f, durationSeconds));
        }
    }
}
