using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponManager : NetworkBehaviour
    {
        [SerializeField] private List<GameObject> weapons = new List<GameObject>();
        [SerializeField] private Animator characterAnimation;
        [SerializeField] private int maxWeaponSlots = 2;

        private NetworkVariable<int> networkedWeaponIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        public static WeaponManager LocalInstance { get; private set; }

        /// <summary>
        /// Raised after the networked weapon index has been applied locally.
        /// Presentation systems use this to update first/third-person visuals
        /// without polling the network variable every frame.
        /// </summary>
        public event Action<int> WeaponIndexChanged;

        public int WeaponCount => weapons != null ? weapons.Count : 0;
        public int CurrentWeaponIndex => WeaponCount > 0
            ? Mathf.Clamp(networkedWeaponIndex.Value, 0, WeaponCount - 1)
            : 0;
        public GameObject CurrentWeapon => WeaponCount > 0
            ? weapons[Mathf.Clamp(networkedWeaponIndex.Value, 0, WeaponCount - 1)]
            : null;
        public GameObject UnusedWeapon => WeaponCount > 1
            ? weapons[(networkedWeaponIndex.Value + 1) % WeaponCount]
            : null;
        public Animator CharacterAnimation => characterAnimation;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) LocalInstance = this;

            networkedWeaponIndex.OnValueChanged += OnWeaponChanged;

            #region agent log
            int nullWeaponSlots = 0;
            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i] == null)
                        nullWeaponSlots++;
            }
            GameLog.Info(() => $"[WeaponManager][dbg] OnNetworkSpawn owner={IsOwner} weaponCount={WeaponCount} nullSlots={nullWeaponSlots} currentIndex={networkedWeaponIndex.Value}");
            #region agent log
            GameLog.DebugSession("initial", "W1", "WeaponManager.cs:45", "weapon configuration at network spawn", $"{{\"owner\":{(IsOwner ? "true" : "false")},\"count\":{WeaponCount},\"nullSlots\":{nullWeaponSlots},\"index\":{networkedWeaponIndex.Value}}}");
            #endregion
            #endregion

            UpdateWeaponVisibility(networkedWeaponIndex.Value);
            ApplyOwnerStateToWeapons();
            ReportCurrentWeaponTelemetry();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this)
                LocalInstance = null;

            networkedWeaponIndex.OnValueChanged -= OnWeaponChanged;
        }

        private void ApplyOwnerStateToWeapons()
        {
            if (weapons == null)
                return;

            foreach (var weaponObj in weapons)
            {
                if (weaponObj == null)
                    continue;

                var weapon = weaponObj.GetComponent<Weapon>();
                if (weapon != null)
                    weapon.SetOwner(IsOwner);
            }
        }

        [ServerRpc]
        public void RequestSwitchWeaponServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (WeaponCount == 0) return;
            networkedWeaponIndex.Value = (networkedWeaponIndex.Value + 1) % WeaponCount;
            GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(networkedWeaponIndex.Value);
        }

        public void SetEquippedWeaponServer(int slotIndex)
        {
            // Networked clients may not change the authoritative slot. An
            // unspawned manager is also used by editor/offline setup code, so
            // allow that local path to exercise the same visibility/event flow.
            if ((!IsServer && IsSpawned) || WeaponCount == 0)
                return;

            int clampedSlot = Mathf.Clamp(slotIndex, 0, WeaponCount - 1);
            networkedWeaponIndex.Value = clampedSlot;
            if (!IsSpawned)
                OnWeaponChanged(CurrentWeaponIndex, clampedSlot);
            else
                GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(clampedSlot);
        }

        private void OnWeaponChanged(int oldIndex, int newIndex)
        {
            UpdateWeaponVisibility(newIndex);
            WeaponIndexChanged?.Invoke(CurrentWeaponIndex);
            ReportCurrentWeaponTelemetry();
        }

        private void UpdateWeaponVisibility(int index)
        {
            if (weapons == null) return;

            #region agent log
            int nullWeaponSlots = 0;
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i] == null)
                    nullWeaponSlots++;
            GameLog.Info(() => $"[WeaponManager][dbg] UpdateWeaponVisibility index={index} count={weapons.Count} nullSlots={nullWeaponSlots}");
            #region agent log
            GameLog.DebugSession("initial", "W1", "WeaponManager.cs:113", "weapon visibility update", $"{{\"index\":{index},\"count\":{weapons.Count},\"nullSlots\":{nullWeaponSlots}}}");
            #endregion
            #endregion

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                    continue;

                weapons[i].SetActive(i == index);
            }

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateWeaponUI();
        }

        public void AddWeapon(GameObject newWeapon)
        {
            if (weapons == null)
                weapons = new List<GameObject>();

            if (weapons.Count < maxWeaponSlots)
                weapons.Add(newWeapon);
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
