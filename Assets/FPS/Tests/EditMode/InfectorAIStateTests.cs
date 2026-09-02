using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public class InfectorAIStateTests
    {
        private GameObject infectorGo;
        private SI_Infector infector;
        private GameObject playerPrefab;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Enemies/Infector/Prefabs/Infector.prefab");
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab");
            Assert.NotNull(prefab, "Authored Infector prefab is required by AI tests.");
            Assert.NotNull(playerPrefab, "Authored player prefab is required by AI tests.");
            infectorGo = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            infector = infectorGo.GetComponent<SI_Infector>();
            Assert.NotNull(infector);
        }

        [TearDown]
        public void TearDown()
        {
            if (infectorGo != null)
                Object.DestroyImmediate(infectorGo);
        }

        [Test]
        public void Infector_DefaultState_IsSearch()
        {
            Assert.AreEqual(InfectorState.Search, infector.CurrentState);
        }

        [Test]
        public void Infector_SpecialType_IsInfector()
        {
            Assert.AreEqual(SpecialType.Infector, infector.Type);
        }

        [Test]
        public void Infector_ImplantParameters_AreBalanced()
        {
            Assert.AreEqual(15f, infector.ImplantDamage);
            Assert.AreEqual(30f, infector.ImplantInfectionAmount);
            Assert.AreEqual(2.2f, infector.ImplantRange);
            Assert.AreEqual(0.5f, infector.ImplantWindup);
            Assert.AreEqual(12f, infector.ImplantCooldown);
            Assert.AreEqual(5f, infector.RetreatDuration);
            Assert.AreEqual(200f, infector.FixedMaxHealth);
        }

        [Test]
        public void Infector_TargetScoring_PrioritizesIsolatedPlayer()
        {
            var isolatedPlayer = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            var groupedPlayer = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            isolatedPlayer.name = "IsolatedPlayer";
            groupedPlayer.name = "GroupedPlayer";

            try
            {
                var isolatedProfile = new PlayerProfile
                {
                    playerIndex = 0,
                    playerTransform = isolatedPlayer.transform,
                    isIsolated = true,
                    currentHealth = 100f,
                    distanceToNearestAlly = 15f
                };

                var groupedProfile = new PlayerProfile
                {
                    playerIndex = 1,
                    playerTransform = groupedPlayer.transform,
                    isIsolated = false,
                    currentHealth = 100f,
                    distanceToNearestAlly = 2f
                };

                float isolatedScore = infector.CalculateTargetScore(isolatedProfile);
                float groupedScore = infector.CalculateTargetScore(groupedProfile);

                Assert.Greater(isolatedScore, groupedScore, "Infector must prioritize isolated players (+2.0 isolation bonus, avoids grouped players).");
            }
            finally
            {
                Object.DestroyImmediate(isolatedPlayer);
                Object.DestroyImmediate(groupedPlayer);
            }
        }

        [Test]
        public void Infector_TargetScoring_PenalizesAlreadyInfectedPlayers()
        {
            var healthyPlayer = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            healthyPlayer.name = "HealthyPlayer";
            var healthyInfection = healthyPlayer.GetComponent<PlayerInfectionController>();
            healthyInfection.SetInfectionServer(0f);

            var infectedPlayer = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            infectedPlayer.name = "InfectedPlayer";
            var sickInfection = infectedPlayer.GetComponent<PlayerInfectionController>();
            sickInfection.SetInfectionServer(80f);

            try
            {
                var healthyProfile = new PlayerProfile
                {
                    playerIndex = 0,
                    playerTransform = healthyPlayer.transform,
                    isIsolated = false,
                    currentHealth = 100f
                };

                var infectedProfile = new PlayerProfile
                {
                    playerIndex = 1,
                    playerTransform = infectedPlayer.transform,
                    isIsolated = false,
                    currentHealth = 100f
                };

                float healthyScore = infector.CalculateTargetScore(healthyProfile);
                float infectedScore = infector.CalculateTargetScore(infectedProfile);

                Assert.Greater(healthyScore, infectedScore, "Infector must heavily penalize already infected players to spread disease across uninfected teammates.");
            }
            finally
            {
                Object.DestroyImmediate(healthyPlayer);
                Object.DestroyImmediate(infectedPlayer);
            }
        }

        [Test]
        public void Infector_TargetScoring_PrioritizesLowHealthAndReloadingPlayers()
        {
            var healthyPlayer = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            var vulnerablePlayer = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            healthyPlayer.name = "HealthyPlayer";
            vulnerablePlayer.name = "VulnerablePlayer";

            try
            {
                var healthyProfile = new PlayerProfile
                {
                    playerIndex = 0,
                    playerTransform = healthyPlayer.transform,
                    isIsolated = false,
                    currentHealth = 100f,
                    isReloading = false
                };

                var vulnerableProfile = new PlayerProfile
                {
                    playerIndex = 1,
                    playerTransform = vulnerablePlayer.transform,
                    isIsolated = false,
                    currentHealth = 20f,
                    isReloading = true
                };

                float healthyScore = infector.CalculateTargetScore(healthyProfile);
                float vulnerableScore = infector.CalculateTargetScore(vulnerableProfile);

                Assert.Greater(vulnerableScore, healthyScore, "Infector should prioritize low health (+0.8) and reloading (+0.6) targets.");
            }
            finally
            {
                Object.DestroyImmediate(healthyPlayer);
                Object.DestroyImmediate(vulnerablePlayer);
            }
        }

        [Test]
        public void Infector_ResetAI_RestoresSearchState()
        {
            infector.ResetAI();
            Assert.AreEqual(InfectorState.Search, infector.CurrentState);
            Assert.IsFalse(infector.IsPerformingImplant);
        }

        [Test]
        public void Infector_ImpactEligibility_RejectsTargetsBehindOrOutOfRange()
        {
            var target = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            target.name = "ImpactTarget";
            try
            {
                infectorGo.transform.SetPositionAndRotation(new Vector3(10000f, 0f, 10000f), Quaternion.identity);

                target.transform.position = infectorGo.transform.position + Vector3.forward * 2f;
                Assert.IsTrue(SI_Infector.CanImpactTarget(infectorGo.transform, target.transform, 2.2f, 120f));

                target.transform.position = infectorGo.transform.position - Vector3.forward * 2f;
                Assert.IsFalse(SI_Infector.CanImpactTarget(infectorGo.transform, target.transform, 2.2f, 120f));

                target.transform.position = infectorGo.transform.position + Vector3.forward * 2.3f;
                Assert.IsFalse(SI_Infector.CanImpactTarget(infectorGo.transform, target.transform, 2.2f, 120f));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(GamePhase.RELAX, 0)]
        [TestCase(GamePhase.BUILD, 2)]
        [TestCase(GamePhase.PEAK, 4)]
        public void SpecialBudget_MatchesAuthoredDirectorPhase(GamePhase phase, int expectedBudget)
        {
            Assert.AreEqual(expectedBudget, SpecialInfectedRegistry.GetPhaseBudget(phase));
        }

        [TestCase(SpecialType.Screamer, 1)]
        [TestCase(SpecialType.Infector, 2)]
        [TestCase(SpecialType.Tank, 3)]
        public void SpecialCost_MatchesBudgetPolicy(SpecialType type, int expectedCost)
        {
            Assert.AreEqual(expectedCost, SpecialInfectedRegistry.GetSpecialCost(type));
        }

        [Test]
        public void Infector_TeamSpawnGuard_UsesAverageAndRejectsCriticalOrDownedPlayers()
        {
            var first = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            var second = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            try
            {
                PlayerHealth firstHealth = first.GetComponent<PlayerHealth>();
                PlayerHealth secondHealth = second.GetComponent<PlayerHealth>();
                PlayerInfectionController firstInfection = first.GetComponent<PlayerInfectionController>();
                PlayerInfectionController secondInfection = second.GetComponent<PlayerInfectionController>();
                var profiles = new[]
                {
                    new PlayerProfile { playerTransform = first.transform, cachedHealth = firstHealth, cachedInfection = firstInfection },
                    new PlayerProfile { playerTransform = second.transform, cachedHealth = secondHealth, cachedInfection = secondInfection }
                };
                var healthyTeam = new[]
                {
                    new PlayerTeamHealthSnapshot(100f, 100f, false),
                    new PlayerTeamHealthSnapshot(100f, 100f, false)
                };

                firstInfection.SetInfectionServer(40f);
                secondInfection.SetInfectionServer(59f);
                Assert.IsTrue(infector.ShouldSpawnForTeam(profiles, healthyTeam), "Average 49.5% must remain eligible.");

                secondInfection.SetInfectionServer(60f);
                Assert.IsFalse(infector.ShouldSpawnForTeam(profiles, healthyTeam), "Average 50% must be blocked.");

                secondInfection.SetInfectionServer(71f);
                Assert.IsFalse(infector.ShouldSpawnForTeam(profiles, healthyTeam), "Any Critical player must block Infector spawn.");

                secondInfection.SetInfectionServer(0f);
                var downedTeam = new[]
                {
                    healthyTeam[0],
                    new PlayerTeamHealthSnapshot(100f, 100f, true)
                };
                Assert.IsFalse(infector.ShouldSpawnForTeam(profiles, downedTeam), "Any downed/dead player must block Infector spawn.");
                Assert.IsFalse(infector.ShouldSpawnForTeam(null, null), "Missing team data must fail closed.");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
