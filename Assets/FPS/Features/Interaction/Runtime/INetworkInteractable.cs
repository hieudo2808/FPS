using Unity.Netcode;

namespace FPS
{
    /// <summary>
    /// Client-facing bridge for interactions whose result must be decided by the server.
    /// Implementations must treat this call as a request, never as local authority.
    /// </summary>
    public interface INetworkInteractable : IInteractable
    {
        FactoryObjectiveId ObjectiveId { get; }
        void RequestNetworkInteraction(NetworkObject interactorObject);
    }
}
