using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FactoryObjectiveInteractable : MonoBehaviour, INetworkInteractable
    {
        [SerializeField] private FactoryObjectiveId objectiveId;
        [SerializeField] private string prompt = "Operate";
        [SerializeField] private Transform interactionPoint;
        [SerializeField, Min(1f)] private float maximumInteractionDistance = 4f;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask lineOfSightMask = Physics.DefaultRaycastLayers;

        private FactoryMissionController controller;

        public FactoryObjectiveId ObjectiveId => objectiveId;
        public bool CanInteract => ResolveController() != null && controller.CanInteract(objectiveId);

        private void Awake()
        {
            ResolveController();
        }

        public string GetInteractText()
        {
            return $"[F] {prompt}";
        }

        public void Interact(NetworkObject interactorObject)
        {
            RequestNetworkInteraction(interactorObject);
        }

        public void RequestNetworkInteraction(NetworkObject interactorObject)
        {
            ResolveController()?.RequestInteraction(objectiveId, interactorObject);
        }

        public FactoryInteractionResult ValidateServerInteraction(NetworkObject interactorObject)
        {
            FactoryMissionController mission = ResolveController();
            if (mission == null || !mission.CanInteract(objectiveId))
                return FactoryInteractionResult.PrerequisiteMissing;
            if (interactorObject == null)
                return FactoryInteractionResult.InvalidInteractor;

            Vector3 target = interactionPoint != null ? interactionPoint.position : transform.position;
            Vector3 origin = interactorObject.transform.position + Vector3.up * 1.35f;
            float allowedDistance = Mathf.Max(1f, maximumInteractionDistance);
            if ((target - origin).sqrMagnitude > allowedDistance * allowedDistance)
                return FactoryInteractionResult.OutOfRange;

            if (!requireLineOfSight)
                return FactoryInteractionResult.Accepted;

            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
                return FactoryInteractionResult.Accepted;

            if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance,
                    lineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                return FactoryInteractionResult.Accepted;
            }

            return hit.transform == transform || hit.transform.IsChildOf(transform)
                ? FactoryInteractionResult.Accepted
                : FactoryInteractionResult.LineOfSightBlocked;
        }

        public void Configure(
            FactoryObjectiveId id,
            string interactionPrompt,
            float interactionDistance = 4f,
            bool needsLineOfSight = true)
        {
            objectiveId = id;
            prompt = interactionPrompt;
            maximumInteractionDistance = Mathf.Max(1f, interactionDistance);
            requireLineOfSight = needsLineOfSight;
        }

        private FactoryMissionController ResolveController()
        {
            if (controller == null)
                controller = GetComponentInParent<FactoryMissionController>() ?? FactoryMissionController.Instance;
            return controller;
        }
    }
}
