using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class RecoilRevisionFourTests
    {
        static RecoilPattern Profile(string name) => AssetDatabase.LoadAssetAtPath<WeaponData>(
            $"Assets/FPS/Features/Weapons/Content/{name}/{name}.asset").recoilPattern;

        [TestCase("Vandal", 1f, 0f)]
        [TestCase("Classic", 1f, 1f)]
        [TestCase("Odin", 1f, 0f)]
        [TestCase("Warden", 1f, 0f)]
        [TestCase("Operator", 1f, 1f)]
        [TestCase("Bucky", 1f, 1f)]
        public void CandidateUsesExplicitPresentationGain(string name, float pitch, float yaw)
        {
            Assert.That(Profile(name).cameraPopScale, Is.EqualTo(new Vector2(pitch, yaw)),
                "Presentation gain is a documented project adaptation, not a native multiplier.");
        }

        [TestCase("Vandal", 1f)]
        [TestCase("Classic", .8f)]
        [TestCase("Odin", 1f)]
        [TestCase("Warden", 1f)]
        [TestCase("Operator", 1f)]
        [TestCase("Bucky", 0f)]
        public void CandidateSeparatesHipFollowAndKeepsAimedFollow(string name, float hip)
        {
            Assert.That(Profile(name).cameraRecoilFollow, Is.EqualTo(Vector2.one * hip));
            Assert.That(Profile(name).adsCameraRecoilFollow, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void WardenUsesResolvedPop15ShapeOnBothAxes()
        {
            var p = Profile("Warden");
            Assert.That(p.HasCameraPopCurves, Is.True, "replace the 130 ms legacy pulse with resolved Pop15");
            foreach (var curve in new[] { p.cameraPopPitchCurve, p.cameraPopYawCurve })
            {
                Assert.That(curve.Length, Is.EqualTo(3));
                Assert.That(curve[1].time, Is.EqualTo(.01f));
                Assert.That(curve[1].value, Is.EqualTo(.15f));
                Assert.That(curve[1].mode, Is.EqualTo(2));
                Assert.That(curve[1].outTangent, Is.EqualTo(-.059356f).Within(.0001));
                Assert.That(p.Evaluate(curve, .13f), Is.EqualTo(.1422486f).Within(.0001));
                Assert.That(p.Evaluate(curve, .5f), Is.EqualTo(.1053852f).Within(.0001));
                Assert.That(p.Evaluate(curve, 1), Is.Zero);
            }
            var clone = Object.Instantiate(p);
            try
            {
                clone.cameraPopScale = Vector2.one;
                var buffer = new RecoilPopBuffer(clone, .1538462f);
                buffer.Add(10, 17);
                Vector2 peak = buffer.Sample(10 + 1.0 / 30);
                Assert.That(peak.x, Is.EqualTo(.15f).Within(.0001));
                Assert.That(Mathf.Abs(peak.y), Is.EqualTo(.15f).Within(.0001));
            }
            finally { Object.DestroyImmediate(clone); }
        }

        [TestCase(-.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void FollowRejectsValuesOutsideFiniteUnitInterval(float invalid)
        {
            var clone = Object.Instantiate(Profile("Vandal"));
            try
            {
                clone.cameraRecoilFollow = new Vector2(invalid, 1);
                Assert.Throws<System.ArgumentException>(() => clone.ValidateCurves());
                clone.cameraRecoilFollow = Vector2.one;
                clone.adsCameraRecoilFollow = new Vector2(1, invalid);
                Assert.Throws<System.ArgumentException>(() => clone.ValidateCurves());
            }
            finally { Object.DestroyImmediate(clone); }
        }

        [TestCase(0, .284f, 0, .284f, 0, .5f, .142f)]
        [TestCase(0, .6f, 0, .05f, 0, .5f, .025f)]
        [TestCase(.6f, .3f, 0, 0, .3f, .5f, .15f)]
        [TestCase(.6f, 0, 0, 0, .3f, .5f, 0)]
        [TestCase(.6f, 0, .05f, -.05f, .025f, .5f, 0)]
        [TestCase(.4f, -.6f, .1f, -.7f, .2f, .5f, -.3f)]
        [TestCase(-.4f, .6f, -.1f, .7f, -.2f, .5f, .3f)]
        [TestCase(.4f, .6f, 0, .2f, .2f, 1, .2f)]
        public void SeparationUsesAppliedKickAndReturnsEvenAfterCompensation(float before, float after,
            float debt, float applied, float separation, float follow, float expected)
            => Assert.That(RecoilController.AdvanceCameraSeparation(before, after, debt, applied, separation, follow),
                Is.EqualTo(expected).Within(.0001));
    }
}
