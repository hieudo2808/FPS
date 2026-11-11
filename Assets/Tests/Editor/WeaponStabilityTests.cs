using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FPS.RecoilPort;
using Unity.Netcode;
using Unity.Collections;

namespace FPS.Tests
{
    public sealed class WeaponStabilityTests
    {
        private RecoilPattern profile;
        private WeaponData weapon;
        private WeaponStabilityContext idle;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Warden/Warden.asset");
            Assert.That(source.recoilPattern, Is.Not.Null, "Warden must reference its authored recoil profile.");
            weapon = Object.Instantiate(source);
            profile = Object.Instantiate(source.recoilPattern);
            weapon.recoilPattern = profile;
            idle = new WeaponStabilityContext { grounded = true };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(weapon);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ThreeShotsReachEnvelopeInsteadOfAddingAbsoluteKeys()
        {
            var state = new WeaponStability();
            float[] pitches = { .6f, 1.5f, 2.5f };
            for (int i = 0; i < 3; i++)
            {
                state.Fire(profile, 10 + i * (double)weapon.FireInterval, weapon.FireInterval, idle, (uint)i);
                Assert.That(state.pitchTarget, Is.EqualTo(pitches[i]).Within(.0001));
                Assert.That(state.firingErrorIndex, Is.EqualTo(state.index).Within(.0001));
            }
            Assert.That(state.pitchTarget, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(state.index, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void FallingEnvelopeCanApplyNegativePitch()
        {
            var state = new WeaponStability();
            for (int i = 0; i < 11; i++) state.Fire(profile, 10 + i * (double)weapon.FireInterval, weapon.FireInterval, idle, (uint)i);
            float pitch = state.pitchTarget;
            state.Fire(profile, 10 + 11 * (double)weapon.FireInterval, weapon.FireInterval, idle, 11);
            Assert.That(state.pitchTarget, Is.LessThan(pitch));
        }

        [Test]
        public void RecoveryUsesAbsoluteTimeAndResumesFromFractionalIndex()
        {
            var state = new WeaponStability();
            state.Fire(profile, 10, weapon.FireInterval, idle, 1);
            double half = 10 + weapon.FireInterval + profile.Evaluate(profile.recoveryTime, 1) * 0.5;
            Assert.That(state.IndexAt(profile, half, weapon.FireInterval), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(state.TargetAt(profile, half, weapon.FireInterval).x, Is.EqualTo(0.3f).Within(0.0001f));
            state.Fire(profile, half, weapon.FireInterval, idle, 2);
            Assert.That(state.index, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(state.IndexAt(profile, half + 1, weapon.FireInterval), Is.Zero);
        }

        [Test]
        public void SpreadUsesPreShotIndexAndAdsAffineTransform()
        {
            var state = new WeaponStability();
            Assert.That(state.Fire(profile, 10, weapon.FireInterval, idle, 1), Is.EqualTo(0.15f).Within(0.00001f));
            idle.aimed = true;
            state = default;
            Assert.That(state.Fire(profile, 10, weapon.FireInterval, idle, 1), Is.EqualTo(0.0425f).Within(0.00001f));
            Assert.That(state.pitchTarget, Is.EqualTo(0.42f).Within(0.00001f));
        }

        [Test]
        public void MovementKeepsConstantSegmentsAndJumpPenalty()
        {
            idle.normalizedSpeed = 0.2749f;
            Assert.That(profile.GetSpread(0, idle), Is.EqualTo(0.15f).Within(0.00001f));
            idle.normalizedSpeed = 0.275f;
            Assert.That(profile.GetSpread(0, idle), Is.EqualTo(3));
            idle.sprinting = true;
            Assert.That(profile.GetSpread(0, idle), Is.EqualTo(6));
            idle.grounded = false;
            Assert.That(profile.GetSpread(0, idle), Is.EqualTo(10));
        }

        [Test]
        public void RejectedOrDuplicateRequestsDoNotAdvanceStability()
        {
            var state = new WeaponServerState();
            state.EnsureInitialized(1, weapon);
            Assert.That(state.TryConsumeFire(weapon, 10, 1, true, idle, 42), Is.True);
            var before = state.Stability;
            Assert.That(state.TryConsumeFire(weapon, 10.001, 2, true, idle, 43), Is.False);
            Assert.That(state.TryConsumeFire(weapon, 11, 1, true, idle, 42), Is.False);
            Assert.That(state.Stability.Equals(before), Is.True);
            Assert.That(state.MagazineAmmo, Is.EqualTo(weapon.magazineSize - 1));
        }

        [Test]
        public void ARejectedSequenceCannotBeRetriedAsANewShot()
        {
            var state = new WeaponServerState();
            state.EnsureInitialized(1, weapon);
            state.RecordFireResult(65535);
            Assert.That(state.TryConsumeFire(weapon, 10, 65535, true, idle, 1), Is.False);
            Assert.That(state.TryConsumeFire(weapon, 10, 0, true, idle, 2), Is.True);
            state.RecordFireResult(65534);
            Assert.That(state.ResolvedFireSequence, Is.EqualTo(65535));
        }

        [Test]
        public void ReconciliationRemovesRejectedShotAndReplaysOnlyUnresolvedShots()
        {
            var prediction = new WeaponStabilityPrediction();
            prediction.Fire(profile, weapon.FireInterval, 1, 10, idle, 11, true);
            prediction.Fire(profile, weapon.FireInterval, 2, 10.2, idle, 12, true);
            var authoritative = new WeaponStability();
            prediction.Reconcile(profile, weapon.FireInterval, authoritative, true, 1);
            authoritative.Fire(profile, 10.2, weapon.FireInterval, idle, 12);
            Assert.That(prediction.PendingCount, Is.EqualTo(1));
            Assert.That(prediction.State.Equals(authoritative), Is.True);
            prediction.Reconcile(profile, weapon.FireInterval, authoritative, true, 2);
            Assert.That(prediction.PendingCount, Is.Zero);
            Assert.That(prediction.State.Equals(authoritative), Is.True);
        }

        [Test]
        public void AcknowledgementsHandleSequenceWrapAndCoalescedUpdates()
        {
            var prediction = new WeaponStabilityPrediction();
            prediction.Fire(profile, weapon.FireInterval, 65535, 10, idle, 11, true);
            prediction.Fire(profile, weapon.FireInterval, 0, 10.2, idle, 12, true);
            prediction.Reconcile(profile, weapon.FireInterval, default, true, 65535);
            Assert.That(prediction.PendingCount, Is.EqualTo(1));
            prediction.Reconcile(profile, weapon.FireInterval, default, true, 0);
            Assert.That(prediction.PendingCount, Is.Zero);
        }

        [Test]
        public void ReconnectSnapshotRestoresTheSameRecoveryAndRandomState()
        {
            var server = new WeaponServerState();
            server.EnsureInitialized(1, weapon);
            Assert.That(server.TryConsumeFire(weapon, 10, 65535, true, idle, 99), Is.True);
            server.RecordFireResult(65535);
            var snapshot = server.Capture(0, weapon);
            var restored = new WeaponServerState();
            restored.Restore(snapshot, 2);
            Assert.That(restored.Stability.Equals(server.Stability), Is.True);
            Assert.That(restored.ResolvedFireSequence, Is.EqualTo(65535));
            Assert.That(restored.TryConsumeFire(weapon, 10 + weapon.FireInterval, 0, true, idle, 100), Is.True);
            Assert.That(server.TryConsumeFire(weapon, 10 + weapon.FireInterval, 0, true, idle, 100), Is.True);
            Assert.That(restored.Stability.Equals(server.Stability), Is.True);
        }

        [Test]
        public void SustainedSprayIsBoundedAndDeterministic()
        {
            var a = new WeaponStability();
            var b = new WeaponStability();
            for (uint i = 0; i < 500; i++)
            {
                double time = 10 + i * (double)weapon.FireInterval;
                a.Fire(profile, time, weapon.FireInterval, idle, i);
                b.Fire(profile, time, weapon.FireInterval, idle, i);
                Assert.That(a.Equals(b), Is.True);
                Assert.That(a.index, Is.LessThanOrEqualTo(profile.MaximumStabilityIndex));
                Assert.That(Mathf.Abs(a.pitchTarget), Is.LessThan(10));
                Assert.That(Mathf.Abs(a.yawMagnitude), Is.LessThan(4));
            }
        }

        [TestCase(0f, .284f, 0f, .284f)]
        [TestCase(.3f, .1f, .05f, -.05f)]
        [TestCase(.4f, -.6f, .1f, -.7f)]
        [TestCase(-.4f, .6f, -.1f, .7f)]
        [TestCase(.3f, .1f, 0f, 0f)]
        [TestCase(.4f, -.6f, 0f, -.6f)]
        [TestCase(.1f, .4f, 0f, .3f)]
        [TestCase(-.3f, 0f, -.05f, .05f)]
        public void CameraDeltaSeparatesOldReturnFromNewKick(float before, float after, float debt, float expected)
            => Assert.That(RecoilController.ResolveRecoilDelta(before, after, debt), Is.EqualTo(expected).Within(.0001));

        [TestCase(5f, -8f, -5f)]
        [TestCase(-5f, 8f, 5f)]
        [TestCase(0f, -5f, 0f)]
        [TestCase(5f, 2f, 0f)]
        public void RecoveryCannotUndoMoreThanUncompensatedRecoil(float debt, float delta, float expected)
            => Assert.That(RecoilController.LimitReturn(delta, debt), Is.EqualTo(expected));

        [TestCase("Brimstone")]
        [TestCase("Clove")]
        [TestCase("Gekko")]
        [TestCase("Sage")]
        public void EveryPlayerHasLocalRecoilReferences(string character)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab");
            Assert.That(player.GetComponent<RecoilController>(), Is.Not.Null);
            Assert.That(player.GetComponent<MouseMovement>(), Is.Not.Null);
        }

        [Test]
        public void CurveEvaluatorRejectsUnsortedInput()
        {
            Assert.Throws<System.ArgumentException>(() => UnrealRecoilCurve.Validate(new[] {
                new UnrealRecoilCurve.Key { time = 1 }, new UnrealRecoilCurve.Key { time = 0 } }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => profile.Evaluate(profile.pitchEnvelope, float.NaN));
            profile.ValidateCurves();
        }

        [Test]
        public void NetworkSnapshotsRoundTripStabilityAndAcknowledgement()
        {
            var state = new WeaponServerState();
            state.EnsureInitialized(1, weapon);
            Assert.That(state.TryConsumeFire(weapon, 10, 65535, true, idle, 99), Is.True);
            state.RecordFireResult(65535);
            var snapshot = state.Capture(0, weapon);
            Assert.That(RoundTrip(snapshot).Equals(snapshot), Is.True);
            var owner = new WeaponOwnerState { stability = state.Stability, hasResolvedFireSequence = true,
                resolvedFireSequence = 65535, acknowledgedFireSequence = 65535, authoritativeShotTick = 42 };
            Assert.That(RoundTrip(owner).Equals(owner), Is.True);
        }

        [TestCase(9f, .2694444f)]
        [TestCase(1f, .425f)]
        public void TapEfficiencySlowsOnlyPartiallyRecoveredFiringError(float efficiency, float expectedSpread)
        {
            profile.tapEfficiency = efficiency;
            var state = new WeaponStability();
            state.Fire(profile, 10, weapon.FireInterval, idle, 1);
            // recovery at index 1: .025 + (.15-.025)/2 = .0875 seconds.
            double half = 10 + weapon.FireInterval + .0875 * .5;
            state.Fire(profile, half, weapon.FireInterval, idle, 2);
            Assert.That(state.index, Is.EqualTo(1.5f).Within(.0001));
            Assert.That(state.firingErrorIndex, Is.EqualTo(efficiency == 9 ? 1.0555556f : 1.5f).Within(.0001));
            float spread = state.Fire(profile, half + weapon.FireInterval, weapon.FireInterval, idle, 3);
            Assert.That(spread, Is.EqualTo(expectedSpread).Within(.0001));
            Assert.That(state.index, Is.EqualTo(2.5f).Within(.0001), "TapEfficiency cannot change pitch/yaw index");
            Assert.That(state.Fire(profile, half + 10, weapon.FireInterval, idle, 4), Is.EqualTo(.15f).Within(.0001));
            Assert.That(state.index, Is.EqualTo(1f).Within(.0001));
            Assert.That(state.firingErrorIndex, Is.EqualTo(1f).Within(.0001));
        }

        [TestCase("tapEfficiency", .9f)]
        [TestCase("yawSwitchDuration", 0f)]
        [TestCase("yawSwitchChance", -1f)]
        [TestCase("yawSwitchChance", 1.1f)]
        [TestCase("protectedShots", -1f)]
        [TestCase("snappiness", float.NaN)]
        [TestCase("adsErrorMultiplier", float.PositiveInfinity)]
        public void ProfileValidationRejectsInvalidScalars(string field, float value)
        {
            var member = typeof(RecoilPattern).GetField(field);
            Assert.That(member, Is.Not.Null, "profile must expose " + field);
            member.SetValue(profile, member.FieldType == typeof(int) ? (object)(int)value : value);
            Assert.Throws<System.ArgumentException>(() => profile.ValidateCurves());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FourthAcceptedShotStartsContinuousYawWithoutInitialCooldown(bool partialRecovery)
        {
            profile.yawSwitchChance = 1;
            profile.recoveryTime = new[] { new UnrealRecoilCurve.Key { time = 0, value = 1 } };
            var state = new WeaponStability();
            const float interval = .1f;
            double now = 10;
            float sign = 0;
            for (uint i = 1; i <= 3; i++)
            {
                state.Fire(profile, now, interval, idle, i);
                if (i == 1) sign = Mathf.Sign(state.TargetAt(profile, now, interval).y);
                Assert.That(state.TargetAt(profile, now, interval).y * sign, Is.GreaterThan(0), "three protected shots");
                now += interval + (partialRecovery ? .5 : 0);
            }
            state.Fire(profile, now, interval, idle, 4);
            Assert.That(state.TargetAt(profile, now, interval).y * sign, Is.GreaterThan(0), "no instantaneous flip");
            if (partialRecovery) Assert.That(state.index, Is.LessThan(3), "eligible count is independent of fractional index");
            state.Fire(profile, now + interval, interval, idle, 5);
            Assert.That(state.TargetAt(profile, now + .3, interval).y, Is.EqualTo(0).Within(.0001), "midpoint; fifth shot cannot restart transition");
            Assert.That(state.TargetAt(profile, now + .6, interval).y * sign, Is.LessThan(0), "transition reaches opposite side");
        }

        [Test]
        public void SamplingCannotChangeYawRngAndZeroProbabilityNeverSwitches()
        {
            foreach (float chance in new[] { 0f, .5f, 1f })
            {
                profile.yawSwitchChance = chance;
                var a = new WeaponStability();
                var b = new WeaponStability();
                float firstSign = 0;
                for (uint shot = 0; shot < 100; shot++)
                {
                    double now = 10 + shot * (double)weapon.FireInterval;
                    a.Fire(profile, now, weapon.FireInterval, idle, shot);
                    b.Fire(profile, now, weapon.FireInterval, idle, shot);
                    for (int frame = 0; frame < 144; frame++) a.TargetAt(profile, now + frame / 1440.0, weapon.FireInterval);
                    Assert.That(a.Equals(b), Is.True);
                    if (shot == 0) firstSign = Mathf.Sign(a.TargetAt(profile, now, weapon.FireInterval).y);
                    if (chance == 0) Assert.That(a.TargetAt(profile, now, weapon.FireInterval).y * firstSign, Is.GreaterThanOrEqualTo(0));
                }
            }
        }

        [Test]
        public void MidTransitionSnapshotRoundTripsAndReplaysWrappedPendingShots()
        {
            profile.yawSwitchChance = 1;
            profile.recoveryTime = new[] { new UnrealRecoilCurve.Key { value = 1 } };
            var authoritative = new WeaponStability();
            for (uint shot = 0; shot < 4; shot++) authoritative.Fire(profile, 10 + shot * .1, .1f, idle, shot);
            var owner = new WeaponOwnerState { stability = authoritative, hasResolvedFireSequence = true, resolvedFireSequence = 65535 };
            var restored = RoundTrip(owner);
            Assert.That(restored.Equals(owner), Is.True);
            foreach (double now in new[] { 10.3, 10.45, 10.6, 10.9, 11.5 })
                Assert.That(restored.stability.TargetAt(profile, now, .1f), Is.EqualTo(authoritative.TargetAt(profile, now, .1f)));
            var prediction = new WeaponStabilityPrediction();
            prediction.Fire(profile, .1f, 65535, 10.3, idle, 3, true);
            prediction.Fire(profile, .1f, 0, 10.4, idle, 4, true);
            prediction.Reconcile(profile, .1f, restored.stability, true, 65535);
            authoritative.Fire(profile, authoritative.lastShotTime + .1f, .1f, idle, 4);
            Assert.That(prediction.PendingCount, Is.EqualTo(1));
            Assert.That(prediction.State.Equals(authoritative), Is.True);
            prediction.Reconcile(profile, .1f, authoritative, true, 0);
            Assert.That(prediction.PendingCount, Is.Zero);
            Assert.That(prediction.State.Equals(authoritative), Is.True);
        }

        [TestCase((ushort)10)]
        [TestCase((ushort)11)]
        [TestCase((ushort)13)]
        public void SnapshotSchemaMismatchCannotRewriteOrDisconnectAReservation(ushort schema)
        {
            var registry = new PlayerSessionRegistry(2);
            Assert.That(registry.TryRegisterNew("recoil", 7, 0, out var record, out _, out _), Is.True);
            var before = record.Snapshot;
            var invalid = before;
            invalid.schemaVersion = schema;
            invalid.health = 1;
            Assert.That(registry.Reserve(7, invalid, 100), Is.False);
            Assert.That(record.IsConnected, Is.True);
            Assert.That(record.Snapshot.health, Is.EqualTo(before.health));
            Assert.That(registry.Reserve(7, before, 100), Is.True);
            uint revision = record.Snapshot.revision;
            Assert.That(registry.UpdateReservedSnapshot(record.PlayerId, invalid, 100), Is.False);
            Assert.That(record.Snapshot.revision, Is.EqualTo(revision));
        }

        [Test]
        public void ReconnectRejectsSchemaBeforePreparingAnyState()
        {
            var go = new GameObject("schema boundary");
            go.SetActive(false);
            try
            {
                var health = go.AddComponent<PlayerHealth>();
                var invalid = PlayerRuntimeSnapshot.CreateDefault(default, Vector3.one, Quaternion.identity);
                invalid.schemaVersion = 9;
                Assert.Throws<System.InvalidOperationException>(() => health.PrepareReconnect(invalid));
                Assert.That((bool)typeof(PlayerHealth).GetField("hasPreparedSnapshot",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(health), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SnapshotReaderRejectsSchemaBeforeReadingTheBody()
        {
            using var writer = new FastBufferWriter(2, Allocator.Temp);
            writer.WriteValueSafe((ushort)9);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            Assert.Throws<System.InvalidOperationException>(() => reader.ReadNetworkSerializable(out PlayerRuntimeSnapshot _));
        }

        [Test]
        public void RecoilWireVersionsAreExplicitlyIncompatibleWithPreviousPorts()
        {
            Assert.That(NetworkProtocol.Version, Is.EqualTo(14));
            Assert.That(NetworkProtocol.SnapshotSchemaVersion, Is.EqualTo(12));
        }

        [TestCase((ushort)12)]
        [TestCase((ushort)13)]
        [TestCase((ushort)15)]
        public void ApprovalRejectsOtherProtocolsBeforeAccessingSessionState(ushort protocol)
        {
            var go = new GameObject("protocol boundary");
            go.SetActive(false);
            try
            {
                var manager = go.AddComponent<NetworkGameManager>();
                var response = new NetworkManager.ConnectionApprovalResponse { Approved = true, CreatePlayerObject = true };
                var request = new NetworkManager.ConnectionApprovalRequest
                {
                    Payload = ConnectionPayload.Encode(new ConnectionPayload {
                        protocolVersion = protocol, buildVersion = Application.version, unityPlayerId = "recoil" })
                };
                typeof(NetworkGameManager).GetMethod("ApproveConnection",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(manager, new object[] { request, response });
                Assert.That(response.Approved, Is.False);
                Assert.That(response.CreatePlayerObject, Is.False);
                Assert.That(response.Reason, Is.EqualTo(SessionFailureReason.ProtocolMismatch.ToString()));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void EveryStabilityFieldParticipatesInEqualityAndSerialization()
        {
            foreach (var member in typeof(WeaponStability).GetFields())
            {
                object boxed = default(WeaponStability);
                object value = member.FieldType == typeof(bool) ? (object)true
                    : System.Convert.ChangeType(1, member.FieldType);
                member.SetValue(boxed, value);
                var changed = (WeaponStability)boxed;
                Assert.That(changed.Equals(default), Is.False, member.Name + " equality");
                Assert.That(RoundTrip(changed).Equals(changed), Is.True, member.Name + " serialization");
            }
        }

        [Test]
        public void CompleteRecoveryStartsANewProtectedBurst()
        {
            profile.yawSwitchChance = 1;
            var state = new WeaponStability();
            for (uint shot = 0; shot < 5; shot++) state.Fire(profile, 10 + shot * (double)weapon.FireInterval, weapon.FireInterval, idle, shot);
            state.Fire(profile, 20, weapon.FireInterval, idle, 123);
            Assert.That(state.acceptedShots, Is.EqualTo(1));
            Assert.That(state.firingErrorIndex, Is.EqualTo(1));
            Assert.That(state.yawFromSign, Is.EqualTo(state.yawToSign));
        }

        private static T RoundTrip<T>(T value) where T : unmanaged, INetworkSerializable
        {
            using var writer = new FastBufferWriter(2048, Allocator.Temp);
            writer.WriteNetworkSerializable(value);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out T restored);
            return restored;
        }
    }
}
