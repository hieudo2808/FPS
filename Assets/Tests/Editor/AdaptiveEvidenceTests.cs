using NUnit.Framework;

namespace FPS.Tests
{
    public sealed class AdaptiveEvidenceTests
    {
        static PlayerPerformanceSample Strong(ulong id) => PlayerPerformanceSample.Create(id, 1, 0, 0, 1, 20, 8, 0);
        static PlayerPerformanceSample Down(ulong id) => PlayerPerformanceSample.Create(id, 0, .3f, 1f/3f, 0, 0, 0, 1);

        [Test]
        public void DownWithoutKills_IsEvidenceToDecreaseDifficulty()
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var result = evaluator.Evaluate(DifficultyLevel.Medium, new[] { Down(1) }, true);
            Assert.That(result.Multiplier, Is.EqualTo(.95f).Within(.00001f), "A struggling player with no kills must not be invisible to DDA.");
        }

        [Test]
        public void OneWeakThreeStrong_CannotIncreaseDifficulty()
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var result = evaluator.Evaluate(DifficultyLevel.Medium,
                new[] { Down(1), Strong(2), Strong(3), Strong(4) }, true);
            Assert.That(result.Multiplier, Is.EqualTo(.95f).Within(.00001f));
        }

        [TestCase(19, 8)]
        [TestCase(20, 7)]
        [TestCase(0, 0)]
        [TestCase(20, 1)]
        public void InsufficientPerformanceWithoutDistress_Holds(int shots, int kills)
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var sample = PlayerPerformanceSample.Create(1, 1, 0, 0, 1, shots, kills, 0);
            Assert.That(evaluator.Evaluate(DifficultyLevel.Medium, new[] { sample }, true).Multiplier, Is.EqualTo(1));
        }

        [Test]
        public void OffBoundary_NeverCommitsEvenDistress()
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            Assert.That(evaluator.Evaluate(DifficultyLevel.Medium, new[] { Down(1) }, false).Multiplier, Is.EqualTo(1));
        }

        [TestCase(4.9f, .30f, 1f)]
        [TestCase(5f, .30f, .95f)]
        [TestCase(5f, .299f, 1f)]
        public void CombatExposureAndNormalizedDamageAreBothRequired(float seconds, float damage, float expected)
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var sample = PlayerPerformanceSample.Create(1, 0, damage, 0, 0, 0, 0, 0, seconds, damage);
            Assert.That(evaluator.Evaluate(DifficultyLevel.Medium, new[] { sample }, true).Multiplier,
                Is.EqualTo(expected).Within(.00001));
        }

        [TestCase(DifficultyLevel.Easy, .6f)]
        [TestCase(DifficultyLevel.Medium, .85f)]
        [TestCase(DifficultyLevel.Hard, .9f)]
        [TestCase(DifficultyLevel.Pandemonium, 1f)]
        public void RepeatedDistressRespectsTierFloor(DifficultyLevel tier, float floor)
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            for (int i = 0; i < 30; i++) evaluator.Evaluate(tier, new[] { Down(1) }, true);
            Assert.That(evaluator.CurrentMultiplier, Is.EqualTo(floor).Within(.00001));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidEvidenceCannotPoisonMultiplier(float invalid)
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var sample = PlayerPerformanceSample.Create(1, invalid, 0, 0, 1, 20, 8, 0);
            Assert.That(evaluator.Evaluate(DifficultyLevel.Medium, new[] { sample }, true).Multiplier, Is.EqualTo(1));
        }
    }
}
