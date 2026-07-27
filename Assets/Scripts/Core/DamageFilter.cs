using UnityEngine;

namespace FPS
{
    public class DamageFilter : MonoBehaviour
    {
        [SerializeField] private DamageType acceptedTypes = DamageType.All;
        [SerializeField] private bool acceptUnspecified = true;

        public DamageType AcceptedTypes => acceptedTypes;
        public bool AcceptUnspecified => acceptUnspecified;

        public bool Allows(DamageInfo damageInfo)
        {
            if (damageInfo.damageType == DamageType.Unspecified)
                return acceptUnspecified;

            return (acceptedTypes & damageInfo.damageType) != 0;
        }
    }
}
