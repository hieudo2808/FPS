using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum PickupType { Ammo, Health, Weapon }

    public class PickupItem : NetworkBehaviour, IInteractable
    {
        [Header("Pickup Settings")]
        [SerializeField] private PickupType pickupType = PickupType.Ammo;
        [SerializeField] private int ammoAmount = 30;
        [SerializeField] private float healthAmount = 25f;
        [SerializeField] private PrimaryWeaponId primaryWeaponId = PrimaryWeaponId.Vandal;
        [SerializeField] private string displayName = "Ammo Box";

        [Header("After Pickup")]
        [SerializeField] private float despawnDelay = 0f;

        private bool canInteract = true;
        public bool CanInteract => canInteract;
        public PickupType Type => pickupType;
        public PrimaryWeaponId PrimaryWeapon => primaryWeaponId;

        public string GetInteractText()
        {
            string interactKey = FormatKeyName(InputManager.Instance != null
                ? InputManager.Instance.GetKeyForAction("Interact")
                : KeyCode.F);

            return pickupType switch
            {
                PickupType.Ammo   => $"[{interactKey}] Pick up {displayName} (+{ammoAmount} ammo)",
                PickupType.Health => $"[{interactKey}] Pick up {displayName} (+{healthAmount} HP)",
                PickupType.Weapon => $"[{interactKey}] Pick up {displayName} ({primaryWeaponId})",
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
            if (!ApplyPickup(interactorObject))
            {
                canInteract = true;
                return PickupResultCode.InventoryFull;
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

            if (pickupType == PickupType.Weapon)
            {
                WeaponManager manager = player.GetComponent<WeaponManager>();
                return manager != null
                    && manager.ActivePrimaryWeaponId != primaryWeaponId
                    && manager.TryGetPrimaryCandidate(primaryWeaponId, out GameObject candidate)
                    && candidate != null
                    && candidate.GetComponent<Weapon>()?.Data != null;
            }

            WeaponFireHandler fireHandler = player.GetComponent<WeaponFireHandler>();
            return fireHandler != null && fireHandler.CanReceiveAmmoServer();
        }

        private bool ApplyPickup(NetworkObject player)
        {
            switch (pickupType)
            {
                case PickupType.Ammo:
                    return GiveAmmo(player);
                case PickupType.Health:
                    return GiveHealth(player);
                case PickupType.Weapon:
                    return GiveWeapon(player);
                default:
                    return false;
            }
        }

        private bool GiveAmmo(NetworkObject player)
        {
            WeaponManager weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager == null) return false;

            return weaponManager.GetComponent<WeaponFireHandler>()?.AddReserveAmmoServer(ammoAmount) == true;
        }

        private bool GiveHealth(NetworkObject player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead || health.CurrentHealth >= health.MaxHealth)
                return false;

            health.Heal(healthAmount);
            return true;
        }

        private bool GiveWeapon(NetworkObject player)
        {
            WeaponManager manager = player.GetComponent<WeaponManager>();
            return manager != null
                && manager.ActivePrimaryWeaponId != primaryWeaponId
                && manager.TryReplacePrimaryWeaponServer(primaryWeaponId);
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
