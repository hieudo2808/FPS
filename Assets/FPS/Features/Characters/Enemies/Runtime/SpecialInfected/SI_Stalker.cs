using UnityEngine;

namespace FPS
{
    public class SI_Stalker : SpecialInfectedBase
    {
        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Stalker;
            allowedInSoloMode = true;
        }

        public override void UseAbility()
        {
            // Framework-only until a tested stealth ability is promoted to playable.
        }

        protected override bool CanUseAbility() => false;

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return profile.isCamping;
        }
    }
}
