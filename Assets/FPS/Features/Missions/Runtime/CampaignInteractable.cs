using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class CampaignInteractable : MonoBehaviour, INetworkInteractable
    {
        public CampaignObjectiveId objectiveId;
        public CampaignFileId fileId;
        public bool documentOnly;
        [Tooltip("Only this child visual is hidden after pickup. Never assign the console/door owner.")]
        public GameObject pickupVisual;
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
        private CampaignObjectiveDefinition Definition => CampaignMissionController.Instance?.ContentCatalog?.FindObjective(objectiveId);
        public CampaignFileDefinition LinkedFile
        {
            get
            {
                var catalog = CampaignMissionController.Instance?.ContentCatalog;
                var file = catalog?.FindFile(Definition?.file ?? CampaignFileId.None);
                return file != null && file.required ? file : null;
            }
        }

        public bool ProvidesFile(CampaignFileDefinition file, CampaignContentCatalog catalog)
        {
            if (file == null || file.id == CampaignFileId.None) return false;
            return documentOnly ? fileId == file.id
                : file.required && catalog?.FindObjective(objectiveId)?.file == file.id;
        }
        public string Title => documentOnly ? CampaignMissionController.Instance?.ContentCatalog?.FindFile(fileId)?.title ?? displayName
            : Definition?.title ?? displayName;
        public string Instructions => Definition?.instructions ?? document;
        public string Hint => Definition?.hint ?? hint;
        public string[] Choices => Definition?.choices ?? choices;
        public string[] SecondaryChoices => Definition?.secondaryChoices ?? secondaryChoices;
        public bool CanInteract => CampaignMissionController.Instance != null
            && CampaignMissionController.Instance.State.phase == CampaignPhase.Exploring
            && (documentOnly
                ? !CampaignMissionController.Instance.State.HasFile(fileId)
                    && CampaignMissionController.Instance.ContentCatalog?.FindFile(fileId)?.chapter == CampaignMissionController.Instance.State.chapter
                : CampaignMissionController.Instance.State.chapter == CampaignRules.ChapterOf(objectiveId));
        public Vector3 Point => interactionPoint != null ? interactionPoint.position : transform.position;
        public string GetInteractText() => documentOnly
            ? $"[{InputManager.Instance?.GetKeyForAction("Interact") ?? KeyCode.F}] READ {Title}"
            : $"[{InputManager.Instance?.GetKeyForAction("Interact") ?? KeyCode.F}] {Title}";
        public void Interact(NetworkObject interactorObject) => RequestNetworkInteraction(interactorObject);
        public void RequestNetworkInteraction(NetworkObject interactorObject)
        {
            if (documentOnly)
            {
                CampaignMissionController.Instance?.RequestDiscoverFile(fileId);
                return;
            }
            CampaignHUD.Instance?.Open(this);
        }

        public bool InReach(PlayerHealth actor, float range)
        {
            if (actor == null || !actor.CanUseCombat) return false;
            Vector3 eye = actor.transform.position + Vector3.up * 1.55f;
            if (Vector3.Distance(eye, Point) > range) return false;
            if (!Physics.Linecast(eye, Point, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return OwnsCollider(hit.collider);
        }
        public void SetRecorded(bool recorded)
        {
            if (!documentOnly) return;
            if (pickupVisual != null && pickupVisual != gameObject && pickupVisual.transform.IsChildOf(transform))
                pickupVisual.SetActive(!recorded);
            if (interactionBody != null) interactionBody.enabled = !recorded;
        }
        public bool OwnsCollider(Collider collider) => collider == interactionBody || collider.transform == transform
            || collider.transform.IsChildOf(transform) || transform.IsChildOf(collider.transform);
    }
}
