using UnityEngine;
using UnityEngine.Serialization;

namespace FPS
{
    [CreateAssetMenu(fileName = "New Recoil Pattern", menuName = "FPS/Recoil Pattern")]
    public class RecoilPattern : ScriptableObject
    {
        [Header("Curve stability (BR-Port)")]
        public bool useStabilityCurves;
        public RecoilPort.UnrealRecoilCurve.Key[] pitchEnvelope, yawEnvelope, firingError, recoveryTime;
        public RecoilPort.UnrealRecoilCurve.Key[] walkingError, runningError, jumpingError;
        public Vector2 sprintRecoilMultiplier = new Vector2(1.5f, 1.5f);
        public Vector2 adsRecoilMultiplier = new Vector2(.7f, .7f);
        public RecoilPort.UnrealRecoilCurve.Key[] aimedFiringError;
        public RecoilPort.UnrealRecoilCurve.Key[] cameraPopPitchCurve, cameraPopYawCurve;
        public Vector2 cameraPopScale = Vector2.one;
        [Tooltip("Camera fraction of new gameplay recoil. Does not change canonical bullet aim.")]
        public Vector2 cameraRecoilFollow = Vector2.one;
        public Vector2 adsCameraRecoilFollow = Vector2.one;
        [Tooltip("Optional hip pitch follow by shot stability index; empty keeps the constant follow.")]
        public RecoilPort.UnrealRecoilCurve.Key[] hipPitchCameraFollow;
        [Min(0)] public int protectedShots = 3;
        [FormerlySerializedAs("yawSwitchInterval"), Min(0.001f)] public float yawSwitchDuration = 0.6f;
        [Range(0f, 1f)] public float yawSwitchChance = 0.07f;
        [Min(1f)] public float tapEfficiency = 1f;
        [Min(0f)] public float adsErrorMultiplier = 1.15f;
        public float adsErrorAddend = -0.13f;
        [Min(0f), Tooltip("Cosmetic camera pulse in degrees, excluded from bullet aim.")]
        public float cameraPop = 0.15f;

        public float MaximumStabilityIndex => Mathf.Max(1f, Mathf.Max(
            Mathf.Max(LastTime(pitchEnvelope), LastTime(yawEnvelope)),
            Mathf.Max(LastTime(firingError), Mathf.Max(LastTime(recoveryTime), LastTime(aimedFiringError)))));

        private static float LastTime(RecoilPort.UnrealRecoilCurve.Key[] curve)
            => curve == null || curve.Length == 0 ? 0f : curve[curve.Length - 1].time;

        public bool HasCameraPopCurves => (cameraPopPitchCurve != null && cameraPopPitchCurve.Length > 0)
            || (cameraPopYawCurve != null && cameraPopYawCurve.Length > 0);
        public float CameraPopDuration => Mathf.Max(LastTime(cameraPopPitchCurve), LastTime(cameraPopYawCurve));

        public int PopCapacity(float minFireInterval)
        {
            if (float.IsNaN(minFireInterval) || float.IsInfinity(minFireInterval) || minFireInterval <= 0f)
                throw new System.ArgumentException("Pop fire interval must be finite and positive.");
            if (!HasCameraPopCurves) return 0;
            double capacity = System.Math.Ceiling((double)CameraPopDuration / minFireInterval) + 1;
            if (capacity < 1 || capacity > 64) throw new System.ArgumentException("Pop needs more than 64 live events.");
            return (int)capacity;
        }

        public float Evaluate(RecoilPort.UnrealRecoilCurve.Key[] curve, float x)
            => RecoilPort.UnrealRecoilCurve.Evaluate(curve, x);

        public float GetSpread(float stabilityIndex, WeaponStabilityContext context)
        {
            var movement = !context.grounded ? jumpingError : context.sprinting ? runningError : walkingError;
            bool aimedOverride = context.aimed && aimedFiringError != null && aimedFiringError.Length > 0;
            float error = Mathf.Max(Evaluate(aimedOverride ? aimedFiringError : firingError, stabilityIndex),
                Evaluate(movement, Mathf.Clamp01(context.normalizedSpeed)));
            return Mathf.Clamp(context.aimed && !aimedOverride ? error * adsErrorMultiplier + adsErrorAddend : error, 0f, 45f);
        }

