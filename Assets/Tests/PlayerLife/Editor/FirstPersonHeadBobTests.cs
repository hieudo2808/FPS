using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests.PlayerLife.Editor
{
    public sealed class FirstPersonHeadBobTests
    {
        [Test]
        public void StationaryPlayer_ProducesZeroBob()
        {
            var bob = new FirstPersonHeadBob();
            var settings = FirstPersonHeadBobSettings.Default;
            var input = new FirstPersonHeadBobInput
            {
                planarSpeed = 0f,
                maxSpeed = 5f,
                moveInput = Vector2.zero,
                isGrounded = true,
                isSprinting = false,
                aimBlend = 0f
            };

            FirstPersonHeadBobOutput output = default;
            for (int i = 0; i < 60; i++)
                output = bob.Evaluate(in settings, in input, 1f / 60f);

            Assert.AreEqual(Vector3.zero, output.cameraPositionOffset);
            Assert.AreEqual(Vector3.zero, output.cameraEulerOffset);
            Assert.AreEqual(Vector3.zero, output.weaponPositionOffset);
            Assert.AreEqual(Vector3.zero, output.weaponEulerOffset);
        }

        [Test]
        public void WalkAndSprint_ProduceGentleAndProportionalBob()
        {
            var settings = FirstPersonHeadBobSettings.Default;
            float walkPeakCam = MeasurePeakCameraDisplacement(settings, planarSpeed: 2.5f, isSprinting: false, aimBlend: 0f);
            float sprintPeakCam = MeasurePeakCameraDisplacement(settings, planarSpeed: 5.0f, isSprinting: true, aimBlend: 0f);
            float walkPeakWeapon = MeasurePeakWeaponDisplacement(settings, planarSpeed: 2.5f, isSprinting: false, aimBlend: 0f);
            float sprintPeakWeapon = MeasurePeakWeaponDisplacement(settings, planarSpeed: 5.0f, isSprinting: true, aimBlend: 0f);

            Assert.Greater(walkPeakCam, 0.005f, "Walk camera bob should be noticeable.");
            Assert.Less(walkPeakCam, 0.05f, "Walk camera bob should stay subtle.");
            Assert.Greater(sprintPeakCam, walkPeakCam * 1.3f, "Sprint camera bob should be stronger than walk.");
            Assert.Greater(walkPeakWeapon, 0.002f, "Walk weapon bob should be noticeable.");
            Assert.Greater(sprintPeakWeapon, walkPeakWeapon * 1.3f, "Sprint weapon bob should be stronger than walk.");
        }

        [Test]
        public void AimingDownSights_StronglyDampsCameraAndWeaponBob()
        {
            var settings = FirstPersonHeadBobSettings.Default;
            float hipPeakCam = MeasurePeakCameraDisplacement(settings, planarSpeed: 2.5f, isSprinting: false, aimBlend: 0f);
            float adsPeakCam = MeasurePeakCameraDisplacement(settings, planarSpeed: 2.5f, isSprinting: false, aimBlend: 1f);
            float hipPeakWeapon = MeasurePeakWeaponDisplacement(settings, planarSpeed: 2.5f, isSprinting: false, aimBlend: 0f);
            float adsPeakWeapon = MeasurePeakWeaponDisplacement(settings, planarSpeed: 2.5f, isSprinting: false, aimBlend: 1f);

            Assert.Less(adsPeakCam, hipPeakCam * 0.25f, "ADS should strongly damp camera bob to keep aim steady.");
            Assert.Less(adsPeakWeapon, hipPeakWeapon * 0.25f, "ADS should strongly damp weapon viewmodel bob.");
        }

        [Test]
        public void ScopedOverlayOrSuppressed_DecaysBobToZero()
        {
            var bob = new FirstPersonHeadBob();
            var settings = FirstPersonHeadBobSettings.Default;
            var moving = new FirstPersonHeadBobInput
            {
                planarSpeed = 5f,
                maxSpeed = 5f,
                moveInput = Vector2.up,
                isGrounded = true,
                isSprinting = true,
                aimBlend = 0f
            };
            for (int i = 0; i < 45; i++)
                bob.Evaluate(in settings, in moving, 1f / 60f);

            var scoped = moving;
            scoped.aimBlend = 1f;
            scoped.isScopedOverlay = true;
            FirstPersonHeadBobOutput output = default;
            for (int i = 0; i < 90; i++)
                output = bob.Evaluate(in settings, in scoped, 1f / 60f);

            Assert.Less(output.cameraPositionOffset.magnitude, 0.0005f);
            Assert.Less(output.weaponPositionOffset.magnitude, 0.0005f);
        }

        [Test]
        public void Landing_ProducesTemporaryDownwardDipAndRecovers()
        {
            var bob = new FirstPersonHeadBob();
            var settings = FirstPersonHeadBobSettings.Default;
            var airborne = new FirstPersonHeadBobInput { isGrounded = false, maxSpeed = 5f };
            bob.Evaluate(in settings, in airborne, 1f / 60f);

            var landed = new FirstPersonHeadBobInput { isGrounded = true, maxSpeed = 5f };
            float minY = 0f;
            FirstPersonHeadBobOutput finalOut = default;
            for (int i = 0; i < 90; i++)
            {
                finalOut = bob.Evaluate(in settings, in landed, 1f / 60f);
                if (finalOut.cameraPositionOffset.y < minY)
                    minY = finalOut.cameraPositionOffset.y;
            }

            Assert.Less(minY, -0.005f, "Landing should cause a gentle downward camera dip.");
            Assert.Less(Mathf.Abs(finalOut.cameraPositionOffset.y), 0.0005f, "Landing dip should recover smoothly.");
        }

        private static float MeasurePeakCameraDisplacement(FirstPersonHeadBobSettings settings, float planarSpeed, bool isSprinting, float aimBlend)
        {
            var bob = new FirstPersonHeadBob();
            var input = new FirstPersonHeadBobInput
            {
                planarSpeed = planarSpeed,
                maxSpeed = 5f,
                moveInput = Vector2.up,
                isGrounded = true,
                isSprinting = isSprinting,
                aimBlend = aimBlend
            };

            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                var o = bob.Evaluate(in settings, in input, 1f / 60f);
                peak = Mathf.Max(peak, o.cameraPositionOffset.magnitude);
            }
            return peak;
        }

        private static float MeasurePeakWeaponDisplacement(FirstPersonHeadBobSettings settings, float planarSpeed, bool isSprinting, float aimBlend)
        {
            var bob = new FirstPersonHeadBob();
            var input = new FirstPersonHeadBobInput
            {
                planarSpeed = planarSpeed,
                maxSpeed = 5f,
                moveInput = Vector2.up,
                isGrounded = true,
                isSprinting = isSprinting,
                aimBlend = aimBlend
            };

            float peak = 0f;
            for (int i = 0; i < 120; i++)
            {
                var o = bob.Evaluate(in settings, in input, 1f / 60f);
                peak = Mathf.Max(peak, o.weaponPositionOffset.magnitude);
            }
            return peak;
        }
    }
}
