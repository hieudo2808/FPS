using UnityEngine;

namespace FPS
{
    // Unity needs a matching script filename to persist the MonoScript in player builds.
    [CreateAssetMenu(menuName = "FPS/AI/Enemy Scaling Profile", fileName = "EnemyScalingProfile")]
    public sealed class EnemyScalingProfile : ScriptableObject
    {
        [Header("Shared team curves (1-4 players)")]
        [SerializeField] private PlayerCountCurve commonHealthMultiplier = new(1f, 1.35f, 1.70f, 2.05f);
        [SerializeField] private PlayerCountCurve damageAndStatusMultiplier = new(1f, 1.08f, 1.15f, 1.20f);
        [SerializeField] private PlayerCountCurve controlResistanceMultiplier = new(1f, 1.15f, 1.30f, 1.45f);
        [SerializeField] private PlayerCountCurve abilityCooldownMultiplier = new(1f, 0.95f, 0.90f, 0.85f);
        [SerializeField] private PlayerCountCurve directorSpawnPressureMultiplier = new(1f, 1.20f, 1.40f, 1.60f);

        [Header("Special max health (before difficulty)")]
        [SerializeField] private PlayerCountCurve screamerHealth = new(150f, 200f, 250f, 300f);
        [SerializeField] private PlayerCountCurve infectorHealth = new(500f, 750f, 975f, 1200f);
        [SerializeField] private PlayerCountCurve tankHealth = new(2500f, 5000f, 7500f, 10000f);

        public float GetCommonHealthMultiplier(int playerCount) => commonHealthMultiplier.Evaluate(playerCount);
        public float GetDamageAndStatusMultiplier(int playerCount) => damageAndStatusMultiplier.Evaluate(playerCount);
        public float GetControlResistanceMultiplier(int playerCount) => controlResistanceMultiplier.Evaluate(playerCount);
        public float GetAbilityCooldownMultiplier(int playerCount) => abilityCooldownMultiplier.Evaluate(playerCount);
        public float GetDirectorSpawnPressureMultiplier(int playerCount) => directorSpawnPressureMultiplier.Evaluate(playerCount);

        public float GetSpecialHealth(SpecialType type, int playerCount, float fallbackAuthoredHealth)
        {
            return type switch
            {
                SpecialType.Screamer => screamerHealth.Evaluate(playerCount),
                SpecialType.Infector => infectorHealth.Evaluate(playerCount),
                SpecialType.Tank => tankHealth.Evaluate(playerCount),
                _ => Mathf.Max(1f, fallbackAuthoredHealth * commonHealthMultiplier.Evaluate(playerCount))
            };
        }
    }
}