        public void ValidateCurves()
        {
            var scalars = new[] { yawSwitchDuration, yawSwitchChance, tapEfficiency, adsErrorMultiplier,
                adsErrorAddend, cameraPop, snappiness, returnSpeed, resetCooldown, pitchJitter,
                yawJitter, sustainedPitch, sustainedYaw, sprintRecoilMultiplier.x, sprintRecoilMultiplier.y,
                adsRecoilMultiplier.x, adsRecoilMultiplier.y, cameraPopScale.x, cameraPopScale.y,
                cameraRecoilFollow.x, cameraRecoilFollow.y, adsCameraRecoilFollow.x, adsCameraRecoilFollow.y };
            foreach (float value in scalars)
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new System.ArgumentException("Recoil scalars must be finite.");
            if (tapEfficiency < 1f || yawSwitchDuration <= 0f || protectedShots < 0
                || yawSwitchChance < 0f || yawSwitchChance > 1f || adsErrorMultiplier < 0f
                || sprintRecoilMultiplier.x < 0f || sprintRecoilMultiplier.y < 0f
                || adsRecoilMultiplier.x < 0f || adsRecoilMultiplier.y < 0f
                || cameraPopScale.x < 0f || cameraPopScale.y < 0f || cameraPop < 0f)
                throw new System.ArgumentException("Invalid recoil efficiency or yaw configuration.");
            if (cameraRecoilFollow.x < 0f || cameraRecoilFollow.x > 1f
                || cameraRecoilFollow.y < 0f || cameraRecoilFollow.y > 1f
                || adsCameraRecoilFollow.x < 0f || adsCameraRecoilFollow.x > 1f
                || adsCameraRecoilFollow.y < 0f || adsCameraRecoilFollow.y > 1f)
                throw new System.ArgumentException("Camera recoil follow must be in [0, 1].");
            var curves = new[] { pitchEnvelope, yawEnvelope, firingError, recoveryTime,
                walkingError, runningError, jumpingError };
            var names = new[] { nameof(pitchEnvelope), nameof(yawEnvelope), nameof(firingError), nameof(recoveryTime),
                nameof(walkingError), nameof(runningError), nameof(jumpingError) };
            for (int i = 0; i < curves.Length; i++)
            {
                try { RecoilPort.UnrealRecoilCurve.Validate(curves[i]); }
                catch (System.ArgumentException error)
                { throw new System.ArgumentException("Invalid recoil curve: " + names[i], error); }
            }
            if (aimedFiringError != null && aimedFiringError.Length > 0)
                RecoilPort.UnrealRecoilCurve.Validate(aimedFiringError);
            if (hipPitchCameraFollow != null && hipPitchCameraFollow.Length > 0)
            {
                RecoilPort.UnrealRecoilCurve.Validate(hipPitchCameraFollow);
                foreach (float time in RecoilPort.UnrealRecoilCurve.CriticalTimes(hipPitchCameraFollow))
                {
                    float value = Evaluate(hipPitchCameraFollow, time);
                    if (value < 0f || value > 1f || float.IsNaN(value) || float.IsInfinity(value))
                        throw new System.ArgumentException("Hip pitch camera follow must stay in [0, 1].");
                }
            }
            ValidatePop(cameraPopPitchCurve);
            ValidatePop(cameraPopYawCurve);
            foreach (float time in RecoilPort.UnrealRecoilCurve.CriticalTimes(recoveryTime))
            {
                float value = Evaluate(recoveryTime, time);
                if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                    throw new System.ArgumentException("Recovery must be finite and nonnegative over its entire curve.");
            }
        }

        private static void ValidatePop(RecoilPort.UnrealRecoilCurve.Key[] curve)
        {
            if (curve == null || curve.Length == 0) return;
            RecoilPort.UnrealRecoilCurve.Validate(curve);
            if (curve[0].time < 0f || LastTime(curve) <= 0f || curve[curve.Length - 1].value != 0f)
                throw new System.ArgumentException("Pop must have a positive lifetime and end at zero.");
        }

