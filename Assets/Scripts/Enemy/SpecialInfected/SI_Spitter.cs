using UnityEngine;

namespace FPS
{
    public class SI_Spitter : SpecialInfectedBase
    {
        [Header("Spitter Settings")]
        [SerializeField] private GameObject acidPoolPrefab;
        [SerializeField] private float spitRange = 15f;
        [SerializeField] private float acidDamage = 5f;
        [SerializeField] private float acidDuration = 5f;
        
        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Spitter;
            allowedInSoloMode = true;
        }

        public override void UseAbility()
        {
            // TODO: Spawn acid pool at player position
            Debug.Log("[Spitter] Spit acid!");
        }

        protected override bool CanUseAbility()
        {
            // Only spit if player is in range
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return false;
            
            float dist = Vector3.Distance(transform.position, player.transform.position);
            return dist <= spitRange;
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return TeamAnalyzer.Instance?.Formation == TeamFormation.GROUPED;
        }
    }
}
