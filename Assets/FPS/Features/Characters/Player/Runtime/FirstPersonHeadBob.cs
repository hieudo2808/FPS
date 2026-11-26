using System;
using UnityEngine;

namespace FPS
{
    [Serializable]
    public struct FirstPersonHeadBobSettings
    {
        [Header("Cycle Frequency (strides/sec)")]
        [Min(0.1f)] public float walkFrequency;
        [Min(0.1f)] public float sprintFrequency;

        [Header("Camera Bob (Walk)")]
        [Min(0f)] public float walkCameraVertical;
        [Min(0f)] public float walkCameraHorizontal;
        [Min(0f)] public float walkCameraPitchDegrees;
        [Min(0f)] public float walkCameraRollDegrees;

        [Header("Camera Bob (Sprint)")]
        [Min(0f)] public float sprintCameraVertical;
        [Min(0f)] public float sprintCameraHorizontal;
        [Min(0f)] public float sprintCameraPitchDegrees;
        [Min(0f)] public float sprintCameraRollDegrees;

        [Header("Weapon Viewmodel Bob (Walk)")]
        [Min(0f)] public float walkWeaponVertical;
        [Min(0f)] public float walkWeaponHorizontal;
        [Min(0f)] public float walkWeaponPitchDegrees;
        [Min(0f)] public float walkWeaponRollDegrees;

        [Header("Weapon Viewmodel Bob (Sprint)")]
        [Min(0f)] public float sprintWeaponVertical;
        [Min(0f)] public float sprintWeaponHorizontal;
        [Min(0f)] public float sprintWeaponPitchDegrees;
        [Min(0f)] public float sprintWeaponRollDegrees;

        [Header("Strafe Tilt & Landing")]
        [Min(0f)] public float strafeRollDegrees;
        [Min(0f)] public float landingCameraDip;
        [Min(0f)] public float landingWeaponDip;
        [Min(1f)] public float landingRecoverySpeed;

        [Header("ADS Damping (Keep Aim Steady)")]
        [Range(0f, 1f)] public float adsCameraPositionScale;
        [Range(0f, 1f)] public float adsCameraRotationScale;
        [Range(0f, 1f)] public float adsWeaponBobScale;

        [Header("Smoothing")]
        [Min(1f)] public float blendInSpeed;
        [Min(1f)] public float blendOutSpeed;
        [Min(0.01f)] public float minMoveSpeedThreshold;

        public static FirstPersonHeadBobSettings Default => new FirstPersonHeadBobSettings
        {
            walkFrequency = 1.75f,
            sprintFrequency = 2.45f,

            walkCameraVertical = 0.022f,
            walkCameraHorizontal = 0.010f,
            walkCameraPitchDegrees = 0.18f,
            walkCameraRollDegrees = 0.28f,

            sprintCameraVertical = 0.042f,
            sprintCameraHorizontal = 0.020f,
            sprintCameraPitchDegrees = 0.38f,
            sprintCameraRollDegrees = 0.60f,

            walkWeaponVertical = 0.008f,
            walkWeaponHorizontal = 0.011f,
            walkWeaponPitchDegrees = 0.40f,
            walkWeaponRollDegrees = 0.65f,

            sprintWeaponVertical = 0.016f,
            sprintWeaponHorizontal = 0.022f,
            sprintWeaponPitchDegrees = 0.90f,
            sprintWeaponRollDegrees = 1.45f,

            strafeRollDegrees = 0.40f,
            landingCameraDip = 0.032f,
            landingWeaponDip = 0.012f,
            landingRecoverySpeed = 12f,

            adsCameraPositionScale = 0.15f,
            adsCameraRotationScale = 0.10f,
            adsWeaponBobScale = 0.12f,

            blendInSpeed = 10f,
            blendOutSpeed = 12f,
            minMoveSpeedThreshold = 0.15f
        };
    }

    public struct FirstPersonHeadBobInput
    {
        public float planarSpeed;
        public float maxSpeed;
        public Vector2 moveInput;
        public bool isGrounded;
        public bool isSprinting;
        public float aimBlend;
        public bool isScopedOverlay;
        public bool isSuppressed;
    }

