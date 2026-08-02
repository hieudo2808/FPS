using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponManager : NetworkBehaviour
    {
        [SerializeField] private List<GameObject> weapons;
        [SerializeField] private Animator characterAnimation;
        [SerializeField] private int maxWeaponSlots = 2;

        private NetworkVariable<int> networkedWeaponIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        public static WeaponManager LocalInstance { get; private set; }

        public int WeaponCount => weapons != null ? weapons.Count : 0;
        public int CurrentWeaponIndex => WeaponCount > 0
            ? Mathf.Clamp(networkedWeaponIndex.Value, 0, WeaponCount - 1)
            : 0;
        public GameObject CurrentWeapon => WeaponCount > 0
            ? weapons[Mathf.Clamp(networkedWeaponIndex.Value, 0, WeaponCount - 1)]
            : null;
        public GameObject UnusedWeapon => WeaponCount > 0
            ? weapons[(networkedWeaponIndex.Value + 1) % WeaponCount]
            : null;
        public Animator CharacterAnimation => characterAnimation;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) LocalInstance = this;

            networkedWeaponIndex.OnValueChanged += OnWeaponChanged;
            UpdateWeaponVisibility(networkedWeaponIndex.Value);

            // Set owner flag sau khi IsOwner đã chính xác
            if (weapons == null)
                return;

            foreach (var weaponObj in weapons)
            {
                var weapon = weaponObj.GetComponent<Weapon>();
                if (weapon != null)
                    weapon.SetOwner(IsOwner);
            }

            ReportCurrentWeaponTelemetry();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this)
                LocalInstance = null;

            networkedWeaponIndex.OnValueChanged -= OnWeaponChanged;
        }



        [ServerRpc]
        public void RequestSwitchWeaponServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (WeaponCount == 0) return;
            networkedWeaponIndex.Value = (networkedWeaponIndex.Value + 1) % weapons.Count;
            GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(networkedWeaponIndex.Value);
        }

        public void SetEquippedWeaponServer(int slotIndex)
        {
            if (!IsServer || WeaponCount == 0)
                return;

            networkedWeaponIndex.Value = Mathf.Clamp(slotIndex, 0, WeaponCount - 1);
            GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(networkedWeaponIndex.Value);
        }

        private void OnWeaponChanged(int oldIndex, int newIndex)
        {
            UpdateWeaponVisibility(newIndex);
            ReportCurrentWeaponTelemetry();
        }

        private void UpdateWeaponVisibility(int index)
        {
            if (weapons == null) return;
            for (int i = 0; i < weapons.Count; i++)
                weapons[i].SetActive(i == index);

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateWeaponUI();
        }

        public void AddWeapon(GameObject newWeapon)
        {
            if (weapons.Count < maxWeaponSlots) weapons.Add(newWeapon);
        }



        /// <summary>
        /// Thêm đạn cho vũ khí hiện tại — tránh chain call qua CurrentWeapon.
        /// </summary>
        public void AddAmmoToCurrentWeapon(int amount)
        {
            if (IsServer)
                GetComponent<WeaponFireHandler>()?.AddReserveAmmoServer(amount);

            AddAmmoToCurrentWeaponLocalOnly(amount);
        }

        public void AddAmmoToCurrentWeaponLocalOnly(int amount)
        {
            var weapon = CurrentWeapon?.GetComponent<Weapon>();
            weapon?.AddReserveAmmo(amount);
        }

        public void ReportCurrentWeaponTelemetry()
        {
            var weapon = CurrentWeapon?.GetComponent<Weapon>();
            weapon?.ReportCombatTelemetry();
        }

        public Weapon GetWeapon(int slotIndex)
        {
            if (weapons == null || slotIndex < 0 || slotIndex >= weapons.Count)
                return null;

            return weapons[slotIndex] != null ? weapons[slotIndex].GetComponent<Weapon>() : null;
        }

        public void ApplyAuthoritativeWeaponState(WeaponOwnerState state)
        {
            Weapon weapon = GetWeapon(state.slotIndex);
            weapon?.SetLocalAmmoState(state.magazineAmmo, state.reserveAmmo, state.isReloading);

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateAmmoInfo();
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
