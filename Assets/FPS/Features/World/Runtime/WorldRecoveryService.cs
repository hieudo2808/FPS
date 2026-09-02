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

        public IReadOnlyList<MapRecoveryPoint> RecoveryPoints => recoveryPoints;

        public void Configure(MapRecoveryPoint[] points, float cooldownSeconds = 1f)
        {
            recoveryPoints = points;
            recoveryCooldownSeconds = Mathf.Max(0.1f, cooldownSeconds);
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
