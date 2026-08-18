using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace FPS
{
    public class WeaponInputHandler : NetworkBehaviour
    {
        private WeaponManager _weaponManager;
        private WeaponManager weaponManager => _weaponManager != null ? _weaponManager : (_weaponManager = GetComponent<WeaponManager>());

        private void Update()
        {
            if (!IsOwner || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HandlePrimaryWeaponDebugSelection();
#endif

            if (InputManager.Instance == null || weaponManager == null || !weaponManager.IsSpawned)
                return;

            if (InputManager.Instance.GetWeapon1InputDown())
            {
                weaponManager.RequestEquipWeaponServerRpc(0);
            }
            else if (InputManager.Instance.GetWeapon2InputDown())
            {
                weaponManager.RequestEquipWeaponServerRpc(1);
            }
            else if (InputManager.Instance.GetInspectInputDown())
            {
                weaponManager.TryInspectCurrentWeapon();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandlePrimaryWeaponDebugSelection()
        {
            // Inventory mutations remain server-authoritative. This shortcut is
            // intentionally Host-only and is compiled out of release builds.
            if (!IsServer || weaponManager == null || Keyboard.current == null)
                return;

            PrimaryWeaponId? selectedWeapon = null;
            if (Keyboard.current.f6Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Vandal;
            else if (Keyboard.current.f7Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Operator;
            else if (Keyboard.current.f8Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Odin;
            else if (Keyboard.current.f9Key.wasPressedThisFrame)
                selectedWeapon = PrimaryWeaponId.Bucky;

            if (!selectedWeapon.HasValue)
                return;

            PrimaryWeaponId weaponId = selectedWeapon.Value;
            if (!weaponManager.TryReplacePrimaryWeaponServer(weaponId))
            {
                GameLog.Warning(() => $"[WeaponDebug] {weaponId} is not configured as a primary candidate.");
                return;
            }

            if (weaponManager.CurrentWeaponIndex != 0)
                weaponManager.SetEquippedWeaponServer(0);

            GameLog.Info(() => $"[WeaponDebug] Equipped primary {weaponId}.");
        }
#endif
    }
}
