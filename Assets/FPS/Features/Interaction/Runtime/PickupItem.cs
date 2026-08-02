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
            TryClaimServer(interactorObject);
        }

        public PickupResultCode TryClaimServer(NetworkObject interactorObject)
        {
            if (!IsServer || interactorObject == null)
                return PickupResultCode.InvalidPlayer;
            if (!canInteract)
                return PickupResultCode.AlreadyClaimed;

            if (!CanApply(interactorObject))
                return PickupResultCode.InventoryFull;

            // Unity gameplay and RPC callbacks run on the main thread. Set this before mutating
            // inventory so a second request can never observe the item as available.
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

            PlayerHealth playerHealth = interactorObject.GetComponent<PlayerHealth>();
            NetworkGameManager.Instance?.Telemetry?.RecordPickup(
                playerHealth != null ? playerHealth.StablePlayerId : default,
                NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Tick : 0);

            if (despawnDelay <= 0f)
                DespawnItem();
            else
                Invoke(nameof(DespawnItem), despawnDelay);

            return PickupResultCode.Accepted;
        }

        private bool CanApply(NetworkObject player)
        {
            if (pickupType == PickupType.Health)
            {
                PlayerHealth health = player.GetComponent<PlayerHealth>();
                return health != null && !health.IsDead && health.CurrentHealth < health.MaxHealth;
            }

            WeaponFireHandler fireHandler = player.GetComponent<WeaponFireHandler>();
            return fireHandler != null && fireHandler.CanReceiveAmmoServer();
        }

        private void GiveAmmo(NetworkObject player)
        {
            WeaponManager weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager == null) return;

            weaponManager.GetComponent<WeaponFireHandler>()?.AddReserveAmmoServer(ammoAmount);
        }

        private void GiveHealth(NetworkObject player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
                health.Heal(healthAmount);
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
