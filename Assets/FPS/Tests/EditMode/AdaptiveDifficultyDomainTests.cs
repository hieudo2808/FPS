using NUnit.Framework;
using System.Collections.Generic;

namespace FPS.Tests
{
    public sealed class AdaptiveDifficultyDomainTests
    {
        [Test]
        public void DynamicDifficulty_UsesWeakestPlayerFloor()
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var samples = new[]
            {
                PlayerPerformanceSample.Create(1, 0.95f, 0.05f, 0f, 0.95f, 200, 40, 0),
                PlayerPerformanceSample.Create(2, 0.20f, 0.85f, 1f, 0.15f, 200, 40, 3),
                PlayerPerformanceSample.Create(3, 0.90f, 0.10f, 0f, 0.90f, 200, 40, 0),
                PlayerPerformanceSample.Create(4, 0.88f, 0.10f, 0f, 0.88f, 200, 40, 0)
            };

            DynamicDifficultyEvaluation result = evaluator.Evaluate(
                DifficultyLevel.Hard,
                samples,
                relaxBoundary: true);

            Assert.That(result.HasEvidence, Is.True);
            Assert.That(result.TeamPerformanceScore, Is.LessThan(0.65f));
            Assert.That(result.Multiplier, Is.LessThan(1f));
        }

        [Test]
        public void DynamicDifficulty_OnlyUpdatesAtRelaxBoundary()
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var samples = new[]
            {
                PlayerPerformanceSample.Create(1, 1f, 0f, 0f, 1f, 200, 40, 0)
            };

            DynamicDifficultyEvaluation combatResult = evaluator.Evaluate(
                DifficultyLevel.Medium,
                samples,
                relaxBoundary: false);
            DynamicDifficultyEvaluation relaxResult = evaluator.Evaluate(
                DifficultyLevel.Medium,
                samples,
                relaxBoundary: true);

