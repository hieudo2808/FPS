using System.Collections.Generic;
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
        [SerializeField] private EnemyLocomotionCalibration locomotionCalibration =
            new EnemyLocomotionCalibration(5f, 0.75f, 1.35f, 0.05f);

        [Header("Audio")]
        [SerializeField] protected AudioClip attackSound;
        [SerializeField] protected AudioClip deathSound;
        [SerializeField] protected float soundVolume = 1f;
        [SerializeField, Min(0.01f)] private float attackAudioMinDistance = 3f;
        [SerializeField, Min(0.02f)] private float attackAudioMaxDistance = 30f;
        [SerializeField, Min(0.01f)] private float deathAudioMinDistance = 3f;
        [SerializeField, Min(0.02f)] private float deathAudioMaxDistance = 35f;

        [Header("Target Switching (Multiplayer)")]
        [SerializeField] private float targetSwitchCooldown = 2f;
        [SerializeField] private float maxTargetDistance = 30f;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimLocomotionRate = Animator.StringToHash("LocomotionRate");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead = Animator.StringToHash("Die");
        private static readonly Dictionary<int, AnimatorParameterSupport> AnimatorParameterSupportByController = new();
        private static readonly HashSet<long> WarnedMissingAnimatorParameters = new();

        private readonly struct AnimatorParameterSupport
        {
            public readonly bool HasSpeed;
            public readonly bool HasLocomotionRate;

            public AnimatorParameterSupport(bool hasSpeed, bool hasLocomotionRate)
            {
                HasSpeed = hasSpeed;
                HasLocomotionRate = hasLocomotionRate;
            }
        }

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
        private SpecialSpatialAudio spatialAudio;
        private uint motionLockGeneration;
        private uint activeMotionLockId;
        private float activeMotionLockDeadline;
        private EnemyMotionLockHandle genericAttackMotionLock;
        private RuntimeAnimatorController cachedAnimatorController;
        private AnimatorParameterSupport cachedAnimatorParameterSupport;

        public float AttackDamage => attackDamage;
        public bool IsMotionLocked => activeMotionLockId != 0;

        /// <summary>Begin an authoritative directed encounter without expanding ambient detection.</summary>
        public void BeginDirectorPursuit(Transform target)
        {
            if (!CanRunServerLogic() || !UsesGenericServerBrain || currentState == State.Dead || !IsValidTarget(target)) return;
            int targetIndex = PlayerProfiler.Instance?.GetProfileByTransform(target)?.playerIndex ?? -1;
            SetCurrentTarget(target, targetIndex);
            SwitchState(State.Chase);
        }
        protected Transform CurrentTarget => player;
        protected int CurrentTargetIndex => currentTargetIndex;
        protected float MaxTargetDistance => maxTargetDistance;
        protected float TimeSinceTargetSwitch => Time.time - lastTargetSwitchTime;

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
            CacheAnimatorParameterSupport();
            spatialAudio = GetComponent<SpecialSpatialAudio>();
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
            CancelMotionLock();
            replicatedState.OnValueChanged -= OnReplicatedStateChanged;
            base.OnNetworkDespawn();
        }

        protected virtual void OnDisable()
        {
            CancelMotionLock();
        }

        protected virtual void OnEnable()
        {
            if (animator != null)
            {
                CacheAnimatorParameterSupport();
                animator.Rebind();
                animator.Update(0f);
            }

            meleeAttack.Reset();
            CancelMotionLock();
            lastFramePosition = transform.position;
            hasLastFramePosition = true;
        }

        protected virtual void Update()
        {
            if (!CanRunServerLogic()) return;
            if (currentState == State.Dead) return;

            brainTickCount++;
            lastBrainTickTime = Time.time;
            UpdateMotionLock();

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
                ReleaseMotionLock(genericAttackMotionLock);
                genericAttackMotionLock = default;
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
                    ResumeAgentMotion();
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

        protected bool TrySubmitAgentDestination(Vector3 destination)
        {
            if (IsMotionLocked || !IsAgentReady()) return false;

            float minRepathDistance = Mathf.Max(0f, destinationRepathDistance);
            if (hasSubmittedAgentDestination &&
                (destination - lastSubmittedAgentDestination).sqrMagnitude < minRepathDistance * minRepathDistance)
            {
                return false;
            }

            if (agent.SetDestination(destination))
            {
                lastSubmittedAgentDestination = destination;
                hasSubmittedAgentDestination = true;
                lastAgentDestinationRequestTime = Time.time;
                agentDestinationRequestCount++;
                return true;
            }

            return false;
        }

        private void AttackBehavior()
        {
            if (player == null) return;
            if (!meleeAttack.TryBegin(transform, player, Time.time)) return;

            genericAttackMotionLock = AcquireMotionLock(attackActionLockDuration);

            if (animator != null)
                animator.SetTrigger(AnimAttack);

            if (attackSound != null)
                PlayLocalSound(attackSound, attackAudioMinDistance, attackAudioMaxDistance);

            BeginReplicatedAction();
        }

        public void ApplyAttackHit()
        {
            ProcessPendingAttackDamage(forceImpact: true);
        }

        protected virtual void FindPlayer(bool forceRefresh = false)
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

        protected virtual void UpdateTarget()
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
                SetCurrentTarget(bestTarget, bestIndex);
            }
        }

        protected virtual float ScoreTarget(PlayerProfile profile, int profileIndex, float distance)
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

            TrySetAnimatorFloat(AnimSpeed, speed, 0.08f, Time.deltaTime);
            TrySetAnimatorFloat(
                AnimLocomotionRate,
                locomotionCalibration.ResolvePlaybackRate(speed),
                0.08f,
                Time.deltaTime);
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
            CancelMotionLock();

            AttackSlotManager.Instance?.ReleaseSlot(this);

            StopAgentMotion();

            if (agent != null)
                agent.enabled = false;

            if (animator != null)
                animator.SetTrigger(AnimDead);

            if (deathSound != null)
                PlayLocalSound(deathSound, deathAudioMinDistance, deathAudioMaxDistance);

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
            CancelMotionLock();
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

        protected void PlayLocalSound(
            AudioClip clip,
            float minimumDistance = 3f,
            float maximumDistance = 30f,
            float volumeMultiplier = 1f)
        {
            if (clip == null)
                return;

            if (spatialAudio == null)
                spatialAudio = GetComponent<SpecialSpatialAudio>();
            if (spatialAudio == null)
                spatialAudio = gameObject.AddComponent<SpecialSpatialAudio>();

            spatialAudio.PlayOneShot(
                clip,
                soundVolume * Mathf.Max(0f, volumeMultiplier),
                minimumDistance,
                maximumDistance);
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
            {
                TrySetAnimatorFloat(AnimSpeed, current.normalizedSpeed / 255f * runSpeed);
                TrySetAnimatorFloat(
                    AnimLocomotionRate,
                    locomotionCalibration.ResolvePlaybackRate(current.normalizedSpeed / 255f * runSpeed));
            }

            bool newAction = force || current.actionSequence != lastPresentedActionSequence;
            int elapsedTicks = Mathf.Max(0, GetServerTick() - current.actionStartServerTick);
            int attackPresentationTicks = Mathf.CeilToInt(
                Mathf.Max(attackActionLockDuration, minimumAttackImpactDelay) * GetNetworkTickRate());

            if ((current.actionFlags & EnemyActionFlags.Dead) != 0
                && (force || (previous.actionFlags & EnemyActionFlags.Dead) == 0))
            {
                TrySetAnimatorTrigger(AnimDead);
                PlayLocalSound(deathSound, deathAudioMinDistance, deathAudioMaxDistance);
            }
            else if (newAction
                && (current.actionFlags & EnemyActionFlags.Attack) != 0
                && elapsedTicks <= attackPresentationTicks)
            {
                TrySetAnimatorTrigger(AnimAttack);
                PlayLocalSound(attackSound, attackAudioMinDistance, attackAudioMaxDistance);
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

        protected void TrySetAnimatorTrigger(int triggerHash)
        {
            // UnityEngine.Object can be a CLR-non-null destroyed/missing reference.
            // Use Unity's overloaded null check instead of the null-conditional operator.
            if (animator != null)
                animator.SetTrigger(triggerHash);
        }

        private void CacheAnimatorParameterSupport()
        {
            RuntimeAnimatorController controller = animator != null
                ? animator.runtimeAnimatorController
                : null;
            if (controller == cachedAnimatorController)
                return;

            cachedAnimatorController = controller;
            cachedAnimatorParameterSupport = default;
            if (controller == null)
                return;

            int controllerId = controller.GetEntityId().GetHashCode();
            if (AnimatorParameterSupportByController.TryGetValue(
                    controllerId,
                    out cachedAnimatorParameterSupport))
            {
                return;
            }

            bool hasSpeed = false;
            bool hasLocomotionRate = false;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                if (parameter.type != AnimatorControllerParameterType.Float)
                    continue;

                hasSpeed |= parameter.nameHash == AnimSpeed;
                hasLocomotionRate |= parameter.nameHash == AnimLocomotionRate;
            }

            cachedAnimatorParameterSupport = new AnimatorParameterSupport(
                hasSpeed,
                hasLocomotionRate);
            AnimatorParameterSupportByController[controllerId] = cachedAnimatorParameterSupport;
        }

        private void TrySetAnimatorFloat(
            int parameterHash,
            float value,
            float dampTime = 0f,
            float deltaTime = 0f)
        {
            if (animator == null)
                return;

            CacheAnimatorParameterSupport();
            bool supported = parameterHash == AnimSpeed
                ? cachedAnimatorParameterSupport.HasSpeed
                : parameterHash == AnimLocomotionRate
                    ? cachedAnimatorParameterSupport.HasLocomotionRate
                    : false;
            if (!supported)
            {
                WarnMissingAnimatorParameterOnce(parameterHash);
                return;
            }

            if (dampTime > 0f)
                animator.SetFloat(parameterHash, value, dampTime, deltaTime);
            else
                animator.SetFloat(parameterHash, value);
        }

        private void WarnMissingAnimatorParameterOnce(int parameterHash)
        {
            if (cachedAnimatorController == null)
                return;

            long warningKey = ((long)cachedAnimatorController.GetEntityId().GetHashCode() << 32)
                ^ (uint)parameterHash;
            if (!WarnedMissingAnimatorParameters.Add(warningKey))
                return;

            string parameterName = parameterHash == AnimSpeed
                ? "Speed"
                : parameterHash == AnimLocomotionRate
                    ? "LocomotionRate"
                    : parameterHash.ToString();
            GameLog.Warning(() =>
                $"[EnemyAI] '{name}' controller '{cachedAnimatorController.name}' has no float parameter '{parameterName}'. The related animation update will be skipped.");
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

        protected bool IsAgentReady()
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

        protected void StopAgentMotion()
        {
            if (!IsAgentReady())
                return;

            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.nextPosition = transform.position;
        }

        public EnemyMotionLockHandle AcquireMotionLock(float durationSeconds)
        {
            motionLockGeneration++;
            if (motionLockGeneration == 0)
                motionLockGeneration = 1;

            activeMotionLockId = motionLockGeneration;
            activeMotionLockDeadline = Time.time + Mathf.Max(0f, durationSeconds);
            StopAgentMotion();
            return new EnemyMotionLockHandle(activeMotionLockId);
        }

        public EnemyMotionLockHandle AcquireMotionLock(EnemyActionTiming timing)
        {
            return AcquireMotionLock(timing.MotionLockSeconds);
        }

        public bool ReleaseMotionLock(EnemyMotionLockHandle handle)
        {
            if (!handle.IsValid || handle.Id != activeMotionLockId)
                return false;

            activeMotionLockId = 0;
            activeMotionLockDeadline = 0f;
            ResumeAgentMotion();
            return true;
        }

        protected void CancelMotionLock()
        {
            activeMotionLockId = 0;
            activeMotionLockDeadline = 0f;
            genericAttackMotionLock = default;
            StopAgentMotion();
        }

        protected bool ResumeAgentMotion()
        {
            if (IsMotionLocked || !IsAgentReady())
                return false;

            agent.nextPosition = transform.position;
            agent.isStopped = false;
            return true;
        }

        private void UpdateMotionLock()
        {
            if (!IsMotionLocked)
                return;

            if (Time.time >= activeMotionLockDeadline)
            {
                ReleaseMotionLock(new EnemyMotionLockHandle(activeMotionLockId));
                return;
            }

            StopAgentMotion();
        }

        private bool CanHitTarget(Transform target)
        {
            return meleeAttack.CanHit(transform, target);
        }

        public void NotifyAttackSlotChanged()
        {
            ForcePathRefresh();
        }

        protected void ForcePathRefresh()
        {
            lastDestinationRequestTime = -Mathf.Infinity;
            hasSubmittedAgentDestination = false;
        }

        protected virtual float CalculateVisualMoveSpeed()
        {
            float speed = 0f;
            if (hasLastFramePosition && Time.deltaTime > 0.0001f)
            {
                Vector3 delta = transform.position - lastFramePosition;
                delta.y = 0f;
                speed = delta.magnitude / Time.deltaTime;
            }

            lastFramePosition = transform.position;
            hasLastFramePosition = true;

            return IsMotionLocked || currentState == State.Dead ? 0f : speed;
        }

        protected bool IsValidTarget(Transform target)
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

        protected void SetCurrentTarget(Transform target, int targetIndex, bool releaseAttackSlot = true)
        {
            if (releaseAttackSlot && target != player)
                AttackSlotManager.Instance?.ReleaseSlot(this);

            player = target;
            currentTargetIndex = targetIndex;
            lastTargetSwitchTime = Time.time;
            loggedMissingPlayer = target == null;
            ForcePathRefresh();
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

    }
}
