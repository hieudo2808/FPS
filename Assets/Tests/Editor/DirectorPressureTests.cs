using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class DirectorPressureTests
    {
        [Test]
        public void TwoThreatsAtFullHealth_CreateTwentyPressure()
        {
            var p = new DirectorPressure();
            var s = p.Sample(1, 0, 1, 2, false, true);
            Assert.That(s.Intensity, Is.EqualTo(20));
            Assert.That(s.Protect, Is.False);
        }
        [Test]
        public void DamageBurstAtFiftyHP_ProtectsBeforeNearDeath()
        {
            var p = new DirectorPressure();
            p.RecordDamage(1, 10, 40, 100);
            var s = p.Sample(1, 10, .5f, 2, false, true);
            Assert.That(s.Intensity, Is.EqualTo(75).Within(.001));
            Assert.That(s.Protect, Is.True);
        }
        [TestCase(.20f, 1, true)]
        [TestCase(.21f, 1, false)]
        [TestCase(.10f, 0, false)]
        [TestCase(.10f, 1, true)]
        public void LowHealthRequiresCurrentThreat(float hp, int threats, bool protect)
        {
            var p = new DirectorPressure();
            Assert.That(p.Sample(1, 0, hp, threats, false, true).Protect, Is.EqualTo(protect));
        }
        [TestCase(.60f, 30f, true)]
        [TestCase(.60f, 29.9f, false)]
        [TestCase(.601f, 30f, false)]
        public void BurstProtectionBoundaries(float hp, float damage, bool protect)
        {
            var p = new DirectorPressure();
            p.RecordDamage(1, 0, damage, 100);
            Assert.That(p.Sample(1, 0, hp, 0, false, true).Protect, Is.EqualTo(protect));
        }
        [Test]
        public void PressureCapsThreatsAtFour_ThenDecaysTenPerSecond()
        {
            var p = new DirectorPressure();
            Assert.That(p.Sample(1, 0, 1, 70, false, true).Intensity, Is.EqualTo(40));
            Assert.That(p.Sample(1, 1, 1, 0, false, true).Intensity, Is.EqualTo(30));
            Assert.That(p.Sample(1, 4, 1, 0, false, true).Intensity, Is.Zero);
        }
        [Test]
        public void DownGraceUsesDownTime_ReviveDoesNotRestartIt()
        {
            var p = new DirectorPressure();
            p.RecordDown(1, 1);
            Assert.That(p.Sample(1, 21, 1, 0, false, true).Protect, Is.True);
            Assert.That(p.Sample(1, 21.01, 1, 0, false, true).Protect, Is.False);
        }
        [Test]
        public void DamageWindowExpiresAfterThreeSeconds()
        {
            var p = new DirectorPressure();
            p.RecordDamage(1, 0, 30, 100);
            Assert.That(p.Sample(1, 3, .6f, 0, false, true).Damage3Seconds, Is.EqualTo(.3f));
            Assert.That(p.Sample(1, 3.001, .6f, 0, false, true).Damage3Seconds, Is.Zero);
        }
        [Test]
        public void SuspendedAndAbsentTimeDoesNotCountAsExposure()
        {
            var p = new DirectorPressure();
            p.Sample(1, 0, 1, 1, false, true);
            Assert.That(p.Sample(1, .1, 1, 1, false, true).ExposureSeconds, Is.EqualTo(.1f));
            Assert.That(p.Sample(1, 10, 1, 1, false, true, false).ExposureSeconds, Is.Zero);
            Assert.That(p.Sample(1, 20, 1, 1, false, false).ExposureSeconds, Is.Zero);
        }

        [Test]
        public void WarmPressureSamplingForFourActors_DoesNotAllocate()
        {
            var p = new DirectorPressure();
            for (ulong actor = 1; actor <= 4; actor++) p.Sample(actor, 0, 1, 4, false, true);
            for (int i = 0; i < 100; i++)
                for (ulong actor = 1; actor <= 4; actor++) p.Sample(actor, i * .1, .8f, 4, false, true);
            var timings = new double[1000];
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < timings.Length; i++)
            {
                long started = Stopwatch.GetTimestamp();
                for (ulong actor = 1; actor <= 4; actor++) p.Sample(actor, 10 + i * .1, .8f, 4, false, true);
                timings[i] = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Array.Sort(timings);
            Directory.CreateDirectory("Documents/AI-Director-Defense/Captures");
            File.WriteAllText("Documents/AI-Director-Defense/Captures/domain-pressure-microbenchmark.json",
                "{\"scope\":\"Editor domain only; not Player/roster/frame-time acceptance\",\"actors\":4,\"iterations\":1000,\"allocatedBytes\":"
                + allocated + ",\"p95Milliseconds\":" + timings[950].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
            Assert.That(allocated, Is.Zero, "Steady pure pressure sampling should reuse its storage.");
        }
    }
}
