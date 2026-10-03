using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>Routes a hit on existing scenery to its separately authored objective marker.</summary>
    [DisallowMultipleComponent]
    public sealed class CampaignInteractionProxy : MonoBehaviour, INetworkInteractable
    {
        public CampaignInteractable target;
        [Tooltip("Optional next step on the same physical device, selected after target is completed.")]
        public CampaignInteractable nextTarget;
        public CampaignInteractable Resolve(CampaignState state) => target != null && nextTarget != null
            && !target.documentOnly && state != null && state.Has(target.objectiveId) ? nextTarget : target;
        private CampaignInteractable Current => Resolve(CampaignMissionController.Instance?.State);
        public bool CanInteract => Current != null && Current.CanInteract;
        public string GetInteractText() => Current != null ? Current.GetInteractText() : "";
        public void Interact(NetworkObject actor) => RequestNetworkInteraction(actor);
        public void RequestNetworkInteraction(NetworkObject actor)
        {
            var current = Current;
            if (current != null && current.CanInteract) current.RequestNetworkInteraction(actor);
        }
    }
}
