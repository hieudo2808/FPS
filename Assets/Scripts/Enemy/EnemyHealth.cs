using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class EnemyHealth : NetworkBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("Death Settings")]
        [SerializeField] private float destroyDelay = 3f;
        [SerializeField] private bool usePooling = true;

        private EnemyAI enemyAI;
        private bool isDead = false;
        private float originalMaxHealth;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead;

        private void Start()
        {
            originalMaxHealth = maxHealth;
            currentHealth = maxHealth;
            enemyAI = GetComponent<EnemyAI>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                currentHealth = maxHealth;
                isDead = false;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage)
        {
            TakeDamageInternal(damage);
        }

        public void TakeDamage(float damage)
        {
            if (IsServer)
            {
                TakeDamageInternal(damage);
            }
            else
            {
                TakeDamageServerRpc(damage);
            }
        }

        private void TakeDamageInternal(float damage)
        {
            if (isDead) return;

            currentHealth -= damage;
            currentHealth = Mathf.Max(0f, currentHealth);
            Debug.Log($"[EnemyHealth] {gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetMaxHealthServerRpc(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = maxHealth;
            Debug.Log($"[EnemyHealth] {gameObject.name} maxHealth scaled to {maxHealth}");
        }

        public void SetMaxHealth(float newMaxHealth)
        {
            if (IsServer)
            {
                maxHealth = newMaxHealth;
                currentHealth = maxHealth;
            }
            else
            {
                SetMaxHealthServerRpc(newMaxHealth);
            }
        }

        private void Die()
        {
            if (isDead) return;

            isDead = true;
            currentHealth = 0f;
            Debug.Log(gameObject.name + " died!");

            if (enemyAI != null)
            {
                enemyAI.OnDeath();
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                StartCoroutine(DespawnRoutine(netObj, destroyDelay));
            }
            else if (CanUseLocalPooling())
            {
                Invoke(nameof(ReturnToPool), destroyDelay);
            }
            else
            {
                Destroy(gameObject, destroyDelay);
            }
        }

        private IEnumerator DespawnRoutine(NetworkObject netObj, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                netObj.Despawn(true);
            }
        }

        private void ReturnToPool()
        {
            if (ZombiePoolManager.HasInstance)
            {
                ZombiePoolManager.Instance.ReturnZombie(gameObject);
            }
        }

        public void ResetHealth()
        {
            isDead = false;
            maxHealth = originalMaxHealth;
            currentHealth = maxHealth;
            CancelInvoke();
        }

        public void Heal(float amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        }

        private bool CanUseLocalPooling()
        {
            return usePooling && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) && ZombiePoolManager.HasInstance;
        }
    }
}
