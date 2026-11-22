using System;
using Unity.Netcode;

namespace FPS
{
    public enum EnemyLocomotionState : byte
    {
        Idle,
        Moving,
        Attacking,
        Dead
    }

    public enum EnemySpecialActionKind : byte
    {
        None,
        Primary,
        Secondary,
        Stagger
    }

    /// <summary>What the server decided a burst of hits does. Flinch is presentation only.</summary>
    public enum EnemyHitReactionKind : byte
    {
        None,
        Flinch,
        Stumble,
        Interrupt
    }

    [Flags]
    public enum EnemyActionFlags : byte
    {
        None = 0,
        Attack = 1 << 0,
        Stagger = 1 << 1,
        Dead = 1 << 2,
        SpecialAbility = 1 << 3
    }

    public struct EnemyReplicatedState : INetworkSerializable, IEquatable<EnemyReplicatedState>
    {
        public EnemyLocomotionState locomotion;
        public byte normalizedSpeed;
        public EnemyActionFlags actionFlags;
        public EnemySpecialActionKind specialActionKind;
        public ushort actionSequence;
        public int actionStartServerTick;
        public int specialAbilityDeadlineTick;

        // Latest server-confirmed hit. Clients present a hit once per hitSequence change.
        public byte hitSequence;
        public EnemyHitReactionKind hitKind;
        public byte hitSegment;   // index into the prefab's HitboxSegment list, 255 = none
        public sbyte hitOffsetX, hitOffsetY, hitOffsetZ; // centimetres in the segment's local space
        public byte hitYaw;       // bullet travel direction around the enemy's up axis, 256 steps
        public byte hitStrength;  // 0..255

        public bool Equals(EnemyReplicatedState other)
        {
            return locomotion == other.locomotion
                && normalizedSpeed == other.normalizedSpeed
                && actionFlags == other.actionFlags
                && specialActionKind == other.specialActionKind
                && actionSequence == other.actionSequence
                && actionStartServerTick == other.actionStartServerTick
                && specialAbilityDeadlineTick == other.specialAbilityDeadlineTick
                && hitSequence == other.hitSequence
                && hitKind == other.hitKind
                && hitSegment == other.hitSegment
                && hitOffsetX == other.hitOffsetX
                && hitOffsetY == other.hitOffsetY
                && hitOffsetZ == other.hitOffsetZ
                && hitYaw == other.hitYaw
                && hitStrength == other.hitStrength;
        }

        public override bool Equals(object obj) => obj is EnemyReplicatedState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)locomotion;
                hash = (hash * 397) ^ normalizedSpeed;
                hash = (hash * 397) ^ (int)actionFlags;
                hash = (hash * 397) ^ (int)specialActionKind;
                hash = (hash * 397) ^ actionSequence;
                hash = (hash * 397) ^ actionStartServerTick;
                hash = (hash * 397) ^ specialAbilityDeadlineTick;
                hash = (hash * 397) ^ hitSequence;
                return hash;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref locomotion);
            serializer.SerializeValue(ref normalizedSpeed);
            serializer.SerializeValue(ref actionFlags);
            serializer.SerializeValue(ref specialActionKind);
            serializer.SerializeValue(ref actionSequence);
            serializer.SerializeValue(ref actionStartServerTick);
            serializer.SerializeValue(ref specialAbilityDeadlineTick);
            serializer.SerializeValue(ref hitSequence);
            serializer.SerializeValue(ref hitKind);
            serializer.SerializeValue(ref hitSegment);
            serializer.SerializeValue(ref hitOffsetX);
            serializer.SerializeValue(ref hitOffsetY);
            serializer.SerializeValue(ref hitOffsetZ);
            serializer.SerializeValue(ref hitYaw);
            serializer.SerializeValue(ref hitStrength);
        }
    }
}
