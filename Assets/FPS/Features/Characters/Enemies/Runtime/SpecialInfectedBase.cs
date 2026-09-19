using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public readonly struct PlayerTeamHealthSnapshot
    {
        public PlayerTeamHealthSnapshot(float currentHealth, float maxHealth, bool isDownOrDead)
        {
            CurrentHealth = Mathf.Max(0f, currentHealth);
            MaxHealth = Mathf.Max(0f, maxHealth);
            IsDownOrDead = isDownOrDead;
        }

        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public bool IsDownOrDead { get; }

        public float HealthFraction => IsDownOrDead || MaxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(CurrentHealth / MaxHealth);
    }

    public enum SpecialType
    {
        None,
        Stalker,
        Screamer,
        Spitter,
        Charger,
        Tank,
        Infector
    }

    public abstract class SpecialInfectedBase : EnemyAI
    {
        [Header("Special Infected Settings")]
        [SerializeField] protected SpecialType specialType = SpecialType.None;
        [SerializeField] protected float abilityCooldown = 10f;
        [SerializeField] protected bool allowedInSoloMode = true;
        [SerializeField] protected float specialHPMultiplier = 1.5f;
        [SerializeField] private EnemyScalingProfile scalingProfile;
        
        protected float lastAbilityTime;
        protected bool abilityReady => Time.time - lastAbilityTime >= EffectiveAbilityCooldown;
        protected int capturedSpawnPlayerCount = 1;
        private EnemyScalingSnapshot scalingSnapshot;
        private bool hasScalingSnapshot;
        
        public SpecialType Type => specialType;
        public bool AllowedInSoloMode => allowedInSoloMode;
        public int CapturedSpawnPlayerCount => capturedSpawnPlayerCount;
        public EnemyScalingSnapshot ScalingSnapshot => hasScalingSnapshot
            ? scalingSnapshot
            : EnemyScalingResolver.ResolveSpecial(specialType, 1, 100f, 1f, 1f, scalingProfile);
        protected float EffectiveAbilityCooldown => abilityCooldown * ScalingSnapshot.AbilityCooldownMultiplier;
        protected virtual bool AutoTriggerPrimaryAbility => true;

        protected override void Start()
        {
            base.Start();
            ApplySpecialScaling();
            lastAbilityTime = -EffectiveAbilityCooldown;
        }

        protected virtual void ApplySpecialScaling()
        {
            capturedSpawnPlayerCount = ResolvePlayerCountForSpawn();
            DifficultyStats difficulty = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.GetCurrentStats()
                : new DifficultyStats { hpMultiplier = 1f, damageMultiplier = 1f };

            EnemyHealth health = GetComponent<EnemyHealth>();
            float authoredHealth = health != null ? health.AuthoredMaxHealth : 100f;
            scalingSnapshot = EnemyScalingResolver.ResolveSpecial(
                specialType,
                capturedSpawnPlayerCount,
                authoredHealth * Mathf.Max(0.01f, specialHPMultiplier),
                difficulty.hpMultiplier,
                difficulty.damageMultiplier,
                scalingProfile);
            hasScalingSnapshot = true;

            if (health != null)
                health.SetMaxHealth(scalingSnapshot.MaxHealth);
        }

        protected virtual float CalculateMaxHealth(int playerCount, float authoredMaxHealth)
        {
            float difficultyMultiplier = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.GetCurrentStats().hpMultiplier
                : 1f;
            return EnemyScalingResolver.GetSpecialHealth(
                    specialType,
                    playerCount,
                    authoredMaxHealth * Mathf.Max(0.01f, specialHPMultiplier),
                    scalingProfile)
                * Mathf.Max(0.01f, difficultyMultiplier);
        }

        protected virtual int ResolvePlayerCountForSpawn()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
                return ClampSupportedPlayerCount(manager.ConnectedClientsList.Count);

            return ClampSupportedPlayerCount(PlayerProfiler.Instance?.PlayerCount ?? 1);
        }

        public static int ClampSupportedPlayerCount(int playerCount)
        {
            return Mathf.Clamp(playerCount, 1, 4);
        }

        public override void ResetAI()
        {
            base.ResetAI();
            ApplySpecialScaling();
            lastAbilityTime = -EffectiveAbilityCooldown;
        }

        protected float ScaleDamage(float authoredDamage)
        {
            return Mathf.Max(0f, authoredDamage) * ScalingSnapshot.DamageAndStatusMultiplier;
        }

        protected float ScaleStatus(float authoredAmount)
        {
            return Mathf.Max(0f, authoredAmount) * ScalingSnapshot.DamageAndStatusMultiplier;
        }

        protected override void Update()
        {
            base.Update();
            if (!CanRunServerLogic()) return;

            if (AutoTriggerPrimaryAbility && abilityReady && CanUseAbility())
            {
                UseAbility();
                lastAbilityTime = Time.time;
            }
        }

        public abstract void UseAbility();
        
        protected virtual bool CanUseAbility()
        {
            return true;
        }

        public virtual bool ShouldSpawn(PlayerProfile profile)
        {
            return true;
        }

        public virtual bool ShouldSpawnForTeam(
            IReadOnlyList<PlayerProfile> profiles,
            IReadOnlyList<PlayerTeamHealthSnapshot> teamHealth)
        {
            if (profiles == null || profiles.Count == 0)
                return true;

            for (int i = 0; i < profiles.Count; i++)
            {
                PlayerProfile profile = profiles[i];
                if (profile?.playerTransform != null && ShouldSpawn(profile))
                    return true;
            }

            return false;
        }
    }
}
