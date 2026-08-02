using System;
using System.Text;
using System.Threading;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public static class NetworkProtocol
    {
        public const ushort Version = 2;
        public const ushort SnapshotSchemaVersion = 1;
    }

    public enum SessionState : byte
    {
        Offline,
        StartingHost,
        Joining,
        Lobby,
        LoadingMatch,
        InMatch,
        Reconnecting,
        ShuttingDown,
        Failed
    }

    public enum SessionFailureReason : byte
    {
        None,
        Busy,
        ServicesUnavailable,
        InvalidJoinCode,
        TransportStartFailed,
        ProtocolMismatch,
        BuildMismatch,
        SessionFull,
        MatchAlreadyStarted,
        InvalidReconnectToken,
        ReconnectExpired,
        DuplicateConnection,
        SceneLoadFailed,
        SceneLoadTimedOut,
        OperationTimedOut,
        Cancelled,
        HostUnavailable,
        Unknown
    }

    public enum SessionDisconnectReason : byte
    {
        UserLeft,
        HostEndedSession,
        HostUnavailable,
        Kicked,
        ProtocolRejected,
        ReconnectExpired,
        TransportFailure
    }

    public enum ConnectionIntent : byte
    {
        NewPlayer,
        Reconnect
    }

    [Serializable]
    public struct ConnectionPayload
    {
        private const int BuildFieldBytes = 64;
        private const int PlayerIdFieldBytes = 64;
        private const int TokenFieldBytes = 64;
        private const int PlayerNameFieldBytes = 64;
        public const int EncodedSize = 2 + 1 + 8 + BuildFieldBytes + PlayerIdFieldBytes + TokenFieldBytes + PlayerNameFieldBytes;

        public ushort protocolVersion;
        public string buildVersion;
        public string unityPlayerId;
        public ConnectionIntent intent;
        public ulong sessionPlayerId;
        public string reconnectToken;
        public string playerName;

        public static byte[] Encode(ConnectionPayload payload)
        {
            var bytes = new byte[EncodedSize];
            bytes[0] = (byte)payload.protocolVersion;
            bytes[1] = (byte)(payload.protocolVersion >> 8);
            bytes[2] = (byte)payload.intent;
            WriteUInt64(bytes, 3, payload.sessionPlayerId);
            int offset = 11;
            WriteFixedUtf8(bytes, ref offset, BuildFieldBytes, payload.buildVersion);
            WriteFixedUtf8(bytes, ref offset, PlayerIdFieldBytes, payload.unityPlayerId);
            WriteFixedUtf8(bytes, ref offset, TokenFieldBytes, payload.reconnectToken);
            WriteFixedUtf8(bytes, ref offset, PlayerNameFieldBytes, payload.playerName);
            return bytes;
        }

        public static bool TryDecode(byte[] bytes, out ConnectionPayload payload)
        {
            payload = default;
            if (bytes == null || bytes.Length != EncodedSize)
                return false;

            try
            {
                payload.protocolVersion = (ushort)(bytes[0] | (bytes[1] << 8));
                payload.intent = (ConnectionIntent)bytes[2];
                payload.sessionPlayerId = ReadUInt64(bytes, 3);
                int offset = 11;
                payload.buildVersion = ReadFixedUtf8(bytes, ref offset, BuildFieldBytes);
                payload.unityPlayerId = ReadFixedUtf8(bytes, ref offset, PlayerIdFieldBytes);
                payload.reconnectToken = ReadFixedUtf8(bytes, ref offset, TokenFieldBytes);
                payload.playerName = ReadFixedUtf8(bytes, ref offset, PlayerNameFieldBytes);
                return payload.protocolVersion != 0
                    && Enum.IsDefined(typeof(ConnectionIntent), payload.intent)
                    && !string.IsNullOrWhiteSpace(payload.buildVersion)
                    && !string.IsNullOrWhiteSpace(payload.unityPlayerId);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteFixedUtf8(byte[] destination, ref int offset, int fieldBytes, string value)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (encoded.Length > fieldBytes - 1)
                throw new ArgumentException($"Connection field exceeds {fieldBytes - 1} UTF-8 bytes.");

            destination[offset] = (byte)encoded.Length;
            Buffer.BlockCopy(encoded, 0, destination, offset + 1, encoded.Length);
            offset += fieldBytes;
        }

        private static string ReadFixedUtf8(byte[] source, ref int offset, int fieldBytes)
        {
            int length = source[offset];
            if (length > fieldBytes - 1)
                throw new FormatException("Invalid fixed UTF-8 field length.");

            string value = Encoding.UTF8.GetString(source, offset + 1, length);
            offset += fieldBytes;
            return value;
        }

        private static void WriteUInt64(byte[] destination, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                destination[offset + i] = (byte)(value >> (i * 8));
        }

        private static ulong ReadUInt64(byte[] source, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++)
                value |= (ulong)source[offset + i] << (i * 8);
            return value;
        }
    }

    public readonly struct SessionOperationResult
    {
        public readonly bool Succeeded;
        public readonly SessionFailureReason FailureReason;
        public readonly string Message;

        private SessionOperationResult(bool succeeded, SessionFailureReason failureReason, string message)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Message = message ?? string.Empty;
        }

        public static SessionOperationResult Success() => new(true, SessionFailureReason.None, string.Empty);
        public static SessionOperationResult Failure(SessionFailureReason reason, string message) => new(false, reason, message);
    }

    [Serializable]
    public struct SessionPlayerId : INetworkSerializable, IEquatable<SessionPlayerId>
    {
        public ulong Value;

        public SessionPlayerId(ulong value) => Value = value;
        public bool IsValid => Value != 0;
        public bool Equals(SessionPlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SessionPlayerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value);
        }
    }

    public readonly struct SessionOperation
    {
        internal readonly int Version;
        public readonly CancellationToken Token;

        internal SessionOperation(int version, CancellationToken token)
        {
            Version = version;
            Token = token;
        }
    }

    public sealed class SessionCoordinator : IDisposable
    {
        private CancellationTokenSource activeOperation;
        private int operationVersion;

        public SessionState State { get; private set; } = SessionState.Offline;
        public event Action<SessionState, SessionState> StateChanged;
        public bool HasActiveOperation => activeOperation != null;

        public bool TryBegin(SessionState transitionalState, TimeSpan timeout, out SessionOperation operation)
        {
            operation = default;
            if (activeOperation != null)
                return false;

            activeOperation = new CancellationTokenSource(timeout);
            operationVersion++;
            Transition(transitionalState);
            operation = new SessionOperation(operationVersion, activeOperation.Token);
            return true;
        }

        public bool Complete(SessionOperation operation, SessionState nextState)
        {
            if (operation.Version != operationVersion || activeOperation == null)
                return false;

            activeOperation.Dispose();
            activeOperation = null;
            Transition(nextState);
            return true;
        }

        public void CancelActive(SessionState nextState = SessionState.Offline)
        {
            if (activeOperation != null)
            {
                activeOperation.Cancel();
                activeOperation.Dispose();
                activeOperation = null;
                operationVersion++;
            }

            Transition(nextState);
        }

        public void Transition(SessionState nextState)
        {
            if (State == nextState)
                return;

            SessionState previous = State;
            State = nextState;
            StateChanged?.Invoke(previous, nextState);
        }

        public void Dispose() => CancelActive(SessionState.Offline);
    }
}
