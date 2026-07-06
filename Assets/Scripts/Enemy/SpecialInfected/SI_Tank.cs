using UnityEngine;

namespace FPS
{
    public class SI_Tank : SpecialInfectedBase
    {
        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Tank;
            allowedInSoloMode = true;
            specialHPMultiplier = 5f;
        }

        public override void UseAbility()
        {
            // Framework-only until a tested slam/charge ability is promoted to playable.
        }

        protected override bool CanUseAbility() => false;

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return AIDirector.Instance?.CurrentPhase == GamePhase.PEAK;
        }
    }
}
