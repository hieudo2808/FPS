using UnityEngine;

namespace FPS
{
    [System.Flags]
    public enum DamageType
    {
        Unspecified = 0,
        Bullet = 1 << 0,
        Explosion = 1 << 1,
        Melee = 1 << 2,
        Environment = 1 << 3,
        All = Bullet | Explosion | Melee | Environment
    }

    public readonly struct DamageInfo
    {
        public readonly float amount;
        public readonly ulong attackerClientId;
        public readonly int attackerPlayerIndex;
        public readonly Vector3 hitPoint;
        public readonly bool isHeadshot;
        public readonly float reactionTime;
        public readonly DamageType damageType;
        public readonly HitboxZone hitZone;
        public readonly float damageMultiplier;

        public bool HasAttacker => amount > 0f && (attackerPlayerIndex >= 0 || attackerClientId != ulong.MaxValue);
        public DamageType DamageType => damageType;
        public HitboxZone HitZone => hitZone;
        public float DamageMultiplier => damageMultiplier;

        public DamageInfo(
            float amount,
            ulong attackerClientId = ulong.MaxValue,
            int attackerPlayerIndex = -1,
            Vector3 hitPoint = default,
            bool isHeadshot = false,
            float reactionTime = 0f,
            DamageType damageType = DamageType.Unspecified,
            HitboxZone hitZone = HitboxZone.Body,
            float damageMultiplier = 1f)
        {
            bool resolvedHeadshot = isHeadshot || hitZone == HitboxZone.Head;
            this.amount = amount;
            this.attackerClientId = attackerClientId;
            this.attackerPlayerIndex = attackerPlayerIndex;
            this.hitPoint = hitPoint;
            this.isHeadshot = resolvedHeadshot;
            this.reactionTime = reactionTime;
            this.damageType = damageType;
            this.hitZone = resolvedHeadshot && hitZone == HitboxZone.Body ? HitboxZone.Head : hitZone;
            this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
        }
    }

    public interface IAttributedDamageable : IDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
    }
}