    public struct FirstPersonHeadBobOutput
    {
        public Vector3 cameraPositionOffset;
        public Vector3 cameraEulerOffset;
        public Vector3 weaponPositionOffset;
        public Vector3 weaponEulerOffset;
    }

    /// <summary>
    /// Pure cosmetic FPS head-bob and weapon-bob evaluator.
    /// Produces gentle figure-8 locomotion feel while walking/sprinting and
    /// strongly damps offsets during ADS so aiming remains rock-steady.
    /// </summary>
    public sealed class FirstPersonHeadBob
    {
        private float phase;
        private float sprintBlend;
        private float landingOffset;
        private bool wasGrounded = true;
        private Vector3 currentCamPos;
        private Vector3 currentCamEuler;
        private Vector3 currentWeaponPos;
        private Vector3 currentWeaponEuler;

        public FirstPersonHeadBobOutput Current => new FirstPersonHeadBobOutput
        {
            cameraPositionOffset = currentCamPos,
            cameraEulerOffset = currentCamEuler,
            weaponPositionOffset = currentWeaponPos,
            weaponEulerOffset = currentWeaponEuler
        };

        public void Reset()
        {
            phase = 0f;
            sprintBlend = 0f;
            landingOffset = 0f;
            wasGrounded = true;
            currentCamPos = Vector3.zero;
            currentCamEuler = Vector3.zero;
            currentWeaponPos = Vector3.zero;
            currentWeaponEuler = Vector3.zero;
        }

