using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerHealth : NetworkBehaviour
    {
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

        /// <summary>
        /// Ai cũng có thể gọi (enemy, traps, etc.) — chạy trên server
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void TakeDamageServerRpc(float damage)
        {
            if (networkIsDead.Value) return;

            networkHealth.Value -= damage;
            networkHealth.Value = Mathf.Max(0, networkHealth.Value);

            Debug.Log($"Player took {damage} damage. HP: {networkHealth.Value}/{maxHealth}");

            if (networkHealth.Value <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Backward-compatible local call — routes to ServerRpc
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (IsServer)
            {
                // Server can apply directly
                TakeDamageServerRpc(damage);
            }
            else
            {
                TakeDamageServerRpc(damage);
            }
        }

        private void Die()
        {
            if (networkIsDead.Value) return;

            networkIsDead.Value = true;
            Debug.Log($"Player {OwnerClientId} died!");

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
    }
}
