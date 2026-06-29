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



        [ServerRpc]
        public void RequestSwitchWeaponServerRpc()
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