using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class CampaignInteractable : MonoBehaviour, INetworkInteractable
    {
        public CampaignObjectiveId objectiveId;
        public string displayName;
        [TextArea(3, 12)] public string document;
        public string[] choices;
        public string[] secondaryChoices;
        [Min(0)] public float holdSeconds = 2;
        public Transform interactionPoint;
        public Collider interactionBody;
        public bool showPanel;
        [TextArea] public string hint;
        public CampaignObjectiveId ObjectiveId => objectiveId;
        public bool CanInteract => CampaignMissionController.Instance != null
            && CampaignMissionController.Instance.State.chapter == CampaignRules.ChapterOf(objectiveId)
            && CampaignMissionController.Instance.State.phase == CampaignPhase.Exploring;
        public Vector3 Point => interactionPoint != null ? interactionPoint.position : transform.position;
        public string GetInteractText() => $"[{InputManager.Instance?.GetKeyForAction("Interact") ?? KeyCode.F}] {displayName}";
        public void Interact(NetworkObject interactorObject) => RequestNetworkInteraction(interactorObject);
        public void RequestNetworkInteraction(NetworkObject interactorObject) => CampaignHUD.Instance?.Open(this);

        public bool InReach(PlayerHealth actor, float range)
        {
            if (actor == null || !actor.CanUseCombat) return false;
            Vector3 eye = actor.transform.position + Vector3.up * 1.55f;
            if (Vector3.Distance(eye, Point) > range) return false;
            if (!Physics.Linecast(eye, Point, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return OwnsCollider(hit.collider);
        }
        public bool OwnsCollider(Collider collider) => collider == interactionBody || collider.transform == transform
            || collider.transform.IsChildOf(transform) || transform.IsChildOf(collider.transform);
    }
}
