using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponInputHandler : NetworkBehaviour
    {
        private WeaponManager _weaponManager;
        private WeaponManager weaponManager => _weaponManager != null ? _weaponManager : (_weaponManager = GetComponent<WeaponManager>());

        private void Update()
        {
            if (!IsOwner || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;

            if (InputManager.Instance != null && (InputManager.Instance.GetWeapon1InputDown() || InputManager.Instance.GetWeapon2InputDown()))
            {
                if (weaponManager != null && weaponManager.IsSpawned)
                {
                    weaponManager.RequestSwitchWeaponServerRpc();
                }
            }
        }
    }
}
