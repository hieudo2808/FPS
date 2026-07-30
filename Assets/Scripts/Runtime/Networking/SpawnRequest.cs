namespace FPS
{
    public readonly struct SpawnRequest
    {
        public readonly ulong clientId;
        public readonly float avoidEnemiesRadius;
        public readonly float avoidPlayersRadius;
        public readonly bool allowFallback;

        public SpawnRequest(
            ulong clientId,
            float avoidEnemiesRadius = 8f,
            float avoidPlayersRadius = 2f,
            bool allowFallback = true)
        {
            this.clientId = clientId;
            this.avoidEnemiesRadius = avoidEnemiesRadius;
            this.avoidPlayersRadius = avoidPlayersRadius;
            this.allowFallback = allowFallback;
        }
    }
}
