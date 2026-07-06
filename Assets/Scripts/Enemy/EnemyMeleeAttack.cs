using UnityEngine;

namespace FPS
{
    public sealed class EnemyMeleeAttack
    {
        private float range;
        private float damage;
        private float cooldown;
        private float impactDelay;
        private float minimumImpactDelay;
        private float actionLockDuration;
        private float hitArcDegrees;

        private float lastAttackTime;
        private bool hasLastAttackTime;
        private bool damagePending;
        private float pendingImpactTime;
        private float actionLockedUntil;
        private Transform pendingTarget;
        private Vector3 lockedFacingDirection;
        private bool hasLockedFacingDirection;

        public bool HasPendingDamage => damagePending;

        public void Configure(
            float range,
            float damage,
            float cooldown,
            float impactDelay,
            float minimumImpactDelay,
            float actionLockDuration,
            float hitArcDegrees)
        {
            this.range = range;
            this.damage = damage;
            this.cooldown = cooldown;
            this.impactDelay = impactDelay;
            this.minimumImpactDelay = minimumImpactDelay;
            this.actionLockDuration = actionLockDuration;
            this.hitArcDegrees = hitArcDegrees;
        }

        public bool TryBegin(Transform owner, Transform target, float now)
        {
            if (owner == null || target == null)
                return false;

            if (IsActionLocked(now))
                return false;

            if (damagePending)
                return false;

            if (hasLastAttackTime && now - lastAttackTime < cooldown)
                return false;

            lastAttackTime = now;
            hasLastAttackTime = true;
            pendingTarget = target;
            pendingImpactTime = now + GetImpactDelay();
            actionLockedUntil = now + GetActionLockDuration();
            CaptureFacing(owner, target);
            damagePending = true;
            return true;
        }

        public bool TryConsumeImpact(
            Transform owner,
            Transform fallbackTarget,
            float now,
            bool forceImpact,
            out Transform hitTarget,
            out float hitDamage)
        {
            hitTarget = null;
            hitDamage = 0f;

            if (!damagePending)
                return false;

            if (!forceImpact && now < pendingImpactTime)
                return false;

            Transform target = pendingTarget != null ? pendingTarget : fallbackTarget;
            damagePending = false;
            pendingTarget = null;

            if (!CanHit(owner, target))
                return false;

            hitTarget = target;
            hitDamage = damage;
            return true;
        }

        public void CancelPendingDamage()
        {
            damagePending = false;
            pendingImpactTime = 0f;
            pendingTarget = null;
        }

        public void ClearActionLock()
        {
            actionLockedUntil = 0f;
            lockedFacingDirection = Vector3.zero;
            hasLockedFacingDirection = false;
        }

        public void Reset()
        {
            hasLastAttackTime = false;
            lastAttackTime = 0f;
            CancelPendingDamage();
            ClearActionLock();
        }

        public bool IsActionLocked(float now)
        {
            return now < actionLockedUntil;
        }

        public bool TryGetLockedFacing(float now, out Vector3 facingDirection)
        {
            facingDirection = lockedFacingDirection;
            return IsActionLocked(now) && hasLockedFacingDirection;
        }

        public bool CanHit(Transform owner, Transform target)
        {
            if (owner == null || target == null)
                return false;

            Vector3 toTarget = target.position - owner.position;
            toTarget.y = 0f;

            float distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr > range * range)
                return false;

            if (distanceSqr < 0.0001f)
                return true;

            float minDot = Mathf.Cos(hitArcDegrees * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(owner.forward, toTarget.normalized) >= minDot;
        }

        private float GetImpactDelay()
        {
            return Mathf.Max(impactDelay, minimumImpactDelay);
        }

        private float GetActionLockDuration()
        {
            return Mathf.Max(GetImpactDelay(), actionLockDuration);
        }

        private void CaptureFacing(Transform owner, Transform target)
        {
            Vector3 direction = target.position - owner.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = owner.forward;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                lockedFacingDirection = Vector3.zero;
                hasLockedFacingDirection = false;
                return;
            }

            lockedFacingDirection = direction.normalized;
            hasLockedFacingDirection = true;
        }
    }
}