        private void OnEnable()
        {
            if (useStabilityCurves) ValidateCurves();
        }

        [Tooltip("Each element represents the kick for a consecutive shot. X is upward pitch, Y is horizontal yaw.")]
        public Vector2[] shots;

        [Tooltip("How fast the gun snaps to the new recoil angle (higher = faster kick).")]
        public float snappiness = 20f;

        [Tooltip("How fast the camera returns to the original position when stopped firing.")]
        public float returnSpeed = 5f;
        
        [Tooltip("Time without firing before the recoil pattern completely resets to shot index 0.")]
        public float resetCooldown = 0.25f;

        [Header("Controlled Variation")]
        [Range(0f, 0.5f)]
        [Tooltip("Deterministic per-shot variation applied as a fraction of vertical kick.")]
        public float pitchJitter = 0.12f;

        [Min(0f)]
        [Tooltip("Maximum deterministic horizontal variation, in degrees, added to every shot.")]
        public float yawJitter = 0.25f;

        [Min(0f)]
        [Tooltip("Vertical kick per shot after the authored pattern ends.")]
        public float sustainedPitch = 0.6f;

        [Min(0f)]
        [Tooltip("Horizontal kick per shot after the authored pattern ends.")]
        public float sustainedYaw = 1.5f;

        [Range(1, 8)]
        [Tooltip("How many sustained-fire shots retain one horizontal direction before another deterministic direction is selected.")]
        public int sustainedDirectionHoldShots = 3;

        [Tooltip("Stable seed for controlled variation. Identical pattern and shot index always produce the same recoil.")]
        public int variationSeed = 1337;

        public Vector2 GetShot(int shotIndex)
        {
            return GetShot(shotIndex, 0u);
        }

        public Vector2 GetShot(int shotIndex, uint spraySequence)
        {
            int safeShotIndex = Mathf.Max(0, shotIndex);
            int authoredCount = shots != null ? shots.Length : 0;
            Vector2 baseKick;

            if (safeShotIndex < authoredCount)
            {
                baseKick = shots[safeShotIndex];
            }
            else
            {
                int overflowIndex = safeShotIndex - authoredCount;
                int directionSegment = overflowIndex / Mathf.Max(1, sustainedDirectionHoldShots);
                float side = SignedNoise(directionSegment + 0x45D9, spraySequence) < 0f ? -1f : 1f;
                baseKick = new Vector2(
                    Mathf.Max(0f, sustainedPitch),
                    Mathf.Max(0f, sustainedYaw) * side);
            }

            float pitchScale = 1f + SignedNoise(safeShotIndex * 2, spraySequence) * Mathf.Clamp01(pitchJitter);
            float horizontalVariation = SignedNoise(safeShotIndex * 2 + 1, spraySequence) * Mathf.Max(0f, yawJitter);
            return new Vector2(
                Mathf.Max(0f, baseKick.x) * Mathf.Max(0f, pitchScale),
                baseKick.y + horizontalVariation);
        }

        private float SignedNoise(int sample, uint spraySequence)
        {
            uint value = unchecked((uint)variationSeed)
                ^ unchecked((uint)sample * 0x9E3779B9u)
                ^ RotateLeft(spraySequence * 0x85EBCA6Bu, 13);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return ((value & 0x00FFFFFFu) / 8388607.5f) - 1f;
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        private void OnValidate()
        {
            snappiness = Mathf.Max(0f, snappiness);
            returnSpeed = Mathf.Max(0f, returnSpeed);
            resetCooldown = Mathf.Max(0f, resetCooldown);
            pitchJitter = Mathf.Clamp(pitchJitter, 0f, 0.5f);
            yawJitter = Mathf.Max(0f, yawJitter);
            sustainedPitch = Mathf.Max(0f, sustainedPitch);
            sustainedYaw = Mathf.Max(0f, sustainedYaw);
            sustainedDirectionHoldShots = Mathf.Clamp(sustainedDirectionHoldShots, 1, 8);
        }
    }
}
