using UnityEngine;

namespace FPS
{
    public class NetworkTimer
    {
        private readonly float tickDelta;
        private float accumulator;

        public int CurrentTick { get; set; }

        public float Alpha => Mathf.Clamp01(accumulator / tickDelta);
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
