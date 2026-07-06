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
        [SerializeField] private float minimumAttackImpactDelay = 0.65f;
        [SerializeField] private float attackActionLockDuration = 0.9f;
        [SerializeField] private float attackHitArcDegrees = 130f;

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

        private readonly EnemyMeleeAttack meleeAttack = new EnemyMeleeAttack();
        private Transform player;
        private float lastTargetSwitchTime;
        private int currentTargetIndex = -1;
        private int brainTickCount;
        private float lastBrainTickTime;
        private Vector3 lastDesiredDestination;
        private float lastDestinationRequestTime;
        private Vector3 lastFramePosition;
        private bool hasLastFramePosition;
        private float lastAnimatorSpeed;
        private bool hasDesiredDestination;

        public float AttackDamage => attackDamage;

        protected virtual void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();
            ConfigureMeleeAttack();

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

            RegisterWithRubberBandingIfAuthority();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.OnDeathServer -= OnDeath;
            }

            if (RubberBandingSystem.HasInstance)
                RubberBandingSystem.Instance.UnregisterZombie(this);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (agent != null)
                    agent.enabled = true;

                FindPlayer(forceRefresh: true);
                RegisterWithRubberBandingIfAuthority();
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

            meleeAttack.Reset();
            lastFramePosition = transform.position;
            hasLastFramePosition = true;
        }

        protected virtual void Update()
        {
            if (!CanRunServerLogic()) return;
            if (currentState == State.Dead) return;

            brainTickCount++;
            lastBrainTickTime = Time.time;

            if (!IsValidTarget(player))
            {
                FindPlayer(forceRefresh: true);
                if (player == null)
                {
                    Debug.Log("[EnemyAI] No player found");
                    return;
                }
            }

            UpdateTarget();
            ProcessPendingAttackDamage();

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
                    if (distToPlayer > attackRange)
                    {
                        if (IsAttackMovementLocked())
                            break;

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
            SmoothLookAtMovementOrTarget();
        }

        private void SwitchState(State newState)
        {
            if (currentState == State.Attack && newState != State.Attack)
            {
                meleeAttack.CancelPendingDamage();
                meleeAttack.ClearActionLock();
            }

            currentState = newState;

            if (!IsAgentReady()) return;

            switch (newState)
            {
                case State.Idle:
                case State.Attack:
                    StopAgentMotion();
                    break;

                case State.Chase:
                    agent.isStopped = false;
                    break;
            }
        }

        private void ChaseBehavior()
        {
            if (player == null) return;

            Vector3 destination;
            if (AttackSlotManager.Instance != null && currentTargetIndex >= 0)
            {
                destination = AttackSlotManager.Instance.GetDestinationFor(this, currentTargetIndex, player);
            }
            else
            {
                destination = player.position;
            }

            lastDesiredDestination = destination;
            lastDestinationRequestTime = Time.time;
            hasDesiredDestination = true;

            if (!IsAgentReady()) return;

            agent.SetDestination(destination);
        }

        private void AttackBehavior()
        {
            if (player == null) return;
            if (!meleeAttack.TryBegin(transform, player, Time.time)) return;

            StopAgentMotion();

            if (animator != null)
                animator.SetTrigger(AnimAttack);

            if (attackSound != null)
                PlaySoundClientRpc(true);
        }

        public void ApplyAttackHit()
        {
            ProcessPendingAttackDamage(forceImpact: true);
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

                float sqrDist = (transform.position - profile.playerTransform.position).sqrMagnitude;
                float maxTargetDistanceSqr = maxTargetDistance * maxTargetDistance;
                if (sqrDist > maxTargetDistanceSqr)
                    continue;

                float score = ScoreTarget(profile, i, Mathf.Sqrt(sqrDist));

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

                player = bestTarget;
                currentTargetIndex = bestIndex;
                lastTargetSwitchTime = Time.time;
            }
        }

        private float ScoreTarget(PlayerProfile profile, int profileIndex, float distance)
        {
            float score = (maxTargetDistance - distance) * 2f;

            if (profile.currentHealth < 30f)
                score += 30f;

            if (profile.isIsolated)
                score += 40f;

            if (profile.isReloading)
                score += 35f;

            if (profile.currentAmmoPercent < 0.2f)
                score += 15f;

            if (AttackSlotManager.Instance != null)
                score -= AttackSlotManager.Instance.GetZombiesTargeting(profileIndex) * 12f;

            if (TeamAnalyzer.Instance != null)
            {
                PlayerRole role = TeamAnalyzer.Instance.GetPlayerRole(profileIndex);
                if (role == PlayerRole.CARRY)
                    score += 10f;
                else if (role == PlayerRole.LONE_WOLF)
                    score += 15f;
            }

            if (profile.playerTransform == player)
                score += 20f;

            return score;
        }

        private void UpdateAnimation()
        {
            float speed = CalculateVisualMoveSpeed();
            lastAnimatorSpeed = speed;

            if (animator == null) return;

            animator.SetFloat(AnimSpeed, speed, 0.08f, Time.deltaTime);
        }

        private void SmoothLookAtMovementOrTarget()
        {
            if (currentState == State.Idle) return;

            Vector3 dir = GetLookDirection();
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir.normalized);

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

            meleeAttack.CancelPendingDamage();
            meleeAttack.ClearActionLock();

            AttackSlotManager.Instance?.ReleaseSlot(this);

            if (IsAgentReady())
                agent.isStopped = true;

            if (agent != null)
                agent.enabled = false;

            if (animator != null)
                animator.SetTrigger(AnimDead);

            if (deathSound != null)
                PlaySoundClientRpc(false);
        }

        public virtual void ResetAI()
        {
            currentState = State.Idle;
            lastTargetSwitchTime = 0f;
            currentTargetIndex = -1;
            player = null;
            meleeAttack.Reset();
            brainTickCount = 0;
            lastBrainTickTime = 0f;
            lastDesiredDestination = Vector3.zero;
            lastDestinationRequestTime = 0f;
            hasDesiredDestination = false;
            lastAnimatorSpeed = 0f;
            lastFramePosition = transform.position;
            hasLastFramePosition = true;

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
            ConfigureMeleeAttack();

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

        private void ConfigureMeleeAttack()
        {
            meleeAttack.Configure(
                attackRange,
                attackDamage,
                attackCooldown,
                attackDelay,
                minimumAttackImpactDelay,
                attackActionLockDuration,
                attackHitArcDegrees);
        }

        private void ProcessPendingAttackDamage(bool forceImpact = false)
        {
            if (!meleeAttack.TryConsumeImpact(transform, player, Time.time, forceImpact, out Transform target, out float damage))
                return;

            if (!CanRunServerLogic()) return;
            if (!IsValidTarget(target)) return;

            IDamageable damageable = GetDamageable(target);
            damageable?.TakeDamage(damage);
        }

        private bool IsAttackMovementLocked()
        {
            return currentState == State.Attack && meleeAttack.IsActionLocked(Time.time);
        }

        private Vector3 GetLookDirection()
        {
            if (currentState == State.Attack && meleeAttack.TryGetLockedFacing(Time.time, out Vector3 attackFacing))
                return attackFacing;

            if (currentState == State.Chase)
            {
                if (IsAgentReady())
                {
                    Vector3 agentDirection = agent.velocity.sqrMagnitude > 0.04f
                        ? agent.velocity
                        : agent.desiredVelocity;

                    agentDirection.y = 0f;
                    if (agentDirection.sqrMagnitude > 0.04f)
                        return agentDirection;

                    if (agent.hasPath)
                    {
                        Vector3 steeringDirection = agent.steeringTarget - transform.position;
                        steeringDirection.y = 0f;
                        if (steeringDirection.sqrMagnitude > 0.04f)
                            return steeringDirection;
                    }
                }

                if (hasDesiredDestination)
                {
                    Vector3 destinationDirection = lastDesiredDestination - transform.position;
                    destinationDirection.y = 0f;
                    if (destinationDirection.sqrMagnitude > 0.04f)
                        return destinationDirection;
                }
            }

            if (player == null)
                return Vector3.zero;

            Vector3 targetDirection = player.position - transform.position;
            targetDirection.y = 0f;
            return targetDirection;
        }

        private void StopAgentMotion()
        {
            if (!IsAgentReady())
                return;

            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.nextPosition = transform.position;
        }

        private bool CanHitTarget(Transform target)
        {
            return meleeAttack.CanHit(transform, target);
        }

        private float CalculateVisualMoveSpeed()
        {
            float speed = 0f;
            if (currentState == State.Chase && IsAgentReady())
            {
                speed = Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);
                if (agent.isStopped)
                    speed = 0f;
            }

            if (hasLastFramePosition && Time.deltaTime > 0.0001f)
            {
                Vector3 delta = transform.position - lastFramePosition;
                delta.y = 0f;
                float displacementSpeed = delta.magnitude / Time.deltaTime;
                if (currentState == State.Chase)
                    speed = Mathf.Max(speed, displacementSpeed);
            }

            lastFramePosition = transform.position;
            hasLastFramePosition = true;

            return currentState == State.Chase ? speed : 0f;
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

            IDamageable damageable = GetDamageable(target);
            if (damageable != null)
                return !damageable.IsDead;

            PlayerHealth health = target.GetComponent<PlayerHealth>();
            return health != null && !health.IsDead;
        }

        private static IDamageable GetDamageable(Transform target)
        {
            if (target == null)
                return null;

            return target.TryGetComponent<IDamageable>(out var damageable) ? damageable : null;
        }

        protected bool CanRunServerLogic()
        {
            return IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }

        private void RegisterWithRubberBandingIfAuthority()
        {
            if (!CanRunServerLogic()) return;
            if (!RubberBandingSystem.HasInstance) return;

            RubberBandingSystem.Instance.RegisterZombie(this);
        }

