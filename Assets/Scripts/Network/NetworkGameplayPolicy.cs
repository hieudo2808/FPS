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

        public const int StateSendEveryNTicks = SimulationHz / SnapshotHz;
    }
}
