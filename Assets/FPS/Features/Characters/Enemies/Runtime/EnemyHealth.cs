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

        private readonly NetworkVariable<float> networkMaxHealth = new NetworkVariable<float>(
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
        private float localCurrentHealth;
        private bool localIsDead;

        // Offline/edit-mode instances do not have a NetworkBehaviour binding. Keep their
        // state in memory instead of writing a NetworkVariable before NGO has spawned them.
        private bool HasBoundNetworkState => IsSpawned
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsListening;

        private bool CanMutateAuthoritativeState => !HasBoundNetworkState || IsServer;

        // Properties
        public float CurrentHealth => HasBoundNetworkState ? currentHealth.Value : localCurrentHealth;
        public float MaxHealth => HasBoundNetworkState ? networkMaxHealth.Value : maxHealth;
        public float AuthoredMaxHealth => originalMaxHealth > 0f ? originalMaxHealth : maxHealth;
        public bool IsDead => HasBoundNetworkState ? isDead.Value : localIsDead;

        // Event để UI hoặc các script khác subscribe
        public event System.Action<float, float> OnHealthChanged; // (current, max)
        public event System.Action OnDied;
        public event System.Action OnDeathServer;

        // ==========================================
        // INITIALIZATION
        // ==========================================

        // FIX 3: Start() chỉ cache component, KHÔNG init state network
        // Tránh race condition với OnNetworkSpawn
        private void Awake()
        {
            originalMaxHealth = maxHealth;
            localCurrentHealth = maxHealth;
            localIsDead = false;
            enemyAI = GetComponent<EnemyAI>();
        }

        public override void OnNetworkSpawn()
        {
            // Subscribe để update UI/visual trên tất cả client
            currentHealth.OnValueChanged += HandleHealthChanged;
            networkMaxHealth.OnValueChanged += HandleMaxHealthChanged;
            isDead.OnValueChanged += HandleDeathChanged;

            // FIX 3: Chỉ server khởi tạo state
            if (IsServer)
            {
                networkMaxHealth.Value = maxHealth;
                currentHealth.Value = maxHealth;
                isDead.Value = false;
            }

            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= HandleHealthChanged;
            networkMaxHealth.OnValueChanged -= HandleMaxHealthChanged;
            isDead.OnValueChanged -= HandleDeathChanged;
        }

        // ==========================================
        // HEALTH CHANGE CALLBACKS (chạy trên tất cả client)
        // ==========================================

        private void HandleHealthChanged(float previous, float current)
        {
            OnHealthChanged?.Invoke(current, MaxHealth);
        }

        private void HandleMaxHealthChanged(float previous, float current)
        {
            OnHealthChanged?.Invoke(CurrentHealth, current);
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
            if (!CanMutateAuthoritativeState) return;
            TakeDamage(new DamageInfo(damage));
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!CanMutateAuthoritativeState) return;
            lastDamageInfo = damageInfo;
            TakeDamageInternal(damageInfo.amount);
        }

        private void TakeDamageInternal(float damage)
        {
            if (IsDead) return;

            float nextHealth = Mathf.Max(0f, CurrentHealth - damage);
            SetCurrentHealth(nextHealth);
            GameLog.Info(() => $"[EnemyHealth] {gameObject.name} took {damage} damage. HP: {CurrentHealth}/{MaxHealth}");

            if (CurrentHealth <= 0f)
                Die();
        }

        // ==========================================
        // SET MAX HEALTH — FIX 1: Chỉ server được scale HP
        // ==========================================

        public void SetMaxHealth(float newMaxHealth)
        {
            if (!CanMutateAuthoritativeState) return;

            maxHealth = Mathf.Max(1f, newMaxHealth);
            if (HasBoundNetworkState)
                networkMaxHealth.Value = maxHealth;

            SetCurrentHealth(maxHealth);
            if (!HasBoundNetworkState)
                OnHealthChanged?.Invoke(localCurrentHealth, maxHealth);

            GameLog.Info(() => $"[EnemyHealth] {gameObject.name} maxHealth scaled to {MaxHealth}");
        }

        // ==========================================
        // DEATH
        // ==========================================

        private void Die()
        {
            if (IsDead) return; // double-check

            SetDead(true);
            SetCurrentHealth(0f);
            GameLog.Info(() => $"[EnemyHealth] {gameObject.name} died!");

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
            else if (NetworkManager.Singleton.IsListening)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
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
            if (!CanMutateAuthoritativeState) return;

            SetDead(false);
            maxHealth = originalMaxHealth;
            if (HasBoundNetworkState)
                networkMaxHealth.Value = maxHealth;
            SetCurrentHealth(maxHealth);
            CancelInvoke();
        }

        public void Heal(float amount)
        {
            if (!CanMutateAuthoritativeState) return;
            if (IsDead) return;

            SetCurrentHealth(Mathf.Min(CurrentHealth + amount, MaxHealth));
        }

        private bool CanUseLocalPooling()
        {
            return usePooling
                && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                && ZombiePoolManager.HasInstance;
        }

        private void SetCurrentHealth(float value)
        {
            if (HasBoundNetworkState)
                currentHealth.Value = value;
            else
                localCurrentHealth = value;
        }

        private void SetDead(bool value)
        {
            if (HasBoundNetworkState)
                isDead.Value = value;
            else
                localIsDead = value;
        }
    }
}
