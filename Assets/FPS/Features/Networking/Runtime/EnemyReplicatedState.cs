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

        public bool Equals(EnemyReplicatedState other)
        {
            return locomotion == other.locomotion
                && normalizedSpeed == other.normalizedSpeed
                && actionFlags == other.actionFlags
                && specialActionKind == other.specialActionKind
                && actionSequence == other.actionSequence
                && actionStartServerTick == other.actionStartServerTick
                && specialAbilityDeadlineTick == other.specialAbilityDeadlineTick;
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
        }
    }
}
