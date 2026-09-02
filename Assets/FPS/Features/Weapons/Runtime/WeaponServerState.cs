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
        private double reloadAmmoCommitTime = -1.0;
        private double reloadCompleteTime = -1.0;
        private double equipCompleteTime = -1.0;
        private ushort lastAcceptedFireSequence;
        private bool hasAcceptedFireSequence;
        private float reloadTimingMultiplier = 1f;

        public int MagazineAmmo => magazineAmmo;
        public int ReserveAmmo => reserveAmmo;
        public double NextAllowedFireTime => nextAllowedFireTime;
        public double ReloadAmmoCommitTime => reloadAmmoCommitTime;
        public double ReloadCompleteTime => reloadCompleteTime;
        public double NextReloadEventTime
        {
            get
            {
                if (reloadAmmoCommitTime < 0.0) return reloadCompleteTime;
                if (reloadCompleteTime < 0.0) return reloadAmmoCommitTime;
                return System.Math.Min(reloadAmmoCommitTime, reloadCompleteTime);
            }
        }
        public double EquipCompleteTime => equipCompleteTime;
        public ushort LastAcceptedFireSequence => lastAcceptedFireSequence;

        public bool IsReloading(double now)
        {
            return reloadCompleteTime >= 0.0 && now < reloadCompleteTime;
        }

        public bool IsEquipping(double now)
        {
            return equipCompleteTime >= 0.0 && now < equipCompleteTime;
        }

        public void BeginEquip(WeaponData weaponData, double now)
        {
            equipCompleteTime = weaponData != null
                ? now + weaponData.EquipDuration
                : -1.0;
        }

        public void CancelReload()
        {
            reloadAmmoCommitTime = -1.0;
            reloadCompleteTime = -1.0;
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
            reloadAmmoCommitTime = -1.0;
            reloadCompleteTime = -1.0;
            equipCompleteTime = -1.0;
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

            AdvanceReloadIfReady(weaponData, now);

            if (IsEquipping(now))
                return false;
            bool interruptPerShellReload = IsReloading(now)
                && weaponData.reloadMode == ReloadMode.PerShell
                && magazineAmmo > 0;
            if (IsReloading(now) && !interruptPerShellReload)
            {
                return false;
            }
            if (magazineAmmo <= 0)
                return false;
            // Arrival jitter cannot justify accepting a shot before the server's
            // authoritative cooldown.  The tiny epsilon is only for floating
            // point precision at the exact cooldown boundary.
            const double cooldownEpsilonSeconds = 0.0001;
            if (now + cooldownEpsilonSeconds < nextAllowedFireTime)
            {
                return false;
            }
            if (enforceSequence && !CanAcceptFireSequence(fireSequence))
                return false;

            // Cancel only after every other validation passes. A rejected
            // rapid/duplicate fire request must not terminate the reload.
            if (interruptPerShellReload)
                CancelReload();

            magazineAmmo--;
            double baseTime = System.Math.Max(now, nextAllowedFireTime);
            nextAllowedFireTime = baseTime + weaponData.FireInterval;
            if (enforceSequence)
            {
                lastAcceptedFireSequence = fireSequence;
                hasAcceptedFireSequence = true;
            }
            return true;
        }

        public bool TryBeginReload(WeaponData weaponData, double now, float timingMultiplier = 1f)
        {
            if (weaponData == null)
                return false;

            AdvanceReloadIfReady(weaponData, now);

            if (IsEquipping(now)) return false;
            if (IsReloading(now)) return false;
            if (reserveAmmo <= 0) return false;
            if (magazineAmmo >= weaponData.magazineSize) return false;

            reloadTimingMultiplier = Mathf.Clamp(timingMultiplier, 1f, 3f);

            if (weaponData.reloadMode == ReloadMode.PerShell)
            {
                int roundsToLoad = weaponData.GetPerShellRoundsToLoad(magazineAmmo, reserveAmmo);
                float opening = weaponData.PerShellOpeningDuration * reloadTimingMultiplier;
                float interval = weaponData.PerShellInterval * reloadTimingMultiplier;
                float closing = weaponData.PerShellClosingDuration * reloadTimingMultiplier;
                reloadAmmoCommitTime = now + opening + interval;
                reloadCompleteTime = reloadAmmoCommitTime
                    + Mathf.Max(0, roundsToLoad - 1) * interval
                    + closing;
            }
            else
            {
                reloadAmmoCommitTime = now + weaponData.ReloadAmmoCommitDuration * reloadTimingMultiplier;
                reloadCompleteTime = now + weaponData.ReloadDuration * reloadTimingMultiplier;
                if (reloadCompleteTime < reloadAmmoCommitTime)
                    reloadCompleteTime = reloadAmmoCommitTime;
            }

            AdvanceReloadIfReady(weaponData, now);
            return true;
        }

        public void CompleteReloadIfReady(WeaponData weaponData, double now)
        {
            AdvanceReloadIfReady(weaponData, now);
        }

        public void AdvanceReloadIfReady(WeaponData weaponData, double now)
        {
            if (weaponData == null)
                return;

            if (reloadAmmoCommitTime >= 0.0 && now >= reloadAmmoCommitTime)
            {
                if (weaponData.reloadMode == ReloadMode.PerShell)
                {
                    double interval = System.Math.Max(0.0001, weaponData.PerShellInterval * reloadTimingMultiplier);
                    while (reloadAmmoCommitTime >= 0.0 && now >= reloadAmmoCommitTime)
                    {
                        CommitReloadAmmo(weaponData, oneRoundOnly: true);
                        bool hasMoreRounds = magazineAmmo < weaponData.magazineSize && reserveAmmo > 0;
                        if (!hasMoreRounds)
                        {
                            reloadAmmoCommitTime = -1.0;
                            break;
                        }

                        reloadAmmoCommitTime += interval;
                    }
                }
                else
                {
                    CommitReloadAmmo(weaponData, oneRoundOnly: false);
                    reloadAmmoCommitTime = -1.0;
                }
            }

            if (reloadCompleteTime >= 0.0 && now >= reloadCompleteTime)
            {
                reloadAmmoCommitTime = -1.0;
                reloadCompleteTime = -1.0;
            }
        }

        private void CommitReloadAmmo(WeaponData weaponData, bool oneRoundOnly)
        {
            int bulletsNeeded = Mathf.Max(0, weaponData.magazineSize - magazineAmmo);
            int bulletsToReload = oneRoundOnly
                ? Mathf.Min(1, Mathf.Min(bulletsNeeded, reserveAmmo))
                : Mathf.Min(bulletsNeeded, reserveAmmo);
            reserveAmmo -= bulletsToReload;
            magazineAmmo += bulletsToReload;
        }

        public void AddReserveAmmo(int amount)
        {
            if (amount <= 0)
                return;

            reserveAmmo += amount;
        }

        public void InitializeForTests(
            int currentWeaponInstanceId,
            int magazineAmmo,
            int reserveAmmo,
            double nextFireTime = 0.0,
            double equipReadyTime = -1.0)
        {
            weaponInstanceId = currentWeaponInstanceId;
            initialized = true;
            this.magazineAmmo = Mathf.Max(0, magazineAmmo);
            this.reserveAmmo = Mathf.Max(0, reserveAmmo);
            nextAllowedFireTime = nextFireTime;
            reloadAmmoCommitTime = -1.0;
            reloadCompleteTime = -1.0;
            equipCompleteTime = equipReadyTime;
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
                reloadAmmoCommitTime = reloadAmmoCommitTime,
                reloadCompleteTime = reloadCompleteTime,
                equipCompleteTime = equipCompleteTime,
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
            reloadAmmoCommitTime = snapshot.reloadAmmoCommitTime;
            reloadCompleteTime = snapshot.reloadCompleteTime;
            equipCompleteTime = snapshot.equipCompleteTime;
            lastAcceptedFireSequence = snapshot.lastAcceptedFireSequence;
            hasAcceptedFireSequence = snapshot.hasAcceptedFireSequence;
        }
    }
}
