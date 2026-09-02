using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class OutOfBoundsRecoveryVolume : MonoBehaviour
    {
        [SerializeField] private WorldRecoveryService recoveryService;
        [SerializeField] private MapRecoveryPoint preferredRecoveryPoint;

        private void Reset()
        {
            BoxCollider volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            recoveryService?.TryRecover(other, preferredRecoveryPoint);
        }

        public void Configure(
            WorldRecoveryService service,
            MapRecoveryPoint preferredPoint = null)
        {
            recoveryService = service;
            preferredRecoveryPoint = preferredPoint;
            GetComponent<BoxCollider>().isTrigger = true;
        }
    }
}
