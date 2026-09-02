using System.Collections;
using System.Collections.Generic;
using UniBT;
using UnityEngine;

namespace FPS
{
    public class SI_Tank : SpecialInfectedBase
    {
        private const float MinimumTeamHealthFraction = 0.25f;

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
        [SerializeField] private float staggerDuration = 1.25f;
        [SerializeField] private float staggerImmunityDuration = 5f;

        [Header("Audio")]
        [SerializeField] private AudioClip roarSound;
        [SerializeField] private AudioClip slamSound;
        [SerializeField] private AudioClip heavySwingSound;
        [SerializeField] private float audioVolume = 1f;

        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimSlam = Animator.StringToHash("Slam");
        private static readonly int AnimStagger = Animator.StringToHash("Stagger");

        private bool isStaggered;
        private bool isPerformingAbility;
        private float lastSlamTime = -999f;
        private float previousHealth = -1f;
        private float staggerImmunityUntil;
        private readonly List<DamageEntry> recentDamageList = new List<DamageEntry>();
        private Coroutine activeAbilityRoutine;
        private Coroutine activeStaggerRoutine;

        private struct DamageEntry
        {
            public float time;
            public float amount;
        }

        public float HealthPerPlayer => healthPerPlayer;
        public float EffectiveMaxHealth => CalculateTankMaxHealth(capturedSpawnPlayerCount, healthPerPlayer);
        public float HeavySwingDamage => heavySwingDamage;
        public float HeavySwingKnockbackForce => heavySwingKnockbackForce;
        public float HeavySwingWindup => heavySwingWindup;
        public float HeavySwingRange => heavySwingRange;
        public float HeavySwingArcDegrees => heavySwingArcDegrees;
        public float SlamDamage => slamDamage;
        public float SlamRadius => slamRadius;
        public float SlamKnockbackForce => slamKnockbackForce;
        public float SlamCooldown => slamCooldown;
        public float SlamWindup => slamWindup;
        public float StaggerDamageFraction => staggerDamageFraction;
        public float StaggerDamageThreshold => Mathf.Max(1f, EffectiveMaxHealth * staggerDamageFraction);
        public float StaggerWindow => staggerWindow;
        public float StaggerDuration => staggerDuration;
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
                return SumRecentDamage();
            }
        }

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

            base.Start();
            DisableBehaviorTreeBrain();
            SubscribeToHealth();
        }

        public override void ResetAI()
        {
            CancelActiveAbility();
            if (activeStaggerRoutine != null)
            {
                StopCoroutine(activeStaggerRoutine);
                activeStaggerRoutine = null;
            }

            isStaggered = false;
            staggerImmunityUntil = 0f;
            recentDamageList.Clear();
            previousHealth = -1f;
            lastSlamTime = -999f;
            base.ResetAI();
            SubscribeToHealth();
        }

        public override void OnDestroy()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
                health.OnHealthChanged -= OnTankHealthChanged;

            base.OnDestroy();
        }

        public override void OnDeath()
        {
            CancelActiveAbility();
            recentDamageList.Clear();
            base.OnDeath();
        }

        protected override float CalculateMaxHealth(int playerCount, float authoredMaxHealth)
        {
            return CalculateTankMaxHealth(playerCount, healthPerPlayer);
        }

        public static float CalculateTankMaxHealth(int playerCount, float healthPerPlayer = 2500f)
        {
            return Mathf.Max(1f, healthPerPlayer) * ClampSupportedPlayerCount(playerCount);
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return AIDirector.Instance != null && AIDirector.Instance.CurrentPhase == GamePhase.PEAK;
        }

        public override bool ShouldSpawnForTeam(
            IReadOnlyList<PlayerProfile> profiles,
            IReadOnlyList<PlayerTeamHealthSnapshot> teamHealth)
        {
            if (AIDirector.Instance == null || AIDirector.Instance.CurrentPhase != GamePhase.PEAK)
                return false;

            return CalculateAverageTeamHealth(teamHealth, out float average)
                && average >= MinimumTeamHealthFraction;
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
            return !isStaggered && !isPerformingAbility;
        }

        public override void UseAbility()
        {
            if (!CanRunServerLogic() || !CanUseAbility())
                return;

            bool slamReady = Time.time - lastSlamTime >= slamCooldown;
            activeAbilityRoutine = StartCoroutine(
                slamReady && CountPlayersInRadius(slamRadius) >= 2
                    ? SlamRoutine()
                    : HeavySwingRoutine());
        }

        private IEnumerator HeavySwingRoutine()
        {
            BeginAbility(EnemySpecialActionKind.Primary, heavySwingWindup + 0.5f, AnimAttack, heavySwingSound);
            yield return new WaitForSeconds(heavySwingWindup);
            if (!isStaggered)
                ExecuteHeavySwingHit();
            yield return new WaitForSeconds(0.4f);
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
                if (candidate == null || candidate.IsDead)
                    continue;

                Vector3 offset = candidate.transform.position - transform.position;
                Vector3 planarOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
                if (planarOffset.sqrMagnitude <= 0.0001f
                    || Vector3.Dot(transform.forward, planarOffset.normalized) < minimumDot)
                    continue;

                if (offset.sqrMagnitude < closestDistanceSqr)
                {
                    closest = candidate;
                    closestDistanceSqr = offset.sqrMagnitude;
                }
            }

            if (closest == null)
                return;

            closest.TakeDamage(heavySwingDamage);
            ApplyKnockback(closest, heavySwingKnockbackForce, 0.25f);
        }

        private IEnumerator SlamRoutine()
        {
            lastSlamTime = Time.time;
            BeginAbility(EnemySpecialActionKind.Secondary, slamWindup + 0.6f, AnimSlam, slamSound);
            yield return new WaitForSeconds(slamWindup);
            if (!isStaggered)
                ExecuteSlamAoE();
            yield return new WaitForSeconds(0.5f);
            CompleteAbility();
        }

        private void ExecuteSlamAoE()
        {
            if (!CanRunServerLogic())
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, slamRadius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            HashSet<PlayerHealth> damagedPlayers = new HashSet<PlayerHealth>();

            for (int i = 0; i < hits.Length; i++)
            {
                PlayerHealth player = hits[i].GetComponentInParent<PlayerHealth>();
                if (player == null || player.IsDead || !damagedPlayers.Add(player))
                    continue;

                player.TakeDamage(slamDamage);
                ApplyKnockback(player, slamKnockbackForce, 0.35f);
            }
        }

        private void ApplyKnockback(PlayerHealth player, float force, float upwardBias)
        {
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement == null)
                return;

            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                direction = transform.forward;
            direction = direction.normalized;
            direction.y = upwardBias;
            movement.TryApplyServerKnockback(direction.normalized * force);
        }

        private void BeginAbility(EnemySpecialActionKind actionKind, float presentationDuration,
            int animationTrigger, AudioClip sound)
        {
            isPerformingAbility = true;
            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialActionReplicated(actionKind, now + presentationDuration);
            SetAgentStopped(true);
            animator?.SetTrigger(animationTrigger);
            if (sound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(sound, audioVolume);
        }

        private void CompleteAbility()
        {
            activeAbilityRoutine = null;
            isPerformingAbility = false;
            ClearSpecialActionReplicated();
            if (!isStaggered)
                SetAgentStopped(false);
        }

        private void CancelActiveAbility()
        {
            if (activeAbilityRoutine != null)
            {
                StopCoroutine(activeAbilityRoutine);
                activeAbilityRoutine = null;
            }

            isPerformingAbility = false;
            ClearSpecialActionReplicated();
        }

        private void OnTankHealthChanged(float current, float max)
        {
            if (!CanRunServerLogic())
                return;

            if (previousHealth < 0f || current > previousHealth)
            {
                previousHealth = current;
                return;
            }

            float damageTaken = previousHealth - current;
            previousHealth = current;
            if (damageTaken <= 0f)
                return;

            RecordDamage(damageTaken, Time.time);
            CheckAndTriggerStagger(Time.time);
        }

        public void RecordDamage(float damage, float currentTime)
        {
            if (damage <= 0f || isStaggered || currentTime < staggerImmunityUntil)
                return;

            CleanExpiredDamage(currentTime);
            recentDamageList.Add(new DamageEntry { amount = damage, time = currentTime });
        }

        public bool CheckAndTriggerStagger(float currentTime)
        {
            if (isStaggered || currentTime < staggerImmunityUntil)
                return false;

            CleanExpiredDamage(currentTime);
            if (SumRecentDamage() < StaggerDamageThreshold)
                return false;

            recentDamageList.Clear();
            TriggerStagger(currentTime);
            return true;
        }

        public void TriggerStagger()
        {
            TriggerStagger(Time.time);
        }

        private void TriggerStagger(float currentTime)
        {
            if (isStaggered || currentTime < staggerImmunityUntil)
                return;

            CancelActiveAbility();
            if (activeStaggerRoutine != null)
                StopCoroutine(activeStaggerRoutine);

            isStaggered = true;
            staggerImmunityUntil = currentTime + staggerDuration + staggerImmunityDuration;
            activeStaggerRoutine = StartCoroutine(StaggerRoutine());
        }

        private IEnumerator StaggerRoutine()
        {
            double now = NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
            SetSpecialActionReplicated(EnemySpecialActionKind.Stagger, now + staggerDuration);
            SetAgentStopped(true);
            animator?.SetTrigger(AnimStagger);
            if (roarSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(roarSound, audioVolume);

            yield return new WaitForSeconds(staggerDuration);

            isStaggered = false;
            activeStaggerRoutine = null;
            ClearSpecialActionReplicated();
            SetAgentStopped(false);
        }

        private float SumRecentDamage()
        {
            float total = 0f;
            for (int i = 0; i < recentDamageList.Count; i++)
                total += recentDamageList[i].amount;
            return total;
        }

        private void CleanExpiredDamage(float currentTime)
        {
            float cutoff = currentTime - staggerWindow;
            for (int i = recentDamageList.Count - 1; i >= 0; i--)
            {
                if (recentDamageList[i].time < cutoff)
                    recentDamageList.RemoveAt(i);
            }
        }

        private int CountPlayersInRadius(float radius)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            HashSet<PlayerHealth> seen = new HashSet<PlayerHealth>();
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerHealth player = hits[i].GetComponentInParent<PlayerHealth>();
                if (player != null && !player.IsDead)
                    seen.Add(player);
            }

            return seen.Count;
        }

        private void SubscribeToHealth()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health == null)
                return;

            health.OnHealthChanged -= OnTankHealthChanged;
            health.OnHealthChanged += OnTankHealthChanged;
            previousHealth = health.CurrentHealth;
        }

        private void SetAgentStopped(bool stopped)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = stopped;
        }

        protected override void OnReplicatedSpecialActionStarted(
            EnemySpecialActionKind actionKind, int elapsedTicks)
        {
            isPerformingAbility = actionKind != EnemySpecialActionKind.Stagger;
            isStaggered = actionKind == EnemySpecialActionKind.Stagger;

            switch (actionKind)
            {
                case EnemySpecialActionKind.Primary:
                    animator?.SetTrigger(AnimAttack);
                    PlayReplicatedSound(heavySwingSound);
                    break;
                case EnemySpecialActionKind.Secondary:
                    animator?.SetTrigger(AnimSlam);
                    PlayReplicatedSound(slamSound);
                    break;
                case EnemySpecialActionKind.Stagger:
                    animator?.SetTrigger(AnimStagger);
                    PlayReplicatedSound(roarSound);
                    break;
            }
        }

        protected override void OnReplicatedSpecialActionEnded(EnemySpecialActionKind actionKind)
        {
            isPerformingAbility = false;
            if (actionKind == EnemySpecialActionKind.Stagger)
                isStaggered = false;
        }

        private void PlayReplicatedSound(AudioClip clip)
        {
            if (clip != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(clip, audioVolume);
        }

        private void DisableBehaviorTreeBrain()
        {
            BehaviorTree behaviorTree = GetComponent<BehaviorTree>();
            if (behaviorTree != null)
                behaviorTree.enabled = false;
        }
    }
}
