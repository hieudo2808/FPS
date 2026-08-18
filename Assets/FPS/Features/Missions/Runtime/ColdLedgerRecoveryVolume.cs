using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ColdLedgerRecoveryVolume : MonoBehaviour
    {
        [SerializeField] private Transform[] recoveryPoints;

        private void OnTriggerEnter(Collider other)
        {
            NetworkObject playerObject = other.GetComponentInParent<NetworkObject>();
            if (playerObject == null || playerObject.GetComponent<PlayerMovement>() == null)
                return;

            if (NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening
                && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            Transform destination = FindNearestRecoveryPoint(playerObject.transform.position);
            if (destination == null)
                return;

            CharacterController characterController = playerObject.GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;

            playerObject.transform.SetPositionAndRotation(destination.position, destination.rotation);

            Rigidbody body = playerObject.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (characterController != null)
                characterController.enabled = true;
        }

        public void Configure(Transform[] points)
        {
            recoveryPoints = points;
        }

        private Transform FindNearestRecoveryPoint(Vector3 position)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            if (recoveryPoints == null)
                return null;

            for (int i = 0; i < recoveryPoints.Length; i++)
            {
                Transform candidate = recoveryPoints[i];
                if (candidate == null)
                    continue;

                float distance = (candidate.position - position).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }
    }
}
