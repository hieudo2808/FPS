using UnityEngine;

namespace FPS
{
    public class EnemyHealth : MonoBehaviour
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

        public void SetMaxHealth(float newMaxHealth)
        {
            maxHealth = newMaxHealth;
            currentHealth = maxHealth;
        }

        public void TakeDamage(float damage)
        {
            if (isDead) return;

            currentHealth -= damage;

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (isDead) return;
            
            isDead = true;
            currentHealth = 0;

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

            if (usePooling && ZombiePoolManager.Instance != null)
            {
                Invoke(nameof(ReturnToPool), destroyDelay);
            }
            else
            {
                Destroy(gameObject, destroyDelay);
            }
        }

        private void ReturnToPool()
        {
            ZombiePoolManager.Instance?.ReturnZombie(gameObject);
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
    }
}
