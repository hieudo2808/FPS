namespace FPS
{
    public static class NetworkGameplayPolicy
    {
        public const int SimulationHz = 60;
        public const int SnapshotHz = 30;
        public const int HitboxHistoryHz = 30;

        public const float HitboxHistorySeconds = 0.35f;
        public const float MaxRewindSeconds = 0.25f;
        public const float WarmupSeconds = 5f;
        public const float RespawnSeconds = 5f;

        public const float InputSilenceSeconds = 0.1f;
        public const float OwnerHardSnapDistance = 2f;
        public const int MaxFutureInputTicks = 2;
        public const int MaxPastInputTicks = SimulationHz;
        public const float RewindJitterMarginSeconds = 0.03f;

        public const int StateSendEveryNTicks = SimulationHz / SnapshotHz;
        public const int MaxRepeatedInputTicks = (int)(SimulationHz * InputSilenceSeconds);

        public static bool ShouldNeutralizeInput(int repeatedTicks, int maxRepeatedTicks)
        {
            return repeatedTicks > maxRepeatedTicks;
        }
    }
}
