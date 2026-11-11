using UnityEngine;
using Unity.Netcode;

namespace FPS
{
    public sealed partial class CampaignMissionController
    {
        private EnemyHealth campaignTank;
        private System.Action tankDeathHandler;
        private double nextTankAttempt;
        private readonly CampaignTankAttempt tankAttempt = new();
        private readonly NetworkVariable<NetworkObjectReference> tankTarget = new();
        public bool TryGetTankTarget(out Vector3 position)
        {
            position = default;
            if (state.tankStage != CampaignTankStage.Active || !tankTarget.Value.TryGet(out var target)
                || target == null || !target.IsSpawned || !target.gameObject.activeInHierarchy) return false;
            var health = target.GetComponent<EnemyHealth>();
            if (health == null || health.IsDead) return false;
            position = target.transform.position;
            return true;
        }

        private void ReleaseTankTracking()
        {
            if (campaignTank != null && tankDeathHandler != null) campaignTank.OnDeathServer -= tankDeathHandler;
            campaignTank = null; tankDeathHandler = null;
            nextTankAttempt = 0; tankAttempt.Reset();
            if (IsServer && IsSpawned) tankTarget.Value = default;
        }

        private void UpdateTankEncounter()
        {
            if (state.phase is CampaignPhase.Insertion or CampaignPhase.Transitioning or CampaignPhase.Completed or CampaignPhase.Failed) return;
            if (CampaignRules.ShouldArmTank(state))
            {
                ReleaseTankTracking();
                state.tankStage = CampaignTankStage.Pending;
                state.tankSpawnRemaining = 0;
                Publish();
            }
            // Saves written before the escalation ladder may still hold the retired failure stage.
            if (state.tankStage == CampaignTankStage.SpawnFailed)
            {
                ReleaseTankTracking();
                state.tankStage = CampaignTankStage.Pending;
                Publish();
            }
            if (state.tankStage == CampaignTankStage.Active)
            {
                if (campaignTank != null && campaignTank.IsDead) OnCampaignTankKilled(campaignTank);
                else if (campaignTank == null || !campaignTank.gameObject.activeInHierarchy
                    || campaignTank.GetComponent<NetworkObject>()?.IsSpawned != true)
                {
                    // Pool cleanup/despawn is not a kill; retain branch progress and re-arm.
                    ReleaseTankTracking();
                    state.tankStage = CampaignTankStage.Pending;
                    state.tankSpawnRemaining = 0;
                    Publish();
                }
            }
            if (state.tankStage != CampaignTankStage.Pending) return;
            bool noActor = ActivePlayerCount == 0, reading = Now < readingUntil;
            bool protectedSpawn = AIDirector.Instance?.AdaptiveSpawningBlocked == true;
            tankAttempt.Tick(Now, noActor || reading);
            var registry = SpecialInfectedRegistry.Instance;
            var spawns = DirectorSpawnService.Instance;
            // A mandatory story beat waits politely for one timeout, then stops yielding to pacing rules.
            int tier = tankAttempt.Tier(settings.tankSpawnTimeoutSeconds);
            bool relaxed = tier >= 2;
            var reason = noActor ? CampaignTankWaitReason.NoActor : reading ? CampaignTankWaitReason.Reading
                : registry == null || spawns == null ? CampaignTankWaitReason.NoSpawnService
                : protectedSpawn && !relaxed ? CampaignTankWaitReason.Protection
                : registry.HasLivingSpecial && !relaxed ? CampaignTankWaitReason.SpecialBusy : CampaignTankWaitReason.None;
            if (noActor || reading) { PublishTankWait(reason); return; }
            if (Now < nextTankAttempt) return;
            nextTankAttempt = Now + 1;
            GameObject spawned = null;
            if (reason == CampaignTankWaitReason.None)
            {
                if (!spawns.TryGetCampaignTankPosition(out var position, tier == 1, relaxed))
                    reason = CampaignTankWaitReason.NoSafePosition;
                else
                {
                    spawned = registry.TrySpawnCampaignTank(position, relaxed);
                    if (spawned == null) reason = CampaignTankWaitReason.SpawnRejected;
                }
            }
            PublishTankWait(reason);
            if (spawned == null)
            {
                if (tier == 3)
                {
                    Debug.LogWarning($"[Campaign] Tank could not be staged in {state.chapter} ({reason}); encounter skipped so the squad can continue.");
                    CampaignRules.SkipUnstageableTank(state);
                    ReleaseTankTracking();
                    Publish();
                }
                return;
            }
            campaignTank = spawned.GetComponent<EnemyHealth>();
            var tracked = campaignTank;
            tankDeathHandler = () => OnCampaignTankKilled(tracked);
            campaignTank.OnDeathServer += tankDeathHandler;
            tankTarget.Value = new NetworkObjectReference(spawned.GetComponent<NetworkObject>());
            state.tankStage = CampaignTankStage.Active;
            state.tankWaitReason = CampaignTankWaitReason.None; state.tankSpawnRemaining = 0;
            Publish();
        }

        private void PublishTankWait(CampaignTankWaitReason reason)
        {
            // Diagnostics only; no countdown is replicated because players are never shown one.
            bool fallback = tankAttempt.Fallback(settings.tankSpawnTimeoutSeconds);
            bool changed = reason != state.tankWaitReason || fallback != state.tankFallback;
            state.tankWaitReason = reason; state.tankSpawnRemaining = 0; state.tankFallback = fallback;
            if (changed) Publish();
        }

        private void OnCampaignTankKilled(EnemyHealth source)
        {
            if (!CampaignRules.ConfirmTankDefeat(state, IsServer, source != null && source == campaignTank, source != null && source.IsDead)) return;
            ReleaseTankTracking();
            Publish();
        }
    }
}
