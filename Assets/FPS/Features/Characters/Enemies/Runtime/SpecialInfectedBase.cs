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
            
            // Use the same linear multiplayer curve as normal zombies. The
            // previous exponential curve made a four-player Screamer over
            // fifteen times tougher than its solo version.
            float hpScale = 1f + (Mathf.Max(1, playerCount) - 1) * 0.35f;
            
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
            {
                float newHP = health.MaxHealth * hpScale * specialHPMultiplier;
                health.SetMaxHealth(newHP);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!CanRunServerLogic()) return;

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
