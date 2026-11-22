using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Per-body-part hit reaction. The server merges the hits of one burst (all pellets of a
    /// shotgun shell, or rapid fire inside <see cref="EnemyHitReactionRules.PresentInterval"/>),
    /// decides the gameplay effect, and replicates where and how hard the enemy was hit.
    /// Every peer, the host included, presents the flinch and blood from that replicated hit.
    /// </summary>
    public partial class EnemyAI
    {
        private const byte NoHitSegment = byte.MaxValue;
        private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");

        private HitboxSegment[] hitSegments;
        private EnemyHitReactionPresenter hitPresenter;

        // Server burst accumulation.
        private bool hasPendingHit;
        private float pendingHitDamage;
        private bool pendingHitHeadshot;
        private DamageInfo pendingHitStrongest;
        private float lastHitPublishTime = float.NegativeInfinity;
        private float lastGameplayHitReactionTime = float.NegativeInfinity;

        // Last published hit, copied into EnemyReplicatedState.
        private byte serverHitSequence;
        private EnemyHitReactionKind serverHitKind;
        private byte serverHitSegment = NoHitSegment;
        private sbyte serverHitOffsetX, serverHitOffsetY, serverHitOffsetZ;
        private byte serverHitYaw, serverHitStrength;
        private byte lastPresentedHitSequence;

        /// <summary>The reaction most recently decided by the server. For tests and diagnostics.</summary>
        public EnemyHitReactionKind LastHitReaction => serverHitKind;
        public int HitReactionCount => serverHitSequence;

        private HitboxSegment[] HitSegments => hitSegments ??= GetComponentsInChildren<HitboxSegment>(true);

        private EnemyHitReactionPresenter HitPresenter
        {
            get
            {
                if (hitPresenter == null && !TryGetComponent(out hitPresenter))
                    hitPresenter = gameObject.AddComponent<EnemyHitReactionPresenter>();
                return hitPresenter;
            }
        }

        private void OnServerDamageApplied(DamageInfo info)
        {
            if (!CanRunServerLogic() || currentState == State.Dead || !EnemyHitReactionRules.Reacts(info.damageType))
                return;

            if (!hasPendingHit || info.amount > pendingHitStrongest.amount)
                pendingHitStrongest = info;
            hasPendingHit = true;
            pendingHitDamage += info.amount;
            pendingHitHeadshot |= info.isHeadshot;
        }

        /// <summary>Called once per server frame before the replicated state is published.</summary>
        private void ResolvePendingHit()
        {
            if (!hasPendingHit || Time.time - lastHitPublishTime < EnemyHitReactionRules.PresentInterval)
                return;

            float maxHealth = pressureHealth != null ? pressureHealth.MaxHealth : 0f;
            float sinceGameplay = float.IsNegativeInfinity(lastGameplayHitReactionTime)
                ? -1f
                : Time.time - lastGameplayHitReactionTime;
            EnemyHitReactionKind kind = EnemyHitReactionRules.Resolve(
                pendingHitStrongest.hitZone,
                pendingHitHeadshot,
                pendingHitDamage,
                maxHealth,
                alive: currentState != State.Dead && (pressureHealth == null || !pressureHealth.IsDead),
                gameplayAllowed: UsesGenericServerBrain && !specialAbilityActive,
                attackWindingUp: currentState == State.Attack && meleeAttack.HasPendingDamage,
                secondsSinceGameplayReaction: sinceGameplay);
            float strength = EnemyHitReactionRules.Strength(pendingHitDamage, maxHealth);
            DamageInfo strongest = pendingHitStrongest;
            hasPendingHit = false;
            pendingHitDamage = 0f;
            pendingHitHeadshot = false;
            if (kind == EnemyHitReactionKind.None)
                return;

            lastHitPublishTime = Time.time;
            ApplyHitGameplay(kind);
            RecordServerHit(kind, strongest, kind == EnemyHitReactionKind.Interrupt ? 1f : strength);
            PresentHit(serverHitKind, serverHitSegment, serverHitOffsetX, serverHitOffsetY, serverHitOffsetZ,
                serverHitYaw, serverHitStrength);
            lastPresentedHitSequence = serverHitSequence;
        }

        private void ApplyHitGameplay(EnemyHitReactionKind kind)
        {
            switch (kind)
            {
                case EnemyHitReactionKind.Interrupt:
                    // The swing has not landed yet: it deals no damage and the enemy recoils.
                    meleeAttack.CancelPendingDamage();
                    meleeAttack.ClearActionLock();
                    ReleaseMotionLock(genericAttackMotionLock);
                    genericAttackMotionLock = default;
                    AcquireMotionLock(EnemyHitReactionRules.InterruptSeconds);
                    ForcePathRefresh();
                    lastGameplayHitReactionTime = Time.time;
                    break;
                case EnemyHitReactionKind.Stumble:
                    if (IsMotionLocked) return; // An attack or ability already holds the body still.
                    AcquireMotionLock(EnemyHitReactionRules.StumbleSeconds);
                    // The lock clears the NavMesh path; ask for a fresh one once it releases.
                    ForcePathRefresh();
                    lastGameplayHitReactionTime = Time.time;
                    break;
            }
        }

        private void RecordServerHit(EnemyHitReactionKind kind, DamageInfo info, float strength)
        {
            serverHitSequence = (byte)(serverHitSequence == byte.MaxValue ? 1 : serverHitSequence + 1); // 0 means "no hit yet"
            serverHitKind = kind;
            serverHitStrength = EnemyHitReactionRules.QuantizeStrength(strength);

            Vector3 travel = info.sourcePosition.HasValue && info.hitPoint != Vector3.zero
                ? info.hitPoint - info.sourcePosition.Value
                : Vector3.zero;
            travel.y = 0f;
            if (travel.sqrMagnitude < 0.0001f) travel = transform.forward; // Unknown source: knock straight back.
            serverHitYaw = EnemyHitReactionRules.QuantizeYaw(Quaternion.Inverse(transform.rotation) * travel.normalized);

            serverHitSegment = NoHitSegment;
            serverHitOffsetX = serverHitOffsetY = serverHitOffsetZ = 0;
            if (info.hitPoint == Vector3.zero || !TryFindHitSegment(info.hitZone, info.hitPoint, out int index, out Vector3 surface))
                return;

            Transform segment = HitSegments[index].transform;
            Vector3 offset = Quaternion.Inverse(segment.rotation) * (surface - segment.position);
            serverHitSegment = (byte)index;
            serverHitOffsetX = EnemyHitReactionRules.QuantizeCentimetres(offset.x);
            serverHitOffsetY = EnemyHitReactionRules.QuantizeCentimetres(offset.y);
            serverHitOffsetZ = EnemyHitReactionRules.QuantizeCentimetres(offset.z);
        }

        /// <summary>
        /// The hit point comes from a rewound pose. Pick the segment of the same zone that is closest
        /// now and clamp the point onto its collider so the blood starts on the body.
        /// </summary>
        private bool TryFindHitSegment(HitboxZone zone, Vector3 point, out int bestIndex, out Vector3 surface)
        {
            bestIndex = -1;
            surface = point;
            float best = float.MaxValue;
            HitboxSegment[] segments = HitSegments;
            for (int pass = 0; pass < 2 && bestIndex < 0; pass++)
            {
                for (int index = 0; index < segments.Length && index < NoHitSegment; index++)
                {
                    HitboxSegment segment = segments[index];
                    if (segment == null || pass == 0 && segment.Zone != zone) continue;
                    Collider body = segment.GetComponent<Collider>();
                    Vector3 closest = body != null && body.enabled ? body.ClosestPoint(point) : segment.transform.position;
                    float distance = (closest - point).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    bestIndex = index;
                    surface = closest;
                }
            }
            return bestIndex >= 0;
        }

        private void PresentReplicatedHit(EnemyReplicatedState current, bool force)
        {
            if (current.hitSequence == lastPresentedHitSequence) return;
            lastPresentedHitSequence = current.hitSequence;
            // A late join or pool reuse must not replay an old hit.
            if (force || current.hitSequence == 0) return;
            PresentHit(current.hitKind, current.hitSegment, current.hitOffsetX, current.hitOffsetY, current.hitOffsetZ,
                current.hitYaw, current.hitStrength);
        }

        private void PresentHit(EnemyHitReactionKind kind, byte segmentIndex, sbyte x, sbyte y, sbyte z, byte yaw, byte strength)
        {
            if (kind == EnemyHitReactionKind.None) return;
            if (voice != null) voice.PlayHurt();
            if (kind == EnemyHitReactionKind.Interrupt && animator != null)
            {
                animator.ResetTrigger(AnimAttack);
                if (animator.HasState(0, LocomotionStateHash))
                    animator.CrossFadeInFixedTime(LocomotionStateHash, 0.12f, 0);
            }

            HitboxSegment[] segments = HitSegments;
            HitboxSegment segment = segmentIndex < segments.Length ? segments[segmentIndex] : null;
            Vector3 travel = transform.rotation * EnemyHitReactionRules.DequantizeYaw(yaw);
            HitPresenter.Play(segment, new Vector3(x, y, z) * 0.01f, travel, strength / 255f, kind);
        }

        private void ResetHitReaction()
        {
            hasPendingHit = false;
            pendingHitDamage = 0f;
            pendingHitHeadshot = false;
            lastHitPublishTime = float.NegativeInfinity;
            lastGameplayHitReactionTime = float.NegativeInfinity;
            serverHitSequence = 0;
            serverHitKind = EnemyHitReactionKind.None;
            serverHitSegment = NoHitSegment;
            serverHitOffsetX = serverHitOffsetY = serverHitOffsetZ = 0;
            serverHitYaw = serverHitStrength = 0;
            lastPresentedHitSequence = 0;
            if (hitPresenter != null) hitPresenter.Clear();
        }
    }
}
