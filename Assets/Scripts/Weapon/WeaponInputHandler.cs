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
            if (!IsOwner) return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (weaponManager != null)
                {
                    weaponManager.RequestSwitchWeaponServerRpc();
                }
            }
        }
    }
}
