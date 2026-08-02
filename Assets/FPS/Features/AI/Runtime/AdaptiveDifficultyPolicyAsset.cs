using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(menuName = "FPS/AI/Adaptive Difficulty Policy")]
    public sealed class AdaptiveDifficultyPolicyAsset : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float headshotWeight = 0.20f;
        [SerializeField, Range(0f, 1f)] private float damageTakenWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float downedWeight = 0.30f;
        [SerializeField, Range(0f, 1f)] private float ammoEfficiencyWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float neutralScore = 0.5f;
        [SerializeField] private float gain = 0.30f;
        [SerializeField] private float globalMinMultiplier = 0.6f;
        [SerializeField] private float globalMaxMultiplier = 1.5f;
        [SerializeField] private float maxStepPerRelax = 0.10f;
        [SerializeField] private int minimumShots = 20;
        [SerializeField] private int minimumKills = 8;
        [SerializeField, Range(0f, 1f)] private float weakestPlayerWeight = 0.40f;
        [SerializeField, Range(0f, 1f)] private float medianPlayerWeight = 0.60f;
        [SerializeField] private float weakestPlayerCeilingOffset = 0.25f;

        public DynamicDifficultyPolicy ToPolicy()
        {
            return new DynamicDifficultyPolicy(
                headshotWeight,
                damageTakenWeight,
                downedWeight,
                ammoEfficiencyWeight,
                neutralScore,
                gain,
                globalMinMultiplier,
                globalMaxMultiplier,
                maxStepPerRelax,
                minimumShots,
                minimumKills,
                weakestPlayerWeight,
                medianPlayerWeight,
                weakestPlayerCeilingOffset);
        }
    }
}
