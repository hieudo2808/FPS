using System;
using System.Collections.Generic;

namespace FPS
{
    public enum PickupResultCode : byte
    {
        Accepted,
        AlreadyClaimed,
        InvalidPlayer,
        TargetMissing,
        NotInteractable,
        OutOfRange,
        LineOfSightBlocked,
        InventoryFull,
        RateLimited,
        InvalidSequence,
        ServerUnavailable
    }

    public readonly struct PickupTransactionResult
    {
        public readonly ulong TargetNetworkObjectId;
        public readonly uint RequestSequence;
        public readonly PickupResultCode Code;

        public PickupTransactionResult(ulong targetNetworkObjectId, uint requestSequence, PickupResultCode code)
        {
            TargetNetworkObjectId = targetNetworkObjectId;
            RequestSequence = requestSequence;
            Code = code;
        }
    }

    /// <summary>
    /// Session-scoped, server-only idempotency and rate-limit ledger for pickup requests.
    /// Unity executes the callback on its main thread, so marking a successful claim before
    /// returning makes the transaction atomic with respect to all InteractionManager RPCs.
    /// </summary>
    public sealed class PickupTransactionService
    {
        private const int MaxCachedResultsPerClient = 64;
        private const double RateWindowSeconds = 1.0;

        private sealed class ClientLedger
        {
            public readonly Dictionary<uint, PickupTransactionResult> Results = new();
            public readonly Queue<uint> ResultOrder = new();
            public readonly Queue<double> RequestTimes = new();
            public uint LastSequence;
            public bool HasSequence;
        }

        private readonly Dictionary<ulong, ClientLedger> clients = new();
        private readonly HashSet<ulong> claimedObjects = new();
        private readonly int maxRequestsPerSecond;

        public PickupTransactionService(int maxRequestsPerSecond)
        {
            this.maxRequestsPerSecond = Math.Max(1, maxRequestsPerSecond);
        }

        public PickupTransactionResult Execute(
            ulong clientId,
            uint requestSequence,
            ulong targetNetworkObjectId,
            double now,
            Func<PickupResultCode> validateAndApply)
        {
            ClientLedger ledger = GetClientLedger(clientId);
            if (ledger.Results.TryGetValue(requestSequence, out PickupTransactionResult cached))
                return cached;

            if (ledger.HasSequence && !NetworkSequence.IsNewer(requestSequence, ledger.LastSequence))
                return Cache(ledger, new PickupTransactionResult(
                    targetNetworkObjectId, requestSequence, PickupResultCode.InvalidSequence));

            ledger.LastSequence = requestSequence;
            ledger.HasSequence = true;

            while (ledger.RequestTimes.Count > 0 && now - ledger.RequestTimes.Peek() >= RateWindowSeconds)
                ledger.RequestTimes.Dequeue();

            if (ledger.RequestTimes.Count >= maxRequestsPerSecond)
                return Cache(ledger, new PickupTransactionResult(
                    targetNetworkObjectId, requestSequence, PickupResultCode.RateLimited));

            ledger.RequestTimes.Enqueue(now);

            if (claimedObjects.Contains(targetNetworkObjectId))
                return Cache(ledger, new PickupTransactionResult(
                    targetNetworkObjectId, requestSequence, PickupResultCode.AlreadyClaimed));

            PickupResultCode code = validateAndApply != null
                ? validateAndApply()
                : PickupResultCode.TargetMissing;

            if (code == PickupResultCode.Accepted)
                claimedObjects.Add(targetNetworkObjectId);

            return Cache(ledger, new PickupTransactionResult(targetNetworkObjectId, requestSequence, code));
        }

        public bool IsClaimed(ulong targetNetworkObjectId) => claimedObjects.Contains(targetNetworkObjectId);

        public void Clear()
        {
            clients.Clear();
            claimedObjects.Clear();
        }

        private ClientLedger GetClientLedger(ulong clientId)
        {
            if (!clients.TryGetValue(clientId, out ClientLedger ledger))
            {
                ledger = new ClientLedger();
                clients.Add(clientId, ledger);
            }

            return ledger;
        }

        private static PickupTransactionResult Cache(ClientLedger ledger, PickupTransactionResult result)
        {
            ledger.Results[result.RequestSequence] = result;
            ledger.ResultOrder.Enqueue(result.RequestSequence);

            while (ledger.ResultOrder.Count > MaxCachedResultsPerClient)
            {
                uint oldest = ledger.ResultOrder.Dequeue();
                ledger.Results.Remove(oldest);
            }

            return result;
        }
    }
}
