using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

namespace FPS.Tests
{
    public sealed class RecoilFireClockTests
    {
        WeaponData data;
        RecoilPattern profile;
        readonly WeaponStabilityContext idle = new WeaponStabilityContext { grounded = true };

        [SetUp] public void SetUp()
        {
            data = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset"));
            profile = UnityEngine.Object.Instantiate(data.recoilPattern);
            data.recoilPattern = profile;
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        bool Fire(WeaponServerState state, double now, ushort sequence, bool continuous)
            => state.TryConsumeFire(data, now, sequence, true, idle, sequence, continuous);
        WeaponServerState State()
        {
            var state = new WeaponServerState();
            state.EnsureInitialized(1, data);
            return state;
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void HeldAutoUsesNominalShotClockDespiteRenderQuantization(int fps)
        {
            var server = State();
            double start = 10, interval = data.FireInterval;
            for (ushort shot = 1; shot <= 25; shot++)
            {
                double due = start + (shot-1) * interval;
                double rendered = Math.Ceiling((due-start)*fps-1e-8)/fps + start;
                Assert.That(Fire(server, rendered, shot, shot > 1), Is.True, "frame must not create cumulative cooldown drift");
                Assert.That(server.Stability.index, Is.EqualTo(Math.Min(shot, profile.MaximumStabilityIndex)).Within(.0001), "held input is not a recovery tap");
                Assert.That(server.Stability.lastShotTime, Is.EqualTo(due).Within(.0001));
                Assert.That(server.NextAllowedFireTime, Is.EqualTo(start+shot*interval).Within(.0001));
                var before = server.Stability;
                Assert.That(Fire(server, due + interval*.1, (ushort)(shot+1), true), Is.False, "continuous bit cannot bypass server deadline");
                Assert.That(server.Stability.Equals(before), Is.True);
            }
        }

        [Test]
        public void ContinuationIsIgnoredForSingleFireAndCannotSpendFutureCredit()
        {
            data.fireMode = FireMode.Single;
            var server = State();
            Assert.That(Fire(server, 10, 1, true), Is.True);
            double now = 10 + data.FireInterval + .02;
            Assert.That(Fire(server, now, 2, true), Is.True);
            Assert.That(server.Stability.lastShotTime, Is.EqualTo(now));
            Assert.That(Fire(server, now + data.FireInterval * .9, 3, true), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void CoalescedAckReplayKeepsOriginalHeldOrTapInput(bool continuous)
        {
            var server = State();
            var predicted = new WeaponStabilityPrediction();
            Assert.That(Fire(server, 10, 65535, false), Is.True);
            predicted.Fire(profile, data.FireInterval, 65535, 10, idle, 65535, true);
            var first = server.Stability;
            for (ushort shot = 0; shot < 3; shot++)
            {
                double now = server.NextAllowedFireTime + .02;
                Assert.That(Fire(server, now, shot, continuous), Is.True);
                predicted.Fire(profile, data.FireInterval, shot, now, idle, shot, true, continuous);
                Assert.That(predicted.State.Equals(server.Stability), Is.True);
            }
            predicted.Reconcile(profile, data.FireInterval, first, true, 65535);
            Assert.That(predicted.PendingCount, Is.EqualTo(3));
            Assert.That(predicted.State.Equals(server.Stability), Is.True);
            predicted.Reconcile(profile, data.FireInterval, server.Stability, true, 2);
            Assert.That(predicted.PendingCount, Is.Zero);
            Assert.That(predicted.State.Equals(server.Stability), Is.True);
        }

        [Test]
        public void FireCommandSerializesContinuationAlongWithOriginalSample()
        {
            var command = new FireCommand { sequence = 65535, estimatedServerTick = 42,
                inputSequence = 73, weaponSlot = 1, continuousAuto = true };
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteNetworkSerializable(command);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out FireCommand restored);
            Assert.That(restored.sequence, Is.EqualTo(command.sequence));
            Assert.That(restored.estimatedServerTick, Is.EqualTo(42));
            Assert.That(restored.inputSequence, Is.EqualTo(73));
            Assert.That(restored.weaponSlot, Is.EqualTo(1));
            Assert.That(restored.continuousAuto, Is.True);
        }

        [TestCase(false)] [TestCase(true)]
        public void ReleaseOrLongHitchReanchorsWithoutCatchup(bool hitch)
        {
            var server = State();
            Assert.That(Fire(server, 10, 65535, false), Is.True);
            double now = 10 + data.FireInterval + (hitch ? 1 : .02);
            Assert.That(Fire(server, now, 0, hitch), Is.True);
            Assert.That(server.Stability.lastShotTime, Is.EqualTo(now).Within(.0001));
            Assert.That(server.NextAllowedFireTime, Is.EqualTo(now+data.FireInterval).Within(.0001));
            Assert.That(Fire(server, now, 1, true), Is.False);
            var saved = server.Capture(0, data);
            var restored = State();
            restored.Restore(saved, 2);
            Assert.That(Fire(restored, now+data.FireInterval+.01, 1, true), Is.True);
            Assert.That(Fire(server, now+data.FireInterval+.01, 1, true), Is.True);
            Assert.That(restored.Stability.Equals(server.Stability), Is.True);
            Assert.That(Fire(server, now+10, 1, true), Is.False, "duplicate after idle remains rejected");
        }
    }
}
