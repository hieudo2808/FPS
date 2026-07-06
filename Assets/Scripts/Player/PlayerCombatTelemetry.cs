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

        public bool IsReloading => isReloading;
        public float AmmoPercent => ammoPercent;
        public double LastUpdatedServerTime => lastUpdatedServerTime;

        public void ReportWeaponState(bool reloading, int currentAmmo, int magazineSize)
        {
            float percent = CalculateAmmoPercent(currentAmmo, magazineSize);

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            {
                ReportWeaponStateServerRpc(reloading, percent);
                return;
            }

            ApplyWeaponState(reloading, percent, GetServerTime());
        }

        public void ApplyWeaponState(bool reloading, int currentAmmo, int magazineSize, double serverTime)
        {
            ApplyWeaponState(reloading, CalculateAmmoPercent(currentAmmo, magazineSize), serverTime);
        }

        public bool IsFresh(double serverTime)
        {
            return serverTime - lastUpdatedServerTime <= telemetryFreshnessSeconds;
        }

        public bool IsFresh()
        {
            return IsFresh(GetServerTime());
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ReportWeaponStateServerRpc(bool reloading, float reportedAmmoPercent)
        {
            ApplyWeaponState(reloading, reportedAmmoPercent, GetServerTime());
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
    }
}
