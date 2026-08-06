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

        private const int MAX_TICKS_PER_FRAME = 8;
        private int ticksThisFrame;

        public void Accumulate(float dt)
        {
            accumulator += dt;
            ticksThisFrame = 0;
            if (accumulator > tickDelta * MAX_TICKS_PER_FRAME)
            {
                accumulator = tickDelta * MAX_TICKS_PER_FRAME;
            }
        }

        public bool CanTick()
        {
            return accumulator >= tickDelta && ticksThisFrame < MAX_TICKS_PER_FRAME;
        }

        public void ConsumeTick()
        {
            accumulator -= tickDelta;
            ticksThisFrame++;
        }

        public void Reset()
        {
            accumulator = 0f;
        }
    }
}
