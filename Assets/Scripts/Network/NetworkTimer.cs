using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Fixed-rate tick timer for CSP.
    /// 
    /// Usage:
    ///   timer.Accumulate(Time.deltaTime);     // once per Update
    ///   while (timer.CanTick()) {
    ///       timer.ConsumeTick();
    ///       // simulate tick with timer.TickDelta...
    ///       timer.CurrentTick++;
    ///   }
    /// </summary>
    public class NetworkTimer
    {
        private readonly float tickDelta;
        private float accumulator;

        public int CurrentTick { get; set; }

        /// <summary>Fraction between previous and current tick (0-1), for visual interpolation.</summary>
        public float Alpha => Mathf.Clamp01(accumulator / tickDelta);

        /// <summary>Fixed time step per tick in seconds.</summary>
        public float TickDelta => tickDelta;

        public NetworkTimer(float tickDelta)
        {
            this.tickDelta = tickDelta;
        }

        public void Accumulate(float dt)
        {
            accumulator += dt;
        }

        public bool CanTick()
        {
            return accumulator >= tickDelta;
        }

        public void ConsumeTick()
        {
            accumulator -= tickDelta;
        }

        public void Reset()
        {
            accumulator = 0f;
        }
    }
}
