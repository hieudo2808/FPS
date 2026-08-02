using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FPS
{
    public readonly struct SessionCredentials
    {
        public readonly SessionPlayerId PlayerId;
        public readonly string ReconnectToken;

        public SessionCredentials(SessionPlayerId playerId, string reconnectToken)
        {
            PlayerId = playerId;
            ReconnectToken = reconnectToken;
        }
    }

    public sealed class PlayerSessionRecord
    {
        public SessionPlayerId PlayerId { get; internal set; }
        public string UnityPlayerId { get; internal set; }
        public ulong ClientId { get; internal set; }
        public bool IsConnected { get; internal set; }
        public double ReservationExpiresAt { get; internal set; }
        public PlayerRuntimeSnapshot Snapshot { get; internal set; }
        internal byte[] TokenHash { get; set; }
    }

    public sealed class PlayerSessionRegistry
    {
        private readonly Dictionary<ulong, PlayerSessionRecord> byStableId = new();
        private readonly Dictionary<ulong, PlayerSessionRecord> byClientId = new();
        private readonly int capacity;
        private ulong nextStableId = 1;
        private uint snapshotRevision;

        public PlayerSessionRegistry(int capacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public int Count => byStableId.Count;
        public int ConnectedCount => byClientId.Count;

        public bool TryRegisterNew(
            string unityPlayerId,
            ulong clientId,
            double now,
            out PlayerSessionRecord record,
            out SessionCredentials credentials,
            out SessionFailureReason failure)
        {
            ExpireReservations(now);
            record = null;
            credentials = default;
            failure = SessionFailureReason.None;

            if (string.IsNullOrWhiteSpace(unityPlayerId))
            {
                failure = SessionFailureReason.Unknown;
                return false;
            }

            foreach (PlayerSessionRecord existing in byStableId.Values)
            {
                if (!string.Equals(existing.UnityPlayerId, unityPlayerId, StringComparison.Ordinal))
                    continue;

                failure = SessionFailureReason.DuplicateConnection;
                return false;
            }

            if (byStableId.Count >= capacity)
            {
                failure = SessionFailureReason.SessionFull;
                return false;
            }

            string token = GenerateToken();
            record = new PlayerSessionRecord
            {
                PlayerId = new SessionPlayerId(nextStableId++),
                UnityPlayerId = unityPlayerId,
                ClientId = clientId,
                IsConnected = true,
                Snapshot = PlayerRuntimeSnapshot.CreateDefault(default, default, UnityEngine.Quaternion.identity),
                TokenHash = HashToken(token)
            };
            record.Snapshot = PlayerRuntimeSnapshot.CreateDefault(record.PlayerId, default, UnityEngine.Quaternion.identity);

            byStableId.Add(record.PlayerId.Value, record);
            byClientId.Add(clientId, record);
            credentials = new SessionCredentials(record.PlayerId, token);
            return true;
        }

        public bool TryReconnect(
            string unityPlayerId,
            SessionPlayerId stableId,
            string token,
            ulong newClientId,
            double now,
            out PlayerSessionRecord record,
            out SessionFailureReason failure)
        {
            ExpireReservations(now);
            failure = SessionFailureReason.None;
            record = null;

            if (!stableId.IsValid || !byStableId.TryGetValue(stableId.Value, out record))
            {
                failure = SessionFailureReason.ReconnectExpired;
                return false;
            }

            if (record.IsConnected)
            {
                failure = SessionFailureReason.DuplicateConnection;
                return false;
            }

            if (record.ReservationExpiresAt <= now)
            {
                failure = SessionFailureReason.ReconnectExpired;
                return false;
            }

            if (!string.Equals(record.UnityPlayerId, unityPlayerId, StringComparison.Ordinal)
                || !TokenMatches(record.TokenHash, token))
            {
                failure = SessionFailureReason.InvalidReconnectToken;
                return false;
            }

            record.ClientId = newClientId;
            record.IsConnected = true;
            record.ReservationExpiresAt = 0.0;
            byClientId[newClientId] = record;
            return true;
        }

        public bool Reserve(ulong clientId, PlayerRuntimeSnapshot snapshot, double expiresAt)
        {
            if (!byClientId.TryGetValue(clientId, out PlayerSessionRecord record))
                return false;

            byClientId.Remove(clientId);
            record.IsConnected = false;
            record.ReservationExpiresAt = expiresAt;
            StoreSnapshot(record, snapshot);
            return true;
        }

        public bool UpdateReservedSnapshot(
            SessionPlayerId stableId,
            PlayerRuntimeSnapshot snapshot,
            double expiresAt)
        {
            if (!stableId.IsValid
                || !byStableId.TryGetValue(stableId.Value, out PlayerSessionRecord record)
                || record.IsConnected)
            {
                return false;
            }

            if (record.ReservationExpiresAt <= 0.0)
                record.ReservationExpiresAt = expiresAt;
            StoreSnapshot(record, snapshot);
            return true;
        }

        public bool Remove(ulong clientId)
        {
            if (!byClientId.TryGetValue(clientId, out PlayerSessionRecord record))
                return false;

            byClientId.Remove(clientId);
            byStableId.Remove(record.PlayerId.Value);
            return true;
        }

        public bool TryGetByClientId(ulong clientId, out PlayerSessionRecord record)
        {
            return byClientId.TryGetValue(clientId, out record);
        }

        public bool TryGetByStableId(SessionPlayerId stableId, out PlayerSessionRecord record)
        {
            return byStableId.TryGetValue(stableId.Value, out record);
        }

        public void ExpireReservations(double now)
        {
            List<ulong> expired = null;
            foreach (KeyValuePair<ulong, PlayerSessionRecord> pair in byStableId)
            {
                PlayerSessionRecord record = pair.Value;
                if (record.IsConnected || record.ReservationExpiresAt <= 0.0 || record.ReservationExpiresAt > now)
                    continue;

                expired ??= new List<ulong>();
                expired.Add(pair.Key);
            }

            if (expired == null)
                return;

            for (int i = 0; i < expired.Count; i++)
                byStableId.Remove(expired[i]);
        }

        public void Clear()
        {
            byClientId.Clear();
            byStableId.Clear();
            nextStableId = 1;
            snapshotRevision = 0;
        }

        private void StoreSnapshot(PlayerSessionRecord record, PlayerRuntimeSnapshot snapshot)
        {
            snapshot.schemaVersion = NetworkProtocol.SnapshotSchemaVersion;
            snapshot.revision = ++snapshotRevision;
            snapshot.sessionPlayerId = record.PlayerId;
            record.Snapshot = snapshot;
        }

        private static string GenerateToken()
        {
            byte[] bytes = new byte[16];
            using RandomNumberGenerator random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static byte[] HashToken(string token)
        {
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty));
        }

        private static bool TokenMatches(byte[] expectedHash, string suppliedToken)
        {
            if (expectedHash == null || string.IsNullOrEmpty(suppliedToken))
                return false;

            byte[] actualHash = HashToken(suppliedToken);
            if (actualHash.Length != expectedHash.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < actualHash.Length; i++)
                difference |= actualHash[i] ^ expectedHash[i];

            return difference == 0;
        }
    }
}
