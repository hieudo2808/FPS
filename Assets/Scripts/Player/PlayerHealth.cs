using UnityEngine;

namespace FPS
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        public delegate void OnHealthChanged(float current, float max);
        public event OnHealthChanged HealthChangedEvent;

        public delegate void OnPlayerDeath();
        public event OnPlayerDeath PlayerDeathEvent;

        private bool isDead = false;

        private void Start()
        {
            currentHealth = maxHealth;
            HealthChangedEvent?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(float damage)
        {
            if (isDead) return;

            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);
            
            Debug.Log("Player took " + damage + " damage. HP: " + currentHealth + "/" + maxHealth);
            HealthChangedEvent?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (isDead) return;
            
            isDead = true;
            Debug.Log("Player died!");
            PlayerDeathEvent?.Invoke();

            // TODO: Xử lý game over, respawn, v.v.
        }

        public void Heal(float amount)
        {
            if (isDead) return;
            
            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            HealthChangedEvent?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            isDead = false;
            currentHealth = maxHealth;
            HealthChangedEvent?.Invoke(currentHealth, maxHealth);
        }
    }
}
