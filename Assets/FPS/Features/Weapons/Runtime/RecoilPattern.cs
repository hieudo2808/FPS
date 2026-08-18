using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(fileName = "New Recoil Pattern", menuName = "FPS/Recoil Pattern")]
    public class RecoilPattern : ScriptableObject
    {
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
