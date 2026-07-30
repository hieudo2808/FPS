using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        public static event System.Action<PlayerHealth, ulong> PlayerDiedServer;
        public static event System.Action<PlayerHealth> PlayerSpawnedServer;
        public static event System.Action<PlayerHealth> PlayerDespawnedServer;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        private NetworkVariable<float> networkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private NetworkVariable<bool> networkIsDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public float CurrentHealth => networkHealth.Value;
        public float MaxHealth => maxHealth;
        public bool IsDead => networkIsDead.Value;

        public delegate void OnHealthChanged(float current, float max);
        public event OnHealthChanged HealthChangedEvent;

        public delegate void OnPlayerDeath();
        public event OnPlayerDeath PlayerDeathEvent;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                networkHealth.Value = maxHealth;
                networkIsDead.Value = false;
                PlayerSpawnedServer?.Invoke(this);
            }

            // Subscribe to network variable changes for UI updates
            networkHealth.OnValueChanged += OnHealthValueChanged;
            networkIsDead.OnValueChanged += OnDeadValueChanged;

            // Initial UI update
            HealthChangedEvent?.Invoke(networkHealth.Value, maxHealth);
        }

        public override void OnNetworkDespawn()
        {
            networkHealth.OnValueChanged -= OnHealthValueChanged;
            networkIsDead.OnValueChanged -= OnDeadValueChanged;

            if (IsServer)
                PlayerDespawnedServer?.Invoke(this);
        }

        private void OnHealthValueChanged(float oldValue, float newValue)
        {
            HealthChangedEvent?.Invoke(newValue, maxHealth);
        }

        private void OnDeadValueChanged(bool oldValue, bool newValue)
        {
            if (newValue && !oldValue)
            {
                PlayerDeathEvent?.Invoke();
            }
        }

        public void TakeDamage(float damage)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;

            networkHealth.Value = Mathf.Max(0, networkHealth.Value - Mathf.Max(0f, damage));

            GameLog.Info(() => $"Player took {damage} damage. HP: {networkHealth.Value}/{maxHealth}");

            if (networkHealth.Value <= 0)
                Die();
        }

        private void Die()
        {
            if (networkIsDead.Value) return;

            networkIsDead.Value = true;
            GameLog.Info(() => $"Player {OwnerClientId} died!");
            PlayerDiedServer?.Invoke(this, OwnerClientId);

            // Notify all clients
            OnPlayerDiedClientRpc();
        }

        [ClientRpc]
        private void OnPlayerDiedClientRpc()
        {
            PlayerDeathEvent?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsServer) return;
            if (networkIsDead.Value) return;

            networkHealth.Value = Mathf.Min(networkHealth.Value + amount, maxHealth);
        }

        public void ResetHealth()
        {
            if (!IsServer) return;
            networkIsDead.Value = false;
            networkHealth.Value = maxHealth;
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) return;

            ApplyRespawnPose(position, rotation);
            networkIsDead.Value = false;
            networkHealth.Value = maxHealth;
            RespawnClientRpc(position, rotation);
        }

        [ClientRpc]
        private void RespawnClientRpc(Vector3 position, Quaternion rotation)
        {
            ApplyRespawnPose(position, rotation);
        }

        private void ApplyRespawnPose(Vector3 position, Quaternion rotation)
        {
            PlayerMovement movement = GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.TeleportForRespawn(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
