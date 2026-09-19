using System.Collections;
using System.Collections.Generic;
using UniBT;
using UnityEngine;
using UnityEngine.AI;

namespace FPS
{
    public class SI_Tank : SpecialInfectedBase
    {
        private const float MinimumTeamHealthFraction = 0.25f;
        private const float ThreatLedgerWindow = 8f;
        private const float ThreatHalfLife = 4f;
        private const float TargetCommitmentSeconds = 4f;
        private const float TargetSwitchMultiplier = 1.2f;
        private const float TargetEvaluationInterval = 0.2f;
        private const float SwingDuration = 2.7f;
        private const float SlamDuration = 2.233f;
        private const float FullStaggerDuration = 5.167f;

        [Header("Durability")]
        [SerializeField, Min(1f)] private float healthPerPlayer = 2500f;

        [Header("Heavy Swing — §22")]
        [SerializeField] private float heavySwingDamage = 50f;
        [SerializeField] private float heavySwingKnockbackForce = 8f;
        [SerializeField] private float heavySwingWindup = 0.8f;
        [SerializeField] private float heavySwingRange = 3.5f;
        [SerializeField, Range(1f, 360f)] private float heavySwingArcDegrees = 120f;

        [Header("Slam AoE — §22")]
        [SerializeField] private float slamDamage = 25f;
        [SerializeField] private float slamRadius = 4.5f;
        [SerializeField] private float slamKnockbackForce = 12f;
        [SerializeField] private float slamCooldown = 15f;
        [SerializeField] private float slamWindup = 1.2f;

        [Header("Stagger — §22")]
        [SerializeField, Range(0.01f, 1f)] private float staggerDamageFraction = 0.15f;
        [SerializeField] private float staggerWindow = 3f;
        [SerializeField] private float staggerDuration = FullStaggerDuration;
        [SerializeField] private float staggerImmunityDuration = 5f;

        [Header("Encounter Gate")]
        [SerializeField] private bool allowAuthoredMiniBossEncounter;

        [Header("Audio")]
        [SerializeField] private AudioClip roarSound;
        [SerializeField] private AudioClip slamSound;
        [SerializeField] private AudioClip heavySwingSound;
        [SerializeField] private float audioVolume = 1f;

        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimSlam = Animator.StringToHash("Slam");
        private static readonly int AnimStagger = Animator.StringToHash("Stagger");

        private readonly List<DamageEntry> recentDamageList = new();
        private NavMeshPath reusableTargetPath;
        private bool isStaggered;
        private bool isPerformingAbility;
        private bool isDead;
        private float lastSlamTime = -999f;
        private float staggerImmunityUntil;
        private float nextTargetEvaluationTime;
        private EnemyMotionLockHandle abilityMotionLock;
        private EnemyMotionLockHandle staggerMotionLock;
        private Coroutine activeAbilityRoutine;
        private Coroutine activeStaggerRoutine;

        private struct DamageEntry
        {
            public float time;
            public float amount;
            public ulong attackerClientId;
            public int attackerPlayerIndex;
        }

        public float HealthPerPlayer => healthPerPlayer;
        public float EffectiveMaxHealth
        {
            get
            {
                EnemyHealth health = GetComponent<EnemyHealth>();
                return health != null ? health.MaxHealth : ScalingSnapshot.MaxHealth;
            }
        }
        public float HeavySwingDamage => heavySwingDamage;
        public float EffectiveHeavySwingDamage => ScaleDamage(heavySwingDamage);
        public float HeavySwingKnockbackForce => heavySwingKnockbackForce;
        public float HeavySwingWindup => heavySwingWindup;
        public float HeavySwingRange => heavySwingRange;
        public float HeavySwingArcDegrees => heavySwingArcDegrees;
        public float SlamDamage => slamDamage;
        public float EffectiveSlamDamage => ScaleDamage(slamDamage);
        public float SlamRadius => slamRadius;
        public float SlamKnockbackForce => slamKnockbackForce;
        public float SlamCooldown => slamCooldown;
        public float SlamWindup => slamWindup;
        public float StaggerDamageFraction => staggerDamageFraction;
        public float StaggerDamageThreshold => Mathf.Max(1f, EffectiveMaxHealth * staggerDamageFraction);
        public float StaggerWindow => staggerWindow;
        public float StaggerDuration => FullStaggerDuration;
        public float StaggerImmunityDuration => staggerImmunityDuration;
        public float StaggerImmunityUntil => staggerImmunityUntil;
        public bool IsStaggered => isStaggered;
        public bool IsStaggerImmune => isStaggered || Time.time < staggerImmunityUntil;
        public bool IsPerformingAbility => isPerformingAbility;
        public float LastSlamTime => lastSlamTime;