            Assert.That(combatResult.Updated, Is.False);
            Assert.That(combatResult.Multiplier, Is.EqualTo(1f));
            Assert.That(relaxResult.Updated, Is.True);
            Assert.That(relaxResult.Multiplier, Is.GreaterThan(1f));
        }

        [Test]
        public void DynamicDifficulty_InsufficientEvidenceKeepsPreviousValue()
        {
            var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var samples = new[]
            {
                PlayerPerformanceSample.Create(1, 1f, 0f, 0f, 1f, 5, 1, 0)
            };

            DynamicDifficultyEvaluation result = evaluator.Evaluate(
                DifficultyLevel.Pandemonium,
                samples,
                relaxBoundary: true);

            Assert.That(result.HasEvidence, Is.False);
            Assert.That(result.Updated, Is.False);
            Assert.That(result.Multiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DynamicDifficulty_ClampsAndLimitsRelaxStep()
        {
            var policy = DynamicDifficultyPolicy.Default;
            var evaluator = new DynamicDifficultyEvaluator(policy);
            var strong = new[]
            {
                PlayerPerformanceSample.Create(1, 1f, 0f, 0f, 1f, 500, 100, 0),
                PlayerPerformanceSample.Create(2, 1f, 0f, 0f, 1f, 500, 100, 0),
                PlayerPerformanceSample.Create(3, 1f, 0f, 0f, 1f, 500, 100, 0),
                PlayerPerformanceSample.Create(4, 1f, 0f, 0f, 1f, 500, 100, 0)
            };

            DynamicDifficultyEvaluation first = evaluator.Evaluate(
                DifficultyLevel.Medium,
                strong,
                relaxBoundary: true);
            DynamicDifficultyEvaluation second = evaluator.Evaluate(
                DifficultyLevel.Medium,
                strong,
                relaxBoundary: true);

            Assert.That(first.Multiplier, Is.InRange(0.6f, 1.5f));
            Assert.That(second.Multiplier, Is.InRange(0.6f, 1.5f));
            Assert.That(second.Multiplier - first.Multiplier, Is.LessThanOrEqualTo(policy.MaxStepPerRelax + 0.0001f));
        }

        [Test]
        public void DynamicDifficulty_TierEnvelopeDoesNotCrossStaticDifficultyBoundary()
        {
            var strong = new[]
            {
                PlayerPerformanceSample.Create(1, 1f, 0f, 0f, 1f, 500, 100, 0)
            };
            var weak = new[]
            {
                PlayerPerformanceSample.Create(1, 0f, 1f, 1f, 0f, 500, 100, 3)
            };

            var easyEvaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            var pandemoniumEvaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
            DynamicDifficultyEvaluation easy = easyEvaluator.Evaluate(DifficultyLevel.Easy, strong, true);
            DynamicDifficultyEvaluation pandemonium = pandemoniumEvaluator.Evaluate(DifficultyLevel.Pandemonium, weak, true);

            Assert.That(easy.Multiplier, Is.LessThanOrEqualTo(1f));
            Assert.That(pandemonium.Multiplier, Is.GreaterThanOrEqualTo(1f));
        }

        [Test]
        public void Director_UsesFivePhaseOrderAndMandatoryRelax()
        {
            var machine = new DirectorStateMachine(DirectorPolicy.Default);
            DirectorStepResult result = default;

            result = Advance(machine, 3f, DirectorPhase.Calm);
            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.BuildUp));

            result = Advance(machine, 15f, DirectorPhase.BuildUp);
            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.Combat));

            result = Advance(machine, 60f, DirectorPhase.Combat, intensity: 100f);
            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.Peak));

            result = Advance(machine, 15f, DirectorPhase.Peak);
            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(result.EnteredRelax, Is.True);
        }

        [Test]
        public void Director_WeakPlayerOrRecentDownedForcesRelax()
        {
            var machine = new DirectorStateMachine(DirectorPolicy.Default);
            Advance(machine, 3f, DirectorPhase.Calm);
            Advance(machine, 15f, DirectorPhase.BuildUp);

            DirectorStepResult result = machine.Advance(0.1f, new DirectorInput(
                weakestHealth01: 0.1f,
                teamSeparation01: 0.8f,
                idleSeconds: 12f,
                recentDownedSeconds: 2f,
                currentAlive: 10,
                intensity: 20f));

            Assert.That(result.Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(result.EnteredRelax, Is.True);
        }

        [Test]
        public void Director_SpecialGateOnlyOpensDuringPeakAfterCooldown()
        {
            var machine = new DirectorStateMachine(DirectorPolicy.Default);
            Assert.That(machine.GetDecision().SpecialGateOpen, Is.False);

            machine.SetStateForTests(DirectorPhase.Peak, 20f);
            Assert.That(machine.GetDecision().SpecialGateOpen, Is.True);
        }

        [Test]
        public void SpawnController_RespectsRelaxAndAliveCap()
        {
            var controller = new SpawnController();
            var director = new DirectorDecision(
                DirectorPhase.Relax,
                spawnRateMultiplier: 0f,
                specialGateOpen: false,
                protectWeakestPlayer: false,
                teamSeparation01: 0f,
                idleSeconds: 0f);

            SpawnDecision decision = controller.Decide(
                director,
                StaticDifficultyProfiles.Get(DifficultyLevel.Medium),
                dynamicMultiplier: 1f,
                currentAlive: 10,
                baseMaxAlive: 10,
                playerCount: 4,
                baseInterval: 2f,
                minimumInterval: 0.5f,
                specialEnabled: true,
                random01: 0f);

            Assert.That(decision.CanSpawn, Is.False);
            Assert.That(decision.MaxAlive, Is.EqualTo(10));
        }

        [Test]
        public void SpawnController_SeparatesPeakGateFromSpecialRoll()
        {
            var controller = new SpawnController();
            var director = new DirectorDecision(
                DirectorPhase.Peak,
                spawnRateMultiplier: 1.5f,
                specialGateOpen: true,
                protectWeakestPlayer: false,
                teamSeparation01: 0f,
                idleSeconds: 0f);

            SpawnDecision decision = controller.Decide(
                director,
                StaticDifficultyProfiles.Get(DifficultyLevel.Pandemonium),
                dynamicMultiplier: 1f,
                currentAlive: 0,
                baseMaxAlive: 10,
                playerCount: 1,
                baseInterval: 2f,
                minimumInterval: 0.5f,
                specialEnabled: true,
                random01: 0.01f);

            Assert.That(decision.CanSpawn, Is.True);
            Assert.That(decision.SpawnSpecial, Is.True);
            Assert.That(decision.SpecialChance, Is.EqualTo(0.35f));
        }

        [Test]
        public void MetricsCollector_BuildsNormalizedSamplesFromAuthoritativeTicks()
        {
            var collector = new AdaptiveDifficultyMetricsCollector();
            var player = new SessionPlayerId(7);
            collector.Record(new ServerTelemetrySnapshot(
                1, player, true, false, 10, 30, 100f, 20f, 0, 4, 2, 40, 20, 10, 1));

            List<PlayerPerformanceSample> samples = collector.BuildSamples();

            Assert.That(samples, Has.Count.EqualTo(1));
            Assert.That(samples[0].StablePlayerId, Is.EqualTo(7ul));
            Assert.That(samples[0].HeadshotRatio, Is.EqualTo(0.5f));
            Assert.That(samples[0].AmmoEfficiency, Is.EqualTo(0.1f));
            Assert.That(samples[0].DamageTakenNorm, Is.EqualTo(0.05f));
            Assert.That(samples[0].DownedCountNorm, Is.EqualTo(1f / 3f).Within(0.0001f));
        }

        private static DirectorStepResult Advance(
            DirectorStateMachine machine,
            float seconds,
            DirectorPhase expectedPhase,
            float intensity = 0f)
        {
            machine.SetStateForTests(expectedPhase, 0f);
            return machine.Advance(seconds, new DirectorInput(
                weakestHealth01: 1f,
                teamSeparation01: 0f,
                idleSeconds: 0f,
                recentDownedSeconds: 999f,
                currentAlive: 10,
                intensity: intensity));
        }
    }
}