        public FirstPersonHeadBobOutput Evaluate(in FirstPersonHeadBobSettings settings, in FirstPersonHeadBobInput input, float dt)
        {
            if (dt <= 0f || !float.IsFinite(dt))
                return Current;

            float safeDt = Mathf.Min(dt, 0.1f);
            float aim = Mathf.Clamp01(input.aimBlend);
            bool active = !input.isSuppressed && !input.isScopedOverlay;

            if (active && input.isGrounded && !wasGrounded)
            {
                landingOffset = 1f;
            }
            wasGrounded = input.isGrounded;

            if (landingOffset > 0f)
            {
                float landDecay = 1f - Mathf.Exp(-Mathf.Max(1f, settings.landingRecoverySpeed) * safeDt);
                landingOffset = Mathf.Lerp(landingOffset, 0f, landDecay);
                if (landingOffset < 0.001f)
                    landingOffset = 0f;
            }

            float maxSpeed = Mathf.Max(0.1f, input.maxSpeed);
            float speedNorm = Mathf.Clamp01(input.planarSpeed / maxSpeed);
            bool isMoving = active && input.isGrounded && input.planarSpeed >= settings.minMoveSpeedThreshold;

            float targetSprint = isMoving && input.isSprinting && aim < 0.25f ? 1f : 0f;
            float sprintLerp = 1f - Mathf.Exp(-8f * safeDt);
            sprintBlend = Mathf.Lerp(sprintBlend, targetSprint, sprintLerp);

            // Walk runs around half maxSpeed, so normalize amplitude cleanly for walk vs sprint.
            float moveWeight = isMoving ? Mathf.Clamp01(speedNorm * 1.85f) : 0f;

            Vector3 targetCamPos = Vector3.zero;
            Vector3 targetCamEuler = Vector3.zero;
            Vector3 targetWeaponPos = Vector3.zero;
            Vector3 targetWeaponEuler = Vector3.zero;

            if (isMoving)
            {
                float freq = Mathf.Lerp(settings.walkFrequency, settings.sprintFrequency, sprintBlend);
                phase += safeDt * freq * (Mathf.PI * 2f);
                if (phase > Mathf.PI * 200f)
                    phase -= Mathf.PI * 200f;

                float sinH = Mathf.Sin(phase);
                float cos2V = -Mathf.Cos(phase * 2f); // 2 vertical steps per full left-right stride
                float sin2V = Mathf.Sin(phase * 2f);

                float camV = Mathf.Lerp(settings.walkCameraVertical, settings.sprintCameraVertical, sprintBlend);
                float camH = Mathf.Lerp(settings.walkCameraHorizontal, settings.sprintCameraHorizontal, sprintBlend);
                float camPitch = Mathf.Lerp(settings.walkCameraPitchDegrees, settings.sprintCameraPitchDegrees, sprintBlend);
                float camRoll = Mathf.Lerp(settings.walkCameraRollDegrees, settings.sprintCameraRollDegrees, sprintBlend);

                float weapV = Mathf.Lerp(settings.walkWeaponVertical, settings.sprintWeaponVertical, sprintBlend);
                float weapH = Mathf.Lerp(settings.walkWeaponHorizontal, settings.sprintWeaponHorizontal, sprintBlend);
                float weapPitch = Mathf.Lerp(settings.walkWeaponPitchDegrees, settings.sprintWeaponPitchDegrees, sprintBlend);
                float weapRoll = Mathf.Lerp(settings.walkWeaponRollDegrees, settings.sprintWeaponRollDegrees, sprintBlend);

                float adsCamPosScale = Mathf.Lerp(1f, settings.adsCameraPositionScale, aim);
                float adsCamRotScale = Mathf.Lerp(1f, settings.adsCameraRotationScale, aim);
                float adsWeaponScale = Mathf.Lerp(1f, settings.adsWeaponBobScale, aim);

                targetCamPos = new Vector3(
                    sinH * camH * moveWeight * adsCamPosScale,
                    cos2V * camV * moveWeight * adsCamPosScale,
                    0f);

                float strafeRoll = -Mathf.Clamp(input.moveInput.x, -1f, 1f) * settings.strafeRollDegrees * adsCamRotScale;
                targetCamEuler = new Vector3(
                    sin2V * camPitch * moveWeight * adsCamRotScale,
                    0f,
                    (sinH * camRoll * moveWeight * adsCamRotScale) + strafeRoll);

                // Weapon lags slightly in phase and uses a subtle figure-8 motion.
                targetWeaponPos = new Vector3(
                    -sinH * weapH * moveWeight * adsWeaponScale,
                    Mathf.Abs(Mathf.Cos(phase)) * -weapV * moveWeight * adsWeaponScale,
                    0f);

                targetWeaponEuler = new Vector3(
                    sin2V * weapPitch * moveWeight * adsWeaponScale,
                    sinH * (weapRoll * 0.35f) * moveWeight * adsWeaponScale,
                    -sinH * weapRoll * moveWeight * adsWeaponScale);
            }
            else if (active && !input.isScopedOverlay)
            {
                // Subtle strafe tilt decays smoothly when stopping.
                phase = 0f;
            }

            if (active && landingOffset > 0f)
            {
                float landAdsCamScale = Mathf.Lerp(1f, settings.adsCameraPositionScale, aim);
                float landAdsWeapScale = Mathf.Lerp(1f, settings.adsWeaponBobScale, aim);
                targetCamPos.y -= landingOffset * settings.landingCameraDip * landAdsCamScale;
                targetWeaponPos.y -= landingOffset * settings.landingWeaponDip * landAdsWeapScale;
            }

            float smoothSpeed = isMoving ? settings.blendInSpeed : settings.blendOutSpeed;
            float t = 1f - Mathf.Exp(-Mathf.Max(1f, smoothSpeed) * safeDt);

            currentCamPos = Vector3.Lerp(currentCamPos, targetCamPos, t);
            currentCamEuler = Vector3.Lerp(currentCamEuler, targetCamEuler, t);
            currentWeaponPos = Vector3.Lerp(currentWeaponPos, targetWeaponPos, t);
            currentWeaponEuler = Vector3.Lerp(currentWeaponEuler, targetWeaponEuler, t);

            if (!isMoving && landingOffset <= 0f)
            {
                if (currentCamPos.sqrMagnitude < 1e-10f) currentCamPos = Vector3.zero;
                if (currentCamEuler.sqrMagnitude < 1e-8f) currentCamEuler = Vector3.zero;
                if (currentWeaponPos.sqrMagnitude < 1e-10f) currentWeaponPos = Vector3.zero;
                if (currentWeaponEuler.sqrMagnitude < 1e-8f) currentWeaponEuler = Vector3.zero;
            }

            return Current;
        }
    }
}
