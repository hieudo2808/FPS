using System;
using Unity.Netcode;

namespace FPS
{
    // One replicated value keeps kind and all action timestamps coherent on remote peers.
    public struct GrenadeThrowState : INetworkSerializable, IEquatable<GrenadeThrowState>
    {
        public bool Active;
        public uint Sequence;
        public ThrowableKind Kind;
        public double StartedAt, ReleaseAt, EndsAt;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Active);
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref StartedAt);
            serializer.SerializeValue(ref ReleaseAt);
            serializer.SerializeValue(ref EndsAt);
        }

        public bool Equals(GrenadeThrowState other) => Active == other.Active && Sequence == other.Sequence
            && Kind == other.Kind && StartedAt == other.StartedAt && ReleaseAt == other.ReleaseAt && EndsAt == other.EndsAt;
    }
}
