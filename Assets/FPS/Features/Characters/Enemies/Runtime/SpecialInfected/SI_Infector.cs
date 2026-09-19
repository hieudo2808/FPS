using System.Collections;
using System.Collections.Generic;
using UniBT;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum InfectorState
    {
        Search,
        Stalk,
        Approach,
        Implant,
        Retreat,
        Cooldown,
        Dead
    }

    public class SI_Infector : SpecialInfectedBase
    {
        private const float DefaultRunSpeed = 5.75f;
        private const float DefaultRetreatSpeed = 6.2f;
        private const float SoloMaxHealth = 500f;
        private const float ImplantArcDegrees = 120f;
        private const float ImplantActionDuration = 1.625f;
        private const float HiddenConfirmationDuration = 1.5f;
        private const float RetreatReplanInterval = 1f / 3f;

        [Header("Infector Combat Settings")]
        [SerializeField] private float implantDamage = 15f;
        [SerializeField] private float implantInfectionAmount = 30f;
        [SerializeField] private float implantRange = 2.2f;
        [SerializeField] private float implantWindup = 0.5f;
        [SerializeField] private float implantCooldown = 12f;
        [SerializeField] private float retreatDuration = 8f;
        [SerializeField] private float retreatDistance = 12f;
        [SerializeField] private float stalkDistance = 8f;
        [SerializeField] private LayerMask visibilityMask = Physics.DefaultRaycastLayers;

        [Header("Audio")]
        [SerializeField] private AudioClip stalkHissSound;
        [SerializeField] private AudioClip implantWindupSound;
        [SerializeField] private AudioClip implantStabSound;
        [SerializeField] private AudioClip roarSound;
        [SerializeField] private float audioVolume = 1f;

        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimRoar = Animator.StringToHash("Roar");
        private static readonly EnemyActionTiming ImplantTiming = new(
            EnemyActionType.Implant,
            0.5f,
            ImplantActionDuration,
            ImplantActionDuration);

        private readonly SpecialEscapePlanner escapePlanner = new();
        private InfectorState currentInfectorState = InfectorState.Search;
        private Transform currentTargetTransform;
        private PlayerProfile currentTargetProfile;
        private float stateStartTime;
        private float retreatStartedAt;
        private float hiddenSince = -1f;
        private float nextRetreatPlanTime;
        private Vector3 lastRetreatDestination;
        private bool isPerformingImplant;
        private bool lastRetreatSucceeded;
        private EnemyMotionLockHandle implantMotionLock;
        private Coroutine activeImplantRoutine;
        private Coroutine activePresentationRoutine;

        public InfectorState CurrentState => currentInfectorState;
        public float ImplantDamage => implantDamage;
        public float EffectiveImplantDamage => ScaleDamage(implantDamage);
        public float ImplantInfectionAmount => implantInfectionAmount;
        public float EffectiveImplantInfectionAmount => ScaleStatus(implantInfectionAmount);
        public float ImplantRange => implantRange;
        public float ImplantWindup => implantWindup;
        public float ImplantCooldown => implantCooldown;
        public float RetreatDuration => retreatDuration;
        public float FixedMaxHealth => SoloMaxHealth;
        public bool IsPerformingImplant => isPerformingImplant;
        public bool LastRetreatSucceeded => lastRetreatSucceeded;

        protected override bool UsesGenericServerBrain => false;
        protected override bool AutoTriggerPrimaryAbility => false;
        protected override bool PreserveAuthoredAgentSettings => true;

        protected override void Start()
        {
            specialType = SpecialType.Infector;
            allowedInSoloMode = true;
            specialHPMultiplier = 1f;
            abilityCooldown = implantCooldown;
            base.Start();
            DisableBehaviorTreeBrain();
            if (animator != null)
                animator.applyRootMotion = false;
        }

        public override void ResetAI()
        {
            CancelActiveCoroutines();
            specialType = SpecialType.Infector;
            abilityCooldown = implantCooldown;
            base.ResetAI();
            currentInfectorState = InfectorState.Search;
            currentTargetTransform = null;
            currentTargetProfile = null;
            isPerformingImplant = false;
            retreatStartedAt = 0f;
            hiddenSince = -1f;
            nextRetreatPlanTime = 0f;
            lastRetreatDestination = Vector3.zero;
            lastRetreatSucceeded = false;
            if (agent != null && agent.enabled)
                agent.speed = DefaultRunSpeed;
        }

        protected override void TickCustomServerBrain()
        {
            switch (currentInfectorState)
            {
                case InfectorState.Search:
                    UpdateSearchState();
                    break;
                case InfectorState.Stalk:
                    UpdateStalkState();
                    break;
                case InfectorState.Approach:
                    UpdateApproachState();
                    break;
                case InfectorState.Retreat:
                    UpdateRetreatState();
                    break;
                case InfectorState.Cooldown:
                    UpdateCooldownState();
                    break;
            }
        }

        private void UpdateSearchState()
        {
            SelectBestTarget();
            if (currentTargetTransform == null)
                return;

            currentInfectorState = InfectorState.Stalk;
            stateStartTime = Time.time;
            TrySetAnimatorTrigger(AnimRoar);
            PlayLocalSound(roarSound, 4f, 35f, audioVolume);
        }

        private void UpdateStalkState()
        {
            if (!IsTargetValid())
            {
                currentInfectorState = InfectorState.Search;
                return;
            }

            float distance = Vector3.Distance(transform.position, currentTargetTransform.position);
            if (agent != null)
                agent.speed = DefaultRunSpeed * 0.85f;
            TrySubmitAgentDestination(currentTargetTransform.position);

            if (distance <= stalkDistance || Time.time - stateStartTime > 2.5f)
            {
                currentInfectorState = InfectorState.Approach;
                stateStartTime = Time.time;
                PlayLocalSound(stalkHissSound, 2f, 20f, audioVolume * 0.7f);
            }
        }

        private void UpdateApproachState()
        {
            if (!IsTargetValid())
            {
                currentInfectorState = InfectorState.Search;
                return;
            }

            if (agent != null)
                agent.speed = DefaultRunSpeed;
            TrySubmitAgentDestination(currentTargetTransform.position);

            if (Vector3.Distance(transform.position, currentTargetTransform.position) <= implantRange
                && abilityReady
                && !isPerformingImplant)
            {
                StartImplantAttack();
            }
        }

        private void StartImplantAttack()
        {
            if (isPerformingImplant || !CanRunServerLogic())
                return;

            currentInfectorState = InfectorState.Implant;
            activeImplantRoutine = StartCoroutine(ImplantRoutine());
        }

        private IEnumerator ImplantRoutine()
        {
            isPerformingImplant = true;
            lastAbilityTime = Time.time;
            implantMotionLock = AcquireMotionLock(ImplantTiming);
            TrySetAnimatorTrigger(AnimAttack);
            PlayLocalSound(implantWindupSound, 2f, 15f, audioVolume);

            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialAbilityReplicated(true, now + ImplantTiming.PresentationSeconds);

            yield return new WaitForSeconds(ImplantTiming.ImpactSeconds);
            PlayLocalSound(implantStabSound, 2f, 15f, audioVolume);
            ApplyImplantImpact();

            float recovery = ImplantTiming.MotionLockSeconds - ImplantTiming.ImpactSeconds;
            if (recovery > 0f)
                yield return new WaitForSeconds(recovery);

            ReleaseMotionLock(implantMotionLock);
            implantMotionLock = default;
            isPerformingImplant = false;
            activeImplantRoutine = null;
            SetSpecialAbilityReplicated(false);
            BeginRetreat();
        }

        private void ApplyImplantImpact()
        {
            if (!CanImpactCurrentTarget())
                return;

            float damage = ScaleDamage(implantDamage);
            float infectionAmount = ScaleStatus(implantInfectionAmount);
            PlayerHealth playerHealth = currentTargetProfile?.cachedHealth;
            if (playerHealth == null)
                currentTargetTransform.TryGetComponent(out playerHealth);
            playerHealth?.TakeDamage(damage);

            PlayerInfectionController infection = currentTargetProfile?.cachedInfection;
            if (infection == null)
                currentTargetTransform.TryGetComponent(out infection);
            infection?.AddInfectionServer(infectionAmount);
            GameLog.Info(() => $"[Infector] Implant hit: damage={damage:F1}, infection={infectionAmount:F1}");
        }

        private void BeginRetreat()
        {
            currentInfectorState = InfectorState.Retreat;
            retreatStartedAt = Time.time;
            hiddenSince = -1f;
            nextRetreatPlanTime = 0f;
            lastRetreatSucceeded = false;
            if (agent != null)
                agent.speed = DefaultRetreatSpeed;
            ReplanRetreat(true);
        }

        private void UpdateRetreatState()
        {
            IReadOnlyList<PlayerProfile> observers = GetObservers();
            bool visible = SpecialEscapePlanner.IsVisibleToAnyLivingPlayer(
                transform.position + Vector3.up * 0.9f,
                observers,
                visibilityMask);

            if (visible)
            {
                hiddenSince = -1f;
                ReplanRetreat(false);
            }
            else
            {
                if (hiddenSince < 0f)
                    hiddenSince = Time.time;
                if (Time.time - hiddenSince >= HiddenConfirmationDuration)
                {
                    lastRetreatSucceeded = true;
                    EnterCooldown();
                    return;
                }
            }

            bool pathFinished = !IsAgentReady()
                || !agent.hasPath
                || agent.pathStatus != UnityEngine.AI.NavMeshPathStatus.PathComplete
                || agent.remainingDistance <= Mathf.Max(agent.stoppingDistance + 0.2f, 0.4f);
            if (pathFinished)
                ReplanRetreat(false);

            if (Time.time - retreatStartedAt >= Mathf.Max(8f, retreatDuration))
                FailRetreatToStalk();
        }

        private void ReplanRetreat(bool force)
        {
            if (!force && Time.time < nextRetreatPlanTime)
                return;

            nextRetreatPlanTime = Time.time + RetreatReplanInterval;
            IReadOnlyList<PlayerProfile> observers = GetObservers();
            if (!escapePlanner.TryPlan(
                    agent,
                    transform.position,
                    ResolveFleeDirection(observers),
                    observers,
                    retreatDistance,
                    visibilityMask,
                    lastRetreatDestination,
                    out SpecialEscapePlan plan))
            {
                return;
            }

            lastRetreatDestination = plan.Destination;
            ResumeAgentMotion();
            TrySubmitAgentDestination(plan.Destination);
        }

        private Vector3 ResolveFleeDirection(IReadOnlyList<PlayerProfile> observers)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            if (observers != null)
            {
                for (int i = 0; i < observers.Count; i++)
                {
                    PlayerProfile profile = observers[i];
                    if (!IsLivingProfile(profile))
                        continue;
                    center += profile.playerTransform.position;
                    count++;
                }
            }

            if (count > 0)
                return transform.position - center / count;
            return currentTargetTransform != null
                ? transform.position - currentTargetTransform.position
                : -transform.forward;
        }

        private void FailRetreatToStalk()
        {
            lastRetreatSucceeded = false;
            currentInfectorState = InfectorState.Stalk;
            stateStartTime = Time.time;
            hiddenSince = -1f;
            if (!IsTargetValid())
                SelectBestTarget();
        }

        private void EnterCooldown()
        {
            currentInfectorState = InfectorState.Cooldown;
            stateStartTime = Time.time;
            if (IsAgentReady())
            {
                agent.ResetPath();
                ResumeAgentMotion();
            }
        }

        private void UpdateCooldownState()
        {
            if (abilityReady)
                currentInfectorState = InfectorState.Search;
        }

        public override void UseAbility()
        {
            if (CanUseAbility())
                StartImplantAttack();
        }

        protected override bool CanUseAbility()
        {
            return currentInfectorState == InfectorState.Approach
                && !isPerformingImplant
                && abilityReady
                && IsTargetValid()
                && Vector3.Distance(transform.position, currentTargetTransform.position) <= implantRange;
        }

        private bool CanImpactCurrentTarget()
        {
            return IsTargetValid()
                && CanImpactTarget(transform, currentTargetTransform, implantRange, ImplantArcDegrees);
        }

        public static bool CanImpactTarget(Transform attacker, Transform target, float range, float arcDegrees)
        {
            if (attacker == null || target == null || range <= 0f)
                return false;

            Vector3 origin = attacker.position + Vector3.up;
            Vector3 targetPoint = target.position + Vector3.up;
            Vector3 toTarget = targetPoint - origin;
            if (toTarget.sqrMagnitude > range * range)
                return false;

            Vector3 flatForward = Vector3.ProjectOnPlane(attacker.forward, Vector3.up);
            Vector3 flatTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flatTarget.sqrMagnitude > 0.0001f
                && Vector3.Angle(flatForward, flatTarget) > arcDegrees * 0.5f)
            {
                return false;
            }

            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == target || hit.transform.IsChildOf(target);
            }

            return true;
        }

        private void SelectBestTarget()
        {
            IReadOnlyList<PlayerProfile> profiles = GetObservers();
            PlayerProfile bestProfile = null;
            float highestScore = float.MinValue;
            if (profiles != null)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    PlayerProfile profile = profiles[i];
                    if (!IsLivingProfile(profile))
                        continue;

                    float score = CalculateTargetScore(profile);
                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestProfile = profile;
                    }
                }
            }

            currentTargetProfile = bestProfile;
            currentTargetTransform = bestProfile?.playerTransform;
        }

        public float CalculateTargetScore(PlayerProfile profile)
        {
            if (profile?.playerTransform == null)
                return float.MinValue;

            float distance = Vector3.Distance(transform.position, profile.playerTransform.position);
            float distanceScore = Mathf.Clamp01(1f - distance / 40f) * 2f;
            float isolationScore = profile.isIsolated ? 2f : 0f;
            float healthScore = Mathf.Clamp01(1f - profile.currentHealth / 100f) * 0.8f;
            float reloadScore = profile.isReloading ? 0.6f : 0f;
            float ammoScore = Mathf.Clamp01(1f - profile.currentAmmoPercent) * 0.3f;
            float campingScore = profile.isCamping ? 0.4f : 0f;
            float infectedPenalty = 0f;

            PlayerInfectionController infection = profile.cachedInfection;
            if (infection == null && profile.playerTransform != null)
                profile.playerTransform.TryGetComponent(out infection);
            if (infection != null && infection.IsInfected)
                infectedPenalty = 3f * infection.CurrentInfection / 100f;

            float teammateClosenessPenalty = profile.distanceToNearestAlly < 4f ? 0.8f : 0f;
            return distanceScore + isolationScore + healthScore + reloadScore + ammoScore
                + campingScore - infectedPenalty - teammateClosenessPenalty;
        }

        private bool IsTargetValid()
        {
            return IsLivingProfile(currentTargetProfile)
                && currentTargetProfile.playerTransform == currentTargetTransform;
        }

        private static bool IsLivingProfile(PlayerProfile profile)
        {
            if (profile?.playerTransform == null || !profile.playerTransform.gameObject.activeInHierarchy)
                return false;
            PlayerHealth health = profile.cachedHealth;
            if (health == null)
                profile.playerTransform.TryGetComponent(out health);
            return health != null && !health.IsDead && health.LifeState == PlayerLifeState.Alive;
        }

        private static IReadOnlyList<PlayerProfile> GetObservers()
        {
            return PlayerProfiler.Instance != null ? PlayerProfiler.Instance.AllProfiles : null;
        }

        protected override void OnReplicatedSpecialAbilityStarted(int elapsedTicks)
        {
            isPerformingImplant = true;
            TrySetAnimatorTrigger(AnimAttack);

            float elapsedSeconds = elapsedTicks / (float)GetPresentationTickRate();
            if (elapsedSeconds < ImplantTiming.ImpactSeconds)
                PlayLocalSound(implantWindupSound, 2f, 15f, audioVolume);

            if (activePresentationRoutine != null)
                StopCoroutine(activePresentationRoutine);
            activePresentationRoutine = StartCoroutine(PresentReplicatedImpact(elapsedSeconds));
        }

        protected override void OnReplicatedSpecialAbilityEnded()
        {
            isPerformingImplant = false;
            if (activePresentationRoutine != null)
            {
                StopCoroutine(activePresentationRoutine);
                activePresentationRoutine = null;
            }
        }

        private IEnumerator PresentReplicatedImpact(float elapsedSeconds)
        {
            float remaining = Mathf.Max(0f, ImplantTiming.ImpactSeconds - elapsedSeconds);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            if (elapsedSeconds <= ImplantTiming.PresentationSeconds)
                PlayLocalSound(implantStabSound, 2f, 15f, audioVolume);
            activePresentationRoutine = null;
        }

        private int GetPresentationTickRate()
        {
            return NetworkManager != null && NetworkManager.NetworkConfig != null
                ? Mathf.Max(1, (int)NetworkManager.NetworkConfig.TickRate)
                : NetworkGameplayPolicy.SimulationHz;
        }

        private void CancelActiveCoroutines()
        {
            if (activeImplantRoutine != null)
            {
                StopCoroutine(activeImplantRoutine);
                activeImplantRoutine = null;
            }
            if (activePresentationRoutine != null)
            {
                StopCoroutine(activePresentationRoutine);
                activePresentationRoutine = null;
            }

            ReleaseMotionLock(implantMotionLock);
            implantMotionLock = default;
            ClearSpecialActionReplicated();
        }

        public override void OnDeath()
        {
            if (currentInfectorState == InfectorState.Dead)
                return;

            CancelActiveCoroutines();
            isPerformingImplant = false;
            currentInfectorState = InfectorState.Dead;
            base.OnDeath();
        }

        public override void OnNetworkDespawn()
        {
            CancelActiveCoroutines();
            isPerformingImplant = false;
            base.OnNetworkDespawn();
        }

        protected override float CalculateVisualMoveSpeed() => base.CalculateVisualMoveSpeed();

        protected override Vector3 GetLookDirection()
        {
            if (currentInfectorState == InfectorState.Implant && currentTargetTransform != null)
                return currentTargetTransform.position - transform.position;
            return base.GetLookDirection();
        }

        protected override bool ShouldRotateForPresentation()
        {
            return currentInfectorState != InfectorState.Search
                && currentInfectorState != InfectorState.Dead;
        }

        protected override EnemyLocomotionState ResolveReplicatedLocomotion()
        {
            return currentInfectorState switch
            {
                InfectorState.Stalk => EnemyLocomotionState.Moving,
                InfectorState.Approach => EnemyLocomotionState.Moving,
                InfectorState.Retreat => EnemyLocomotionState.Moving,
                InfectorState.Implant => EnemyLocomotionState.Attacking,
                InfectorState.Dead => EnemyLocomotionState.Dead,
                _ => EnemyLocomotionState.Idle
            };
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            if (!IsLivingProfile(profile))
                return false;
            PlayerInfectionController infection = profile.cachedInfection;
            if (infection == null)
                profile.playerTransform.TryGetComponent(out infection);
            return infection != null && !infection.IsCritical;
        }

        public override bool ShouldSpawnForTeam(
            IReadOnlyList<PlayerProfile> profiles,
            IReadOnlyList<PlayerTeamHealthSnapshot> teamHealth)
        {
            if (profiles == null || profiles.Count == 0 || teamHealth == null || teamHealth.Count != profiles.Count)
                return false;

            float totalInfection = 0f;
            for (int i = 0; i < profiles.Count; i++)
            {
                PlayerProfile profile = profiles[i];
                if (!IsLivingProfile(profile) || teamHealth[i].IsDownOrDead)
                    return false;
                PlayerInfectionController infection = profile.cachedInfection;
                if (infection == null || infection.CurrentStage >= InfectionStage.Critical)
                    return false;
                totalInfection += infection.CurrentInfection;
            }

            return totalInfection / profiles.Count < 50f;
        }

        private void DisableBehaviorTreeBrain()
        {
            BehaviorTree behaviorTree = GetComponent<BehaviorTree>();
            if (behaviorTree != null)
                behaviorTree.enabled = false;
        }
    }
}
