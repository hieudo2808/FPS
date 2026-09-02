using System.Collections;
using System.Collections.Generic;
using UniBT;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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
        private const float DefaultRunSpeed = 5.75f; // ~1.15x common zombie
        private const float DefaultRetreatSpeed = 6.2f;
        private const float AuthoredMaxHealth = 200f;
        private const float ImplantArcDegrees = 120f;
        private const float ImplantRecovery = 0.4f;

        [Header("Infector Combat Settings")]
        [SerializeField] private float implantDamage = 15f;
        [SerializeField] private float implantInfectionAmount = 30f;
        [SerializeField] private float implantRange = 2.2f;
        [SerializeField] private float implantWindup = 0.5f;
        [SerializeField] private float implantCooldown = 12f;
        [SerializeField] private float retreatDuration = 5f;
        [SerializeField] private float retreatDistance = 12f;
        [SerializeField] private float stalkDistance = 8f;

        [Header("Audio")]
        [SerializeField] private AudioClip stalkHissSound;
        [SerializeField] private AudioClip implantWindupSound;
        [SerializeField] private AudioClip implantStabSound;
        [SerializeField] private AudioClip roarSound;
        [SerializeField] private float audioVolume = 1f;

        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimRoar = Animator.StringToHash("Roar");

        private InfectorState currentInfectorState = InfectorState.Search;
        private Transform currentTargetTransform;
        private PlayerProfile currentTargetProfile;
        private float stateStartTime;
        private float retreatEndTime;
        private bool isPerformingImplant;
        private Coroutine activeImplantRoutine;
        private Coroutine activePresentationRoutine;

        public InfectorState CurrentState => currentInfectorState;
        public float ImplantDamage => implantDamage;
        public float ImplantInfectionAmount => implantInfectionAmount;
        public float ImplantRange => implantRange;
        public float ImplantWindup => implantWindup;
        public float ImplantCooldown => implantCooldown;
        public float RetreatDuration => retreatDuration;
        public float FixedMaxHealth => AuthoredMaxHealth;
        public bool IsPerformingImplant => isPerformingImplant;

        protected override bool UsesGenericServerBrain => false;
        protected override bool AutoTriggerPrimaryAbility => false;
        protected override bool PreserveAuthoredAgentSettings => true;

        protected override void Start()
        {
            base.Start();
        }

        protected override float CalculateMaxHealth(int playerCount, float authoredMaxHealth)
        {
            return AuthoredMaxHealth;
        }

        public override void ResetAI()
        {
            base.ResetAI();
            CancelActiveCoroutines();
            currentInfectorState = InfectorState.Search;
            currentTargetTransform = null;
            currentTargetProfile = null;
            isPerformingImplant = false;
        }

        protected override void TickCustomServerBrain()
        {
            UpdateInfectorStateMachine();
        }

        private void UpdateInfectorStateMachine()
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
                case InfectorState.Implant:
                    // Handled via Coroutine
                    break;
                case InfectorState.Retreat:
                    UpdateRetreatState();
                    break;
                case InfectorState.Cooldown:
                    UpdateCooldownState();
                    break;
                case InfectorState.Dead:
                    break;
            }
        }

        // =========================================================
        // STATE TRANSITIONS & LOGIC
        // =========================================================
        private void UpdateSearchState()
        {
            SelectBestTarget();

            if (currentTargetTransform != null)
            {
                currentInfectorState = InfectorState.Stalk;
                stateStartTime = Time.time;
            }
        }

        private void UpdateStalkState()
        {
            if (!IsTargetValid())
            {
                currentInfectorState = InfectorState.Search;
                return;
            }

            float dist = Vector3.Distance(transform.position, currentTargetTransform.position);

            // Move towards stalk position (flanking/medium range)
            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = DefaultRunSpeed * 0.85f;
                agent.SetDestination(currentTargetTransform.position);
            }

            // If close enough or stalked for > 2 seconds, transition to Approach
            if (dist <= stalkDistance || Time.time - stateStartTime > 2.5f)
            {
                currentInfectorState = InfectorState.Approach;
                stateStartTime = Time.time;

                if (stalkHissSound != null && AudioManager.Instance != null)
                    AudioManager.Instance.PlaySFXSound(stalkHissSound, audioVolume * 0.7f);
            }
        }

        private void UpdateApproachState()
        {
            if (!IsTargetValid())
            {
                currentInfectorState = InfectorState.Search;
                return;
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = DefaultRunSpeed;
                agent.SetDestination(currentTargetTransform.position);
            }

            float dist = Vector3.Distance(transform.position, currentTargetTransform.position);
            if (dist <= implantRange && abilityReady && !isPerformingImplant && IsTargetValid())
            {
                StartImplantAttack();
            }
        }

        private void StartImplantAttack()
        {
            if (isPerformingImplant) return;

            currentInfectorState = InfectorState.Implant;
            activeImplantRoutine = StartCoroutine(ImplantRoutine());
        }

        private IEnumerator ImplantRoutine()
        {
            isPerformingImplant = true;
            lastAbilityTime = Time.time;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            if (animator != null)
                animator.SetTrigger(AnimAttack);

            // Windup presentation starts on the same authoritative action start tick.
            if (implantWindupSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(implantWindupSound, audioVolume);

            // Replicate action to clients
            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialAbilityReplicated(true, now + implantWindup + ImplantRecovery);

            yield return new WaitForSeconds(implantWindup);

            if (implantStabSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(implantStabSound, audioVolume);

            if (CanImpactCurrentTarget())
            {
                if (currentTargetProfile?.cachedHealth != null)
                {
                    currentTargetProfile.cachedHealth.TakeDamage(implantDamage);
                }
                else if (currentTargetTransform.TryGetComponent<PlayerHealth>(out var playerHealth))
                {
                    playerHealth.TakeDamage(implantDamage);
                }

                PlayerInfectionController infection = currentTargetProfile?.cachedInfection;
                if (infection == null)
                    currentTargetTransform.TryGetComponent(out infection);
                infection?.AddInfectionServer(implantInfectionAmount);
                GameLog.Info(() => $"[Infector] Successfully implanted parasite into target (+{implantInfectionAmount}%)");
            }

            yield return new WaitForSeconds(ImplantRecovery);

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }

            isPerformingImplant = false;
            activeImplantRoutine = null;
            SetSpecialAbilityReplicated(false);

            // Transition to Retreat immediately after implant attempt
            BeginRetreat();
        }

        private void BeginRetreat()
        {
            currentInfectorState = InfectorState.Retreat;
            retreatEndTime = Time.time + retreatDuration;

            if (currentTargetTransform != null && agent != null && agent.isOnNavMesh)
            {
                agent.speed = DefaultRetreatSpeed;
                Vector3 fleeDirection = (transform.position - currentTargetTransform.position).normalized;
                if (!TrySetCompleteRetreatPath(fleeDirection))
                    EnterCooldown();
            }
            else
                EnterCooldown();
        }

        private void UpdateRetreatState()
        {
            if (Time.time >= retreatEndTime)
            {
                EnterCooldown();
            }
        }

        private void EnterCooldown()
        {
            currentInfectorState = InfectorState.Cooldown;
            stateStartTime = Time.time;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
        }

        private bool TrySetCompleteRetreatPath(Vector3 preferredDirection)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return false;

            Vector3 flatPreferred = preferredDirection;
            flatPreferred.y = 0f;
            if (flatPreferred.sqrMagnitude < 0.0001f)
                flatPreferred = -transform.forward;
            flatPreferred.Normalize();

            var path = new NavMeshPath();
            for (int i = 0; i < 8; i++)
            {
                float angle = i == 0 ? 0f : ((i + 1) / 2) * 45f * (i % 2 == 1 ? 1f : -1f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * flatPreferred;
                Vector3 candidate = transform.position + direction * retreatDistance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;

                agent.isStopped = false;
                return agent.SetPath(path);
            }

            return false;
        }

        private void UpdateCooldownState()
        {
            if (abilityReady)
            {
                currentInfectorState = InfectorState.Search;
            }
        }

        public override void UseAbility()
        {
            if (currentInfectorState == InfectorState.Approach || currentInfectorState == InfectorState.Stalk)
            {
                StartImplantAttack();
            }
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
            if (!IsTargetValid()) return false;
            return CanImpactTarget(transform, currentTargetTransform, implantRange, ImplantArcDegrees);
        }

        public static bool CanImpactTarget(Transform attacker, Transform target, float range, float arcDegrees)
        {
            if (attacker == null || target == null || range <= 0f) return false;

            Vector3 origin = attacker.position + Vector3.up;
            Vector3 targetPoint = target.position + Vector3.up;
            Vector3 toTarget = targetPoint - origin;
            if (toTarget.sqrMagnitude > range * range) return false;

            Vector3 flatForward = attacker.forward;
            flatForward.y = 0f;
            Vector3 flatTarget = toTarget;
            flatTarget.y = 0f;
            if (flatTarget.sqrMagnitude > 0.0001f
                && Vector3.Angle(flatForward, flatTarget) > arcDegrees * 0.5f)
                return false;

            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == target || hit.transform.IsChildOf(target);
            }

            return true;
        }

        // =========================================================
        // TARGET SELECTION UTILITY SCORING
        // =========================================================
        private void SelectBestTarget()
        {
            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
            {
                currentTargetTransform = null;
                currentTargetProfile = null;
                return;
            }

            var profiles = PlayerProfiler.Instance.AllProfiles;
            PlayerProfile bestProfile = null;
            float highestScore = float.MinValue;

            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile?.playerTransform == null) continue;

                // Ignore dead or downed players
                if (profile.cachedHealth != null && (profile.cachedHealth.IsDead || profile.cachedHealth.LifeState != PlayerLifeState.Alive))
                    continue;

                float score = CalculateTargetScore(profile);
                if (score > highestScore)
                {
                    highestScore = score;
                    bestProfile = profile;
                }
            }

            if (bestProfile != null)
            {
                currentTargetProfile = bestProfile;
                currentTargetTransform = bestProfile.playerTransform;
            }
        }

        public float CalculateTargetScore(PlayerProfile profile)
        {
            if (profile?.playerTransform == null) return float.MinValue;

            float distance = Vector3.Distance(transform.position, profile.playerTransform.position);
            float distanceScore = Mathf.Clamp01(1f - (distance / 40f)) * 2.0f;

            float isolationScore = profile.isIsolated ? 2.0f : 0f;
            float healthScore = Mathf.Clamp01(1f - (profile.currentHealth / 100f)) * 0.8f;
            float reloadScore = profile.isReloading ? 0.6f : 0f;
            float campingScore = profile.isCamping ? 0.4f : 0f;

            // Heavily penalize already infected players so Infector spreads infection across the team
            float infectedPenalty = 0f;
            if (profile.cachedInfection != null)
            {
                if (profile.cachedInfection.IsInfected)
                {
                    infectedPenalty = 3.0f * (profile.cachedInfection.CurrentInfection / 100f);
                }
            }
            else if (profile.playerTransform != null && profile.playerTransform.TryGetComponent<PlayerInfectionController>(out var infection))
            {
                if (infection.IsInfected)
                {
                    infectedPenalty = 3.0f * (infection.CurrentInfection / 100f);
                }
            }

            float teammateClosenessPenalty = profile.distanceToNearestAlly < 4.0f ? 0.8f : 0f;

            return distanceScore + isolationScore + healthScore + reloadScore + campingScore - infectedPenalty - teammateClosenessPenalty;
        }

        private bool IsTargetValid()
        {
            if (currentTargetTransform == null) return false;
            PlayerHealth health = currentTargetProfile?.cachedHealth;
            if (health == null)
                currentTargetTransform.TryGetComponent(out health);
            return health != null && !health.IsDead && health.LifeState == PlayerLifeState.Alive;
        }

        // =========================================================
        // REPLICATION & BEHAVIOR TREE BRAIN OVERRIDE
        // =========================================================
        protected override void OnReplicatedSpecialAbilityStarted(int elapsedTicks)
        {
            isPerformingImplant = true;
            if (animator != null)
                animator.SetTrigger(AnimAttack);

            float elapsedSeconds = elapsedTicks / (float)GetPresentationTickRate();
            if (elapsedSeconds < implantWindup && implantWindupSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(implantWindupSound, audioVolume);

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
            float remaining = Mathf.Max(0f, implantWindup - elapsedSeconds);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            if (implantStabSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(implantStabSound, audioVolume);
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

        protected override float CalculateVisualMoveSpeed()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
                return 0f;
            return Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);
        }

        protected override Vector3 GetLookDirection()
        {
            if (currentInfectorState == InfectorState.Implant && currentTargetTransform != null)
                return currentTargetTransform.position - transform.position;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 direction = agent.velocity.sqrMagnitude > 0.04f ? agent.velocity : agent.desiredVelocity;
                if (direction.sqrMagnitude > 0.04f)
                    return direction;
            }
            return currentTargetTransform != null
                ? currentTargetTransform.position - transform.position
                : Vector3.zero;
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
            if (profile == null || profile.playerTransform == null) return false;

            // Only spawn if not all players are already critically infected
            if (profile.cachedInfection != null)
            {
                return !profile.cachedInfection.IsCritical;
            }

            if (profile.playerTransform != null && profile.playerTransform.TryGetComponent<PlayerInfectionController>(out var infection))
            {
                return !infection.IsCritical;
            }

            return false;
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
                if (profile?.playerTransform == null || teamHealth[i].IsDownOrDead)
                    return false;

                PlayerHealth health = profile.cachedHealth;
                if (health == null || health.IsDead || health.LifeState != PlayerLifeState.Alive)
                    return false;

                PlayerInfectionController infection = profile.cachedInfection;
                if (infection == null || infection.CurrentStage >= InfectionStage.Critical)
                    return false;

                totalInfection += infection.CurrentInfection;
            }

            return totalInfection / profiles.Count < 50f;
        }
    }
}
