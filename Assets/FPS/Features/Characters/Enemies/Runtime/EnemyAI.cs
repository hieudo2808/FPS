using Unity.Netcode;
using Unity.Netcode.Components;
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
        [SerializeField] private float pathRefreshInterval = 0.15f;
        [SerializeField] private float destinationRepathDistance = 0.75f;

        [Header("Audio")]
        [SerializeField] protected AudioClip attackSound;
        [SerializeField] protected AudioClip deathSound;
        [SerializeField] protected float soundVolume = 1f;

        [Header("Target Switching (Multiplayer)")]
        [SerializeField] private float targetSwitchCooldown = 2f;
        [SerializeField] private float maxTargetDistance = 30f;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead = Animator.StringToHash("Die");

        private readonly NetworkVariable<EnemyReplicatedState> replicatedState = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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
        private int intentDestinationRequestCount;
        private Vector3 lastSubmittedAgentDestination;
        private float lastAgentDestinationRequestTime;
        private int agentDestinationRequestCount;
        private Vector3 lastFramePosition;
        private bool hasLastFramePosition;
        private float lastAnimatorSpeed;
        private bool hasDesiredDestination;
        private bool hasSubmittedAgentDestination;
        private bool loggedMissingPlayer;
        private ushort serverActionSequence;
        private int serverActionStartTick;
        private bool specialAbilityActive;
        private EnemySpecialActionKind specialActionKind;
        private int specialAbilityDeadlineTick;
        private ushort lastPresentedActionSequence;

        public float AttackDamage => attackDamage;

        /// <summary>
        /// Specials with a complete FSM can opt out of the common idle/chase/attack brain
        /// while continuing to use the replicated presentation owned by this component.
        /// </summary>
        protected virtual bool UsesGenericServerBrain => true;
        protected virtual bool PreserveAuthoredAgentSettings => false;

        protected virtual void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponent<Animator>();
            ConfigureMeleeAttack();

            if (agent != null)
            {
                if (!PreserveAuthoredAgentSettings)
                    agent.speed = runSpeed;
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
            replicatedState.OnValueChanged += OnReplicatedStateChanged;
            ConfigureNetworkTransform();
            if (IsServer)
            {
                if (agent != null)
                    agent.enabled = true;

                FindPlayer(forceRefresh: true);
                RegisterWithRubberBandingIfAuthority();
                PublishReplicatedState(force: true);
            }
            else
            {
                if (agent != null)
                    agent.enabled = false;

                ApplyReplicatedState(default, replicatedState.Value, force: true);
            }
        }

        private void ConfigureNetworkTransform()
        {
            NetworkTransform networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform == null)
                return;

            NetworkHardeningSettings settings = NetworkGameManager.Instance != null
                ? NetworkGameManager.Instance.Settings
                : NetworkHardeningSettings.Default;
            networkTransform.Interpolate = true;
            networkTransform.UseUnreliableDeltas = true;
            networkTransform.UseHalfFloatPrecision = true;
            networkTransform.PositionThreshold = settings.EnemyPositionThreshold;
            networkTransform.RotAngleThreshold = settings.EnemyRotationThresholdDegrees;
        }

        public override void OnNetworkDespawn()
        {
            replicatedState.OnValueChanged -= OnReplicatedStateChanged;
            base.OnNetworkDespawn();
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

            if (!UsesGenericServerBrain)
            {
                TickCustomServerBrain();
                UpdateAnimation();
                SmoothLookAtMovementOrTarget();
                PublishReplicatedState();
                return;
            }

            if (!IsValidTarget(player))
            {
                FindPlayer(forceRefresh: true);
                if (player == null)
                {
                    if (!loggedMissingPlayer)
                    {
                        GameLog.Info("[EnemyAI] No player found");
                        loggedMissingPlayer = true;
                    }

                    return;
                }
            }
            else
            {
                loggedMissingPlayer = false;
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
            PublishReplicatedState();
        }

        protected virtual void TickCustomServerBrain()
        {
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
                    ForcePathRefresh();
                    break;
            }
        }

        private void ChaseBehavior()
        {
            if (player == null) return;
            if (!ShouldRefreshPath())
                return;

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
            intentDestinationRequestCount++;
            hasDesiredDestination = true;

            TrySubmitAgentDestination(destination);
        }

        private bool ShouldRefreshPath()
        {
            if (!hasDesiredDestination)
                return true;

            return Time.time - lastDestinationRequestTime >= Mathf.Max(0.02f, pathRefreshInterval);
        }

        private void TrySubmitAgentDestination(Vector3 destination)
        {
            if (!IsAgentReady()) return;

            float minRepathDistance = Mathf.Max(0f, destinationRepathDistance);
            if (hasSubmittedAgentDestination &&
                (destination - lastSubmittedAgentDestination).sqrMagnitude < minRepathDistance * minRepathDistance)
            {
                return;
            }

            if (agent.SetDestination(destination))
            {
                lastSubmittedAgentDestination = destination;
                hasSubmittedAgentDestination = true;
                lastAgentDestinationRequestTime = Time.time;
                agentDestinationRequestCount++;
            }
        }

        private void AttackBehavior()
        {
            if (player == null) return;
            if (!meleeAttack.TryBegin(transform, player, Time.time)) return;

            StopAgentMotion();

            if (animator != null)
                animator.SetTrigger(AnimAttack);

            if (attackSound != null)
                PlayLocalSound(attackSound);

            BeginReplicatedAction();
        }

        public void ApplyAttackHit()
        {
            ProcessPendingAttackDamage(forceImpact: true);
        }

        private void FindPlayer(bool forceRefresh = false)
        {
            if (GetType() == typeof(EnemyAI)
                && InfectionThreatService.Instance != null
                && InfectionThreatService.Instance.TryGetPriorityTarget(transform.position, out Transform threatTarget)
                && IsValidTarget(threatTarget))
            {
                player = threatTarget;
                currentTargetIndex = -1;
                return;
            }

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
                ForcePathRefresh();
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
            if (!ShouldRotateForPresentation()) return;

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

        protected virtual bool ShouldRotateForPresentation()
        {
            return currentState != State.Idle && currentState != State.Dead;
        }

        public virtual void OnDeath()
        {
            if (currentState == State.Dead) return;

            currentState = State.Dead;
            specialAbilityActive = false;
            specialActionKind = EnemySpecialActionKind.None;
            specialAbilityDeadlineTick = 0;

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
                PlayLocalSound(deathSound);

            BeginReplicatedAction();
            PublishReplicatedState(force: true);
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
            intentDestinationRequestCount = 0;
            hasDesiredDestination = false;
            hasSubmittedAgentDestination = false;
            lastSubmittedAgentDestination = Vector3.zero;
            lastAgentDestinationRequestTime = 0f;
            agentDestinationRequestCount = 0;
            lastAnimatorSpeed = 0f;
            serverActionSequence = 0;
            serverActionStartTick = 0;
            specialAbilityActive = false;
            specialActionKind = EnemySpecialActionKind.None;
            specialAbilityDeadlineTick = 0;
            lastPresentedActionSequence = 0;
            lastFramePosition = transform.position;
            hasLastFramePosition = true;

            if (agent != null)
            {
                agent.enabled = true;
                if (!PreserveAuthoredAgentSettings)
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

        private void PlayLocalSound(AudioClip clip)
        {
            // AudioManager is authored by the application bootstrap scene and persists
            // across gameplay scenes. Do not touch Instance when no authored manager is
            // present: Singleton.Instance would manufacture a runtime fallback object,
            // hiding a missing scene/bootstrap dependency (and breaking direct-scene tests).
            if (clip != null && AudioManager.HasInstance)
                AudioManager.Instance.PlaySFXSound(clip, soundVolume);
        }

        protected void SetSpecialAbilityReplicated(bool active, double deadlineServerTime = 0.0)
        {
            if (active)
                SetSpecialActionReplicated(EnemySpecialActionKind.Primary, deadlineServerTime);
            else
                ClearSpecialActionReplicated();
        }

        protected void SetSpecialActionReplicated(
            EnemySpecialActionKind actionKind,
            double deadlineServerTime)
        {
            if (!CanRunServerLogic())
                return;

            if (actionKind == EnemySpecialActionKind.None)
            {
                ClearSpecialActionReplicated();
                return;
            }

            if (!specialAbilityActive || specialActionKind != actionKind)
                BeginReplicatedAction();

            specialAbilityActive = true;
            specialActionKind = actionKind;
            specialAbilityDeadlineTick = ServerTimeToTick(deadlineServerTime);
            PublishReplicatedState(force: true);
        }

        protected void ClearSpecialActionReplicated()
        {
            if (!CanRunServerLogic())
                return;

            specialAbilityActive = false;
            specialActionKind = EnemySpecialActionKind.None;
            specialAbilityDeadlineTick = 0;
            PublishReplicatedState(force: true);
        }

        protected virtual void OnReplicatedSpecialAbilityStarted(int elapsedTicks)
        {
        }

        protected virtual void OnReplicatedSpecialAbilityEnded()
        {
        }

        protected virtual void OnReplicatedSpecialActionStarted(
            EnemySpecialActionKind actionKind,
            int elapsedTicks)
        {
            if (actionKind == EnemySpecialActionKind.Primary)
                OnReplicatedSpecialAbilityStarted(elapsedTicks);
        }

        protected virtual void OnReplicatedSpecialActionEnded(EnemySpecialActionKind actionKind)
        {
            OnReplicatedSpecialAbilityEnded();
        }

        private void BeginReplicatedAction()
        {
            serverActionSequence++;
            serverActionStartTick = GetServerTick();
        }

        private void PublishReplicatedState(bool force = false)
        {
            if (!IsServer || !IsSpawned)
                return;

            float normalized = runSpeed > 0.001f ? Mathf.Clamp01(lastAnimatorSpeed / runSpeed) : 0f;
            // Sixteen speed steps avoid dirtying the NetworkVariable for tiny NavMesh velocity noise.
            byte quantizedSpeed = (byte)(Mathf.RoundToInt(normalized * 15f) * 17);
            EnemyActionFlags flags = EnemyActionFlags.None;
            if (currentState == State.Attack && IsAttackMovementLocked())
                flags |= EnemyActionFlags.Attack;
            if (currentState == State.Dead)
                flags |= EnemyActionFlags.Dead;
            if (specialAbilityActive)
                flags |= EnemyActionFlags.SpecialAbility;
            if (specialActionKind == EnemySpecialActionKind.Stagger)
                flags |= EnemyActionFlags.Stagger;

            EnemyReplicatedState next = new EnemyReplicatedState
            {
                locomotion = ResolveReplicatedLocomotion(),
                normalizedSpeed = quantizedSpeed,
                actionFlags = flags,
                specialActionKind = specialActionKind,
                actionSequence = serverActionSequence,
                actionStartServerTick = serverActionStartTick,
                specialAbilityDeadlineTick = specialAbilityDeadlineTick
            };

            if (force || !next.Equals(replicatedState.Value))
                replicatedState.Value = next;
        }

        protected virtual EnemyLocomotionState ResolveReplicatedLocomotion()
        {
            return currentState switch
            {
                State.Chase => EnemyLocomotionState.Moving,
                State.Attack => EnemyLocomotionState.Attacking,
                State.Dead => EnemyLocomotionState.Dead,
                _ => EnemyLocomotionState.Idle
            };
        }

        private void OnReplicatedStateChanged(EnemyReplicatedState previous, EnemyReplicatedState current)
        {
            if (!IsServer)
                ApplyReplicatedState(previous, current, force: false);
        }

        private void ApplyReplicatedState(
            EnemyReplicatedState previous,
            EnemyReplicatedState current,
            bool force)
        {
            if (animator != null)
                animator.SetFloat(AnimSpeed, current.normalizedSpeed / 255f * runSpeed);

            bool newAction = force || current.actionSequence != lastPresentedActionSequence;
            int elapsedTicks = Mathf.Max(0, GetServerTick() - current.actionStartServerTick);
            int attackPresentationTicks = Mathf.CeilToInt(
                Mathf.Max(attackActionLockDuration, minimumAttackImpactDelay) * GetNetworkTickRate());

            if ((current.actionFlags & EnemyActionFlags.Dead) != 0
                && (force || (previous.actionFlags & EnemyActionFlags.Dead) == 0))
            {
                animator?.SetTrigger(AnimDead);
                PlayLocalSound(deathSound);
            }
            else if (newAction
                && (current.actionFlags & EnemyActionFlags.Attack) != 0
                && elapsedTicks <= attackPresentationTicks)
            {
                animator?.SetTrigger(AnimAttack);
                PlayLocalSound(attackSound);
            }

            bool specialStarted = (current.actionFlags & EnemyActionFlags.SpecialAbility) != 0
                && (force || (previous.actionFlags & EnemyActionFlags.SpecialAbility) == 0 || newAction);
            bool specialEnded = (current.actionFlags & EnemyActionFlags.SpecialAbility) == 0
                && (previous.actionFlags & EnemyActionFlags.SpecialAbility) != 0;
            if (specialStarted && GetServerTick() < current.specialAbilityDeadlineTick)
                OnReplicatedSpecialActionStarted(current.specialActionKind, elapsedTicks);
            else if (specialEnded)
                OnReplicatedSpecialActionEnded(previous.specialActionKind);

            lastPresentedActionSequence = current.actionSequence;
        }

        private int GetServerTick()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Tick
                : Mathf.FloorToInt((float)(Time.timeAsDouble * GetNetworkTickRate()));
        }

        private int ServerTimeToTick(double serverTime)
        {
            if (serverTime <= 0.0)
                return 0;

            return Mathf.CeilToInt((float)(serverTime * GetNetworkTickRate()));
        }

        private int GetNetworkTickRate()
        {
            return NetworkManager != null && NetworkManager.NetworkConfig != null
                ? Mathf.Max(1, (int)NetworkManager.NetworkConfig.TickRate)
                : NetworkGameplayPolicy.SimulationHz;
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

        protected virtual Vector3 GetLookDirection()
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

        public void NotifyAttackSlotChanged()
        {
            ForcePathRefresh();
        }

        private void ForcePathRefresh()
        {
            lastDestinationRequestTime = -Mathf.Infinity;
            hasSubmittedAgentDestination = false;
        }

        protected virtual float CalculateVisualMoveSpeed()
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
            public readonly int intentDestinationRequestCount;
            public readonly float lastAgentDestinationRequestTime;
            public readonly int agentDestinationRequestCount;
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
                int intentDestinationRequestCount,
                float lastAgentDestinationRequestTime,
                int agentDestinationRequestCount,
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
                this.intentDestinationRequestCount = intentDestinationRequestCount;
                this.lastAgentDestinationRequestTime = lastAgentDestinationRequestTime;
                this.agentDestinationRequestCount = agentDestinationRequestCount;
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
                intentDestinationRequestCount,
                lastAgentDestinationRequestTime,
                agentDestinationRequestCount,
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
            loggedMissingPlayer = target == null;
            if (target != null)
                ForcePathRefresh();
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

        public void DebugForcePathRefreshForTests()
        {
            ForcePathRefresh();
        }

        public void DebugSmoothLookForTests()
        {
            SmoothLookAtMovementOrTarget();
        }
#endif
    }
}
