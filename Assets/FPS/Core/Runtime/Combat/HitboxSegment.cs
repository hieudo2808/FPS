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
        public NetworkObject OwnerNetworkObject => ownerNetworkObject;
        public IDamageable DamageTarget => damageTarget as IDamageable;

        public void Configure(HitboxZone newZone, float multiplier, NetworkObject netObj, MonoBehaviour target)
        {
            zone = newZone;
            damageMultiplier = multiplier;
            ownerNetworkObject = netObj;
            damageTarget = target is IDamageable ? target : FindDamageTargetInParents();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            damageMultiplier = GetDefaultMultiplier(zone);
            ResolveReferences(force: true);
        }

        private void OnValidate()
        {
            if (damageMultiplier <= 0f)
                damageMultiplier = GetDefaultMultiplier(zone);
            ResolveReferences();
        }

        private void ResolveReferences(bool force = false)
        {
            if (force || ownerNetworkObject == null)
                ownerNetworkObject = GetComponentInParent<NetworkObject>();
            if (force || damageTarget == null || damageTarget is not IDamageable)
                damageTarget = FindDamageTargetInParents();
        }

        private MonoBehaviour FindDamageTargetInParents()
        {
            Transform current = transform;
            while (current != null)
            {
                MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour != null && behaviour is IDamageable)
                        return behaviour;
                }

                current = current.parent;
            }

            return null;
        }

        public static float GetDefaultMultiplier(HitboxZone hitboxZone)
        {
            return hitboxZone switch
            {
                HitboxZone.Head => 4f,
                HitboxZone.Arm => 0.75f,
                HitboxZone.Leg => 0.5f,
                _ => 1f
            };
        }
    }
}
