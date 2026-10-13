using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WorldRecoveryService : NetworkBehaviour
    {
        [SerializeField] private MapRecoveryPoint[] recoveryPoints;
        [SerializeField, Min(0.1f)] private float recoveryCooldownSeconds = 1f;

        private readonly Dictionary<ulong, double> nextAllowedRecoveryTime = new();
        private readonly Dictionary<ulong, Pose> campaignSafePoses = new();
        private double nextSafePoseSample;

        public IReadOnlyList<MapRecoveryPoint> RecoveryPoints => recoveryPoints;

        public void Configure(MapRecoveryPoint[] points, float cooldownSeconds = 1f)
        {
            if (!ReferenceEquals(recoveryPoints, points)) campaignSafePoses.Clear();
            recoveryPoints = points;
            recoveryCooldownSeconds = Mathf.Max(0.1f, cooldownSeconds);
        }

        private void Update()
        {
            var campaign = CampaignMissionController.Instance;
            if (!IsServer || campaign == null || campaign.BlocksInput || campaign.Now < nextSafePoseSample) return;
            nextSafePoseSample = campaign.Now + .25;
            foreach (var player in campaign.Players)
            {
                if (!player.CanUseCombat || player.CampaignTransferPending) continue;
                if (CampaignPlacement.TryFloor(campaign.Current, player.transform.position, out var floor)
                    && CampaignPlacement.Clear(floor, player.StablePlayerId.Value))
                    campaignSafePoses[player.NetworkObjectId] = new Pose(floor + Vector3.up * .12f, player.transform.rotation);
            }
        }

        public bool TryRecover(Collider other, MapRecoveryPoint preferredPoint = null)
        {
            if (other == null)
                return false;

            NetworkObject playerObject = other.GetComponentInParent<NetworkObject>();
            PlayerMovement movement = playerObject != null
                ? playerObject.GetComponent<PlayerMovement>()
                : other.GetComponentInParent<PlayerMovement>();
            if (movement == null)
                return false;

            bool networkActive = IsSpawned && NetworkManager != null && NetworkManager.IsListening;
            if (networkActive && !IsServer)
                return false;

            ulong key = playerObject != null
                ? playerObject.NetworkObjectId
                : unchecked((ulong)movement.GetEntityId().GetHashCode());
            double now = networkActive ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
            if (nextAllowedRecoveryTime.TryGetValue(key, out double allowedAt) && now < allowedAt)
                return false;

            var campaign = CampaignMissionController.Instance;
            if (campaign != null)
            {
                var health = movement.GetComponent<PlayerHealth>();
                if (!networkActive || health == null || health.IsDead || campaign.BlocksInput || health.CampaignTransferPending) return false;
                if (!TryCampaignRecovery(campaign.Current, health, out var pose)) return false;
                nextAllowedRecoveryTime[key] = now + recoveryCooldownSeconds;
                health.RelocateCampaign(pose.position, pose.rotation);
                return true;
            }

            MapRecoveryPoint destination = preferredPoint != null
                ? preferredPoint
                : FindNearestRecoveryPoint(movement.transform.position);
            if (destination == null)
                return false;

            nextAllowedRecoveryTime[key] = now + recoveryCooldownSeconds;
            ApplyRecovery(movement, destination.transform.position, destination.transform.rotation);

            if (networkActive && playerObject != null && playerObject.IsSpawned)
            {
                RecoverOwnerClientRpc(
                    new NetworkObjectReference(playerObject),
                    destination.transform.position,
                    destination.transform.rotation,
                    CreateTargetParams(playerObject.OwnerClientId));
            }

            return true;
        }

        private bool TryCampaignRecovery(CampaignChapterRoot chapter, PlayerHealth player, out Pose pose)
        {
            pose = default;
            if (chapter == null || recoveryPoints == null || chapter.arrivals == null) return false;
            if (campaignSafePoses.TryGetValue(player.NetworkObjectId, out var last)
                && CampaignPlacement.TryFloor(chapter, last.position, out var source))
            {
                MapRecoveryPoint best = null;
                Vector3 destination = default;
                float bestDistance = float.MaxValue;
                foreach (var point in recoveryPoints)
                {
                    if (point == null || !point.isActiveAndEnabled || Mathf.Abs(point.transform.position.y - source.y) > 1.5f
                        || !CampaignPlacement.TryFloor(chapter, point.transform.position, out var target)
                        || !CampaignPlacement.Clear(target, player.StablePlayerId.Value)
                        || !CampaignPlacement.CompletePath(source, target, out float pathDistance) || pathDistance >= bestDistance) continue;
                    best = point; destination = target; bestDistance = pathDistance;
                }
                if (best != null) { pose = new Pose(destination + Vector3.up * .12f, best.transform.rotation); return true; }
                if (CampaignPlacement.Clear(source, player.StablePlayerId.Value)) { pose = last; return true; }
            }
            // No tracked floor yet (for example immediately after reconnect). Use only a
            // validated arrival in this chapter, never nearest geometry in another stacked map.
            foreach (var arrival in chapter.arrivals)
                if (arrival != null && CampaignPlacement.TryFloor(chapter, arrival.position, out var at)
                    && CampaignPlacement.Clear(at, player.StablePlayerId.Value))
                { pose = new Pose(at + Vector3.up * .12f, arrival.rotation); return true; }
            return false;
        }

        private MapRecoveryPoint FindNearestRecoveryPoint(Vector3 position)
        {
            MapRecoveryPoint nearest = null;
            float nearestDistance = float.MaxValue;
            if (recoveryPoints == null)
                return null;

            for (int i = 0; i < recoveryPoints.Length; i++)
            {
                MapRecoveryPoint point = recoveryPoints[i];
                if (point == null || !point.isActiveAndEnabled)
                    continue;

                float distance = (point.transform.position - position).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearest = point;
                nearestDistance = distance;
            }

            return nearest;
        }

        private static void ApplyRecovery(
            PlayerMovement movement,
            Vector3 position,
            Quaternion rotation)
        {
            movement.TeleportForRespawn(position, rotation);
        }

        [ClientRpc]
        private void RecoverOwnerClientRpc(
            NetworkObjectReference playerReference,
            Vector3 position,
            Quaternion rotation,
            ClientRpcParams rpcParams = default)
        {
            if (!playerReference.TryGet(out NetworkObject playerObject) || playerObject == null)
                return;

            PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
            if (movement != null)
                ApplyRecovery(movement, position, rotation);
        }

        private static ClientRpcParams CreateTargetParams(ulong clientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };
        }
    }
}
