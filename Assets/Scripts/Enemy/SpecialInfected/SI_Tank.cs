using UnityEngine;

namespace FPS
{
    public class SI_Tank : SpecialInfectedBase
    {
        [Header("Tank Settings")]
        [SerializeField] private float chargeSpeed = 10f;
        [SerializeField] private float chargeDamage = 50f;
        
        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Tank;
            allowedInSoloMode = true;
            specialHPMultiplier = 5f; // Very tanky
        }

        public override void UseAbility()
        {
            // TODO: Ground slam or throw rock
            Debug.Log("[Tank] Ground slam!");
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            // Spawn during peak phase when team is winning
            return AIDirector.Instance?.CurrentPhase == GamePhase.PEAK;
        }
    }
}
