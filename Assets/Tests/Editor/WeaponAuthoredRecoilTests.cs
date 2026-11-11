using FPS.RecoilPort;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class WeaponAuthoredRecoilTests
    {
        static WeaponData Data(string name) => AssetDatabase.LoadAssetAtPath<WeaponData>(
            $"Assets/FPS/Features/Weapons/Content/{name}/{name}.asset");
        static readonly WeaponStabilityContext Idle = new WeaponStabilityContext { grounded = true };
        const float Tolerance = .0001f;

        [Test]
        public void VandalDefaultMatchesIndependentFixtures()
        {
            var data = Data("Vandal");
            Assert.That(data.recoilPattern, Is.Not.Null);
            Assert.That(data.recoilPattern.pitchEnvelope, Is.Not.Null.And.Not.Empty, "candidate must contain the Default envelope");
            var profile = Object.Instantiate(data.recoilPattern);
            try
            {
                profile.ValidateCurves();
                Assert.That(profile.tapEfficiency, Is.EqualTo(6));
                Assert.That(profile.MaximumStabilityIndex, Is.EqualTo(24.053804f).Within(Tolerance));
                Assert.That(profile.sprintRecoilMultiplier, Is.EqualTo(new Vector2(1.8f, 1)));
                Assert.That(profile.adsRecoilMultiplier, Is.EqualTo(new Vector2(.7f,.7f)));
                Assert.That(profile.pitchEnvelope.Length, Is.EqualTo(11));
                Assert.That(profile.yawEnvelope.Length, Is.EqualTo(8));
                Assert.That(profile.pitchEnvelope[3].mode, Is.EqualTo(2));
                Assert.That(profile.pitchEnvelope[3].outTangent, Is.EqualTo(.967771f).Within(Tolerance));
                var state = new WeaponStability();
                float[] pitches = { .4f, .9f };
                float[] spreads = { .25f, .3f, .35f };
                for (uint shot = 0; shot < 3; shot++)
                {
                    Assert.That(state.Fire(profile, 10 + shot * (double)data.FireInterval, data.FireInterval, Idle, shot),
                        Is.EqualTo(spreads[shot]).Within(Tolerance));
                    if (shot < 2) Assert.That(state.pitchTarget, Is.EqualTo(pitches[shot]).Within(Tolerance));
                }
                var aimed = Idle; aimed.aimed = true; state = default;
                Assert.That(state.Fire(profile, 10, data.FireInterval, aimed, 1), Is.EqualTo(.1575f).Within(Tolerance));
                Assert.That(state.pitchTarget, Is.EqualTo(.28f).Within(Tolerance));
                Assert.That(profile.GetSpread(0, new WeaponStabilityContext { grounded = false }), Is.EqualTo(10));
                Assert.That(profile.cameraPopPitchCurve[1].value, Is.EqualTo(.15f));
                Assert.That(profile.cameraPopPitchCurve[1].time, Is.EqualTo(.01f));
                Assert.That(profile.PopCapacity(data.FireInterval), Is.LessThanOrEqualTo(64));
                CheckProtectedYaw(profile, data.FireInterval, .6f);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ClassicPrimaryMatchesCubicErrorAndRecoveryDomain()
        {
            var data = Data("Classic");
            Assert.That(data.recoilPattern.pitchEnvelope, Is.Not.Null.And.Not.Empty, "candidate must contain the primary envelope");
            var profile = Object.Instantiate(data.recoilPattern);
            try
            {
                profile.ValidateCurves();
                Assert.That(profile.tapEfficiency, Is.EqualTo(4));
                Assert.That(profile.MaximumStabilityIndex, Is.EqualTo(4));
                Assert.That(profile.adsRecoilMultiplier, Is.EqualTo(Vector2.one));
                Assert.That(profile.sprintRecoilMultiplier, Is.EqualTo(Vector2.one));
                Assert.That(profile.yawSwitchChance, Is.Zero);
                Assert.That(profile.firingError[1].mode, Is.EqualTo(2));
                Assert.That(profile.firingError[1].outTangent, Is.EqualTo(.042896625f).Within(Tolerance));
                Assert.That(profile.GetSpread(2, Idle), Is.EqualTo(1.3107242f).Within(Tolerance));
                var aimed = Idle; aimed.aimed = true;
                Assert.That(profile.GetSpread(0, aimed), Is.EqualTo(.4f).Within(Tolerance));
                float[] expected = { 1f / 3, 2f / 3, 1, 1 };
                var state = new WeaponStability();
                for (uint shot = 0; shot < expected.Length; shot++)
                {
                    state.Fire(profile, 10 + shot * (double)data.FireInterval, data.FireInterval, Idle, shot);
                    Assert.That(state.pitchTarget, Is.EqualTo(expected[shot]).Within(Tolerance));
                    Assert.That(state.yawMagnitude, Is.Zero);
                }
                Assert.That(profile.cameraPopPitchCurve[1].time, Is.EqualTo(.025f));
                Assert.That(profile.cameraPopPitchCurve[1].value, Is.EqualTo(.15f));
                Assert.That(data.fireMode, Is.EqualTo(FireMode.Single));
                Assert.That(data.projectileCount, Is.EqualTo(1));
                Assert.That(data.RoundsPerSecond, Is.EqualTo(6.75f).Within(Tolerance));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void OdinDefaultUsesFullSeventyFiveShotDomain()
        {
            var data = Data("Odin");
            Assert.That(data.recoilPattern.pitchEnvelope, Is.Not.Null.And.Not.Empty, "candidate must contain Odin Default");
            var profile = Object.Instantiate(data.recoilPattern);
            try
            {
                profile.ValidateCurves();
                Assert.That(profile.MaximumStabilityIndex, Is.EqualTo(75));
                Assert.That(profile.tapEfficiency, Is.EqualTo(1));
                Assert.That(profile.yawSwitchChance, Is.EqualTo(.05f));
                Assert.That(profile.sprintRecoilMultiplier, Is.EqualTo(new Vector2(1.4f, 1.4f)));
                Assert.That(profile.GetSpread(0, Idle), Is.EqualTo(.8f).Within(Tolerance));
                Assert.That(profile.GetSpread(35, Idle), Is.EqualTo(1.1f).Within(Tolerance));
                Assert.That(profile.GetSpread(75, Idle), Is.EqualTo(1.3f).Within(Tolerance));
                var state = new WeaponStability();
                for (uint shot = 0; shot < 75; shot++)
                {
                    state.Fire(profile, 10 + shot * (double)data.FireInterval, data.FireInterval, Idle, shot);
                    if (shot == 0) Assert.That(state.pitchTarget, Is.EqualTo(.35f).Within(Tolerance));
                    if (shot == 1) Assert.That(state.pitchTarget, Is.EqualTo(1).Within(Tolerance));
                }
                Assert.That(state.index, Is.EqualTo(75));
                Assert.That(state.pitchTarget, Is.EqualTo(17).Within(Tolerance));
                var aimed = Idle; aimed.aimed = true; state = default;
                Assert.That(state.Fire(profile, 10, data.FireInterval, aimed, 1), Is.EqualTo(.79f).Within(Tolerance));
                Assert.That(state.pitchTarget, Is.EqualTo(.245f).Within(Tolerance));
                Assert.That(profile.cameraPopPitchCurve[0].mode, Is.EqualTo(2));
                Assert.That(profile.cameraPopPitchCurve[0].outTangent, Is.EqualTo(7.689682f).Within(Tolerance));
                Assert.That(profile.cameraPopYawCurve[1].value, Is.EqualTo(.125f));
                Assert.That(data.restartFireAnimationPerShot, Is.False);
                Assert.That(data.RoundsPerSecond, Is.EqualTo(12));
                CheckProtectedYaw(profile, data.FireInterval, 1);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void OperatorSeparatesHipAndScopedAccuracyWithZeroRecovery()
        {
            var data = Data("Operator");
            Assert.That(data.recoilPattern.firingError, Is.Not.Null.And.Not.Empty, "candidate must contain Operator accuracy");
            var profile = Object.Instantiate(data.recoilPattern);
            try
            {
                profile.ValidateCurves();
                var aimed = Idle; aimed.aimed = true;
                Assert.That(profile.GetSpread(0, Idle), Is.EqualTo(5).Within(Tolerance));
                Assert.That(profile.GetSpread(0, aimed), Is.Zero);
                Assert.That(profile.GetSpread(.125f, Idle), Is.EqualTo(5.0892857f).Within(Tolerance));
                Assert.That(profile.GetSpread(.125f, aimed), Is.EqualTo(.5f).Within(Tolerance));
                aimed.normalizedSpeed = .149f;
                Assert.That(profile.GetSpread(0, aimed), Is.Zero);
                aimed.normalizedSpeed = .15f;
                Assert.That(profile.GetSpread(0, aimed), Is.EqualTo(10).Within(Tolerance));
                var state = new WeaponStability();
                state.Fire(profile, 10, data.FireInterval, Idle, 1);
                Assert.That(state.index, Is.EqualTo(1));
                Assert.That(state.pitchTarget, Is.Zero);
                Assert.That(state.yawMagnitude, Is.Zero);
                Assert.That(state.RecoveryFraction(profile, 10 + (double)data.FireInterval - .000001, data.FireInterval), Is.Zero);
                Assert.That(state.RecoveryFraction(profile, 10 + (double)data.FireInterval, data.FireInterval), Is.EqualTo(1));
                state.Fire(profile, 10 + (double)data.FireInterval, data.FireInterval, aimed, 2);
                Assert.That(state.acceptedShots, Is.EqualTo(1));
                Assert.That(profile.cameraPopPitchCurve[1].value, Is.EqualTo(1.25f));
                Assert.That(profile.cameraPopYawCurve[1].value, Is.EqualTo(.75f));
                Assert.That(data.exitAimAfterShot, Is.True);
                Assert.That(data.projectileCount, Is.EqualTo(1));
                Assert.That(data.RoundsPerSecond, Is.EqualTo(.6f).Within(Tolerance));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void BuckyDefaultKeepsEightPelletsAndUsesOneThreeDegreeCone()
        {
            var data = Data("Bucky");
            Assert.That(data.recoilPattern, Is.Not.Null, "Bucky needs its own ScriptableObject profile");
            var profile = Object.Instantiate(data.recoilPattern);
            try
            {
                profile.ValidateCurves();
                Assert.That(profile.GetSpread(0, Idle), Is.EqualTo(3));
                var aimed = Idle; aimed.aimed = true;
                Assert.That(profile.GetSpread(0, aimed), Is.EqualTo(3));
                Assert.That(profile.pitchEnvelope[0].value, Is.Zero);
                Assert.That(profile.yawEnvelope.Length, Is.EqualTo(1));
                Assert.That(profile.yawEnvelope[0].value, Is.Zero, "explicit zero adaptation");
                Assert.That(profile.cameraPopPitchCurve[1].value, Is.EqualTo(2.75f));
                Assert.That(profile.cameraPopYawCurve[1].value, Is.EqualTo(.4f));
                Assert.That(profile.CameraPopDuration, Is.EqualTo(.25f));
                Assert.That(data.projectileCount, Is.EqualTo(8));
                Assert.That(data.damage, Is.EqualTo(18.75f));
                Assert.That(data.reloadMode, Is.EqualTo(ReloadMode.PerShell));
                var server = new WeaponServerState();
                server.EnsureInitialized(1, data);
                Assert.That(server.TryConsumeFire(data, 10), Is.True);
                Assert.That(server.MagazineAmmo, Is.EqualTo(data.magazineSize - 1));
                uint seed = WeaponBallistics.BuildShotSeed(1, 1, 0);
                for (int pellet = 0; pellet < data.projectileCount; pellet++)
                {
                    Vector3 direction = WeaponBallistics.GetProjectileDirection(Vector3.forward, 3, seed, pellet);
                    Assert.That(direction.magnitude, Is.EqualTo(1).Within(Tolerance));
                    Assert.That(Vector3.Angle(Vector3.forward, direction), Is.LessThanOrEqualTo(3 + Tolerance));
                }
            }
            finally { Object.DestroyImmediate(profile); }
        }

        static void CheckProtectedYaw(RecoilPattern profile, float interval, float duration)
        {
            Assert.That(profile.protectedShots, Is.EqualTo(6));
            Assert.That(profile.yawSwitchDuration, Is.EqualTo(duration));
            profile.yawSwitchChance = 1;
            profile.recoveryTime = new[] { new UnrealRecoilCurve.Key { value = 2, mode = 1 } };
            var state = new WeaponStability();
            for (uint shot = 0; shot < 6; shot++)
            {
                state.Fire(profile, 10 + shot * (double)interval, interval, Idle, shot);
                Assert.That(state.yawFromSign, Is.EqualTo(state.yawToSign));
            }
            double switchTime = 10 + 6 * (double)interval;
            state.Fire(profile, switchTime, interval, Idle, 6);
            Assert.That(state.yawFromSign, Is.EqualTo(-state.yawToSign));
            Assert.That(state.YawDirectionAt(profile, switchTime + duration / 2), Is.EqualTo(0).Within(Tolerance));
            Assert.That(state.YawDirectionAt(profile, switchTime + duration), Is.EqualTo(state.yawToSign).Within(Tolerance));
        }
    }
}
