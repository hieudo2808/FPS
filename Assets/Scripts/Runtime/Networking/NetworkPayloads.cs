using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Input payload sent from client to server each tick.
    /// </summary>
    public struct PlayerInputPayload : INetworkSerializable
    {
        public int tick;
        public Vector2 move;
        public bool jumpPressed;   // true for exactly 1 tick
        public bool sprint;
        public float yaw;          // player yaw (gameplay), NOT camera pitch

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref move);
            serializer.SerializeValue(ref jumpPressed);
            serializer.SerializeValue(ref sprint);
            serializer.SerializeValue(ref yaw);
        }
    }

    /// <summary>
    /// Authoritative state payload sent from server to clients.
    /// </summary>
    public struct PlayerStatePayload : INetworkSerializable
    {
        public int tick;
        public Vector3 position;
        public float verticalVelocity;
        public bool grounded;
        public float yaw;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref verticalVelocity);
            serializer.SerializeValue(ref grounded);
            serializer.SerializeValue(ref yaw);
        }
    }
}