        public float AccumulatedDamage
        {
            get
            {
                CleanExpiredDamage(Time.time);
                return SumStaggerDamage(Time.time);
            }
        }

        protected override bool UsesGenericServerBrain => false;
        protected override bool AutoTriggerPrimaryAbility => false;
        protected override bool PreserveAuthoredAgentSettings => true;

        public SI_Tank()
        {
            specialType = SpecialType.Tank;
            allowedInSoloMode = true;
            specialHPMultiplier = 1f;
            abilityCooldown = 3f;
        }

        protected override void Start()
        {
            specialType = SpecialType.Tank;
            allowedInSoloMode = true;
            specialHPMultiplier = 1f;
            abilityCooldown = 3f;
            staggerDuration = FullStaggerDuration;
            base.Start();
            DisableBehaviorTreeBrain();
            SubscribeToHealth();
            if (animator != null)
                animator.applyRootMotion = false;
        }

        protected override void TickCustomServerBrain()
        {
            if (isDead || isStaggered || isPerformingAbility)
                return;

            if (!IsValidTarget(CurrentTarget))
                FindPlayer(true);
            UpdateTarget();

            Transform target = CurrentTarget;
            if (!IsValidTarget(target))
            {
                StopAgentMotion();
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > heavySwingRange)
            {
                ResumeAgentMotion();
                TrySubmitAgentDestination(target.position);
                return;
            }

            StopAgentMotion();
            if (abilityReady)
                UseAbility();
        }

        public override void ResetAI()
        {
            CancelActiveAbility();
            CancelStagger();
            specialType = SpecialType.Tank;
            abilityCooldown = 3f;
            base.ResetAI();
            isDead = false;
            isStaggered = false;
            staggerImmunityUntil = 0f;
            recentDamageList.Clear();
            lastSlamTime = -999f;
            nextTargetEvaluationTime = 0f;
            SubscribeToHealth();
        }

        public override void OnDestroy()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
                health.OnDamageApplied -= OnTankDamageApplied;
            base.OnDestroy();
        }

        public override void OnDeath()
        {
            if (isDead)
                return;
            isDead = true;
            CancelActiveAbility();
            CancelStagger();
            recentDamageList.Clear();
            base.OnDeath();
        }

        public override void OnNetworkDespawn()
        {
            CancelActiveAbility();
            CancelStagger();
            base.OnNetworkDespawn();
        }

        protected override float CalculateMaxHealth(int playerCount, float authoredMaxHealth)
        {
            float difficulty = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.GetCurrentStats().hpMultiplier
                : 1f;
            return EnemyScalingResolver.GetSpecialHealth(
                SpecialType.Tank,
                playerCount,
                authoredMaxHealth) * Mathf.Max(0.01f, difficulty);
        }

        public static float CalculateTankMaxHealth(int playerCount, float healthPerPlayer = 2500f)
        {
            int count = ClampSupportedPlayerCount(playerCount);
            if (Mathf.Approximately(healthPerPlayer, 2500f))
                return EnemyScalingResolver.GetSpecialHealth(SpecialType.Tank, count, healthPerPlayer);
            return Mathf.Max(1f, healthPerPlayer) * count;
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return AIDirector.Instance != null
                && AIDirector.Instance.CurrentPhase == GamePhase.PEAK
                && IsTankEncounterAllowed();
        }

        public override bool ShouldSpawnForTeam(
            IReadOnlyList<PlayerProfile> profiles,
            IReadOnlyList<PlayerTeamHealthSnapshot> teamHealth)
        {
            if (AIDirector.Instance == null
                || AIDirector.Instance.CurrentPhase != GamePhase.PEAK
                || !IsTankEncounterAllowed())
            {
                return false;
            }

            return CalculateAverageTeamHealth(teamHealth, out float average)
                && average >= MinimumTeamHealthFraction;
        }

