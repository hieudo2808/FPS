using System;
using FPS.RecoilPort;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class RecoilRevisionFiveTests
    {
        static RecoilPattern Profile(string name) => AssetDatabase.LoadAssetAtPath<WeaponData>(
            $"Assets/FPS/Features/Weapons/Content/{name}/{name}.asset").recoilPattern;

        [TestCase("Warden", 7f, .0024416363f, .4436298f)]
        [TestCase("Odin", 5f, .002442f, .44363f)]
        public void HipFollowUsesComplementOfEachResolvedAlpha(string name, float end, float leave, float arrive)
        {
            var p = Profile(name);
            p.ValidateCurves();
            Assert.That(p.hipPitchCameraFollow, Has.Length.EqualTo(2));
            var a = p.hipPitchCameraFollow[0];
            var b = p.hipPitchCameraFollow[1];
            Assert.That(a.time, Is.Zero);
            Assert.That(a.value, Is.Zero);
            Assert.That(a.mode, Is.EqualTo(2));
            Assert.That(a.outTangent, Is.EqualTo(leave));
            Assert.That(b.time, Is.EqualTo(end));
            Assert.That(b.value, Is.EqualTo(1));
            Assert.That(b.inTangent, Is.EqualTo(arrive));
            Assert.That(p.Evaluate(p.hipPitchCameraFollow, end + 1), Is.EqualTo(1));
            Assert.That(p.adsCameraRecoilFollow, Is.EqualTo(Vector2.one));
        }

        [TestCase(-.1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
        public void HipFollowRejectsInvalidKeys(float value)
        {
            var p = Object.Instantiate(Profile("Vandal"));
            try
            {
                p.hipPitchCameraFollow = new[] { new UnrealRecoilCurve.Key { value = value } };
                Assert.Throws<ArgumentException>(() => p.ValidateCurves());
            }
            finally { Object.DestroyImmediate(p); }
        }

        [Test]
        public void HipFollowRejectsCubicOvershootBetweenValidEndpoints()
        {
            var p = Object.Instantiate(Profile("Vandal"));
            try
            {
                p.hipPitchCameraFollow = new[] {
                    new UnrealRecoilCurve.Key { value = 1, mode = 2, outTangent = 4 },
                    new UnrealRecoilCurve.Key { time = 1, value = 0, inTangent = -4 } };
                Assert.Throws<ArgumentException>(() => p.ValidateCurves());
                p.hipPitchCameraFollow = Array.Empty<UnrealRecoilCurve.Key>();
                Assert.DoesNotThrow(() => p.ValidateCurves());
            }
            finally { Object.DestroyImmediate(p); }
        }

        [TestCase("Vandal")]
        [TestCase("Odin")]
        [TestCase("Warden")]
        public void AutoSprayKeepsPitchPopWithoutIndependentRandomYaw(string name)
        {
            var p = Profile(name);
            var buffer = new RecoilPopBuffer(p, .1f);
            float peakPitch = 0;
            for (uint shot = 0; shot < 20; shot++)
            {
                double now = 10 + shot * .1;
                buffer.Add(now, shot);
                Vector2 pop = buffer.Sample(now + 1.0 / 30);
                Assert.That(pop.y, Is.EqualTo(0f).Within(.0001),
                    "Independent seeded camera yaw must not shake the initial climb or mask gameplay yaw.");
                peakPitch = Mathf.Max(peakPitch, pop.x);
            }
            Assert.That(peakPitch, Is.GreaterThan(.1f), "Keep the vertical camera kick.");
            Assert.That(buffer.Sample(20), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void VandalCandidateKeepsFirstFourCameraKicksSmallBeforeIncreasing()
        {
            var p = Profile("Vandal");
            p.ValidateCurves();
            Assert.That(p.hipPitchCameraFollow[0].value, Is.Zero, "Candidate uses complement of crosshair alpha; native composition unverified.");
            Assert.That(p.hipPitchCameraFollow[1].value, Is.EqualTo(1));
            Assert.That(p.hipPitchCameraFollow[0].outTangent, Is.EqualTo(.0024416363f).Within(.0001));
            Assert.That(p.hipPitchCameraFollow[1].inTangent, Is.EqualTo(.4436298f).Within(.0001));
            Assert.That(p.cameraPopScale, Is.EqualTo(new Vector2(1, 0)));
            float[] pitch = { .4f, .9f, 2.24f, 3.58f, 4.92f, 6.26f, 7.6f };
            float camera = 0, firstFour = 0;
            for (int i = 0; i < pitch.Length; i++)
            {
                camera += (pitch[i] - (i == 0 ? 0 : pitch[i - 1])) * p.Evaluate(p.hipPitchCameraFollow, i + 1);
                if (i == 3) firstFour = camera;
            }
            Assert.That(firstFour, Is.EqualTo(.3375083f).Within(.0001));
            Assert.That(camera, Is.EqualTo(2.9760161f).Within(.0001));
            Assert.That(camera - firstFour, Is.GreaterThan(firstFour * 5));
        }

        [TestCase("Warden", .01942118f, .30650654f)]
        [TestCase("Odin", .10363402f, .79037702f)]
        public void AutomaticCameraClimbStartsSmallAndIncreasesWithItsOwnEnvelope(string name, float afterTwo, float afterFour)
        {
            var p = Profile(name);
            float[] pitch = name == "Warden"
                ? new[] { .6f, 1.5f, 2.5f, 3.75f }
                : new[] { .35f, 1f, 1.7288516f, 2.4577032f };
            float camera = 0;
            for (int i = 0; i < pitch.Length; i++)
            {
                camera += (pitch[i] - (i == 0 ? 0 : pitch[i - 1])) * p.Evaluate(p.hipPitchCameraFollow, i + 1);
                if (i == 1) Assert.That(camera, Is.EqualTo(afterTwo).Within(.0001));
            }
            Assert.That(camera, Is.EqualTo(afterFour).Within(.0001));
        }

        [TestCase("Operator", 1.25f, 1f)]
        [TestCase("Bucky", 2.75f, 0f)]
        public void ZeroEnvelopeWeaponsKeepTheirOwnTwoAxisPop(string name, float peak, float follow)
        {
            var p = Profile(name);
            p.ValidateCurves();
            Assert.That(p.hipPitchCameraFollow, Is.Null.Or.Empty);
            Assert.That(p.cameraRecoilFollow, Is.EqualTo(Vector2.one * follow));
            var buffer = new RecoilPopBuffer(p, .1f);
            buffer.Add(10, 17);
            var pop = buffer.Sample(10 + 1.0 / 30);
            Assert.That(pop.x, Is.EqualTo(peak).Within(.0001));
            Assert.That(Mathf.Abs(pop.y), Is.EqualTo(name == "Operator" ? .75f : .4f).Within(.0001));
        }

        [Test]
        public void ClassicResolvesInheritedAlphaAndKeepsSmallTwoAxisPop()
        {
            var p = Profile("Classic");
            Assert.That(p.hipPitchCameraFollow, Is.Not.Null.And.Length.EqualTo(2));
            var a = p.hipPitchCameraFollow[0];
            var b = p.hipPitchCameraFollow[1];
            Assert.That(a.value, Is.Zero);
            Assert.That(a.mode, Is.EqualTo(1));
            Assert.That(a.inTangent, Is.EqualTo(-.0054213763f).Within(.0001));
            Assert.That(b.time, Is.EqualTo(3));
            Assert.That(b.value, Is.EqualTo(1));
            Assert.That(b.mode, Is.EqualTo(2));
            Assert.That(b.inTangent, Is.EqualTo(.52790296f).Within(.0001));
            Assert.That(b.outTangent, Is.EqualTo(.5278984f).Within(.0001));
            float camera = 0;
            float[] expected = { .0888889f, .2666667f, .5333333f };
            for (int i = 1; i <= 3; i++)
            {
                camera += (1f / 3) * .8f * p.Evaluate(p.hipPitchCameraFollow, i);
                Assert.That(camera, Is.EqualTo(expected[i - 1]).Within(.0001));
            }
            var buffer = new RecoilPopBuffer(p, .2f);
            buffer.Add(10, 17);
            Vector2 pop = buffer.Sample(10 + 1.0 / 30);
            Assert.That(pop.x, Is.EqualTo(.15f).Within(.0001));
            Assert.That(Mathf.Abs(pop.y), Is.EqualTo(.15f).Within(.0001));
        }
    }
}
