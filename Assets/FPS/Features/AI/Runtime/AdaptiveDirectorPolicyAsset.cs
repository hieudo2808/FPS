using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(menuName = "FPS/AI/Director Policy")]
    public sealed class AdaptiveDirectorPolicyAsset : ScriptableObject
    {
        [SerializeField] private float calmDurationSeconds = 3f;
        [SerializeField] private float buildUpDurationSeconds = 15f;
        [SerializeField] private float combatDurationSeconds = 60f;
        [SerializeField] private float peakDurationSeconds = 15f;
        [SerializeField] private float relaxDurationSeconds = 20f;
        [SerializeField] private float peakIntensityThreshold = 80f;
        [SerializeField, Range(0f, 1f)] private float weakestHealthFloor01 = 0.2f;
        [SerializeField] private float recentDownedGraceSeconds = 20f;
        [SerializeField] private float specialCooldownSeconds = 15f;
        [SerializeField] private float idleBuildUpDelay = 0.25f;
        [SerializeField] private float separationBuildUpDelay = 0.25f;

        public DirectorPolicy ToPolicy()
        {
            return new DirectorPolicy(
                calmDurationSeconds,
                buildUpDurationSeconds,
                combatDurationSeconds,
                peakDurationSeconds,
                relaxDurationSeconds,
                peakIntensityThreshold,
                weakestHealthFloor01,
                recentDownedGraceSeconds,
                specialCooldownSeconds,
                idleBuildUpDelay,
                separationBuildUpDelay);
        }
    }
}
