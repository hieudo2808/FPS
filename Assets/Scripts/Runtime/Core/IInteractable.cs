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
}