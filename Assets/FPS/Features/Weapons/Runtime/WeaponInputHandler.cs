using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class WeaponInputHandler : NetworkBehaviour
    {
        private WeaponManager _weaponManager;
        private WeaponManager weaponManager => _weaponManager != null ? _weaponManager : (_weaponManager = GetComponent<WeaponManager>());
        private PlayerHealth playerHealth;
        private SurvivalInventory survivalInventory;

        private void Update()
        {
            if (!IsOwner || !IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;
            if (InputManager.Instance == null || weaponManager == null || !weaponManager.IsSpawned) return;
            playerHealth ??= GetComponent<PlayerHealth>();
            survivalInventory ??= GetComponent<SurvivalInventory>();

            if (survivalInventory != null)
            {
                if (InputManager.Instance.GetCycleGrenadeInputDown()) survivalInventory.CycleThrowable();
                if (InputManager.Instance.GetGrenadeInputDown())
                {
                    Camera camera = GetComponent<MouseMovement>()?.BodyCam ?? Camera.main;
                    survivalInventory.RequestThrow(camera != null ? camera.transform.forward : transform.forward);
                }
                else if (InputManager.Instance.GetMedkitInputDown()) survivalInventory.RequestUse(ConsumableKind.Medkit);
                else if (InputManager.Instance.GetAntidoteInputDown()) survivalInventory.RequestUse(ConsumableKind.Antidote);
            }
            if (playerHealth == null || !playerHealth.CanUseCombat || survivalInventory?.IsUsingItem == true) return;
            if (InputManager.Instance.GetWeapon1InputDown()) weaponManager.RequestEquipWeaponServerRpc(0);
            else if (InputManager.Instance.GetWeapon2InputDown()) weaponManager.RequestEquipWeaponServerRpc(1);
            else if (InputManager.Instance.GetInspectInputDown()) weaponManager.TryInspectCurrentWeapon();
        }
    }
}
