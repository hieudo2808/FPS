using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponManager : NetworkBehaviour
    {
        [SerializeField] private List<GameObject> weapons;
        [SerializeField] private Animator characterAnimation;

        private NetworkVariable<int> networkedWeaponIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        public static WeaponManager LocalInstance { get; private set; }

        public GameObject CurrentWeapon  => weapons[networkedWeaponIndex.Value];
        public GameObject UnusedWeapon   => weapons[(networkedWeaponIndex.Value + 1) % weapons.Count];
        public Animator CharacterAnimation => characterAnimation;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) LocalInstance = this;

            networkedWeaponIndex.OnValueChanged += OnWeaponChanged;
            UpdateWeaponVisibility(networkedWeaponIndex.Value);

            // Set owner flag sau khi IsOwner đã chính xác
            foreach (var weaponObj in weapons)
            {
                var weapon = weaponObj.GetComponent<Weapon>();
                if (weapon != null)
                    weapon.SetOwner(IsOwner);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this)
                LocalInstance = null;

            networkedWeaponIndex.OnValueChanged -= OnWeaponChanged;
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2))
                RequestSwitchWeaponServerRpc();
        }

        [ServerRpc]
        private void RequestSwitchWeaponServerRpc()
        {
            networkedWeaponIndex.Value = (networkedWeaponIndex.Value + 1) % weapons.Count;
        }

        private void OnWeaponChanged(int oldIndex, int newIndex)
        {
            UpdateWeaponVisibility(newIndex);
        }

        private void UpdateWeaponVisibility(int index)
        {
            for (int i = 0; i < weapons.Count; i++)
                weapons[i].SetActive(i == index);

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateWeaponUI();
        }

        public void AddWeapon(GameObject newWeapon)
        {
            if (weapons.Count < 2) weapons.Add(newWeapon);
        }

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
            int currentIndex = networkedWeaponIndex.Value;
            if (currentIndex < 0 || currentIndex >= weapons.Count) return;
            var weapon = weapons[currentIndex]?.GetComponent<Weapon>();
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

            Weapon currentWeapon = CurrentWeapon?.GetComponent<Weapon>();
            if (currentWeapon == null) return;

            currentWeapon.SpawnVisualBullet(spawnPosition, direction);
            currentWeapon.PlayMuzzleEffect();
            currentWeapon.PlayShootSound();
        }

        /// <summary>
        /// Thêm đạn cho vũ khí hiện tại — tránh chain call qua CurrentWeapon.
        /// </summary>
        public void AddAmmoToCurrentWeapon(int amount)
        {
            var weapon = CurrentWeapon?.GetComponent<Weapon>();
            weapon?.AddReserveAmmo(amount);
        }

        /// <summary>
        /// Trigger animation trên character — tránh expose CharacterAnimation.
        /// </summary>
        public void TriggerAnimation(string triggerName)
        {
            characterAnimation?.SetTrigger(triggerName);
        }
    }
}