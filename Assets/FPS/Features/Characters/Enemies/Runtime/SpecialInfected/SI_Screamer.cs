using System.Collections;
using System.Collections.Generic;
using UniBT;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum ScreamerState
    {
        DetectTeam,
        SeekCover,
        Scream,
        Relocate,
        Dead
    }

    public class SI_Screamer : SpecialInfectedBase
    {
        private const float ScreamActionDuration = 2.783f;
        private const float PlannerInterval = 1f / 3f;

        [Header("Screamer Settings")]
        [SerializeField] private float detectionDistance = 30f;
        [SerializeField] private float escapeDistance = 14f;
        [SerializeField] private float destinationTolerance = 0.7f;
        [SerializeField] private LayerMask visibilityMask = Physics.DefaultRaycastLayers;

        [Header("Audio")]
        [SerializeField] private AudioClip screamSound;
        [SerializeField] private float screamVolume = 1f;

        private static readonly int ScreamTrigger = Animator.StringToHash("Scream");
        private static readonly EnemyActionTiming ScreamTiming = new(
            EnemyActionType.Scream,
            ScreamActionDuration,
            ScreamActionDuration,
            ScreamActionDuration);

        private readonly SpecialEscapePlanner escapePlanner = new();
        private ScreamerState screamerState = ScreamerState.DetectTeam;
        private bool isScreaming;
        private bool currentDestinationHidden;
        private float stateStartedAt;
        private float nextPlanTime;
        private Vector3 currentDestination;
        private Vector3 recentDestination;
        private EnemyMotionLockHandle screamMotionLock;
        private Coroutine screamRoutine;

        public bool IsScreaming => isScreaming;
        public bool IsAbilityReady => abilityReady;
        public ScreamerState CurrentScreamerState => screamerState;

        protected override bool UsesGenericServerBrain => false;
        protected override bool AutoTriggerPrimaryAbility => false;
        protected override bool PreserveAuthoredAgentSettings => true;

        protected override void Start()
        {
            specialType = SpecialType.Screamer;
            allowedInSoloMode = true;
            specialHPMultiplier = 1f;
            base.Start();
            DisableBehaviorTreeBrain();
            if (animator != null)
                animator.applyRootMotion = false;
        }

        public override void ResetAI()
        {
            CancelScream();
            specialType = SpecialType.Screamer;
            base.ResetAI();
            screamerState = ScreamerState.DetectTeam;
            isScreaming = false;
            currentDestinationHidden = false;
            stateStartedAt = 0f;
            nextPlanTime = 0f;
            currentDestination = Vector3.zero;
            recentDestination = Vector3.zero;
        }

        protected override void TickCustomServerBrain()
        {
            switch (screamerState)
            {
                case ScreamerState.DetectTeam:
                    DetectTeam();
                    break;
                case ScreamerState.SeekCover:
                    UpdateEscapeMovement(beginScreamAtDestination: true);
                    break;
                case ScreamerState.Relocate:
                    UpdateEscapeMovement(beginScreamAtDestination: false);
                    break;
            }
        }

        private void DetectTeam()
        {
            if (!abilityReady || !TryGetLivingTeam(out IReadOnlyList<PlayerProfile> observers))
                return;

            float detectionSqr = detectionDistance * detectionDistance;
            bool detected = false;
            for (int i = 0; i < observers.Count; i++)
            {
                PlayerProfile profile = observers[i];
                if (!IsLivingObserver(profile))
                    continue;
                if ((profile.playerTransform.position - transform.position).sqrMagnitude <= detectionSqr)
                {
                    detected = true;
                    break;
                }
            }

            if (!detected)
                return;

            EnterEscapeState(ScreamerState.SeekCover);
        }

        private void EnterEscapeState(ScreamerState nextState)
        {
            screamerState = nextState;
            stateStartedAt = Time.time;
            nextPlanTime = 0f;
            currentDestination = Vector3.zero;
            currentDestinationHidden = false;
            PlanEscape(true);
        }

        private void UpdateEscapeMovement(bool beginScreamAtDestination)
        {
            if (!TryGetLivingTeam(out _))
            {
                screamerState = ScreamerState.DetectTeam;
                StopAgentMotion();
                return;
            }

            bool destinationReached = currentDestination != Vector3.zero
                && Vector3.Distance(transform.position, currentDestination) <= destinationTolerance;
            bool pathFailed = !IsAgentReady()
                || currentDestination == Vector3.zero
                || !agent.hasPath
                || agent.pathStatus != UnityEngine.AI.NavMeshPathStatus.PathComplete;

            if (!destinationReached && (pathFailed || Time.time >= nextPlanTime))
                PlanEscape(false);

            if (!destinationReached)
                return;

            if (beginScreamAtDestination)
            {
                bool hiddenNow = !SpecialEscapePlanner.IsVisibleToAnyLivingPlayer(
                    transform.position + Vector3.up * 0.9f,
                    PlayerProfiler.Instance.AllProfiles,
                    visibilityMask);
                if (hiddenNow)
                    UseAbility();
                else
                    PlanEscape(false);
            }
            else
            {
                screamerState = ScreamerState.DetectTeam;
                StopAgentMotion();
            }
        }

        private void PlanEscape(bool force)
        {
            if (!force && Time.time < nextPlanTime)
                return;
            nextPlanTime = Time.time + PlannerInterval;

            IReadOnlyList<PlayerProfile> observers = PlayerProfiler.Instance?.AllProfiles;
            if (observers == null || observers.Count == 0)
                return;

            Vector3 teamCenter = Vector3.zero;
            int count = 0;
            for (int i = 0; i < observers.Count; i++)
            {
                if (!IsLivingObserver(observers[i]))
                    continue;
                teamCenter += observers[i].playerTransform.position;
                count++;
            }
            if (count == 0)
                return;

            if (!escapePlanner.TryPlan(
                    agent,
                    transform.position,
                    transform.position - teamCenter / count,
                    observers,
                    escapeDistance,
                    visibilityMask,
                    recentDestination,
                    out SpecialEscapePlan plan))
            {
                return;
            }

            recentDestination = currentDestination;
            currentDestination = plan.Destination;
            currentDestinationHidden = plan.FullyHidden;
            ResumeAgentMotion();
            TrySubmitAgentDestination(currentDestination);
        }

        public override void UseAbility()
        {
            if (!CanRunServerLogic() || isScreaming || !abilityReady || screamerState != ScreamerState.SeekCover)
                return;

            screamRoutine = StartCoroutine(ScreamRoutine());
        }

        private IEnumerator ScreamRoutine()
        {
            isScreaming = true;
            screamerState = ScreamerState.Scream;
            lastAbilityTime = Time.time;
            screamMotionLock = AcquireMotionLock(ScreamTiming);

            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialAbilityReplicated(true, now + ScreamTiming.PresentationSeconds);
            TrySetAnimatorTrigger(ScreamTrigger);
            PlayLocalSound(screamSound, 4f, 60f, screamVolume);
            SpecialThreatSignal.Raise(transform.position, ScreamTiming.PresentationSeconds);

            yield return new WaitForSeconds(ScreamTiming.MotionLockSeconds);

            if (this == null || screamerState == ScreamerState.Dead)
                yield break;

            RequestReinforcements();
            ReleaseMotionLock(screamMotionLock);
            screamMotionLock = default;
            isScreaming = false;
            screamRoutine = null;
            SetSpecialAbilityReplicated(false);
            EnterEscapeState(ScreamerState.Relocate);
        }

        private void RequestReinforcements()
        {
            if (AIDirector.Instance != null)
            {
                AIDirector.Instance.RequestCrescendo("Screamer call", 12f);
                return;
            }

            if (ZombieFactory.Instance == null)
                return;
            int count = 4 + EnemyScalingResolver.ClampPlayerCount(CapturedSpawnPlayerCount);
            for (int i = 0; i < count; i++)
                ZombieFactory.Instance.SpawnZombieAtFairPressurePosition();
        }

        private void CancelScream()
        {
            if (screamRoutine != null)
            {
                StopCoroutine(screamRoutine);
                screamRoutine = null;
            }
            ReleaseMotionLock(screamMotionLock);
            screamMotionLock = default;
            isScreaming = false;
            ClearSpecialActionReplicated();
        }

        public override void OnDeath()
        {
            if (screamerState == ScreamerState.Dead)
                return;
            CancelScream();
            screamerState = ScreamerState.Dead;
            base.OnDeath();
        }

        public override void OnNetworkDespawn()
        {
            CancelScream();
            base.OnNetworkDespawn();
        }

        protected override void OnReplicatedSpecialAbilityStarted(int elapsedTicks)
        {
            isScreaming = true;
            screamerState = ScreamerState.Scream;
            TrySetAnimatorTrigger(ScreamTrigger);
            PlayLocalSound(screamSound, 4f, 60f, screamVolume);
            SpecialThreatSignal.Raise(transform.position, ScreamTiming.PresentationSeconds);
        }

        protected override void OnReplicatedSpecialAbilityEnded()
        {
            isScreaming = false;
        }

        protected override EnemyLocomotionState ResolveReplicatedLocomotion()
        {
            return screamerState switch
            {
                ScreamerState.SeekCover => EnemyLocomotionState.Moving,
                ScreamerState.Relocate => EnemyLocomotionState.Moving,
                ScreamerState.Scream => EnemyLocomotionState.Attacking,
                ScreamerState.Dead => EnemyLocomotionState.Dead,
                _ => EnemyLocomotionState.Idle
            };
        }

        private static bool TryGetLivingTeam(out IReadOnlyList<PlayerProfile> observers)
        {
            observers = PlayerProfiler.Instance?.AllProfiles;
            if (observers == null)
                return false;
            for (int i = 0; i < observers.Count; i++)
            {
                if (IsLivingObserver(observers[i]))
                    return true;
            }
            return false;
        }

        private static bool IsLivingObserver(PlayerProfile profile)
        {
            if (profile?.playerTransform == null || !profile.playerTransform.gameObject.activeInHierarchy)
                return false;
            PlayerHealth health = profile.cachedHealth;
            if (health == null)
                profile.playerTransform.TryGetComponent(out health);
            return health != null && !health.IsDead && health.LifeState == PlayerLifeState.Alive;
        }

        private void DisableBehaviorTreeBrain()
        {
            BehaviorTree behaviorTree = GetComponent<BehaviorTree>();
            if (behaviorTree != null)
                behaviorTree.enabled = false;
        }
    }
}
