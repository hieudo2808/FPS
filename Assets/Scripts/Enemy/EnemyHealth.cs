using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class EnemyHealth : NetworkBehaviour, IPoolResettable, IAttributedDamageable
    {
        public void ResetForPool() => ResetHealth();
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Death Settings")]
        [SerializeField] private float destroyDelay = 3f;
        [SerializeField] private bool usePooling = true;

        // FIX 2: NetworkVariable để client biết HP hiện tại
        // Cần cho health bar UI, hit feedback, death visual, v.v.
        private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // FIX 5: isDead cũng sync để client trigger animation/ragdoll
        private NetworkVariable<bool> isDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private EnemyAI enemyAI;
        private float originalMaxHealth;
        private DamageInfo lastDamageInfo;

        // Properties
        public float CurrentHealth => currentHealth.Value;
        public float MaxHealth => maxHealth;
        public bool IsDead => isDead.Value;

        // Event để UI hoặc các script khác subscribe
        public event System.Action<float, float> OnHealthChanged; // (current, max)
        public event System.Action OnDied;
        public event System.Action OnDeathServer;

        // ==========================================
        // INITIALIZATION
        // ==========================================

        // FIX 3: Start() chỉ cache component, KHÔNG init state network
        // Tránh race condition với OnNetworkSpawn
        private void Start()
        {
            originalMaxHealth = maxHealth;
            enemyAI = GetComponent<EnemyAI>();
        }

        public override void OnNetworkSpawn()
        {
            // Subscribe để update UI/visual trên tất cả client
            currentHealth.OnValueChanged += HandleHealthChanged;
            isDead.OnValueChanged += HandleDeathChanged;

            // FIX 3: Chỉ server khởi tạo state
            if (IsServer)
            {
                currentHealth.Value = maxHealth;
                isDead.Value = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= HandleHealthChanged;
            isDead.OnValueChanged -= HandleDeathChanged;
        }

        // ==========================================
        // HEALTH CHANGE CALLBACKS (chạy trên tất cả client)
        // ==========================================

        private void HandleHealthChanged(float previous, float current)
        {
            OnHealthChanged?.Invoke(current, maxHealth);
        }

        private void HandleDeathChanged(bool previous, bool current)
        {
            if (current && !previous)
            {
                // Trigger visual death trên tất cả client
                OnDied?.Invoke();
                HandleDeathVisual();
            }
        }

        private void HandleDeathVisual()
        {
            // Disable collider trên tất cả client để không block raycast
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            // Trigger animation die, ragdoll, v.v. ở đây nếu cần
        }

        // ==========================================
        // DAMAGE: Only server-authoritative gameplay code may call TakeDamage.
        // WeaponFireHandler.RequestFireServerRpc validates the shot and looks up damage from WeaponData.
        // ==========================================

        // Gọi trực tiếp từ server-side code (EnemyAI, trap, explosion, v.v.)
        public void TakeDamage(float damage)
        {
            if (!IsServer) return; // Guard cứng — chỉ server
            TakeDamage(new DamageInfo(damage));
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!IsServer) return; // Guard cứng — chỉ server
            lastDamageInfo = damageInfo;
            TakeDamageInternal(damageInfo.amount);
        }

        private void TakeDamageInternal(float damage)
        {
            if (isDead.Value) return;

            currentHealth.Value = Mathf.Max(0f, currentHealth.Value - damage);
            Debug.Log($"[EnemyHealth] {gameObject.name} took {damage} damage. HP: {currentHealth.Value}/{maxHealth}");

            if (currentHealth.Value <= 0f)
                Die();
        }

        // ==========================================
        // SET MAX HEALTH — FIX 1: Chỉ server được scale HP
        // ==========================================

        public void SetMaxHealth(float newMaxHealth)
        {
            if (!IsServer) return; // Guard cứng

            maxHealth = newMaxHealth;
            currentHealth.Value = maxHealth;
            Debug.Log($"[EnemyHealth] {gameObject.name} maxHealth scaled to {maxHealth}");
        }

        // ==========================================
        // DEATH
        // ==========================================

        private void Die()
        {
            if (isDead.Value) return; // double-check

            isDead.Value = true; // sync đến tất cả client qua NetworkVariable
            currentHealth.Value = 0f;
            Debug.Log($"[EnemyHealth] {gameObject.name} died!");

            OnDeathServer?.Invoke();
            AIDirector.Instance?.OnZombieDied();
            PlayerProfiler.Instance?.ReportKill(lastDamageInfo, transform.position);

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

        // FIX 4: Check đầy đủ trước khi Despawn để tránh crash
        private IEnumerator DespawnRoutine(NetworkObject netObj, float delay)
        {
            yield return new WaitForSeconds(delay);

            // Object có thể đã bị destroy hoặc despawn từ nơi khác
            if (this == null || netObj == null) yield break;
            if (!netObj.IsSpawned) yield break;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) yield break;

            if (usePooling && ZombiePoolManager.HasInstance)
            {
                ZombiePoolManager.Instance.ReturnZombie(gameObject);
            }
            else
            {
                netObj.Despawn(true);
            }
        }

        private void ReturnToPool()
        {
            if (ZombiePoolManager.HasInstance)
                ZombiePoolManager.Instance.ReturnZombie(gameObject);
        }

        // ==========================================
        // UTILITY
        // ==========================================

        public void ResetHealth()
        {
            if (!IsServer) return;

            isDead.Value = false;
            maxHealth = originalMaxHealth;
            currentHealth.Value = maxHealth;
            CancelInvoke();
        }

        public void Heal(float amount)
        {
            if (!IsServer) return;
            if (isDead.Value) return;

            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
        }

        private bool CanUseLocalPooling()
        {
            return usePooling
                && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                && ZombiePoolManager.HasInstance;
        }
    }
}
