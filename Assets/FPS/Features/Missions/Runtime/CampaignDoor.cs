using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public enum CampaignDoorRule { Manual, AfterInsertion, Utilities, Logistics, ColdStorage, FactoryCase, LabShortcut, LabArrival }

    [DisallowMultipleComponent]
    public sealed class CampaignDoor : MonoBehaviour, INetworkInteractable
    {
        public CampaignDoorRule rule;
        public Transform leaf;
        public Vector3 openOffset = new(0, 4, 0);
        public Vector3 closedLocalPosition;
        public BoxCollider safetyVolume;
        public NavMeshObstacle navigationBlocker;
        public float speed = 4;
        [Tooltip("Optional inspection target. Inspecting the door never unlocks it.")]
        public CampaignObjectiveId accessObjective;
        public string displayName;
        private bool requestedOpen;
        public bool IsOpen => leaf != null && Vector3.Distance(leaf.localPosition, closedLocalPosition + openOffset) < .05f;
        public bool IsClosed => leaf != null && Vector3.Distance(leaf.localPosition, closedLocalPosition) < .05f;

        public void SetOpen(bool value) => requestedOpen = value;
        public bool CanInteract => accessObjective != CampaignObjectiveId.None
            && CampaignMissionController.Instance != null && CampaignMissionController.Instance.IsSpawned
            && !CampaignMissionController.Instance.BlocksInput;
        public string GetInteractText() => $"[{InputManager.Instance?.GetKeyForAction("Interact") ?? KeyCode.F}] Kiểm tra {displayName}";
        public void Interact(Unity.Netcode.NetworkObject actor) => RequestNetworkInteraction(actor);
        public void RequestNetworkInteraction(Unity.Netcode.NetworkObject actor)
        {
            if (CanInteract) CampaignHUD.Instance?.OpenGate(this);
        }
        private void Update()
        {
            var campaign = CampaignMissionController.Instance;
            if (leaf == null || campaign == null || !campaign.IsSpawned) return;
            bool open = campaign.ResolvedDoorOpen(this);
            Vector3 target = closedLocalPosition + (open ? openOffset : Vector3.zero);
            leaf.localPosition = Vector3.MoveTowards(leaf.localPosition, target, speed * Time.deltaTime);
            if (navigationBlocker != null) navigationBlocker.enabled = !IsOpen;
        }
        public bool DesiredOpen(CampaignState s)
        {
            bool open = rule switch
            {
                CampaignDoorRule.AfterInsertion => s.phase != CampaignPhase.Insertion,
                CampaignDoorRule.Utilities => CampaignRules.UtilitiesReady(s),
                CampaignDoorRule.Logistics => CampaignRules.LogisticsReady(s),
                CampaignDoorRule.ColdStorage => CampaignRules.UtilitiesReady(s) && CampaignRules.LogisticsReady(s),
                CampaignDoorRule.FactoryCase => s.Has(CampaignObjectiveId.FactoryCase),
                CampaignDoorRule.LabShortcut => CampaignRules.LabSecured(s),
                CampaignDoorRule.LabArrival => s.chapter == CampaignChapter.Laboratory && s.phase is not CampaignPhase.Transitioning and not CampaignPhase.Completed,
                _ => requestedOpen
            };
            // Anti-crush may reopen a moving door, but must never unlock a closed
            // progression gate just because somebody approaches its safety sensor.
            // The authority replicates this result; clients do not decide safety.
            if (!open && !IsClosed && safetyVolume != null)
            {
                Bounds b = safetyVolume.bounds;
                foreach (Collider hit in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform == leaf || hit.transform.IsChildOf(leaf)) continue;
                    if (hit.GetComponentInParent<PlayerHealth>() != null || hit.attachedRigidbody != null) { open = true; break; }
                }
            }
            return open;
        }
    }
}
