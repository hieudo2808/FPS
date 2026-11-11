using System;
using FPS.RecoilPort;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class WeaponRecoilProfilesTests
    {
        RecoilPattern profile;
        static UnrealRecoilCurve.Key K(float x, float y, int mode = 1, float arrive = 0, float leave = 0)
            => new UnrealRecoilCurve.Key { time = x, value = y, mode = mode, inTangent = arrive, outTangent = leave };

        [SetUp]
        public void SetUp() => profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/FPS/Features/Weapons/Content/Warden/Warden.asset").recoilPattern);
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [TestCase("firingError", 75f)]
        [TestCase("recoveryTime", 4f)]
        public void DomainIncludesErrorAndRecoveryEvenWithZeroEnvelopes(string curve, float end)
        {
            profile.pitchEnvelope = profile.yawEnvelope = new[] { K(0, 0) };
            profile.firingError = profile.recoveryTime = new[] { K(0, .1f) };
            typeof(RecoilPattern).GetField(curve).SetValue(profile, new[] { K(0, .1f), K(end, .1f) });
            Assert.That(profile.MaximumStabilityIndex, Is.EqualTo(end));
            var state = new WeaponStability();
            for (uint shot = 0; shot < 3; shot++)
                state.Fire(profile, 10 + shot * (double).1f, .1f, new WeaponStabilityContext { grounded = true }, shot);
            Assert.That(state.index, Is.EqualTo(3).Within(.0001));
        }

        [Test]
        public void ConstantCurvesStillAllowOneIndex()
        {
            profile.pitchEnvelope = profile.yawEnvelope = profile.firingError = profile.recoveryTime = new[] { K(0, 0) };
            Assert.That(profile.MaximumStabilityIndex, Is.EqualTo(1));
        }

        [Test]
        public void ZeroRecoveryCompletesExactlyAtFireInterval()
        {
            profile.recoveryTime = new[] { K(0, 0) };
            var state = new WeaponStability();
            state.Fire(profile, 10, .125f, new WeaponStabilityContext { grounded = true }, 1);
            Assert.That(state.RecoveryFraction(profile, 10.124999, .125f), Is.Zero);
            Assert.That(state.RecoveryFraction(profile, 10.125, .125f), Is.EqualTo(1));
            Assert.That(state.RecoveryFraction(profile, 10.126, .125f), Is.EqualTo(1));
            state.Fire(profile, 10.125, .125f, new WeaponStabilityContext { grounded = true }, 2);
            Assert.That(state.acceptedShots, Is.EqualTo(1), "zero recovery begins a clean burst at the boundary");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecoveryCannotBeNegativeAtKeysOrBetweenCubicKeys(bool between)
        {
            profile.recoveryTime = between
                ? new[] { K(0, .1f, 2, 0, -2), K(1, .1f, 2, 2, 0) }
                : new[] { K(0, -.1f) };
            Assert.Throws<ArgumentException>(() => profile.ValidateCurves());
        }

        [Test]
        public void AimedOverrideExpandsDomainAndBypassesAffineAfterMovementMaximum()
        {
            profile.pitchEnvelope = profile.yawEnvelope = profile.firingError = profile.recoveryTime = new[] { K(0, 0) };
            profile.aimedFiringError = new[] { K(0, .5f), K(8, .5f) };
            profile.walkingError = new[] { K(0, .6f) };
            var aimed = new WeaponStabilityContext { grounded = true, aimed = true };
            Assert.That(profile.GetSpread(0, aimed), Is.EqualTo(.6f).Within(.0001));
            Assert.That(profile.MaximumStabilityIndex, Is.EqualTo(8));
            aimed.aimed = false;
            Assert.That(profile.GetSpread(0, aimed), Is.EqualTo(.6f).Within(.0001));
        }

        [Test]
        public void SprintAndAdsMultiplyPerAxisWithoutRescalingPreviousKick()
        {
            profile.pitchEnvelope = profile.yawEnvelope = new[] { K(0, 0), K(3, 3) };
            profile.recoveryTime = new[] { K(0, 1) };
            profile.sprintRecoilMultiplier = new Vector2(2, 3);
            profile.adsRecoilMultiplier = new Vector2(.25f, .5f);
            profile.yawSwitchChance = 0;
            var context = new WeaponStabilityContext { grounded = true, sprinting = true, aimed = true };
            var state = new WeaponStability();
            state.Fire(profile, 10, .125f, context, 1);
            Assert.That(state.pitchTarget, Is.EqualTo(.5f).Within(.0001));
            Assert.That(state.yawMagnitude, Is.EqualTo(1.5f).Within(.0001));
            context.sprinting = context.aimed = false;
            state.Fire(profile, 10.125, .125f, context, 2);
            Assert.That(state.pitchTarget, Is.EqualTo(1.5f).Within(.0001));
            Assert.That(state.yawMagnitude, Is.EqualTo(2.5f).Within(.0001));
        }

        [TestCase("sprintRecoilMultiplier", -1f)]
        [TestCase("adsRecoilMultiplier", float.NaN)]
        [TestCase("adsErrorMultiplier", -1f)]
        public void ProfileRejectsInvalidContextMultipliers(string field, float value)
        {
            var member = typeof(RecoilPattern).GetField(field);
            member.SetValue(profile, member.FieldType == typeof(Vector2) ? (object)new Vector2(1, value) : value);
            Assert.Throws<ArgumentException>(() => profile.ValidateCurves());
        }

        [Test]
        public void AimedOverrideRejectsInvalidKeysButEmptyRetainsWardenAffine()
        {
            profile.aimedFiringError = new[] { K(0, float.PositiveInfinity) };
            Assert.Throws<ArgumentException>(() => profile.ValidateCurves());
            profile.aimedFiringError = Array.Empty<UnrealRecoilCurve.Key>();
            profile.ValidateCurves();
            Assert.That(profile.GetSpread(0, new WeaponStabilityContext { grounded = true, aimed = true }),
                Is.EqualTo(.0425f).Within(.0001));
        }
    }
}
