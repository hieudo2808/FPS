using System.Collections.Generic;
using NUnit.Framework;

namespace FPS.Tests
{
    public sealed class AdaptiveTelemetryTests
    {
        internal static ServerTelemetrySnapshot Snapshot(ulong id, int tick, int shots = 1, float damage = 0, int kills = 0, int downs = 0)
            => new ServerTelemetrySnapshot(tick, new SessionPlayerId(id), false, false, 0, 0, 0,
                damage, 0, kills, 0, shots, 0, 0, downs);

        [Test]
        public void DuplicateSealedPlayerTick_IsCountedOnlyOnce()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.Record(Snapshot(1, 12, 3));
            collector.Record(Snapshot(1, 12, 3));
            Assert.That(collector.BuildSamples()[0].ShotsFired, Is.EqualTo(3), "One sealed player/tick must not double DDA evidence.");
        }

        [Test]
        public void SameTickDifferentActors_KeepSeparateCounts()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.Record(Snapshot(1, 12, 3));
            collector.Record(Snapshot(2, 12, 7));
            Assert.That(collector.BuildSamples(), Has.Count.EqualTo(2));
            Assert.That(collector.BuildSamples()[0].ShotsFired + collector.BuildSamples()[1].ShotsFired, Is.EqualTo(10));
        }

        [Test]
        public void Aggregator_SealsAllEventsInTick_ButNotNextTick()
        {
            var aggregator = new ServerTelemetryAggregator();
            var snapshots = new List<ServerTelemetrySnapshot>();
            var id = new SessionPlayerId(1);
            aggregator.RecordShot(id, 10, true);
            aggregator.RecordShot(id, 10, false);
            aggregator.RecordHealth(id, 10, 60, 40);
            aggregator.RecordShot(id, 11, false);
            aggregator.SealBefore(11, snapshots.Add);
            Assert.That(snapshots, Has.Count.EqualTo(1));
            Assert.That(snapshots[0].ShotsFired, Is.EqualTo(2));
            Assert.That(snapshots[0].DamageTaken, Is.EqualTo(40));
            Assert.That(aggregator.PendingTickCount, Is.EqualTo(1));
        }

        [Test]
        public void Aggregator_RejectsEventsForAlreadySealedTicks()
        {
            var aggregator = new ServerTelemetryAggregator();
            var snapshots = new List<ServerTelemetrySnapshot>();
            var id = new SessionPlayerId(1);
            aggregator.RecordShot(id, 10, true);
            aggregator.SealBefore(11, snapshots.Add);
            aggregator.RecordShot(id, 10, false);
            aggregator.SealBefore(12, snapshots.Add);
            Assert.That(snapshots, Has.Count.EqualTo(1), "A late event cannot reopen a sealed tick and change a committed encounter.");
        }

        [Test]
        public void EncounterReset_DoesNotAdmitReplayOfSealedSnapshot()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.Record(Snapshot(1, 10));
            collector.ResetEncounter();
            collector.Record(Snapshot(1, 10));
            Assert.That(collector.PlayerCount, Is.Zero, "Reset is not permission to attribute an old tick to the next encounter.");
        }

        [Test]
        public void Boundary_IncludesFinalTick_AndPreservesNextWindow()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.Record(Snapshot(1, 9, 2));
            Assert.That(collector.RequestBoundary(10), Is.True);
            Assert.That(collector.RequestBoundary(11), Is.False);
            collector.Record(Snapshot(1, 10, 3));
            collector.Record(Snapshot(1, 11, 7));
            Assert.That(collector.BuildSamples()[0].ShotsFired, Is.EqualTo(5));
            var archived = collector.BuildSamples();
            collector.CompleteBoundary();
            Assert.That(archived[0].ShotsFired, Is.EqualTo(5));
            Assert.That(collector.BuildSamples()[0].ShotsFired, Is.EqualTo(7));
            Assert.That(collector.PendingBoundaryTick, Is.Null);
        }

        [Test]
        public void RetryCancelsBoundary_DiscardsOldTickButAcceptsNewTick()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.RequestBoundary(10);
            collector.Record(Snapshot(1, 10, 3));
            collector.ResetEncounter(11);
            collector.Record(Snapshot(1, 11, 7));
            Assert.That(collector.PlayerCount, Is.Zero);
            collector.Record(Snapshot(1, 12, 2));
            Assert.That(collector.BuildSamples()[0].ShotsFired, Is.EqualTo(2));
            Assert.That(collector.PendingBoundaryTick, Is.Null);
        }

        [Test]
        public void NewSessionCanStartAtLowerServerTickWithoutReusingOldCounters()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.Record(Snapshot(1, 100, 3));
            collector.ResetSession();
            collector.Record(Snapshot(1, 1, 2));
            Assert.That(collector.BuildSamples()[0].ShotsFired, Is.EqualTo(2));
        }

        [Test]
        public void RegisteredReconnectKeepsEvidenceWithStableIdentity_NotNewClientId()
        {
            var registry = new PlayerSessionRegistry(4);
            Assert.That(registry.TryRegisterNew("auth-one", 5, 0, out var record, out var credentials, out _), Is.True);
            var collector = new AdaptiveDifficultyMetricsCollector();
            collector.Record(Snapshot(record.PlayerId.Value, 10, 2));
            Assert.That(registry.Reserve(5, record.Snapshot, 100), Is.True);
            Assert.That(registry.TryReconnect("auth-one", record.PlayerId, credentials.ReconnectToken,
                9, 10, out var reconnected, out _), Is.True);
            collector.Record(Snapshot(reconnected.PlayerId.Value, 11, 3));
            Assert.That(collector.BuildSamples(), Has.Count.EqualTo(1));
            Assert.That(collector.BuildSamples()[0].ShotsFired, Is.EqualTo(5));
            Assert.That(reconnected.ClientId, Is.EqualTo(9));
        }
    }
}
