using UnityEngine;

namespace FPS
{
    public sealed partial class CampaignMissionController
    {
        private EnemyHealth campaignTank;
        private double nextTankAttempt;

        private void ReleaseTankTracking()
        {
            if (campaignTank != null) campaignTank.OnDeathServer -= OnCampaignTankKilled;
            campaignTank = null;
            nextTankAttempt = 0;
        }

        private void UpdateTankEncounter()
        {
            if (state.phase is CampaignPhase.Insertion or CampaignPhase.Transitioning or CampaignPhase.Completed or CampaignPhase.Failed) return;
            if (CampaignRules.ShouldArmTank(state))
            {
                state.tankStage = CampaignTankStage.Pending;
                Publish();
            }
            if (state.tankStage == CampaignTankStage.Active)
            {
                if (campaignTank != null && campaignTank.IsDead) OnCampaignTankKilled();
                else if (campaignTank == null || !campaignTank.gameObject.activeInHierarchy)
                {
                    // Pool cleanup/despawn is not a kill. Re-arm rather than unlock the Factory case.
                    ReleaseTankTracking();
                    state.tankStage = CampaignTankStage.Pending;
                    Publish();
                }
            }
            if (state.tankStage != CampaignTankStage.Pending || ActivePlayerCount == 0
                || state.phase is CampaignPhase.Insertion or CampaignPhase.Transitioning or CampaignPhase.Completed or CampaignPhase.Failed
                || Now < readingUntil || Now < nextTankAttempt) return;
            nextTankAttempt = Now + 1;
            var registry = SpecialInfectedRegistry.Instance;
            var spawns = DirectorSpawnService.Instance;
            if (registry == null || registry.HasLivingSpecial || spawns == null
                || !spawns.TryGetCampaignTankPosition(out Vector3 position)) return;
            var spawned = registry.TrySpawnCampaignTank(position);
            if (spawned == null) return;
            campaignTank = spawned.GetComponent<EnemyHealth>();
            campaignTank.OnDeathServer += OnCampaignTankKilled;
            state.tankStage = CampaignTankStage.Active;
            Publish();
        }

        private void OnCampaignTankKilled()
        {
            if (!IsServer || campaignTank == null || !campaignTank.IsDead || state.tankStage != CampaignTankStage.Active
                || state.phase is CampaignPhase.Failed or CampaignPhase.Completed) return;
            ReleaseTankTracking();
            state.tankStage = CampaignTankStage.Resolved;
            if (state.chapter == CampaignChapter.Factory) state.factoryTankDefeated = true;
            Publish();
        }
    }
}
