using System;
using FPS.RecoilPort;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class RecoilPopTests
    {
        RecoilPattern profile;
        static UnrealRecoilCurve.Key K(float t, float v, int mode = 1, float arrive = 0, float leave = 0)
            => new UnrealRecoilCurve.Key { time = t, value = v, mode = mode, inTangent = arrive, outTangent = leave };
        [SetUp]
        public void SetUp()
        {
            profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Warden/Warden.asset").recoilPattern);
            profile.cameraPopPitchCurve = new[] { K(0, 0), K(.1f, 1), K(.2f, 0) };
            profile.cameraPopYawCurve = Array.Empty<UnrealRecoilCurve.Key>();
            profile.cameraPopScale = Vector2.one;
        }
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void OverlappingEventsAddAndExpiredEventsCannotResurrectAfterHitch()
        {
            var buffer = new RecoilPopBuffer(profile, .05f);
            buffer.Add(10, 1);
            buffer.Add(10.05, 2);
            Assert.That(buffer.Sample(10.1).x, Is.EqualTo(1.5f).Within(.0001));
            Assert.That(buffer.Sample(10.25), Is.EqualTo(Vector2.zero));
            buffer.Add(11, 3);
            Assert.That(buffer.Sample(100), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ShortPeakIsVisibleOnceAndSamplingIntervalIsOpenAtItsStart()
        {
            profile.cameraPopPitchCurve = new[] { K(0, 0), K(.01f, 2.75f), K(.25f, 0) };
            var buffer = new RecoilPopBuffer(profile, .1f);
            buffer.Add(10, 1);
            Assert.That(buffer.Sample(10.03).x, Is.EqualTo(2.75f).Within(.0001));
            Assert.That(buffer.Sample(10.07).x, Is.EqualTo(2.0625f).Within(.0001));
            buffer.Clear();
            Assert.That(buffer.Sample(10.08), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void CubicStationaryPointIsSampledBetweenKeys()
        {
            // Hermite segment is 4u-4u^2, maximum 1 at u=.5, value .75 at u=.75.
            profile.cameraPopPitchCurve = new[] { K(0, 0, 2, 0, 4), K(1, 0, 2, -4, 0) };
            var buffer = new RecoilPopBuffer(profile, .1f);
            buffer.Add(10, 1);
            Assert.That(buffer.Sample(10.75).x, Is.EqualTo(1).Within(.0001));
        }

        [Test]
        public void EqualSignedExcursionsChooseTheLaterTime()
        {
            profile.cameraPopPitchCurve = new[] { K(0, 0), K(.1f, 1), K(.2f, -1), K(1, 0) };
            var buffer = new RecoilPopBuffer(profile, .1f);
            buffer.Add(10, 1);
            Assert.That(buffer.Sample(10.25).x, Is.EqualTo(-1).Within(.0001));
        }

        [Test]
        public void ConstantSegmentPassedBetweenFramesStillContributes()
        {
            profile.cameraPopPitchCurve = new[] { K(0, 1, 0), K(.05f, 0), K(.2f, 0) };
            var buffer = new RecoilPopBuffer(profile, .05f);
            buffer.Add(10, 1);
            Assert.That(buffer.Sample(10.1).x, Is.EqualTo(1).Within(.0001));
        }

        [Test]
        public void YawSignIsSeededAndIndependentFromGameplaySampling()
        {
            profile.cameraPopYawCurve = profile.cameraPopPitchCurve;
            bool positive = false, negative = false;
            for (uint seed = 0; seed < 32; seed++)
            {
                var a = new RecoilPopBuffer(profile, .05f);
                var b = new RecoilPopBuffer(profile, .05f);
                a.Add(10, seed); b.Add(10, seed);
                a.Sample(10.02);
                Vector2 sample = a.Sample(10.1);
                Assert.That(sample, Is.EqualTo(b.Sample(10.1)));
                Assert.That(Mathf.Abs(sample.y), Is.EqualTo(1).Within(.0001));
                positive |= sample.y > 0; negative |= sample.y < 0;
            }
            Assert.That(positive && negative, Is.True);
        }

        [Test]
        public void BufferCapacityIsDerivedAndRejectsUnsupportedConfiguration()
        {
            Assert.That(new RecoilPopBuffer(profile, .05f).Capacity, Is.EqualTo(5));
            Assert.Throws<ArgumentException>(() => new RecoilPopBuffer(profile, .001f));
            Assert.Throws<ArgumentException>(() => new RecoilPopBuffer(profile, 0));
        }

        [TestCase("end")]
        [TestCase("time")]
        [TestCase("scale")]
        [TestCase("mode")]
        public void InvalidPopIsRejectedBeforeBinding(string invalid)
        {
            if (invalid == "end") profile.cameraPopPitchCurve[2].value = .1f;
            if (invalid == "time") profile.cameraPopPitchCurve = new[] { K(0, 0) };
            if (invalid == "scale") profile.cameraPopScale = new Vector2(-1, 1);
            if (invalid == "mode") profile.cameraPopPitchCurve[0].mode = 3;
            Assert.Throws<ArgumentException>(() => profile.ValidateCurves());
        }

        [Test]
        public void SamplingAndOneHundredEventsAllocateNothingAfterBinding()
        {
            var buffer = new RecoilPopBuffer(profile, .05f);
            buffer.Add(0, 0); buffer.Sample(0); buffer.Clear();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (uint i = 0; i < 100; i++)
            {
                double now = i * .05;
                buffer.Add(now, i);
                buffer.Sample(now);
                buffer.Sample(now + .025);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(buffer.Capacity, Is.EqualTo(5));
            Assert.That(buffer.Sample(100), Is.EqualTo(Vector2.zero));
        }
    }
}
