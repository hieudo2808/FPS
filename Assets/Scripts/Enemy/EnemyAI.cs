using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace FPS
{
    public class EnemyAI : MonoBehaviour
    {
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
        private int currentTargetIndex;

        public float AttackDamage => attackDamage;

        protected virtual void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();

            FindPlayer();

            if (agent != null)
            {
                agent.speed = runSpeed;
                agent.stoppingDistance = 0.5f;
                agent.updateRotation = false;
                
                if (agent.isOnNavMesh)
                    Debug.Log("[EnemyAI] NavMeshAgent OK");
                else
                    Debug.LogError("[EnemyAI] NavMeshAgent NOT on NavMesh!");
            }
        }

        private void FindPlayer()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                Debug.Log("[EnemyAI] Found player: " + playerObj.name);
            }
        }

        protected virtual void Update()
        {
            if (currentState == State.Dead) return;
            if (player == null) { FindPlayer(); return; }

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
                        AttackBehavior();
                    break;
            }

            UpdateAnimation();
            SmoothLookAtPlayer();
        }

        private void SwitchState(State newState)
        {
            currentState = newState;

            if (agent == null || !agent.isOnNavMesh) return;

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
            if (agent == null || !agent.isOnNavMesh) return;

            if (AttackSlotManager.Instance != null)
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
            if (Time.time - lastAttackTime < attackCooldown) return;

            lastAttackTime = Time.time;

            if (animator != null)
                animator.SetTrigger(AnimAttack);

            if (attackSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(attackSound, soundVolume);

            Invoke(nameof(DealDamage), attackDelay);
        }

        private void DealDamage()
        {
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= attackRange * 1.2f)
            {
                player.GetComponent<PlayerHealth>()?.TakeDamage(attackDamage);
                Debug.Log($"[EnemyAI] Dealt {attackDamage} damage!");
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null || agent == null) return;
            animator.SetFloat(AnimSpeed, agent.velocity.magnitude, 0.1f, Time.deltaTime);
        }

        private void SmoothLookAtPlayer()
        {
            if (currentState == State.Idle || player == null) return;

            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;

            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSpeed);
            }
        }

        private void UpdateTarget()
        {
            if (Time.time - lastTargetSwitchTime < targetSwitchCooldown) return;
            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount <= 1) return;

            Transform bestTarget = null;
            float bestScore = float.MinValue;
            int bestIndex = 0;

            for (int i = 0; i < PlayerProfiler.Instance.PlayerCount; i++)
            {
                var profile = PlayerProfiler.Instance.GetProfile(i);
                if (profile?.playerTransform == null) continue;

                float dist = Vector3.Distance(transform.position, profile.playerTransform.position);
                if (dist > maxTargetDistance) continue;

                float score = (maxTargetDistance - dist) * 2f;
                if (profile.currentHealth < 30f) score += 30f;
                if (profile.isIsolated) score += 40f;
                if (profile.isReloading) score += 35f;
                if (profile.playerTransform == player) score += 20f;

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

        public virtual void OnDeath()
        {
            if (currentState == State.Dead) return;
            currentState = State.Dead;

            AttackSlotManager.Instance?.ReleaseSlot(this);
            hasSlot = false;

            if (agent != null) agent.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (animator != null) animator.SetTrigger(AnimDead);
            if (deathSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(deathSound, soundVolume);

            RubberBandingSystem.Instance?.UnregisterZombie(this);
        }

        public void SetStats(float speed, float damage, float cooldown)
        {
            runSpeed = speed;
            attackDamage = damage;
            attackCooldown = cooldown;
            if (agent != null) agent.speed = runSpeed;
        }

        public virtual void ResetAI()
        {
            currentState = State.Idle;
            lastAttackTime = 0f;
            hasSlot = false;
            lastTargetSwitchTime = 0f;

            FindPlayer();
            RubberBandingSystem.Instance?.RegisterZombie(this);
        }

        protected virtual void OnEnable()
        {
            FindPlayer();
            
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            
            currentState = State.Idle;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
