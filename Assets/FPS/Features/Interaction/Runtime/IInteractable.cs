using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public interface IInteractable
    {
        string GetInteractText();
        void Interact(NetworkObject interactorObject);
        bool CanInteract { get; }
    }

    /// <summary>Optional inspection surface for authored objects visible before prerequisites are met.</summary>
    public interface ILockedInteractable
    {
        bool CanInspect { get; }
        string GetLockedText();
        void TrackLockedObjective();
    }
}
