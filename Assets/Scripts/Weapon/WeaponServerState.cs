using UnityEngine;

namespace FPS
{
    public sealed class WeaponServerState
    {
        private int weaponInstanceId;
        private bool initialized;
        private int magazineAmmo;
        private int reserveAmmo;
        private double nextAllowedFireTime;
        private double reloadCompleteTime = -1.0;

        public int MagazineAmmo => magazineAmmo;
        public int ReserveAmmo => reserveAmmo;
        public double NextAllowedFireTime => nextAllowedFireTime;

        public bool IsReloading(double now)
        {
            return reloadCompleteTime >= 0.0 && now < reloadCompleteTime;
        }

        public void EnsureInitialized(int currentWeaponInstanceId, WeaponData weaponData)
        {
            if (weaponData == null)
                return;

            if (initialized && weaponInstanceId == currentWeaponInstanceId)
                return;

            weaponInstanceId = currentWeaponInstanceId;
            initialized = true;
            magazineAmmo = Mathf.Max(0, weaponData.magazineSize);
            reserveAmmo = Mathf.Max(0, weaponData.totalAmmo - magazineAmmo);
            nextAllowedFireTime = 0.0;
            reloadCompleteTime = -1.0;
        }

        public bool TryConsumeFire(WeaponData weaponData, double now)
        {
            if (weaponData == null)
                return false;

            CompleteReloadIfReady(weaponData, now);

            if (IsReloading(now)) return false;
            if (magazineAmmo <= 0) return false;
            if (now < nextAllowedFireTime) return false;

            magazineAmmo--;
            nextAllowedFireTime = now + Mathf.Max(0f, weaponData.fireRate);
            return true;
        }

        public bool TryBeginReload(WeaponData weaponData, double now)
        {
            if (weaponData == null)
                return false;

            CompleteReloadIfReady(weaponData, now);

            if (IsReloading(now)) return false;
            if (reserveAmmo <= 0) return false;
            if (magazineAmmo >= weaponData.magazineSize) return false;

            reloadCompleteTime = now + Mathf.Max(0f, weaponData.reloadTime);
            CompleteReloadIfReady(weaponData, now);
            return true;
        }

        public void CompleteReloadIfReady(WeaponData weaponData, double now)
        {
            if (weaponData == null)
                return;

            if (reloadCompleteTime < 0.0 || now < reloadCompleteTime)
                return;

            int bulletsNeeded = Mathf.Max(0, weaponData.magazineSize - magazineAmmo);
            int bulletsToReload = Mathf.Min(bulletsNeeded, reserveAmmo);
            reserveAmmo -= bulletsToReload;
            magazineAmmo += bulletsToReload;
            reloadCompleteTime = -1.0;
        }

        public void AddReserveAmmo(int amount)
        {
            if (amount <= 0)
                return;

            reserveAmmo += amount;
        }

        public void InitializeForTests(int currentWeaponInstanceId, int magazineAmmo, int reserveAmmo, double nextFireTime = 0.0)
        {
            weaponInstanceId = currentWeaponInstanceId;
            initialized = true;
            this.magazineAmmo = Mathf.Max(0, magazineAmmo);
            this.reserveAmmo = Mathf.Max(0, reserveAmmo);
            nextAllowedFireTime = nextFireTime;
            reloadCompleteTime = -1.0;
        }
    }
}
