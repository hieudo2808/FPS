using UnityEngine;

namespace FPS
{
    public enum SpecialType
    {
        None,
        Stalker,    // Stealth, backstab
        Screamer,   // Call horde
        Spitter,    // AoE acid
        Charger,    // Charge + pin
        Tank        // Mini-boss
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
        
        public SpecialType Type => specialType;
        public bool AllowedInSoloMode => allowedInSoloMode;

        protected override void Start()
        {
            base.Start();
            lastAbilityTime = -abilityCooldown; // Ready immediately
            
            // Apply special HP scaling
            ApplySpecialScaling();
        }

        protected virtual void ApplySpecialScaling()
        {
            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            
            // Exponential scaling for specials
            float hpScale = Mathf.Pow(1.5f, playerCount - 1);
            
            // Solo mode: reduce HP
            if (playerCount == 1)
            {
                hpScale *= 0.5f;
            }
            
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                float newHP = health.MaxHealth * hpScale * specialHPMultiplier;
                health.SetMaxHealth(newHP);
            }
        }

        protected virtual void Update()
        {
            // Check ability usage
            if (abilityReady && CanUseAbility())
            {
                UseAbility();
                lastAbilityTime = Time.time;
            }
        }

        // Override in subclasses
        public abstract void UseAbility();
        
        protected virtual bool CanUseAbility()
        {
            return true;
        }

        // Override to define spawn conditions
        public virtual bool ShouldSpawn(PlayerProfile profile)
        {
            return true;
        }
    }
}
