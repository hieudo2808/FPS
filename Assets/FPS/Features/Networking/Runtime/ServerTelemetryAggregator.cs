using System;
using System.Collections.Generic;

namespace FPS
{
    public readonly struct ServerTelemetrySnapshot
    {
        public readonly int ServerTick;
        public readonly SessionPlayerId PlayerId;
        public readonly bool HasWeaponState;
        public readonly bool IsReloading;
        public readonly int MagazineAmmo;
        public readonly int MagazineSize;
        public readonly float Health;
        public readonly float DamageTaken;
        public readonly int PickupCount;
        public readonly int KillCount;
        public readonly int HeadshotCount;
        public readonly int ShotsFired;
        public readonly int ShotsHit;
        public readonly int HeadshotHitCount;
        public readonly int DownedCount;

        public ServerTelemetrySnapshot(
            int serverTick,
            SessionPlayerId playerId,
            bool hasWeaponState,
            bool isReloading,
            int magazineAmmo,
            int magazineSize,
            float health,
            float damageTaken,
            int pickupCount,
            int killCount,
            int headshotCount,
            int shotsFired = 0,
            int shotsHit = 0,
            int headshotHitCount = 0,
            int downedCount = 0)
        {
            ServerTick = serverTick;
            PlayerId = playerId;
            HasWeaponState = hasWeaponState;
            IsReloading = isReloading;
            MagazineAmmo = magazineAmmo;
            MagazineSize = magazineSize;
            Health = health;
            DamageTaken = damageTaken;
            PickupCount = pickupCount;
            KillCount = killCount;
            HeadshotCount = headshotCount;
            ShotsFired = shotsFired;
            ShotsHit = shotsHit;
            HeadshotHitCount = headshotHitCount;
            DownedCount = downedCount;
        }
    }

    /// <summary>
    /// Collects server-authored gameplay events and seals them only after the server tick ends.
    /// Grouping by stable player id makes aggregation independent of NGO client-id rebinding.
    /// </summary>
    public sealed class ServerTelemetryAggregator
    {
        private sealed class Accumulator
        {
            public bool HasWeapon;
            public bool IsReloading;
            public int MagazineAmmo;
            public int MagazineSize;
            public float Health;
            public float DamageTaken;
            public int PickupCount;
            public int KillCount;
            public int HeadshotCount;
            public int ShotsFired;
            public int ShotsHit;
            public int HeadshotHitCount;
            public int DownedCount;
        }

        private readonly SortedDictionary<int, Dictionary<ulong, Accumulator>> pending = new();
        private readonly List<int> ticksToSeal = new();
        private readonly List<ulong> playersToSeal = new();

        public int PendingTickCount => pending.Count;

        public void RecordWeapon(
            SessionPlayerId playerId,
            int serverTick,
            bool isReloading,
            int magazineAmmo,
            int magazineSize)
        {
            if (!playerId.IsValid)
                return;

            Accumulator accumulator = Get(playerId, serverTick);
            accumulator.HasWeapon = true;
            accumulator.IsReloading = isReloading;
            accumulator.MagazineAmmo = Math.Max(0, magazineAmmo);
            accumulator.MagazineSize = Math.Max(0, magazineSize);
        }

        public void RecordHealth(
            SessionPlayerId playerId,
            int serverTick,
            float authoritativeHealth,
            float damageTaken)
        {
            if (!playerId.IsValid)
                return;

            Accumulator accumulator = Get(playerId, serverTick);
            accumulator.Health = Math.Max(0f, authoritativeHealth);
            accumulator.DamageTaken += Math.Max(0f, damageTaken);
        }

        public void RecordPickup(SessionPlayerId playerId, int serverTick)
        {
            if (playerId.IsValid)
                Get(playerId, serverTick).PickupCount++;
        }

        public void RecordKill(SessionPlayerId playerId, int serverTick, bool headshot)
        {
            if (!playerId.IsValid)
                return;

            Accumulator accumulator = Get(playerId, serverTick);
            accumulator.KillCount++;
            if (headshot)
                accumulator.HeadshotCount++;
        }

        public void RecordShot(SessionPlayerId playerId, int serverTick, bool hit, bool headshot = false)
        {
            if (!playerId.IsValid)
                return;

            Accumulator accumulator = Get(playerId, serverTick);
            accumulator.ShotsFired++;
            if (hit)
            {
                accumulator.ShotsHit++;
                if (headshot)
                    accumulator.HeadshotHitCount++;
            }
        }

        public void RecordDowned(SessionPlayerId playerId, int serverTick)
        {
            if (playerId.IsValid)
                Get(playerId, serverTick).DownedCount++;
        }

        public void SealBefore(int exclusiveServerTick, Action<ServerTelemetrySnapshot> publish)
        {
            ticksToSeal.Clear();
            foreach (int tick in pending.Keys)
            {
                if (tick >= exclusiveServerTick)
                    break;
                ticksToSeal.Add(tick);
            }

            for (int tickIndex = 0; tickIndex < ticksToSeal.Count; tickIndex++)
            {
                int tick = ticksToSeal[tickIndex];
                Dictionary<ulong, Accumulator> byPlayer = pending[tick];
                playersToSeal.Clear();
                foreach (ulong playerId in byPlayer.Keys)
                    playersToSeal.Add(playerId);
                playersToSeal.Sort();

                for (int playerIndex = 0; playerIndex < playersToSeal.Count; playerIndex++)
                {
                    ulong value = playersToSeal[playerIndex];
                    Accumulator accumulator = byPlayer[value];
                    publish?.Invoke(new ServerTelemetrySnapshot(
                        tick,
                        new SessionPlayerId(value),
                        accumulator.HasWeapon,
                        accumulator.IsReloading,
                        accumulator.MagazineAmmo,
                        accumulator.MagazineSize,
                        accumulator.Health,
                        accumulator.DamageTaken,
                        accumulator.PickupCount,
                        accumulator.KillCount,
                        accumulator.HeadshotCount,
                        accumulator.ShotsFired,
                        accumulator.ShotsHit,
                        accumulator.HeadshotHitCount,
                        accumulator.DownedCount));
                }

                pending.Remove(tick);
            }
        }

        public void Clear()
        {
            pending.Clear();
            ticksToSeal.Clear();
            playersToSeal.Clear();
        }

        private Accumulator Get(SessionPlayerId playerId, int serverTick)
        {
            if (!pending.TryGetValue(serverTick, out Dictionary<ulong, Accumulator> byPlayer))
            {
                byPlayer = new Dictionary<ulong, Accumulator>();
                pending.Add(serverTick, byPlayer);
            }

            if (!byPlayer.TryGetValue(playerId.Value, out Accumulator accumulator))
            {
                accumulator = new Accumulator();
                byPlayer.Add(playerId.Value, accumulator);
            }

            return accumulator;
        }
    }
}
