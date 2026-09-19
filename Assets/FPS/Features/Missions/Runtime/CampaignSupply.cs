using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public sealed class CampaignSupply : MonoBehaviour, INetworkInteractable
    {
        public string supplyId;
        public CampaignChapter chapter;
        public bool medicineOnly;
        public bool chooseReward;
        [Range(1,4)] public int minimumPartySize = 1;
        public int ammo = 30;
        public PickupType survivalReward = PickupType.Ammo;
        private Renderer[] visuals;
        private Collider[] colliders;
        private bool hidden;
        public bool IsAvailable => CampaignMissionController.Instance != null && CampaignMissionController.Instance.State.supplyPartySize >= minimumPartySize;
        public bool CanInteract => IsAvailable && CampaignMissionController.Instance.State.chapter == chapter
            && !CampaignMissionController.Instance.IsSupplyClaimed(supplyId);
        public bool HasCapacity(SurvivalInventory inventory) => chooseReward
            || SurvivalRules.Capacity(medicineOnly ? PickupType.Medkit : survivalReward) == 0
            || inventory != null && inventory.CanAdd(medicineOnly ? PickupType.Medkit : survivalReward, 1);
        public string GetInteractText()
        {
            string item = medicineOnly ? "MEDKIT +1" : survivalReward switch
            {
                PickupType.FragGrenade => "FRAG GRENADE +1",
                PickupType.IncendiaryGrenade => "INCENDIARY +1",
                PickupType.Medkit => "MEDKIT +1",
                PickupType.Antidote => "ANTIDOTE +1",
                _ => "Tiếp tế hữu hạn"
            };
            return $"[{SurvivalHotbar.Key("Interact", "F")}] {item}";
        }
        public void Interact(NetworkObject actor) => RequestNetworkInteraction(actor);
        public void RequestNetworkInteraction(NetworkObject actor)
        {
            if (chooseReward) CampaignHUD.Instance?.OpenSupply(this);
            else CampaignMissionController.Instance?.RequestSupply(supplyId, medicineOnly);
        }
        public bool InReach(PlayerHealth p, float range)
        {
            if (p == null || !p.CanUseCombat) return false;
            Vector3 eye = p.transform.position + Vector3.up * 1.55f;
            if (Vector3.Distance(eye, transform.position) > range) return false;
            return !Physics.Linecast(eye, transform.position, out var hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        private void Awake() { visuals = GetComponentsInChildren<Renderer>(); colliders = GetComponentsInChildren<Collider>(); }
        private void Update()
        {
            bool value = !IsAvailable || CampaignMissionController.Instance.IsSupplyClaimed(supplyId);
            if (value == hidden) return;
            hidden = value;
            GetComponent<SurvivalPickupPresentation>()?.SetAvailable(!hidden);
            foreach (var r in visuals) r.enabled = !hidden;
            foreach (var c in colliders) c.enabled = !hidden;
        }
    }
}
