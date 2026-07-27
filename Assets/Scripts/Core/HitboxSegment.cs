using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum HitboxZone
    {
        Body,
        Head,
        Chest,
        Arm,
        Leg
    }

    public class HitboxSegment : MonoBehaviour
    {
        [SerializeField] private HitboxZone zone = HitboxZone.Body;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private NetworkObject ownerNetworkObject;
        [SerializeField] private MonoBehaviour damageTarget;

        public virtual HitboxZone Zone => zone;
        public virtual float DamageMultiplier => damageMultiplier > 0f && !Mathf.Approximately(damageMultiplier, 1f)
            ? damageMultiplier
            : GetDefaultMultiplier(Zone);

        public bool IsHeadshot => Zone == HitboxZone.Head;
        public NetworkObject OwnerNetworkObject => ownerNetworkObject != null
            ? ownerNetworkObject
            : GetComponentInParent<NetworkObject>();

        public IDamageable DamageTarget
        {
            get
            {
                if (damageTarget is IDamageable configuredTarget)
                    return configuredTarget;

                return GetComponentInParent<IDamageable>();
            }
        }

        private void Reset()
        {
            damageMultiplier = GetDefaultMultiplier(zone);
            ownerNetworkObject = GetComponentInParent<NetworkObject>();
            damageTarget = GetComponentInParent<MonoBehaviour>();
        }

        private void OnValidate()
        {
            if (damageMultiplier <= 0f)
                damageMultiplier = GetDefaultMultiplier(zone);
        }

        public static float GetDefaultMultiplier(HitboxZone hitboxZone)
        {
            return hitboxZone switch
            {
                HitboxZone.Head => 2f,
                HitboxZone.Arm => 0.75f,
                HitboxZone.Leg => 0.75f,
                _ => 1f
            };
        }
    }
}
