using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class CampaignInteractable : MonoBehaviour, INetworkInteractable, ILockedInteractable
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
        private Light presentationLight;
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
                : CampaignMissionController.Instance.State.chapter == CampaignRules.ChapterOf(objectiveId)
                    && (CampaignRules.CanUse(CampaignMissionController.Instance.State, objectiveId) == CampaignResult.Accepted
                        || CampaignRules.CanReadComparisonSource(CampaignMissionController.Instance.State, objectiveId)));
        public bool CanInspect => documentOnly
            ? CampaignMissionController.Instance != null && !CampaignMissionController.Instance.State.HasFile(fileId)
            : CampaignObjectiveSelection.CanInspect(CampaignMissionController.Instance?.State, objectiveId)
                && CampaignRules.CanUse(CampaignMissionController.Instance?.State, objectiveId) != CampaignResult.AlreadyDone;
        public Vector3 TargetPosition => interactionBody != null ? interactionBody.bounds.center
            : interactionPoint != null ? interactionPoint.position : transform.position;
        public Vector3 Point => TargetPosition;
        public string GetInteractText() => documentOnly
            ? $"[{SurvivalHotbar.Key("Interact", "F")}] READ {PromptTitle()}"
            : CampaignRules.CanReadComparisonSource(CampaignMissionController.Instance?.State, objectiveId)
                ? $"[{SurvivalHotbar.Key("Interact", "F")}] READ {LinkedFile?.title ?? "SOURCE DOCUMENT"}"
            : $"[{SurvivalHotbar.Key("Interact", "F")}] {Title}";
        public string GetLockedText() => $"🔒 Cần: {CampaignRules.LockReason(CampaignMissionController.Instance?.State, objectiveId)}"
            + (CampaignMissionController.Instance?.State.chapter == CampaignChapter.Factory
                ? CampaignRules.CanSwitchFactoryBranch(CampaignMissionController.Instance.State) ? $"\n[{SurvivalHotbar.Key("Track", "T")}] đổi nhánh" : ""
                : $"\n[{SurvivalHotbar.Key("Track", "T")}] theo dõi");
        public void TrackLockedObjective() => CampaignHUD.Instance?.TryTrackObjective(objectiveId, true);
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
            if (Vector3.Distance(eye, TargetPosition) > range) return false;
            if (!Physics.Linecast(eye, TargetPosition, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
            return OwnsCollider(hit.collider);
        }

        private void Start() => EnsurePresentation();
        private void Update()
        {
            if (presentationLight == null) EnsurePresentation();
            UpdatePresentation();
        }

        private void EnsurePresentation()
        {
            if (objectiveId == CampaignObjectiveId.None || CampaignRules.ChapterOf(objectiveId) != CampaignChapter.Factory) return;
            GameObject visual = interactionBody != null ? interactionBody.gameObject : gameObject;
            var beacon = new GameObject("CampaignTargetLight", typeof(Light));
            beacon.transform.SetParent(visual.transform, false);
            presentationLight = beacon.GetComponent<Light>();
            presentationLight.type = LightType.Point;
            presentationLight.range = 3f;
            presentationLight.intensity = 1.4f;
            presentationLight.color = objectiveId == CampaignObjectiveId.FactoryManifest ? SelectedUiTheme.Current.Text : SelectedUiTheme.Current.Accent;
            presentationLight.enabled = false;
        }

        private void UpdatePresentation()
        {
            if (objectiveId == CampaignObjectiveId.None || CampaignRules.ChapterOf(objectiveId) != CampaignChapter.Factory) return;
            var mission = CampaignMissionController.Instance;
            bool complete = mission == null || mission.State.Has(objectiveId)
                || objectiveId == CampaignObjectiveId.FactoryManifest && mission.State.HasFile(CampaignFileId.FactoryManifest);
            var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            bool near = player != null && Vector3.Distance(player.transform.position, TargetPosition) <= 10f;
            bool current = CampaignHUD.Instance != null && CampaignHUD.Instance.TrackedObjectiveId == objectiveId;
            bool active = !complete && near && current;
            if (presentationLight != null) { presentationLight.transform.position = TargetPosition + Vector3.up * .35f; presentationLight.enabled = active; }
        }

        private string PromptTitle()
        {
            if (CampaignMissionController.Instance?.State.HasFile(fileId) == true) return Title;
            return objectiveId == CampaignObjectiveId.FactoryManifest ? "DELIVERY MANIFEST" : "SOURCE DOCUMENT";
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
