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
            return pickupType switch
            {
                PickupType.Ammo   => $"[F] Pick up {displayName} (+{ammoAmount} ammo)",
                PickupType.Health => $"[F] Pick up {displayName} (+{healthAmount} HP)",
                _                 => $"[F] Pick up {displayName}"
            };
        }

        public void Interact(NetworkObject interactorObject)
        {
            if (!IsServer) return;
            if (!canInteract) return;

            canInteract = false;

            if (interactorObject == null) return;

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

            WeaponManager.LocalInstance.AddAmmoToCurrentWeapon(amount);

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