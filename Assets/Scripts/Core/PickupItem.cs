using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum PickupType { Ammo, Health }

    public class PickupItem : NetworkBehaviour, IInteractable
    {
        [Header("Pickup Settings")]
        [SerializeField] private PickupType pickupType = PickupType.Ammo;
        [SerializeField] private int ammoAmount = 30;
        [SerializeField] private float healthAmount = 25f;
        [SerializeField] private string displayName = "Ammo Box";

        [Header("After Pickup")]
        [SerializeField] private float despawnDelay = 0f;

        private bool canInteract = true;
        public bool CanInteract => canInteract;

        public string GetInteractText()
        {
            string interactKey = FormatKeyName(InputManager.Instance != null
                ? InputManager.Instance.GetKeyForAction("Interact")
                : KeyCode.F);

            return pickupType switch
            {
                PickupType.Ammo   => $"[{interactKey}] Pick up {displayName} (+{ammoAmount} ammo)",
                PickupType.Health => $"[{interactKey}] Pick up {displayName} (+{healthAmount} HP)",
                _                 => $"[{interactKey}] Pick up {displayName}"
            };
        }

        private static string FormatKeyName(KeyCode key)
        {
            return key switch
            {
                KeyCode.Mouse0 => "Left Mouse",
                KeyCode.Mouse1 => "Right Mouse",
                KeyCode.Mouse2 => "Middle Mouse",
                KeyCode.Alpha0 => "0",
                KeyCode.Alpha1 => "1",
                KeyCode.Alpha2 => "2",
                KeyCode.Alpha3 => "3",
                KeyCode.Alpha4 => "4",
                KeyCode.Alpha5 => "5",
                KeyCode.Alpha6 => "6",
                KeyCode.Alpha7 => "7",
                KeyCode.Alpha8 => "8",
                KeyCode.Alpha9 => "9",
                _ => key.ToString().Replace("Keypad", "Numpad ")
            };
        }

        public void Interact(NetworkObject interactorObject)
        {
            if (!IsServer) return;
            if (!canInteract) return;

            if (interactorObject == null) return;

            canInteract = false;

            switch (pickupType)
            {
                case PickupType.Ammo:
                    GiveAmmo(interactorObject);
                    break;

                case PickupType.Health:
                    GiveHealth(interactorObject);
                    break;
            }

            ulong clientId = interactorObject.OwnerClientId;
            NotifyPickupClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });

            if (despawnDelay <= 0f)
                DespawnItem();
            else
                Invoke(nameof(DespawnItem), despawnDelay);
        }

        private void GiveAmmo(NetworkObject player)
        {
            WeaponManager weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager == null) return;

            Weapon weapon = weaponManager.CurrentWeapon?.GetComponent<Weapon>();
            if (weapon != null)
            {
                weaponManager.GetComponent<WeaponFireHandler>()?.AddReserveAmmoServer(ammoAmount);

                AddAmmoClientRpc(ammoAmount, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { player.OwnerClientId }
                    }
                });
            }
        }

        private void GiveHealth(NetworkObject player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
                health.Heal(healthAmount);
        }

        [ClientRpc]
        private void AddAmmoClientRpc(int amount, ClientRpcParams rpcParams = default)
        {
            if (WeaponManager.LocalInstance == null) return;

            WeaponManager.LocalInstance.AddAmmoToCurrentWeaponLocalOnly(amount);

            if (HUDManager.HasInstance)
                HUDManager.Instance.UpdateAmmoInfo();
        }

        [ClientRpc]
        private void NotifyPickupClientRpc(ClientRpcParams rpcParams = default)
        {
            Debug.Log($"[Pickup] Picked up {displayName}!");

            // Example: AudioManager.Instance?.PlaySFXSound(pickupSound);
        }

        private void DespawnItem()
        {
            if (!IsServer) return;

            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
        }
    }
}
