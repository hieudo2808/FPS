using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace FPS
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(NetworkTransform))]
    public sealed class SurvivalProjectile : NetworkBehaviour
    {
        [SerializeField] private SurvivalFireZone fireZonePrefab;
        [SerializeField] private GameObject incendiaryCore;
        private readonly NetworkVariable<ThrowableKind> kind = new();
        private Rigidbody body;
        private Vector3 launchVelocity;
        private NetworkObject thrower;
        private ulong attacker;
        private double deadline;
        private bool detonated;
        public void InitializeServer(ThrowableKind throwable, ulong owner, NetworkObject source, Vector3 velocity)
        { kind.Value = throwable; attacker = owner; thrower = source; launchVelocity = velocity; }
        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = !IsServer;
            if (incendiaryCore != null) incendiaryCore.SetActive(kind.Value == ThrowableKind.Incendiary);
            if (!IsServer) return;
            deadline = NetworkManager.ServerTime.Time + 2.2;
            if (thrower != null)
                foreach (var own in GetComponentsInChildren<Collider>())
                    foreach (var player in thrower.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(own, player);
            body.linearVelocity = launchVelocity;
            body.angularVelocity = new Vector3(5, 3, 7);
        }
        private void FixedUpdate()
        { if (IsSpawned && IsServer && !detonated && NetworkManager.ServerTime.Time >= deadline) Detonate(transform.position, Vector3.up); }
        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || !IsSpawned || detonated || collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);
            Detonate(contact.point + contact.normal * .12f, contact.normal);
        }
        private void Detonate(Vector3 point, Vector3 normal)
        {
            if (!IsServer || detonated) return;
            detonated = true;
            // Do not let the projectile collider occlude its own explosion.
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (kind.Value == ThrowableKind.Frag) SurvivalDamage.Apply(point, 4f, attacker, false);
            else SurvivalFireZone.CreateOrExtend(fireZonePrefab, point, attacker, NetworkManager);
            ExplosionClientRpc(point, normal, kind.Value);
            NetworkObject.Despawn();
        }
        [ClientRpc] private void ExplosionClientRpc(Vector3 point, Vector3 normal, ThrowableKind type) => SurvivalEffects.Explosion(point, normal, type);
    }
}
