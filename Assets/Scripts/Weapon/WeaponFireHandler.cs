using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponFireHandler : NetworkBehaviour
    {
        private WeaponManager weaponManager;
        private int serverWeaponInstanceId;
        private bool serverStateInitialized;
        private int serverMagazineAmmo;
        private int serverReserveAmmo;
        private double nextAllowedFireTime;
        private double reloadCompleteTime = -1.0;

        public int ServerMagazineAmmo => serverMagazineAmmo;
        public int ServerReserveAmmo => serverReserveAmmo;
        public bool IsServerReloading => reloadCompleteTime > GetServerTime();

        [ServerRpc]
        public void RequestFireServerRpc(Vector3 spawnPosition, Vector3 direction)
        {
            TryProcessFireServer(spawnPosition, direction, true);
        }

        public bool ProcessFireServerForTests(Vector3 spawnPosition, Vector3 direction)
        {
            return TryProcessFireServer(spawnPosition, direction, false);
        }

        private bool TryProcessFireServer(Vector3 spawnPosition, Vector3 direction, bool emitEffects)
        {
            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            if (weapon == null || weapon.Data == null) return false;

            CompleteServerReloadIfReady();
            double now = GetServerTime();

            if (IsServerReloading) return false;
            if (serverMagazineAmmo <= 0) return false;
            if (now < nextAllowedFireTime) return false;
            if (!IsValidDirection(direction)) return false;

            float dist = Vector3.Distance(transform.position, spawnPosition);
            if (dist > 5.0f)
            {
                Debug.LogWarning($"[WeaponManager] Rejecting fire request from player {OwnerClientId}. Distance {dist}m exceeds limit.");
                return false;
            }

            serverMagazineAmmo--;
            nextAllowedFireTime = now + Mathf.Max(0f, weapon.Data.fireRate);

            float damage = weapon.Data.damage;
            direction = direction.normalized;

            if (Physics.Raycast(spawnPosition, direction, out RaycastHit hit, 500f))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    PlayerHealth playerHealth = damageable as PlayerHealth;
                    if (playerHealth == null || !playerHealth.IsOwner)
                    {
                        if (damageable is IAttributedDamageable attributedDamageable)
                        {
                            EnemyHitbox hitbox = hit.collider.GetComponentInParent<EnemyHitbox>();
                            attributedDamageable.TakeDamage(new DamageInfo(
                                damage,
                                OwnerClientId,
                                GetAttackerPlayerIndex(),
                                hit.point,
                                isHeadshot: hitbox != null && hitbox.IsHeadshot,
                                reactionTime: 0f));
                        }
                        else
                        {
                            damageable.TakeDamage(damage);
                        }
                    }
                }
            }

            if (emitEffects)
                FireEffectsClientRpc(spawnPosition, direction);

            return true;
        }

        [ServerRpc]
        public void RequestReloadServerRpc()
        {
            TryBeginServerReload();
        }

        public bool BeginServerReloadForTests()
        {
            return TryBeginServerReload();
        }

        private bool TryBeginServerReload()
        {
            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            if (weapon == null || weapon.Data == null) return false;

            CompleteServerReloadIfReady();
            if (IsServerReloading) return false;
            if (serverReserveAmmo <= 0) return false;
            if (serverMagazineAmmo >= weapon.Data.magazineSize) return false;

            reloadCompleteTime = GetServerTime() + Mathf.Max(0f, weapon.Data.reloadTime);
            CompleteServerReloadIfReady();
            return true;
        }

        public void AddReserveAmmoServer(int amount)
        {
            if (!IsServer) return;
            if (amount <= 0) return;

            GetCurrentWeaponAndEnsureServerState();
            serverReserveAmmo += amount;
        }

        public void InitializeServerWeaponStateForTests(int magazineAmmo, int reserveAmmo, double nextFireTime = 0.0)
        {
            Weapon weapon = GetCurrentWeapon();
            serverWeaponInstanceId = weapon != null ? weapon.GetInstanceID() : 0;
            serverStateInitialized = true;
            serverMagazineAmmo = Mathf.Max(0, magazineAmmo);
            serverReserveAmmo = Mathf.Max(0, reserveAmmo);
            nextAllowedFireTime = nextFireTime;
            reloadCompleteTime = -1.0;
        }

        public void CompleteServerReloadIfReadyForTests()
        {
            CompleteServerReloadIfReady();
        }

        [ClientRpc]
        private void FireEffectsClientRpc(Vector3 spawnPosition, Vector3 direction)
        {
            if (IsOwner) return;

            Weapon weapon = GetCurrentWeapon();
            if (weapon == null) return;

            weapon.SpawnVisualBullet(spawnPosition, direction);
            weapon.PlayMuzzleEffect();
            weapon.PlayShootSound();
        }

        private int GetAttackerPlayerIndex()
        {
            var profile = PlayerProfiler.Instance?.GetProfileByClientId(OwnerClientId);
            return profile != null ? profile.playerIndex : -1;
        }

        private Weapon GetCurrentWeaponAndEnsureServerState()
        {
            Weapon weapon = GetCurrentWeapon();
            if (weapon == null || weapon.Data == null)
                return null;

            int weaponId = weapon.GetInstanceID();
            if (!serverStateInitialized || serverWeaponInstanceId != weaponId)
            {
                serverWeaponInstanceId = weaponId;
                serverStateInitialized = true;
                serverMagazineAmmo = Mathf.Max(0, weapon.Data.magazineSize);
                serverReserveAmmo = Mathf.Max(0, weapon.Data.totalAmmo - serverMagazineAmmo);
                nextAllowedFireTime = 0.0;
                reloadCompleteTime = -1.0;
            }

            return weapon;
        }

        private Weapon GetCurrentWeapon()
        {
            if (weaponManager == null)
                weaponManager = GetComponent<WeaponManager>();

            if (weaponManager == null || weaponManager.WeaponCount == 0)
                return null;

            GameObject currentWeaponGo = weaponManager.CurrentWeapon;
            return currentWeaponGo != null ? currentWeaponGo.GetComponent<Weapon>() : null;
        }

        private void CompleteServerReloadIfReady()
        {
            if (reloadCompleteTime < 0.0 || GetServerTime() < reloadCompleteTime)
                return;

            Weapon weapon = GetCurrentWeaponAndEnsureServerState();
            if (weapon == null || weapon.Data == null) return;

            int bulletsNeeded = Mathf.Max(0, weapon.Data.magazineSize - serverMagazineAmmo);
            int bulletsToReload = Mathf.Min(bulletsNeeded, serverReserveAmmo);
            serverReserveAmmo -= bulletsToReload;
            serverMagazineAmmo += bulletsToReload;
            reloadCompleteTime = -1.0;
        }

        private static bool IsValidDirection(Vector3 direction)
        {
            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z)) return false;
            if (float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || float.IsInfinity(direction.z)) return false;
            return direction.sqrMagnitude > 0.0001f;
        }

        private double GetServerTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }
    }
}
