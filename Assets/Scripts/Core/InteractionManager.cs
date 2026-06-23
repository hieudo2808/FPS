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
        [SerializeField] private KeyCode interactKey = KeyCode.F;

        [Header("UI")]
        [Tooltip("TextMeshPro element in HUD showing '[F] Pick up Ammo' etc.")]
        [SerializeField] private TextMeshProUGUI interactPromptText;

        [Header("Debug")]
        [SerializeField] private bool showDebugRay = false;

        private IInteractable currentInteractable;
        private ItemOutline currentOutline;
        private NetworkObject currentNetworkObject;

        private Camera playerCamera;

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

            SetPromptVisible(false);
        }

        private void Update()
        {
            ScanForInteractable();

            if (currentInteractable != null
                && currentInteractable.CanInteract
                && Input.GetKeyDown(interactKey))
            {
                TryInteract();
            }
        }

        private void ScanForInteractable()
        {
            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (showDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.yellow);

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactableLayer))
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
            if (currentNetworkObject == null)
            {
                currentInteractable.Interact(null);
                DeselectCurrent();
                return;
            }

            InteractServerRpc(currentNetworkObject.NetworkObjectId);
            DeselectCurrent();
        }

        [ServerRpc]
        private void InteractServerRpc(ulong targetNetworkObjectId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetNetObj))
            {
                Debug.LogWarning($"[InteractionManager] Object {targetNetworkObjectId} no longer exists.");
                return;
            }

            IInteractable interactable = targetNetObj.GetComponentInChildren<IInteractable>();
            if (interactable == null || !interactable.CanInteract)
            {
                Debug.LogWarning($"[InteractionManager] Object {targetNetworkObjectId} is no longer interactable.");
                return;
            }

            float distance = Vector3.Distance(transform.position, targetNetObj.transform.position);
            if (distance > interactRange * 1.5f)
            {
                Debug.LogWarning($"[InteractionManager] Interact out of range: {distance:F1}m");
                return;
            }

            interactable.Interact(GetComponent<NetworkObject>());
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