#if UNITY_INCLUDE_TESTS
        public readonly struct TestSnapshot
        {
            public readonly int brainTickCount;
            public readonly float lastBrainTickTime;
            public readonly Transform currentTarget;
            public readonly int currentTargetIndex;
            public readonly string currentState;
            public readonly Vector3 lastDesiredDestination;
            public readonly float lastDestinationRequestTime;
            public readonly float lastAnimatorSpeed;
            public readonly bool hasPendingAttackDamage;

            public TestSnapshot(
                int brainTickCount,
                float lastBrainTickTime,
                Transform currentTarget,
                int currentTargetIndex,
                string currentState,
                Vector3 lastDesiredDestination,
                float lastDestinationRequestTime,
                float lastAnimatorSpeed,
                bool hasPendingAttackDamage)
            {
                this.brainTickCount = brainTickCount;
                this.lastBrainTickTime = lastBrainTickTime;
                this.currentTarget = currentTarget;
                this.currentTargetIndex = currentTargetIndex;
                this.currentState = currentState;
                this.lastDesiredDestination = lastDesiredDestination;
                this.lastDestinationRequestTime = lastDestinationRequestTime;
                this.lastAnimatorSpeed = lastAnimatorSpeed;
                this.hasPendingAttackDamage = hasPendingAttackDamage;
            }
        }

        public TestSnapshot CaptureTestSnapshot()
        {
            return new TestSnapshot(
                brainTickCount,
                lastBrainTickTime,
                player,
                currentTargetIndex,
                currentState.ToString(),
                lastDesiredDestination,
                lastDestinationRequestTime,
                lastAnimatorSpeed,
                meleeAttack.HasPendingDamage);
        }

        public void DebugConfigureCombatForTests(float range, float damage, float cooldown, float impactDelay)
        {
            attackRange = range;
            attackDamage = damage;
            attackCooldown = cooldown;
            attackDelay = impactDelay;
            minimumAttackImpactDelay = impactDelay;
            ConfigureMeleeAttack();
        }

        public void DebugForceTargetForTests(Transform target, int targetIndex = 0)
        {
            player = target;
            currentTargetIndex = targetIndex;
        }

        public void DebugBeginAttackForTests()
        {
            SwitchState(State.Attack);
            meleeAttack.Reset();
            AttackBehavior();
        }

        public void DebugProcessPendingAttackForTests(bool forceImpact = false)
        {
            ProcessPendingAttackDamage(forceImpact);
        }

        public bool DebugIsTargetValidForTests(Transform target)
        {
            return IsValidTarget(target);
        }

        public bool DebugCanHitTargetForTests(Transform target)
        {
            return CanHitTarget(target);
        }

        public void DebugSetStateForTests(string stateName)
        {
            if (System.Enum.TryParse(stateName, out State parsed))
                SwitchState(parsed);
        }

        public void DebugUpdateAnimationForTests()
        {
            UpdateAnimation();
        }

        public void DebugSetDesiredDestinationForTests(Vector3 destination)
        {
            lastDesiredDestination = destination;
            hasDesiredDestination = true;
        }

        public void DebugSmoothLookForTests()
        {
            SmoothLookAtMovementOrTarget();
        }
#endif
    }
}
