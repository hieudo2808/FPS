using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public class EnemyAI : NetworkBehaviour, IPoolResettable
    {
        public void ResetForPool() => ResetAI();

        [Header("References")]
        [SerializeField] protected NavMeshAgent agent;
        [SerializeField] protected Animator animator;

        [Header("Detection Settings")]
        [SerializeField] private float detectionRange = 20f;
        [SerializeField] private float attackRange = 2.5f;

        [Header("Attack Settings")]
        [SerializeField] private float attackDamage = 10f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float attackDelay = 0.5f;

        [Header("Movement Settings")]
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Audio")]
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private float soundVolume = 1f;

        [Header("Target Switching (Multiplayer)")]
        [SerializeField] private float targetSwitchCooldown = 2f;
        [SerializeField] private float maxTargetDistance = 30f;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead = Animator.StringToHash("Die");

        private enum State { Idle, Chase, Attack, Dead }
        private State currentState = State.Idle;

        private Transform player;
        private float lastAttackTime;
        private bool hasSlot;
        private float lastTargetSwitchTime;
        private int currentTargetIndex = -1;

        public float AttackDamage => attackDamage;

        protected virtual void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();

            if (agent != null)
            {
                agent.speed = runSpeed;
                agent.stoppingDistance = 0.5f;
                agent.updateRotation = false;
            }

            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.OnDeathServer += OnDeath;
            }
        }

        protected virtual void OnDestroy()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.OnDeathServer -= OnDeath;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (agent != null)
                    agent.enabled = true;

                FindPlayer(forceRefresh: true);
                RubberBandingSystem.Instance?.RegisterZombie(this);
            }
            else
            {
                if (agent != null)
                    agent.enabled = false;
            }
        }

        protected virtual void OnEnable()
        {
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            CancelInvoke(nameof(DealDamage));
        }

        protected virtual void Update()
        {
            if (!IsServer) return;
            if (currentState == State.Dead) return;

            if (!IsValidTarget(player))
            {
                FindPlayer(forceRefresh: true);
                if (player == null) {
                    Debug.Log("[EnemyAI] No player found");
                    return;
                }
            }

            UpdateTarget();

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            switch (currentState)
            {
                case State.Idle:
                    if (distToPlayer <= detectionRange)
                        SwitchState(State.Chase);
                    break;

                case State.Chase:
                    if (distToPlayer <= attackRange)
                        SwitchState(State.Attack);
                    else
                        ChaseBehavior();
                    break;

                case State.Attack:
                    if (distToPlayer > attackRange * 1.2f)
                    {
                        SwitchState(State.Chase);
                        ChaseBehavior();
                    }
                    else
                    {
                        AttackBehavior();
                    }
                    break;
            }

            UpdateAnimation();
            SmoothLookAtPlayer();
        }

        private void SwitchState(State newState)
        {
            currentState = newState;

            if (!IsAgentReady()) return;

            switch (newState)
            {
                case State.Idle:
                case State.Attack:
                    agent.isStopped = true;
                    break;

                case State.Chase:
                    agent.isStopped = false;
                    break;
            }
        }

        private void ChaseBehavior()
        {
            if (!IsAgentReady() || player == null) return;

            if (AttackSlotManager.Instance != null && currentTargetIndex >= 0)
            {
                if (!hasSlot)
                    hasSlot = AttackSlotManager.Instance.RequestSlot(this, currentTargetIndex);

                if (hasSlot)
                {
                    if (!AttackSlotManager.Instance.IsAttacker(this))
                    {
                        hasSlot = false;
                        agent.SetDestination(player.position);
                        return;
                    }

                    agent.SetDestination(AttackSlotManager.Instance.GetSlotWorldPosition(this, player));
                    return;
                }
            }

            agent.SetDestination(player.position);
        }

        private void AttackBehavior()
        {
            if (player == null) return;
            if (Time.time - lastAttackTime < attackCooldown) return;

            lastAttackTime = Time.time;

            if (animator != null)
                animator.SetTrigger(AnimAttack);

            if (attackSound != null)
                PlaySoundClientRpc(true);

            CancelInvoke(nameof(DealDamage));
            Invoke(nameof(DealDamage), attackDelay);
        }

        private void DealDamage()
        {
            if (!IsServer) return;
            if (!IsValidTarget(player)) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > attackRange * 1.2f) return;

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
                health.TakeDamage(attackDamage);
        }

        private void FindPlayer(bool forceRefresh = false)
        {
            Transform bestTarget = null;
            int bestIndex = -1;
            float bestDistSqr = float.MaxValue;

            if (PlayerProfiler.Instance != null && PlayerProfiler.Instance.PlayerCount > 0)
            {
                for (int i = 0; i < PlayerProfiler.Instance.PlayerCount; i++)
                {
                    PlayerProfile profile = PlayerProfiler.Instance.GetProfile(i);
                    if (!IsValidTarget(profile?.playerTransform)) continue;

                    float sqrDist = (profile.playerTransform.position - transform.position).sqrMagnitude;
                    if (!forceRefresh && sqrDist > maxTargetDistance * maxTargetDistance) continue;

                    if (sqrDist < bestDistSqr)
                    {
                        bestDistSqr = sqrDist;
                        bestTarget = profile.playerTransform;
                        bestIndex = i;
                    }
                }
            }

            if (bestTarget == null && NetworkManager.Singleton != null)
            {
                int fallbackIndex = 0;

                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    NetworkObject playerObject = client.PlayerObject;
                    if (playerObject == null || !playerObject.IsSpawned) continue;
                    if (!IsValidTarget(playerObject.transform)) continue;

                    float sqrDist = (playerObject.transform.position - transform.position).sqrMagnitude;

                    if (sqrDist < bestDistSqr)
                    {
                        bestDistSqr = sqrDist;
                        bestTarget = playerObject.transform;
                        bestIndex = fallbackIndex;
                    }

                    fallbackIndex++;
                }
            }

            player = bestTarget;
            currentTargetIndex = bestIndex;
        }

        private void UpdateTarget()
        {
            if (Time.time - lastTargetSwitchTime < targetSwitchCooldown) return;
            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount <= 1) return;

            Transform bestTarget = null;
            float bestScore = float.MinValue;
            int bestIndex = -1;

            for (int i = 0; i < PlayerProfiler.Instance.PlayerCount; i++)
            {
                PlayerProfile profile = PlayerProfiler.Instance.GetProfile(i);

                if (profile?.playerTransform == null || !IsValidTarget(profile.playerTransform))
                    continue;

                float dist = Vector3.Distance(transform.position, profile.playerTransform.position);

                if (dist > maxTargetDistance)
                    continue;

                float score = (maxTargetDistance - dist) * 2f;

                if (profile.currentHealth < 30f)
                    score += 30f;

                if (profile.isIsolated)
                    score += 40f;

                if (profile.isReloading)
                    score += 35f;

                if (profile.playerTransform == player)
                    score += 20f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = profile.playerTransform;
                    bestIndex = i;
                }
            }

            if (bestTarget != null && bestTarget != player)
            {
                AttackSlotManager.Instance?.ReleaseSlot(this);

                hasSlot = false;
                player = bestTarget;
                currentTargetIndex = bestIndex;
                lastTargetSwitchTime = Time.time;
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null || !IsAgentReady()) return;

            animator.SetFloat(AnimSpeed, agent.velocity.magnitude, 0.1f, Time.deltaTime);
        }

        private void SmoothLookAtPlayer()
        {
            if (currentState == State.Idle || player == null) return;

            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0f;

            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    lookRot,
                    Time.deltaTime * rotationSpeed
                );
            }
        }

        public virtual void OnDeath()
        {
            if (currentState == State.Dead) return;

            currentState = State.Dead;

            CancelInvoke(nameof(DealDamage));

            AttackSlotManager.Instance?.ReleaseSlot(this);
            hasSlot = false;

            if (IsAgentReady())
                agent.isStopped = true;

            if (agent != null)
                agent.enabled = false;

            if (animator != null)
                animator.SetTrigger(AnimDead);

            if (deathSound != null)
                PlaySoundClientRpc(false);

            if (RubberBandingSystem.HasInstance)
                RubberBandingSystem.Instance.UnregisterZombie(this);
        }

        public virtual void ResetAI()
        {
            currentState = State.Idle;
            lastAttackTime = 0f;
            hasSlot = false;
            lastTargetSwitchTime = 0f;
            currentTargetIndex = -1;
            player = null;

            CancelInvoke(nameof(DealDamage));

            if (agent != null)
            {
                agent.enabled = true;
                agent.speed = runSpeed;

                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.ResetPath();
                }
            }
        }

        public void SetStats(float speed, float damage, float cooldown)
        {
            runSpeed = speed;
            attackDamage = damage;
            attackCooldown = cooldown;

            if (agent != null && agent.enabled)
                agent.speed = runSpeed;
        }

        [ClientRpc]
        private void PlaySoundClientRpc(bool isAttackSound)
        {
            if (AudioManager.Instance == null) return;

            if (isAttackSound && attackSound != null)
                AudioManager.Instance.PlaySFXSound(attackSound, soundVolume);
            else if (!isAttackSound && deathSound != null)
                AudioManager.Instance.PlaySFXSound(deathSound, soundVolume);
        }

        private bool IsAgentReady()
        {
            return agent != null && agent.enabled && agent.isOnNavMesh;
        }

        private bool IsValidTarget(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            // Dùng cache từ PlayerProfiler thay vì GetComponent mỗi frame
            if (PlayerProfiler.Instance != null)
            {
                var profile = PlayerProfiler.Instance.GetProfileByTransform(target);
                if (profile != null)
                    return profile.cachedHealth != null && !profile.cachedHealth.IsDead;
            }

            // Fallback
            PlayerHealth health = target.GetComponent<PlayerHealth>();
            return health != null && !health.IsDead;
        }
    }
}