using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Input payload sent from client to server each tick.
    /// </summary>
    public struct PlayerInputPayload : INetworkSerializable
    {
        public uint sequence;
        public int tick;
        public Vector2 move;
        public bool jumpPressed;   // true for exactly 1 tick
        public bool sprint;
        public bool aim;
        public float yaw;          // player yaw (gameplay), NOT camera pitch
        public float pitch;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            short moveX = 0;
            short moveY = 0;
            ushort quantizedYaw = 0;
            short quantizedPitch = 0;
            byte buttonFlags = 0;
            if (serializer.IsWriter)
            {
                Vector2 clampedMove = Vector2.ClampMagnitude(move, 1f);
                moveX = QuantizeSignedUnit(clampedMove.x);
                moveY = QuantizeSignedUnit(clampedMove.y);
                quantizedYaw = (ushort)Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 360f * ushort.MaxValue);
                quantizedPitch = QuantizeSignedUnit(Mathf.Clamp(pitch, -90f, 90f) / 90f);
                if (jumpPressed) buttonFlags |= 1;
                if (sprint) buttonFlags |= 2;
                if (aim) buttonFlags |= 4;
            }

            serializer.SerializeValue(ref sequence);
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref moveX);
            serializer.SerializeValue(ref moveY);
            serializer.SerializeValue(ref quantizedYaw);
            serializer.SerializeValue(ref quantizedPitch);
            serializer.SerializeValue(ref buttonFlags);

            if (serializer.IsReader)
            {
                move = new Vector2(DequantizeSignedUnit(moveX), DequantizeSignedUnit(moveY));
                yaw = quantizedYaw / (float)ushort.MaxValue * 360f;
                pitch = DequantizeSignedUnit(quantizedPitch) * 90f;
                jumpPressed = (buttonFlags & 1) != 0;
                sprint = (buttonFlags & 2) != 0;
                aim = (buttonFlags & 4) != 0;
            }
        }

        private static short QuantizeSignedUnit(float value)
        {
            return (short)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * short.MaxValue);
        }

        private static float DequantizeSignedUnit(short value)
        {
            return Mathf.Clamp(value / (float)short.MaxValue, -1f, 1f);
        }
    }

    public struct PlayerCommandPacket : INetworkSerializable
    {
        public byte commandCount;
        public PlayerInputPayload latest;
        public PlayerInputPayload previous1;
        public PlayerInputPayload previous2;

        public PlayerInputPayload GetCommand(int index)
        {
            return index switch
            {
                0 => latest,
                1 => previous1,
                2 => previous2,
                _ => default
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref commandCount);
            serializer.SerializeValue(ref latest);
            serializer.SerializeValue(ref previous1);
            serializer.SerializeValue(ref previous2);
        }
    }

    /// <summary>
    /// Authoritative state payload sent from server to clients.
    /// </summary>
    public struct PlayerStatePayload : INetworkSerializable
    {
        public int tick;
        public uint lastProcessedCommand;
        public Vector3 position;
        public float planarSpeed;
        public float verticalVelocity;
        public bool grounded;
        public float yaw;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref lastProcessedCommand);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref planarSpeed);
            serializer.SerializeValue(ref verticalVelocity);
            serializer.SerializeValue(ref grounded);
            serializer.SerializeValue(ref yaw);
        }
    }

    public static class NetworkSequence
    {
        public static bool IsNewer(uint incoming, uint previous)
        {
            return incoming != previous && unchecked((int)(incoming - previous)) > 0;
        }

        public static bool IsNewer(ushort incoming, ushort previous)
        {
            return incoming != previous && unchecked((short)(incoming - previous)) > 0;
        }
    }
}
