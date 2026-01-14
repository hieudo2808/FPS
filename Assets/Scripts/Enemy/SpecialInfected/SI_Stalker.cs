using UnityEngine;
using System.Collections;

namespace FPS
{
    public class SI_Stalker : SpecialInfectedBase
    {
        [Header("Stalker Settings")]
        [SerializeField] private float stealthDuration = 5f;
        [SerializeField] private float backstabDamageMultiplier = 3f;
        
        private bool isStealthed = false;
        private Renderer[] renderers;

        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Stalker;
            allowedInSoloMode = true;
            renderers = GetComponentsInChildren<Renderer>();
        }

        public override void UseAbility()
        {
            if (!isStealthed)
            {
                StartCoroutine(StealthMode());
            }
        }

        private IEnumerator StealthMode()
        {
            isStealthed = true;
            SetVisibility(false);
            
            yield return new WaitForSeconds(stealthDuration);
            
            isStealthed = false;
            SetVisibility(true);
        }

        private void SetVisibility(bool visible)
        {
            foreach (var r in renderers)
            {
                Color c = r.material.color;
                c.a = visible ? 1f : 0.1f;
                r.material.color = c;
            }
        }

        public override bool ShouldSpawn(PlayerProfile profile)
        {
            return profile.isCamping;
        }
    }
}