        private bool IsTankEncounterAllowed()
        {
            return allowAuthoredMiniBossEncounter
                || FactoryMissionController.Instance != null
                && FactoryMissionController.Instance.State == FactoryMissionState.ExtractionActive;
        }

        public static bool CalculateAverageTeamHealth(
            IReadOnlyList<PlayerTeamHealthSnapshot> teamHealth,
            out float average)
        {
            average = 0f;
            if (teamHealth == null || teamHealth.Count == 0)
                return false;

            float total = 0f;
            for (int i = 0; i < teamHealth.Count; i++)
            {
                if (teamHealth[i].MaxHealth <= 0f)
                    return false;
                total += teamHealth[i].HealthFraction;
            }

            average = total / teamHealth.Count;
            return true;
        }

        protected override bool CanUseAbility()
        {
            return !isDead && !isStaggered && !isPerformingAbility && abilityReady;
        }

        public override void UseAbility()
        {
            if (!CanRunServerLogic() || !CanUseAbility())
                return;

            bool slamReady = Time.time - lastSlamTime
                >= slamCooldown * ScalingSnapshot.AbilityCooldownMultiplier;
            activeAbilityRoutine = StartCoroutine(
                slamReady && CountPlayersInRadius(slamRadius) >= 2
                    ? SlamRoutine()
                    : HeavySwingRoutine());
        }

        private IEnumerator HeavySwingRoutine()
        {
            EnemyActionTiming timing = new(
                EnemyActionType.HeavySwing,
                heavySwingWindup,
                SwingDuration,
                SwingDuration);
            BeginAbility(EnemySpecialActionKind.Primary, timing, AnimAttack, heavySwingSound);
            yield return new WaitForSeconds(timing.ImpactSeconds);
            if (!isStaggered && !isDead)
                ExecuteHeavySwingHit();
            yield return new WaitForSeconds(timing.MotionLockSeconds - timing.ImpactSeconds);
            CompleteAbility();
        }

        private IEnumerator SlamRoutine()
        {
            lastSlamTime = Time.time;
            EnemyActionTiming timing = new(
                EnemyActionType.Slam,
                slamWindup,
                SlamDuration,
                SlamDuration);
            BeginAbility(EnemySpecialActionKind.Secondary, timing, AnimSlam, slamSound);
            yield return new WaitForSeconds(timing.ImpactSeconds);
            if (!isStaggered && !isDead)
                ExecuteSlamAoE();
            yield return new WaitForSeconds(timing.MotionLockSeconds - timing.ImpactSeconds);
            CompleteAbility();
        }

        private void ExecuteHeavySwingHit()
        {
            if (!CanRunServerLogic())
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, heavySwingRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            PlayerHealth closest = null;
            float closestDistanceSqr = float.MaxValue;
            float minimumDot = Mathf.Cos(heavySwingArcDegrees * 0.5f * Mathf.Deg2Rad);

            for (int i = 0; i < hits.Length; i++)
            {
                PlayerHealth candidate = hits[i].GetComponentInParent<PlayerHealth>();
                if (candidate == null || candidate.IsDead || candidate.LifeState != PlayerLifeState.Alive)
                    continue;

                Vector3 offset = candidate.transform.position - transform.position;
                Vector3 planarOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
                if (planarOffset.sqrMagnitude <= 0.0001f
                    || Vector3.Dot(transform.forward, planarOffset.normalized) < minimumDot)
                {
                    continue;
                }

                if (offset.sqrMagnitude < closestDistanceSqr)
                {
                    closest = candidate;
                    closestDistanceSqr = offset.sqrMagnitude;
                }
            }

            if (closest == null)
                return;
            closest.TakeDamage(ScaleDamage(heavySwingDamage));
            ApplyKnockback(closest, heavySwingKnockbackForce, 0.25f);
        }

