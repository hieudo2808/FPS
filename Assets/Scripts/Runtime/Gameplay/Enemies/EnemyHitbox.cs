using UnityEngine;

namespace FPS
{
    public enum HitZone
    {
        Body,
        Head
    }

    public class EnemyHitbox : HitboxSegment
    {
        [SerializeField] private HitZone hitZone = HitZone.Body;

        public override HitboxZone Zone => hitZone == HitZone.Head ? HitboxZone.Head : HitboxZone.Body;
        public override float DamageMultiplier => HitboxSegment.GetDefaultMultiplier(Zone);
    }
}
