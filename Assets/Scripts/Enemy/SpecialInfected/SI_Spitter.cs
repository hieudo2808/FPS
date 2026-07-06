using UnityEngine;

namespace FPS
{
    public class SI_Spitter : SpecialInfectedBase
    {
        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Spitter;
            allowedInSoloMode = true;
        }

        public override void UseAbility()
        {
            // Framework-only until a tested acid projectile/pool ability is promoted to playable.
        }

        protected override bool CanUseAbility() => false;

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return TeamAnalyzer.Instance?.Formation == TeamFormation.GROUPED;
        }
    }
}
