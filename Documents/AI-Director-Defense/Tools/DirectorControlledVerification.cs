// Install temporarily in Assets/Editor after the Unity save gate; source belongs outside Assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FPS.Diagnostics
{
    public static class DirectorControlledVerification
    {
        [Serializable] public sealed class Check
        {
            public string name;
            public string status;
            public string evidence;
        }

        [Serializable] public sealed class Report
        {
            public string capturedUtc;
            public string scope = "Isolated real C# domain execution plus read-only live observations. No gameplay session or player telemetry is modified. These are NOT end-to-end gameplay or user-experience tests.";
            public bool editorPlaying;
            public string scene;
            public bool sceneDirty;
            public bool directorFound;
            public bool adaptiveEnabled;
            public bool observeOnly;
            public string phase;
            public string adaptivePhase;
            public float multiplier;
            public int telemetryPlayers;
            public int passed;
            public int failed;
            public int discrepancies;
            public List<Check> checks = new List<Check>();
        }

        [MenuItem("Tools/AI Director/Run Isolated Checks")]
        public static void RunIsolatedChecks()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var report = new Report
            {
                capturedUtc = DateTime.UtcNow.ToString("o"),
                editorPlaying = EditorApplication.isPlaying,
                scene = scene.path,
                sceneDirty = scene.isDirty
            };
            Run(report, "Five phase order and durations", () =>
            {
                var machine = new DirectorStateMachine(DirectorPolicy.Default);
                Require(machine.Phase == DirectorPhase.Calm, "Starts Calm");
                float[] durations = { 3f, 15f, 60f, 15f, 20f };
                DirectorPhase[] phases = { DirectorPhase.BuildUp, DirectorPhase.Combat, DirectorPhase.Peak, DirectorPhase.Relax, DirectorPhase.Calm };
                for (int i = 0; i < phases.Length; i++)
                    Require(machine.Advance(durations[i], Healthy()).Phase == phases[i], "Unexpected phase at step " + i);
                return "Calm(3s) -> BuildUp(15s) -> Combat(60s) -> Peak(15s) -> Relax(20s) -> Calm.";
            });
            Run(report, "Weak HP and recent down trigger Relax", () =>
            {
                var machine = Started();
                var weak = machine.Advance(0f, new DirectorInput(0.2f, 0f, 0f, 999f, 5, 10f));
                Require(weak.EnteredRelax && weak.Phase == DirectorPhase.Relax, "20% HP must protect");
                Require(Started().Advance(0f, new DirectorInput(0.21f, 0f, 0f, 999f, 5, 10f)).Phase == DirectorPhase.BuildUp, "21% HP must not protect by HP alone");
                Require(Started().Advance(0f, new DirectorInput(1f, 0f, 0f, 20f, 5, 10f)).EnteredRelax, "20s grace boundary must protect");
                Require(!Started().Advance(0f, new DirectorInput(1f, 0f, 0f, 20.01f, 5, 10f)).EnteredRelax, "Expired grace must not protect");
                return "HP boundary 0.20/0.21 and down grace boundary 20/20.01 seconds verified in domain.";
            });
            Run(report, "Relax boundary is required for DDA", () =>
            {
                var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
                var result = evaluator.Evaluate(DifficultyLevel.Medium, Samples(1f), false);
                Require(!result.Updated && Near(result.Multiplier, 1f), "DDA changed outside Relax");
                return "A high performance fixture outside Relax leaves multiplier 1.0.";
            });
            Run(report, "Both evidence thresholds are required", () =>
            {
                var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
                Require(!evaluator.Evaluate(DifficultyLevel.Medium, new[] { Sample(1, 1f, 19, 8) }, true).HasEvidence, "19 shots incorrectly accepted");
                Require(!evaluator.Evaluate(DifficultyLevel.Medium, new[] { Sample(1, 1f, 20, 7) }, true).HasEvidence, "7 kills incorrectly accepted");
                Require(!evaluator.Evaluate(DifficultyLevel.Medium, null, true).HasEvidence, "Null samples incorrectly accepted");
                Require(evaluator.Evaluate(DifficultyLevel.Medium, new[] { Sample(1, 1f, 20, 8) }, true).HasEvidence, "Exact thresholds rejected");
                return "19 shots/8 kills: hold; 20 shots/7 kills: hold; no samples: hold; 20 shots/8 kills: eligible.";
            });
            Run(report, "Weakest eligible player limits team score", () =>
            {
                var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
                var result = evaluator.Evaluate(DifficultyLevel.Medium, Samples(0.05f, 0.95f, 0.95f, 0.95f), true);
                Require(Near(result.TeamPerformanceScore, 0.30f), "Expected weakest+0.25 ceiling");
                Require(Near(result.TargetMultiplier, 0.94f), "Expected target 0.94");
                return "Synthetic eligible scores [0.05,0.95,0.95,0.95] -> team=0.30, target=0.94.";
            });
            Run(report, "Tier limits, step limits and actual reachable default range", () =>
            {
                DifficultyLevel[] tiers = { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard, DifficultyLevel.Pandemonium };
                float[] lows = { 0.85f, 0.85f, 0.90f, 1f };
                float[] highs = { 1f, 1.15f, 1.15f, 1.15f };
                for (int t = 0; t < tiers.Length; t++)
                {
                    var evaluator = new DynamicDifficultyEvaluator(DynamicDifficultyPolicy.Default);
                    for (int direction = 0; direction < 2; direction++)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            float before = evaluator.CurrentMultiplier;
                            var result = evaluator.Evaluate(tiers[t], Samples(direction), true);
                            Require(Math.Abs(result.Multiplier - before) <= 0.10001f, "Step exceeded 0.10");
                            Require(result.Multiplier >= lows[t] - 0.00001f && result.Multiplier <= highs[t] + 0.00001f, "Default reachable range exceeded");
                        }
                        Require(Near(evaluator.CurrentMultiplier, direction == 0 ? lows[t] : highs[t]), "Unexpected steady target");
                    }
                }
                return "From 1.0 with synthetic score endpoints: Easy 0.85-1; Medium 0.85-1.15; Hard 0.90-1.15; Pandemonium 1-1.15. Every update <=0.10.";
            });
            Run(report, "Spawn pressure outputs and Relax gate", () =>
            {
                var controller = new SpawnController();
                var stats = StaticDifficultyProfiles.Get(DifficultyLevel.Medium);
                var combat = new DirectorDecision(DirectorPhase.Combat, 1f, false, false, 0f, 0f);
                var normal = controller.Decide(combat, stats, 1f, 0, 30, 1, 2f, 0.5f, true, 0f);
                var harder = controller.Decide(combat, stats, 1.1f, 0, 30, 1, 2f, 0.5f, true, 0f);
                Require(Near(normal.IntervalSeconds, 2f) && Near(harder.IntervalSeconds, 2f / 1.1f), "Unexpected interval");
                Require(normal.MaxAlive == 30 && harder.MaxAlive == 33, "Unexpected population cap");
                Require(Near(harder.SpecialChance, 0.165f), "Unexpected chance");
                var relax = new DirectorDecision(DirectorPhase.Relax, 0f, false, false, 0f, 0f);
                Require(!controller.Decide(relax, stats, 1f, 0, 30, 1, 2f, 0.5f, true, 0f).CanSpawn, "Relax permits new spawn");
                Require(!controller.Decide(combat, stats, 1f, 30, 30, 1, 2f, 0.5f, true, 0f).CanSpawn, "Population cap permits spawn");
                return "DDA 1->1.1: interval 2->1.81818s, cap 30->33, chance .15->.165. Relax/cap block new spawn. Factory/gameplay NOT exercised.";
            });

            InspectSpecialGate(report, DirectorPolicy.Default, "Default special gate over 1000 quarter-second steps");
            Run(report, "Read current runtime settings without mutation", () =>
            {
                AIDirector director = UnityEngine.Object.FindFirstObjectByType<AIDirector>();
                report.directorFound = director != null;
                if (director == null)
                    return "No active AIDirector found. No conclusion about gameplay integration.";
                report.adaptiveEnabled = director.AdaptiveDirectorEnabled;
                report.observeOnly = director.AdaptiveObserveOnly;
                report.phase = director.CurrentPhase.ToString();
                report.adaptivePhase = director.AdaptivePhase.ToString();
                report.multiplier = director.AdaptiveDifficultyMultiplier;
                report.telemetryPlayers = NetworkGameManager.Instance?.AdaptiveMetrics?.PlayerCount ?? 0;
                var policy = ReadField<AdaptiveDirectorPolicyAsset>(director, "adaptiveDirectorPolicy");
                var difficultyPolicy = ReadField<AdaptiveDifficultyPolicyAsset>(director, "adaptiveDifficultyPolicy");
                if (policy != null)
                    InspectSpecialGate(report, policy.ToPolicy(), "Configured policy special gate over 1000 quarter-second steps");
                if (difficultyPolicy != null)
                {
                    var configured = new DynamicDifficultyEvaluator(difficultyPolicy.ToPolicy());
                    var low = configured.Evaluate(DifficultyLevel.Medium, Samples(0f), true);
                    var high = new DynamicDifficultyEvaluator(difficultyPolicy.ToPolicy()).Evaluate(DifficultyLevel.Medium, Samples(1f), true);
                    report.checks.Add(new Check { name = "Configured DDA policy endpoint observations", status = "observation", evidence = "Isolated configured-policy Medium: score0 target=" + low.TargetMultiplier + "; score1 target=" + high.TargetMultiplier + ". HasEvidence=" + low.HasEvidence + "/" + high.HasEvidence + ". Synthetic fixtures, not current players." });
                }
                return "Adaptive=" + report.adaptiveEnabled + "; observeOnly=" + report.observeOnly + "; phase=" + report.phase + "; adaptivePhase=" + report.adaptivePhase + "; multiplier=" + report.multiplier + "; telemetryPlayers=" + report.telemetryPlayers + "; directorPolicy=" + (policy != null ? policy.name : "code defaults") + "; difficultyPolicy=" + (difficultyPolicy != null ? difficultyPolicy.name : "code defaults");
            });

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Documents/AI-Director-Defense/Captures"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "isolated-checks-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("[AI Director verification] " + report.passed + " passed, " + report.failed + " failed, " + report.discrepancies + " discrepancies. Scope: isolated algorithm execution, not gameplay proof. Report: " + path);
        }

        private static void InspectSpecialGate(Report report, DirectorPolicy policy, string name)
        {
            var machine = new DirectorStateMachine(policy);
            int open = 0;
            int runtimeCompatible = 0;
            for (int i = 0; i < 1000; i++)
            {
                DirectorStepResult result = machine.Advance(0.25f, Healthy());
                if (!result.Decision.SpecialGateOpen) continue;
                open++;
                if (result.Phase == DirectorPhase.Peak) runtimeCompatible++;
            }
            bool discrepancy = runtimeCompatible == 0;
            if (discrepancy) report.discrepancies++;
            report.checks.Add(new Check
            {
                name = name,
                status = discrepancy ? "discrepancy" : "observation",
                evidence = "250 simulated seconds, healthy team, no idle/separation: gate-open steps=" + open + "; Peak-compatible steps=" + runtimeCompatible + ". Current runtime TrySpawnSpecial requires PEAK. This diagnoses eligibility only; no actual spawn attempted."
            });
        }

        private static void Run(Report report, string name, Func<string> action)
        {
            try { string evidence = action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass", evidence = evidence }); }
            catch (Exception exception) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", evidence = exception.ToString() }); }
        }

        private static T ReadField<T>(object instance, string name) where T : class =>
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool Near(float actual, float expected) => Math.Abs(actual - expected) < 0.00001f;
        private static DirectorInput Healthy() => new DirectorInput(1f, 0f, 0f, 999f, 0, 0f);
        private static DirectorStateMachine Started() { var machine = new DirectorStateMachine(DirectorPolicy.Default); machine.Advance(3f, Healthy()); return machine; }
        // Synthetic normalized features deliberately exercise score boundaries independently of telemetry collection.
        private static PlayerPerformanceSample Sample(ulong id, float score, int shots = 20, int kills = 8) =>
            PlayerPerformanceSample.Create(id, score, 1f - score, 1f - score, score, shots, kills, 0);
        private static PlayerPerformanceSample[] Samples(params float[] scores)
        {
            var samples = new PlayerPerformanceSample[scores.Length];
            for (int i = 0; i < scores.Length; i++) samples[i] = Sample((ulong)(i + 1), scores[i]);
            return samples;
        }
    }
}
