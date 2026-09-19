using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>Routes a hit on existing scenery to its separately authored objective marker.</summary>
    [DisallowMultipleComponent]
    public sealed class CampaignInteractionProxy : MonoBehaviour, INetworkInteractable
    {
        public CampaignInteractable target;
        public bool CanInteract => target != null && target.CanInteract;
        public string GetInteractText() => target != null ? target.GetInteractText() : "";
        public void Interact(NetworkObject actor) => RequestNetworkInteraction(actor);
        public void RequestNetworkInteraction(NetworkObject actor)
        {
            if (CanInteract) target.RequestNetworkInteraction(actor);
        }
    }
}
