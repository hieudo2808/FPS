using UnityEngine;

namespace FPS
{
    public enum SpecialType
    {
        None,
        Stalker,
        Screamer,
        Spitter,
        Charger,
        Tank
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
            lastAbilityTime = -abilityCooldown;
            
            ApplySpecialScaling();
        }

        protected virtual void ApplySpecialScaling()
        {
            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            
            float hpScale = Mathf.Pow(1.5f, playerCount - 1);
            
            if (playerCount == 1) {
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
            if (abilityReady && CanUseAbility())
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
    }
}
