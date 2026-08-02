using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerCombatTelemetry : NetworkBehaviour
    {
        [SerializeField] private double telemetryFreshnessSeconds = 2.0;

        private bool isReloading;
        private float ammoPercent = 1f;
        private double lastUpdatedServerTime = -999.0;
        private float damageTakenThisTick;
        private int pickupsThisTick;
        private int killsThisTick;
        private int headshotsThisTick;
        private int shotsFiredThisTick;
        private int shotsHitThisTick;
        private int headshotHitsThisTick;
        private int downedThisTick;

        public bool IsReloading => isReloading;
        public float AmmoPercent => ammoPercent;
        public double LastUpdatedServerTime => lastUpdatedServerTime;
        public float DamageTakenThisTick => damageTakenThisTick;
        public int PickupsThisTick => pickupsThisTick;
        public int KillsThisTick => killsThisTick;
        public int HeadshotsThisTick => headshotsThisTick;
        public int ShotsFiredThisTick => shotsFiredThisTick;
        public int ShotsHitThisTick => shotsHitThisTick;
        public int HeadshotHitsThisTick => headshotHitsThisTick;
        public int DownedThisTick => downedThisTick;

        public void ReportWeaponState(bool reloading, int currentAmmo, int magazineSize)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
                return;

            ApplyWeaponState(reloading, CalculateAmmoPercent(currentAmmo, magazineSize), GetServerTime());
        }

        public void ApplyWeaponState(bool reloading, int currentAmmo, int magazineSize, double serverTime)
        {
            PlayerHealth health = GetComponent<PlayerHealth>();
            ServerTelemetryAggregator telemetry = NetworkGameManager.Instance?.Telemetry;
            if (telemetry != null && health != null && health.StablePlayerId.IsValid && IsServer)
            {
                telemetry.RecordWeapon(
                    health.StablePlayerId,
                    GetServerTick(),
                    reloading,
                    currentAmmo,
                    magazineSize);
                return;
            }

            ApplyWeaponState(reloading, CalculateAmmoPercent(currentAmmo, magazineSize), serverTime);
        }

        public void ApplyAggregateSnapshot(ServerTelemetrySnapshot snapshot, double serverTime)
        {
            if (snapshot.HasWeaponState)
                ApplyWeaponState(snapshot.IsReloading,
                    CalculateAmmoPercent(snapshot.MagazineAmmo, snapshot.MagazineSize), serverTime);

            damageTakenThisTick = snapshot.DamageTaken;
            pickupsThisTick = snapshot.PickupCount;
            killsThisTick = snapshot.KillCount;
            headshotsThisTick = snapshot.HeadshotCount;
            shotsFiredThisTick = snapshot.ShotsFired;
            shotsHitThisTick = snapshot.ShotsHit;
            headshotHitsThisTick = snapshot.HeadshotHitCount;
            downedThisTick = snapshot.DownedCount;
            lastUpdatedServerTime = serverTime;
        }

        public bool IsFresh(double serverTime)
        {
            return serverTime - lastUpdatedServerTime <= telemetryFreshnessSeconds;
        }

        public bool IsFresh()
        {
            return IsFresh(GetServerTime());
        }

        private void ApplyWeaponState(bool reloading, float percent, double serverTime)
        {
            isReloading = reloading;
            ammoPercent = Mathf.Clamp01(percent);
            lastUpdatedServerTime = serverTime;
        }

        private static float CalculateAmmoPercent(int currentAmmo, int magazineSize)
        {
            if (magazineSize <= 0) return 0f;
            return Mathf.Clamp01((float)currentAmmo / magazineSize);
        }

        private double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }

        private int GetServerTick()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Tick
                : Mathf.FloorToInt((float)(Time.timeAsDouble * NetworkGameplayPolicy.SimulationHz));
        }
    }
}
