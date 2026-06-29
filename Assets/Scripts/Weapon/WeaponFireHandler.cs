using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponFireHandler : NetworkBehaviour
    {
        private WeaponManager _weaponManager;
        private WeaponManager weaponManager => _weaponManager != null ? _weaponManager : (_weaponManager = GetComponent<WeaponManager>());

        [ServerRpc]
        public void RequestFireServerRpc(Vector3 spawnPosition, Vector3 direction)
        {
            // Xác thực khoảng cách: spawnPosition không được cách quá xa vị trí player thực tế trên server
            float dist = Vector3.Distance(transform.position, spawnPosition);
            if (dist > 5.0f)
            {
                Debug.LogWarning($"[WeaponManager] Rejecting fire request from player {OwnerClientId}. Distance {dist}m exceeds limit.");
                return;
            }

            // Server tự lookup damage từ WeaponData — không tin client
            var currentWeaponGo = weaponManager.CurrentWeapon;
            if (currentWeaponGo == null) return;
            var weapon = currentWeaponGo.GetComponent<Weapon>();
            if (weapon == null || weapon.Data == null) return;
            
            float damage = weapon.Data.damage;

            if (Physics.Raycast(spawnPosition, direction, out RaycastHit hit, 500f))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    PlayerHealth playerHealth = damageable as PlayerHealth;
                    if (playerHealth == null || !playerHealth.IsOwner)
                    {
                        damageable.TakeDamage(damage);
                    }
                }
            }

            FireEffectsClientRpc(spawnPosition, direction);
        }

        [ClientRpc]
        private void FireEffectsClientRpc(Vector3 spawnPosition, Vector3 direction)
        {
            if (IsOwner) return;

            var currentWeaponGo = weaponManager.CurrentWeapon;
            if (currentWeaponGo == null) return;
            Weapon currentWeapon = currentWeaponGo.GetComponent<Weapon>();
            if (currentWeapon == null) return;

            currentWeapon.SpawnVisualBullet(spawnPosition, direction);
            currentWeapon.PlayMuzzleEffect();
            currentWeapon.PlayShootSound();
        }
    }
}
