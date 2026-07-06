using UnityEngine;

namespace FPS
{
    public readonly struct DamageInfo
    {
        public readonly float amount;
        public readonly ulong attackerClientId;
        public readonly int attackerPlayerIndex;
        public readonly Vector3 hitPoint;
        public readonly bool isHeadshot;
        public readonly float reactionTime;

        public bool HasAttacker => amount > 0f && (attackerPlayerIndex >= 0 || attackerClientId != ulong.MaxValue);

        public DamageInfo(
            float amount,
            ulong attackerClientId = ulong.MaxValue,
            int attackerPlayerIndex = -1,
            Vector3 hitPoint = default,
            bool isHeadshot = false,
            float reactionTime = 0f)
        {
            this.amount = amount;
            this.attackerClientId = attackerClientId;
            this.attackerPlayerIndex = attackerPlayerIndex;
            this.hitPoint = hitPoint;
            this.isHeadshot = isHeadshot;
            this.reactionTime = reactionTime;
        }
    }

    public interface IAttributedDamageable : IDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
    }
}