        private void ExecuteSlamAoE()
        {
            if (!CanRunServerLogic())
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            HashSet<PlayerHealth> damagedPlayers = new();
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerHealth player = hits[i].GetComponentInParent<PlayerHealth>();
                if (player == null || player.IsDead || player.LifeState != PlayerLifeState.Alive
                    || !damagedPlayers.Add(player))
                {
                    continue;
                }

                player.TakeDamage(ScaleDamage(slamDamage));
                ApplyKnockback(player, slamKnockbackForce, 0.35f);
            }
        }

        private void ApplyKnockback(PlayerHealth player, float force, float upwardBias)
        {
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement == null)
                return;

            Vector3 direction = Vector3.ProjectOnPlane(
                player.transform.position - transform.position,
                Vector3.up);
            if (direction.sqrMagnitude < 0.001f)
                direction = transform.forward;
            direction = direction.normalized;
            direction.y = upwardBias;
            movement.TryApplyServerKnockback(direction.normalized * force);
        }

        private void BeginAbility(
            EnemySpecialActionKind actionKind,
            EnemyActionTiming timing,
            int animationTrigger,
            AudioClip sound)
        {
            isPerformingAbility = true;
            lastAbilityTime = Time.time;
            abilityMotionLock = AcquireMotionLock(timing);
            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialActionReplicated(actionKind, now + timing.PresentationSeconds);
            TrySetAnimatorTrigger(animationTrigger);
            PlayLocalSound(sound, 3f, 30f, audioVolume);
        }

        private void CompleteAbility()
        {
            activeAbilityRoutine = null;
            isPerformingAbility = false;
            ReleaseMotionLock(abilityMotionLock);
            abilityMotionLock = default;
            ClearSpecialActionReplicated();
        }

        private void CancelActiveAbility()
        {
            if (activeAbilityRoutine != null)
            {
                StopCoroutine(activeAbilityRoutine);
                activeAbilityRoutine = null;
            }
            isPerformingAbility = false;
            ReleaseMotionLock(abilityMotionLock);
            abilityMotionLock = default;
            ClearSpecialActionReplicated();
        }

        private void OnTankDamageApplied(DamageInfo damageInfo)
        {
            if (!CanRunServerLogic())
                return;

            RecordDamage(
                damageInfo.amount,
                Time.time,
                damageInfo.attackerClientId,
                damageInfo.attackerPlayerIndex);
            CheckAndTriggerStagger(Time.time);
        }

        public void RecordDamage(float damage, float currentTime)
        {
            RecordDamage(damage, currentTime, ulong.MaxValue, -1);
        }

        public void RecordDamage(
            float damage,
            float currentTime,
            ulong attackerClientId,
            int attackerPlayerIndex)
        {
            if (damage <= 0f || isStaggered || currentTime < staggerImmunityUntil)
                return;

            CleanExpiredDamage(currentTime);
            recentDamageList.Add(new DamageEntry
            {
                amount = damage,
                time = currentTime,
                attackerClientId = attackerClientId,
                attackerPlayerIndex = attackerPlayerIndex
            });
        }

        public bool CheckAndTriggerStagger(float currentTime)
        {
            if (isStaggered || currentTime < staggerImmunityUntil)
                return false;

            CleanExpiredDamage(currentTime);
            if (SumStaggerDamage(currentTime) < StaggerDamageThreshold)
                return false;

            recentDamageList.Clear();
            TriggerStagger(currentTime);
            return true;
        }

        public void TriggerStagger() => TriggerStagger(Time.time);

        private void TriggerStagger(float currentTime)
        {
            if (isStaggered || currentTime < staggerImmunityUntil)
                return;

            CancelActiveAbility();
            CancelStagger();
            isStaggered = true;
            staggerImmunityUntil = currentTime + FullStaggerDuration + staggerImmunityDuration;
            activeStaggerRoutine = StartCoroutine(StaggerRoutine());
        }

        private IEnumerator StaggerRoutine()
        {
            EnemyActionTiming timing = new(
                EnemyActionType.Stagger,
                0f,
                FullStaggerDuration,
                FullStaggerDuration);
            staggerMotionLock = AcquireMotionLock(timing);
            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialActionReplicated(EnemySpecialActionKind.Stagger, now + FullStaggerDuration);
            TrySetAnimatorTrigger(AnimStagger);
            PlayLocalSound(roarSound, 5f, 50f, audioVolume);

            yield return new WaitForSeconds(FullStaggerDuration);

            isStaggered = false;
            activeStaggerRoutine = null;
            ReleaseMotionLock(staggerMotionLock);
            staggerMotionLock = default;
            ClearSpecialActionReplicated();
        }

        private void CancelStagger()
        {
            if (activeStaggerRoutine != null)
            {
                StopCoroutine(activeStaggerRoutine);
                activeStaggerRoutine = null;
            }
            isStaggered = false;
            ReleaseMotionLock(staggerMotionLock);
            staggerMotionLock = default;
        }

        private float SumStaggerDamage(float currentTime)
        {
            float cutoff = currentTime - Mathf.Max(0.01f, staggerWindow);
            float total = 0f;
            for (int i = 0; i < recentDamageList.Count; i++)
            {
                if (recentDamageList[i].time >= cutoff)
                    total += recentDamageList[i].amount;
            }
            return total;
        }

        private void CleanExpiredDamage(float currentTime)
        {
            float cutoff = currentTime - ThreatLedgerWindow;
            for (int i = recentDamageList.Count - 1; i >= 0; i--)
            {
                if (recentDamageList[i].time < cutoff)
                    recentDamageList.RemoveAt(i);
            }
        }

        protected override void UpdateTarget()
        {
            if (PlayerProfiler.Instance == null || PlayerProfiler.Instance.PlayerCount == 0)
                return;

            PlayerProfile currentProfile = PlayerProfiler.Instance.GetProfileByTransform(CurrentTarget);
            bool currentValid = IsEligibleTarget(currentProfile);
            if (currentValid && Time.time < nextTargetEvaluationTime)
                return;

            nextTargetEvaluationTime = Time.time + TargetEvaluationInterval;
            CleanExpiredDamage(Time.time);

            IReadOnlyList<PlayerProfile> profiles = PlayerProfiler.Instance.AllProfiles;
            float maxThreat = 0f;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (IsEligibleTarget(profiles[i]))
                    maxThreat = Mathf.Max(maxThreat, GetDecayedThreat(profiles[i], Time.time));
            }

            PlayerProfile best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < profiles.Count; i++)
            {
                PlayerProfile profile = profiles[i];
                if (!IsEligibleTarget(profile))
                    continue;

                float score = CalculateTankTargetScore(profile, Time.time, maxThreat);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = profile;
                }
            }

            if (!currentValid)
            {
                SetCurrentTarget(best?.playerTransform, best?.playerIndex ?? -1);
                return;
            }

            if (best == null || best.playerTransform == CurrentTarget)
                return;

            float currentScore = CalculateTankTargetScore(currentProfile, Time.time, maxThreat);
            if (ShouldSwitchTarget(TimeSinceTargetSwitch, currentScore, bestScore))
                SetCurrentTarget(best.playerTransform, best.playerIndex);
        }

        public float CalculateTankTargetScore(PlayerProfile profile, float currentTime, float maxRecentThreat)
        {
            if (!IsEligibleTarget(profile))
                return float.MinValue;

            float normalizedThreat = maxRecentThreat > 0.001f
                ? Mathf.Clamp01(GetDecayedThreat(profile, currentTime) / maxRecentThreat)
                : 0f;
            float distance = Vector3.Distance(transform.position, profile.playerTransform.position);
            float normalizedDistance = Mathf.Clamp01(1f - distance / Mathf.Max(1f, MaxTargetDistance));

            float maxHealth = profile.cachedHealth != null
                ? Mathf.Max(1f, profile.cachedHealth.MaxHealth)
                : 100f;
            return ComposeTargetScore(
                normalizedThreat,
                normalizedDistance,
                Mathf.Clamp01(1f - profile.currentHealth / maxHealth),
                profile.isIsolated,
                profile.isReloading,
                Mathf.Clamp01(1f - profile.currentAmmoPercent));
        }

        public static float ComposeTargetScore(
            float normalizedRecentDamage,
            float normalizedDistanceAndPath,
            float normalizedLowHealth,
            bool isolated,
            bool reloading,
            float normalizedAmmoDeficit)
        {
            return 50f * Mathf.Clamp01(normalizedRecentDamage)
                + 30f * Mathf.Clamp01(normalizedDistanceAndPath)
                + 8f * Mathf.Clamp01(normalizedLowHealth)
                + (isolated ? 5f : 0f)
                + (reloading ? 4f : 0f)
                + 3f * Mathf.Clamp01(normalizedAmmoDeficit);
        }

        public static bool ShouldSwitchTarget(
            float committedSeconds,
            float currentScore,
            float challengerScore)
        {
            return committedSeconds >= TargetCommitmentSeconds
                && challengerScore > Mathf.Max(0f, currentScore) * TargetSwitchMultiplier;
        }

        public float GetDecayedThreat(PlayerProfile profile, float currentTime)
        {
            if (profile == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < recentDamageList.Count; i++)
            {
                DamageEntry entry = recentDamageList[i];
                bool matches = entry.attackerPlayerIndex >= 0
                    ? entry.attackerPlayerIndex == profile.playerIndex
                    : entry.attackerClientId != ulong.MaxValue && entry.attackerClientId == profile.clientId;
                if (!matches)
                    continue;

                float age = Mathf.Max(0f, currentTime - entry.time);
                if (age <= ThreatLedgerWindow)
                    total += entry.amount * Mathf.Pow(0.5f, age / ThreatHalfLife);
            }
            return total;
        }

        private bool IsEligibleTarget(PlayerProfile profile)
        {
            if (profile?.playerTransform == null || !profile.playerTransform.gameObject.activeInHierarchy)
                return false;
            PlayerHealth health = profile.cachedHealth;
            if (health == null)
                profile.playerTransform.TryGetComponent(out health);
            if (health == null || health.IsDead || health.LifeState != PlayerLifeState.Alive)
                return false;
            return HasCompletePath(profile.playerTransform.position);
        }

        private bool HasCompletePath(Vector3 destination)
        {
            reusableTargetPath ??= new NavMeshPath();
            return IsAgentReady()
                && agent.CalculatePath(destination, reusableTargetPath)
                && reusableTargetPath.status == NavMeshPathStatus.PathComplete;
        }

        private int CountPlayersInRadius(float radius)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            HashSet<PlayerHealth> seen = new();
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerHealth player = hits[i].GetComponentInParent<PlayerHealth>();
                if (player != null && !player.IsDead && player.LifeState == PlayerLifeState.Alive)
                    seen.Add(player);
            }
            return seen.Count;
        }

        private void SubscribeToHealth()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health == null)
                return;
            health.OnDamageApplied -= OnTankDamageApplied;
            health.OnDamageApplied += OnTankDamageApplied;
        }

        protected override void OnReplicatedSpecialActionStarted(
            EnemySpecialActionKind actionKind,
            int elapsedTicks)
        {
            isPerformingAbility = actionKind != EnemySpecialActionKind.Stagger;
            isStaggered = actionKind == EnemySpecialActionKind.Stagger;
            switch (actionKind)
            {
                case EnemySpecialActionKind.Primary:
                    TrySetAnimatorTrigger(AnimAttack);
                    PlayLocalSound(heavySwingSound, 3f, 30f, audioVolume);
                    break;
                case EnemySpecialActionKind.Secondary:
                    TrySetAnimatorTrigger(AnimSlam);
                    PlayLocalSound(slamSound, 3f, 30f, audioVolume);
                    break;
                case EnemySpecialActionKind.Stagger:
                    TrySetAnimatorTrigger(AnimStagger);
                    PlayLocalSound(roarSound, 5f, 50f, audioVolume);
                    break;
            }
        }

        protected override void OnReplicatedSpecialActionEnded(EnemySpecialActionKind actionKind)
        {
            isPerformingAbility = false;
            if (actionKind == EnemySpecialActionKind.Stagger)
                isStaggered = false;
        }

        protected override Vector3 GetLookDirection()
        {
            if ((isPerformingAbility || isStaggered) && CurrentTarget != null)
                return CurrentTarget.position - transform.position;
            return base.GetLookDirection();
        }

        protected override EnemyLocomotionState ResolveReplicatedLocomotion()
        {
            if (isDead)
                return EnemyLocomotionState.Dead;
            if (isPerformingAbility || isStaggered)
                return EnemyLocomotionState.Attacking;
            return IsAgentReady() && !agent.isStopped && agent.hasPath
                ? EnemyLocomotionState.Moving
                : EnemyLocomotionState.Idle;
        }

        private void DisableBehaviorTreeBrain()
        {
            BehaviorTree behaviorTree = GetComponent<BehaviorTree>();
            if (behaviorTree != null)
                behaviorTree.enabled = false;
        }
    }
}
