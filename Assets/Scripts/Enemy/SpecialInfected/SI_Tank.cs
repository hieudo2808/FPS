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
            specialHPMultiplier = 5f;
        }

        public override void UseAbility()
        {
            Debug.Log("[Tank] Ground slam!");
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return AIDirector.Instance?.CurrentPhase == GamePhase.PEAK;
        }
    }
}
