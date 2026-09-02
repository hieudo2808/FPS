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
        
        protected float lastAbilityTime;
        protected bool abilityReady => Time.time - lastAbilityTime >= abilityCooldown;
        protected int capturedSpawnPlayerCount = 1;
        
        public SpecialType Type => specialType;
        public bool AllowedInSoloMode => allowedInSoloMode;
        public int CapturedSpawnPlayerCount => capturedSpawnPlayerCount;
        protected virtual bool AutoTriggerPrimaryAbility => true;

        protected override void Start()
        {
            base.Start();
            lastAbilityTime = -abilityCooldown;
            
            ApplySpecialScaling();
        }

        protected virtual void ApplySpecialScaling()
        {
            capturedSpawnPlayerCount = ResolvePlayerCountForSpawn();
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                float newHP = CalculateMaxHealth(capturedSpawnPlayerCount, health.AuthoredMaxHealth);
                health.SetMaxHealth(newHP);
            }
        }

        protected virtual float CalculateMaxHealth(int playerCount, float authoredMaxHealth)
        {
            float hpScale = 1f + (ClampSupportedPlayerCount(playerCount) - 1) * 0.35f;
            return Mathf.Max(1f, authoredMaxHealth * hpScale * specialHPMultiplier);
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
