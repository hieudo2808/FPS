using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace FPS
{
    public class InteractionManager : NetworkBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private bool requireLineOfSight = true;

        [Header("UI")]
        [Tooltip("TextMeshPro element in HUD showing '[F] Pick up Ammo' etc.")]
        [SerializeField] private TextMeshProUGUI interactPromptText;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = false;

        private IInteractable currentInteractable;
        private ItemOutline currentOutline;
        private NetworkObject currentNetworkObject;

        private Camera playerCamera;
        private uint nextRequestSequence;
        private int effectiveInteractableMask;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
                playerCamera = Camera.main;

            effectiveInteractableMask = interactableLayer.value != 0
                ? interactableLayer.value
                : Physics.DefaultRaycastLayers;

            SetPromptVisible(false);
        }

        private void Update()
        {
            ScanForInteractable();

            if (currentInteractable != null
                && currentInteractable.CanInteract
                && GetInteractInputDown())
            {
                TryInteract();
            }
        }

        private bool GetInteractInputDown()
        {
            return InputManager.Instance != null && InputManager.Instance.GetInteractInputDown();
        }

        private void ScanForInteractable()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (showDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.yellow);

            int mask = effectiveInteractableMask != 0
                ? effectiveInteractableMask
                : Physics.DefaultRaycastLayers;
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, mask))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null && interactable.CanInteract)
                {
                    if (interactable == currentInteractable) return;

                    DeselectCurrent();
                    SelectNew(interactable, hit.collider.transform);
                    return;
                }
            }

            DeselectCurrent();
        }

        private void SelectNew(IInteractable interactable, Transform hitTransform)
        {
            currentInteractable = interactable;
            currentNetworkObject = hitTransform.GetComponentInParent<NetworkObject>();

            currentOutline = hitTransform.GetComponentInParent<ItemOutline>();
            currentOutline?.ShowOutline();

            SetPromptText(interactable.GetInteractText());
            SetPromptVisible(true);
        }

        private void DeselectCurrent()
        {
            if (currentInteractable == null) return;

            currentOutline?.HideOutline();
            currentOutline        = null;
            currentInteractable   = null;
            currentNetworkObject  = null;

            SetPromptVisible(false);
        }

        private void TryInteract()
        {
            if (currentInteractable is INetworkInteractable networkInteractable)
            {
                networkInteractable.RequestNetworkInteraction(NetworkObject);
                DeselectCurrent();
                return;
            }

            if (currentNetworkObject == null)
            {
                currentInteractable.Interact(null);
                DeselectCurrent();
                return;
            }

            if (IsSpawned && NetworkManager != null && NetworkManager.IsListening)
            {
                InteractServerRpc(currentNetworkObject.NetworkObjectId, nextRequestSequence++);
            }
            DeselectCurrent();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void RequestVerificationPickup()
        {
            if (!IsOwner || !IsSpawned)
                return;

            PickupItem nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>())
            {
                if (pickup == null || !pickup.CanInteract)
                    continue;

                float distance = (pickup.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = pickup;
                    nearestDistance = distance;
                }
            }

            NetworkObject pickupObject = nearest != null
                ? nearest.GetComponentInParent<NetworkObject>()
                : null;
            PlayerHealth verificationPlayer = GetComponent<PlayerHealth>();
            NetworkDiagnostics.Emit(
                "pickup_transaction",
                NetworkGameManager.Instance != null ? NetworkGameManager.Instance.State : SessionState.InMatch,
                pickupObject != null ? $"verification_request:target={pickupObject.NetworkObjectId}" : "NoTargetAvailable",
                verificationPlayer != null ? verificationPlayer.StablePlayerId : default);
            if (pickupObject != null && IsSpawned && NetworkManager != null && NetworkManager.IsListening)
                InteractServerRpc(pickupObject.NetworkObjectId, nextRequestSequence++);
        }
#endif

        [ServerRpc]
        private void InteractServerRpc(
            ulong targetNetworkObjectId,
            uint requestSequence,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (senderClientId != OwnerClientId)
                return;

            PickupTransactionService transactions = NetworkGameManager.Instance?.PickupTransactions;
            PickupTransactionResult result = transactions != null
                ? transactions.Execute(
                    senderClientId,
                    requestSequence,
                    targetNetworkObjectId,
                    GetServerTime(),
                    () => ValidateAndApplyPickup(targetNetworkObjectId))
                : new PickupTransactionResult(
                    targetNetworkObjectId,
                    requestSequence,
                    PickupResultCode.ServerUnavailable);

            SendPickupResultClientRpc(result.TargetNetworkObjectId, result.RequestSequence, result.Code,
                CreateTargetRpcParams(senderClientId));

            PlayerHealth playerHealth = GetComponent<PlayerHealth>();
            NetworkDiagnostics.Emit(
                "pickup_transaction",
                NetworkGameManager.Instance != null ? NetworkGameManager.Instance.State : SessionState.InMatch,
                $"{result.Code}:target={result.TargetNetworkObjectId}:sequence={result.RequestSequence}",
                playerHealth != null ? playerHealth.StablePlayerId : default);
        }

        private PickupResultCode ValidateAndApplyPickup(ulong targetNetworkObjectId)
        {
            PlayerHealth playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null || playerHealth.IsDead || !playerHealth.IsInputReady)
                return PickupResultCode.InvalidPlayer;

            if (NetworkManager == null
                || NetworkManager.SpawnManager == null
                || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    targetNetworkObjectId, out NetworkObject targetNetObj))
            {
                return PickupResultCode.TargetMissing;
            }

            PickupItem pickup = targetNetObj.GetComponentInChildren<PickupItem>();
            if (pickup == null)
                return PickupResultCode.NotInteractable;
            if (!pickup.CanInteract)
                return PickupResultCode.AlreadyClaimed;

            Vector3 delta = targetNetObj.transform.position - transform.position;
            float allowedRange = Mathf.Max(0.1f, interactRange * 1.5f);
            if (delta.sqrMagnitude > allowedRange * allowedRange)
                return PickupResultCode.OutOfRange;

            if (requireLineOfSight && !HasLineOfSight(targetNetObj))
                return PickupResultCode.LineOfSightBlocked;

            return pickup.TryClaimServer(GetComponent<NetworkObject>());
        }

        private bool HasLineOfSight(NetworkObject target)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 destination = target.transform.position;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
                return true;

            if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            NetworkObject hitObject = hit.collider.GetComponentInParent<NetworkObject>();
            return hitObject == target || hitObject == NetworkObject;
        }

        private static ClientRpcParams CreateTargetRpcParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
        }

        private double GetServerTime()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
        }

        [ClientRpc]
        private void SendPickupResultClientRpc(
            ulong targetNetworkObjectId,
            uint requestSequence,
            PickupResultCode code,
            ClientRpcParams rpcParams = default)
        {
            if (code == PickupResultCode.Accepted)
                GameLog.Info(() => $"[Pickup] Accepted target={targetNetworkObjectId} seq={requestSequence}");
            else
                GameLog.Warning(() => $"[Pickup] Rejected target={targetNetworkObjectId} seq={requestSequence} reason={code}");
        }

        private void SetPromptText(string text)
        {
            if (interactPromptText != null)
                interactPromptText.text = text;
        }

        private void SetPromptVisible(bool visible)
        {
            if (interactPromptText != null)
                interactPromptText.gameObject.SetActive(visible);
        }
    }
}
