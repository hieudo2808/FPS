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
        private ushort lastAcceptedFireSequence;
        private bool hasAcceptedFireSequence;

        public int MagazineAmmo => magazineAmmo;
        public int ReserveAmmo => reserveAmmo;
        public double NextAllowedFireTime => nextAllowedFireTime;
        public double ReloadCompleteTime => reloadCompleteTime;
        public ushort LastAcceptedFireSequence => lastAcceptedFireSequence;

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
            lastAcceptedFireSequence = 0;
            hasAcceptedFireSequence = false;
        }

        public bool CanAcceptFireSequence(ushort sequence)
        {
            return !hasAcceptedFireSequence || NetworkSequence.IsNewer(sequence, lastAcceptedFireSequence);
        }

        public bool TryConsumeFire(
            WeaponData weaponData,
            double now,
            ushort fireSequence = 0,
            bool enforceSequence = false)
        {
            if (weaponData == null)
                return false;

            CompleteReloadIfReady(weaponData, now);

            if (IsReloading(now))
            {
                Debug.LogWarning($"[DIAGNOSTIC][ServerState] REJECTED IsReloading: now={now:F3} reloadCompleteTime={reloadCompleteTime:F3}");
                return false;
            }
            if (magazineAmmo <= 0)
            {
                Debug.LogWarning($"[DIAGNOSTIC][ServerState] REJECTED magazineAmmo={magazineAmmo} <= 0");
                return false;
            }
            // Arrival jitter cannot justify accepting a shot before the server's
            // authoritative cooldown.  The tiny epsilon is only for floating
            // point precision at the exact cooldown boundary.
            const double cooldownEpsilonSeconds = 0.0001;
            if (now + cooldownEpsilonSeconds < nextAllowedFireTime)
            {
                return false;
            }
            if (enforceSequence && !CanAcceptFireSequence(fireSequence))
            {
                Debug.LogWarning($"[DIAGNOSTIC][ServerState] REJECTED Sequence: seq={fireSequence} lastAccepted={lastAcceptedFireSequence}");
                return false;
            }

            magazineAmmo--;
            double baseTime = System.Math.Max(now, nextAllowedFireTime);
            nextAllowedFireTime = baseTime + Mathf.Max(0f, weaponData.fireRate);
            if (enforceSequence)
            {
                lastAcceptedFireSequence = fireSequence;
                hasAcceptedFireSequence = true;
            }
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
            lastAcceptedFireSequence = 0;
            hasAcceptedFireSequence = false;
        }

        public WeaponRuntimeSnapshot Capture(byte slotIndex, WeaponData weaponData)
        {
            return new WeaponRuntimeSnapshot
            {
                slotIndex = slotIndex,
                definitionId = weaponData != null ? weaponData.name : string.Empty,
                magazineAmmo = magazineAmmo,
                reserveAmmo = reserveAmmo,
                nextAllowedFireTime = nextAllowedFireTime,
                reloadCompleteTime = reloadCompleteTime,
                lastAcceptedFireSequence = lastAcceptedFireSequence,
                hasAcceptedFireSequence = hasAcceptedFireSequence
            };
        }

        public void Restore(WeaponRuntimeSnapshot snapshot, int stableWeaponId)
        {
            weaponInstanceId = stableWeaponId;
            initialized = true;
            magazineAmmo = Mathf.Max(0, snapshot.magazineAmmo);
            reserveAmmo = Mathf.Max(0, snapshot.reserveAmmo);
            nextAllowedFireTime = snapshot.nextAllowedFireTime;
            reloadCompleteTime = snapshot.reloadCompleteTime;
            lastAcceptedFireSequence = snapshot.lastAcceptedFireSequence;
            hasAcceptedFireSequence = snapshot.hasAcceptedFireSequence;
        }
    }
}
