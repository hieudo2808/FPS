using UnityEngine;
using TMPro;

namespace FPS
{
    public class HealthBarUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI healthText;
        
        [Header("Colors")]
        [SerializeField] private Color healthyColor = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0f);
        [SerializeField] private Color criticalColor = new Color(0.9f, 0.2f, 0.2f);
        
        [Header("Thresholds")]
        [SerializeField] private float warningThreshold = 0.5f;
        [SerializeField] private float criticalThreshold = 0.25f;
        
        private PlayerHealth playerHealth;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.HealthChangedEvent += OnHealthChanged;
                    UpdateHealthText(playerHealth.CurrentHealth, playerHealth.MaxHealth);
                }
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            UpdateHealthText(current, max);
        }

        private void UpdateHealthText(float current, float max)
        {
            if (healthText == null) return;
            
            float healthPercent = current / max;
            healthText.text = $"HP: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            
            // Đổi màu theo HP
            if (healthPercent <= criticalThreshold)
                healthText.color = criticalColor;
            else if (healthPercent <= warningThreshold)
                healthText.color = warningColor;
            else
                healthText.color = healthyColor;
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChangedEvent -= OnHealthChanged;
            }
        }
    }
}
