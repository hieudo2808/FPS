using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class A1NetworkingHardeningTests
    {
        [Test]
        public void NetworkSequence_IsWrapSafeAndRejectsDuplicates()
        {
            Assert.True(NetworkSequence.IsNewer(0u, uint.MaxValue));
            Assert.True(NetworkSequence.IsNewer(5u, 4u));
            Assert.False(NetworkSequence.IsNewer(4u, 5u));
            Assert.False(NetworkSequence.IsNewer(5u, 5u));
        }

        [Test]
        public void InputSilence_NeutralizesImmediatelyAfterOneHundredMilliseconds()
        {
            int maxRepeatedTicks = Mathf.CeilToInt(
                NetworkHardeningSettings.Default.InputSilenceSeconds * NetworkGameplayPolicy.SimulationHz);
            Assert.AreEqual(6, maxRepeatedTicks);
            Assert.False(NetworkGameplayPolicy.ShouldNeutralizeInput(6, maxRepeatedTicks));
            Assert.True(NetworkGameplayPolicy.ShouldNeutralizeInput(7, maxRepeatedTicks));
        }

        [Test]
        public void ReconnectReservation_HasTransportAllowanceWithoutExtendingClientDeadline()
        {
            Assert.AreEqual(60f, NetworkHardeningSettings.Default.ReconnectGraceSeconds);
            Assert.AreEqual(5f, NetworkHardeningSettings.Default.ReconnectTransportGraceSeconds);
            Assert.AreEqual(65f, NetworkHardeningSettings.Default.ReconnectReservationSeconds);
        }

        [Test]
        public void ConnectionPayload_UsesExactSizedVersionedWireFormat()
        {
            var source = new ConnectionPayload
            {
                protocolVersion = NetworkProtocol.Version,
                buildVersion = "a1-build",
                unityPlayerId = "unity-player-123",
                intent = ConnectionIntent.Reconnect,
                sessionPlayerId = 42,
                reconnectToken = "opaque-token",
                playerName = "Player One"
            };

            byte[] encoded = ConnectionPayload.Encode(source);
            Assert.AreEqual(ConnectionPayload.EncodedSize, encoded.Length);
            Assert.True(ConnectionPayload.TryDecode(encoded, out ConnectionPayload decoded));
            Assert.AreEqual(source.protocolVersion, decoded.protocolVersion);
            Assert.AreEqual(source.buildVersion, decoded.buildVersion);
            Assert.AreEqual(source.unityPlayerId, decoded.unityPlayerId);
            Assert.AreEqual(source.intent, decoded.intent);
            Assert.AreEqual(source.sessionPlayerId, decoded.sessionPlayerId);
            Assert.AreEqual(source.reconnectToken, decoded.reconnectToken);
            Assert.AreEqual(source.playerName, decoded.playerName);

            Assert.False(ConnectionPayload.TryDecode(new byte[encoded.Length - 1], out _));
        }

        [Test]
        public void SessionCoordinator_RejectsConcurrentOperationAndIgnoresStaleCompletion()
        {
            using var coordinator = new SessionCoordinator();
            Assert.True(coordinator.TryBegin(
                SessionState.StartingHost, TimeSpan.FromSeconds(20), out SessionOperation first));
            Assert.False(coordinator.TryBegin(
                SessionState.Joining, TimeSpan.FromSeconds(20), out _));

            coordinator.CancelActive();
            Assert.False(coordinator.Complete(first, SessionState.Lobby));
            Assert.AreEqual(SessionState.Offline, coordinator.State);
        }

        [Test]
        public void PlayerRegistry_ReservesCapacityAndExpiresAtExactGraceBoundary()
        {
            var registry = new PlayerSessionRegistry(1);
            Assert.True(registry.TryRegisterNew(
                "unity-a", 10, 0.0, out PlayerSessionRecord player, out SessionCredentials credentials, out _));

            PlayerRuntimeSnapshot snapshot = PlayerRuntimeSnapshot.CreateDefault(
                player.PlayerId, Vector3.one, Quaternion.identity);
            Assert.True(registry.Reserve(10, snapshot, expiresAt: 60.0));

            Assert.False(registry.TryRegisterNew("unity-b", 11, 59.0, out _, out _, out SessionFailureReason full));
            Assert.AreEqual(SessionFailureReason.SessionFull, full);

            Assert.True(registry.TryReconnect(
                "unity-a", credentials.PlayerId, credentials.ReconnectToken, 12, 59.0, out _, out _));
            Assert.True(registry.Reserve(12, snapshot, expiresAt: 60.0));

            Assert.False(registry.TryReconnect(
                "unity-a", credentials.PlayerId, credentials.ReconnectToken, 13, 60.0, out _, out SessionFailureReason expired));
            Assert.AreEqual(SessionFailureReason.ReconnectExpired, expired);
        }

        [Test]
        public void PlayerRegistry_RejectsWrongTokenAndDuplicateLiveConnection()
        {
            var registry = new PlayerSessionRegistry(4);
            Assert.True(registry.TryRegisterNew(
                "unity-a", 1, 0.0, out PlayerSessionRecord player, out SessionCredentials credentials, out _));

            Assert.False(registry.TryReconnect(
                "unity-a", credentials.PlayerId, credentials.ReconnectToken, 2, 1.0, out _, out SessionFailureReason duplicate));
            Assert.AreEqual(SessionFailureReason.DuplicateConnection, duplicate);

            Assert.True(registry.Reserve(
                1, PlayerRuntimeSnapshot.CreateDefault(player.PlayerId, Vector3.zero, Quaternion.identity), 60.0));
            Assert.False(registry.TryReconnect(
                "unity-a", credentials.PlayerId, "not-the-token", 2, 5.0, out _, out SessionFailureReason invalid));
            Assert.AreEqual(SessionFailureReason.InvalidReconnectToken, invalid);
        }

        [Test]
        public void PlayerRegistry_LateDespawnSnapshotReplacesProvisionalReservation()
        {
            var registry = new PlayerSessionRegistry(4);
            Assert.True(registry.TryRegisterNew(
                "unity-a", 1, 0.0, out PlayerSessionRecord player, out _, out _));
            PlayerRuntimeSnapshot provisional = PlayerRuntimeSnapshot.CreateDefault(
                player.PlayerId, Vector3.zero, Quaternion.identity);
            Assert.True(registry.Reserve(1, provisional, 60.0));

            PlayerRuntimeSnapshot authoritative = provisional;
            authoritative.health = 37f;
            authoritative.position = new Vector3(4f, 0f, 8f);
            Assert.True(registry.UpdateReservedSnapshot(player.PlayerId, authoritative, 60.0));
            Assert.True(registry.TryGetByStableId(player.PlayerId, out PlayerSessionRecord updated));
            Assert.AreEqual(37f, updated.Snapshot.health);
            Assert.AreEqual(authoritative.position, updated.Snapshot.position);
            Assert.Greater(updated.Snapshot.revision, provisional.revision);
        }

        [Test]
        public void PickupTransaction_HasOneWinnerAndReplaysDuplicateResult()
        {
            var service = new PickupTransactionService(10);
            int mutations = 0;

            PickupTransactionResult winner = service.Execute(
                1, 100, 77, 0.0, () => { mutations++; return PickupResultCode.Accepted; });
            PickupTransactionResult duplicate = service.Execute(
                1, 100, 77, 0.01, () => { mutations++; return PickupResultCode.Accepted; });
            PickupTransactionResult loser = service.Execute(
                2, 50, 77, 0.01, () => { mutations++; return PickupResultCode.Accepted; });

            Assert.AreEqual(PickupResultCode.Accepted, winner.Code);
            Assert.AreEqual(PickupResultCode.Accepted, duplicate.Code);
            Assert.AreEqual(PickupResultCode.AlreadyClaimed, loser.Code);
            Assert.AreEqual(1, mutations, "Only the winning transaction may mutate inventory.");
        }

        [Test]
        public void PickupTransaction_RateLimitsPerClient()
        {
            var service = new PickupTransactionService(2);
            Assert.AreEqual(PickupResultCode.Accepted,
                service.Execute(1, 0, 1, 0.0, () => PickupResultCode.Accepted).Code);
            Assert.AreEqual(PickupResultCode.Accepted,
                service.Execute(1, 1, 2, 0.1, () => PickupResultCode.Accepted).Code);
            Assert.AreEqual(PickupResultCode.RateLimited,
                service.Execute(1, 2, 3, 0.2, () => PickupResultCode.Accepted).Code);
            Assert.AreEqual(PickupResultCode.Accepted,
                service.Execute(1, 3, 3, 1.01, () => PickupResultCode.Accepted).Code);
        }

        [Test]
        public void TelemetryAggregator_SealsStablePlayerOrderAtTickBoundary()
        {
            var aggregator = new ServerTelemetryAggregator();
            int[] inputOrder = { 4, 1, 3, 2 };
            foreach (int value in inputOrder)
            {
                var id = new SessionPlayerId((ulong)value);
                aggregator.RecordWeapon(id, 100, false, 10 - value, 10);
                aggregator.RecordHealth(id, 100, 100f - value, value);
                aggregator.RecordPickup(id, 100);
                aggregator.RecordShot(id, 100, value % 2 == 0);
                aggregator.RecordDowned(id, 100);
            }

            var snapshots = new List<ServerTelemetrySnapshot>();
            aggregator.SealBefore(101, snapshots.Add);

            Assert.AreEqual(4, snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                Assert.AreEqual((ulong)(i + 1), snapshots[i].PlayerId.Value);
                Assert.AreEqual(1, snapshots[i].PickupCount);
                Assert.True(snapshots[i].HasWeaponState);
                Assert.AreEqual(1, snapshots[i].ShotsFired);
                Assert.AreEqual(1, snapshots[i].DownedCount);
            }
            Assert.AreEqual(0, aggregator.PendingTickCount);
        }

        [Test]
        public void EnemyReplicatedState_EqualityIncludesActionPhase()
        {
            var left = new EnemyReplicatedState
            {
                locomotion = EnemyLocomotionState.Attacking,
                actionFlags = EnemyActionFlags.Attack,
                actionSequence = 9,
                actionStartServerTick = 100
            };
            EnemyReplicatedState right = left;

            Assert.True(left.Equals(right));
            right.actionStartServerTick++;
            Assert.False(left.Equals(right));
        }

        [Test]
        public void WeaponServerState_DeduplicatesSequenceZeroAfterWrap()
        {
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            try
            {
                data.magazineSize = 4;
                data.totalAmmo = 4;
                data.fireRate = 0f;
                var state = new WeaponServerState();
                state.InitializeForTests(1, 4, 0);

                Assert.True(state.TryConsumeFire(data, 0.0, ushort.MaxValue, enforceSequence: true));
                Assert.True(state.TryConsumeFire(data, 0.1, 0, enforceSequence: true));
                Assert.False(state.TryConsumeFire(data, 0.2, 0, enforceSequence: true));
                Assert.AreEqual(2, state.MagazineAmmo,
                    "A duplicate wrapped sequence must not consume authoritative ammo.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [TestCase(0, 0.0)]
        [TestCase(6, 0.1)]
        [TestCase(15, 0.25)]
        public void LagCompensation_AcceptsShotsInsideRttAwareWindow(int ticksOld, double expectedAge)
        {
            bool accepted = LagCompensationManager.TryResolveRewindTime(
                serverReceiveTime: 10.0,
                serverReceiveTick: 600,
                clientEstimatedServerTick: 600 - ticksOld,
                tickRate: 60,
                roundTripSeconds: 0.5,
                out double rewindTime);

            Assert.True(accepted);
            Assert.AreEqual(10.0 - expectedAge, rewindTime, 0.0001);
        }

        [Test]
        public void LagCompensation_RejectsShotOutsideRewindAndFutureWindows()
        {
            Assert.False(LagCompensationManager.TryResolveRewindTime(
                10.0, 600, 584, 60, 0.5, out _), "More than 250ms old must be rejected.");
            Assert.False(LagCompensationManager.TryResolveRewindTime(
                10.0, 600, 603, 60, 0.1, out _), "More than two future ticks must be rejected.");
        }
    }
}
