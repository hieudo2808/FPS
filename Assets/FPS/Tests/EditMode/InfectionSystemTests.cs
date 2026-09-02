using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public class InfectionSystemTests
    {
        private GameObject playerObject;
        private PlayerInfectionController infectionController;
        private GameObject infectorObject;
        private SI_Infector infector;
        private GameObject playerPrefab;

        [SetUp]
        public void SetUp()
        {
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab");
            GameObject infectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Enemies/Infector/Prefabs/Infector.prefab");
            Assert.NotNull(playerPrefab, "Authored Clove player prefab is required by infection tests.");
            Assert.NotNull(infectorPrefab, "Authored Infector prefab is required by infection tests.");

            playerObject = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            infectionController = playerObject.GetComponent<PlayerInfectionController>();

            infectorObject = PrefabUtility.InstantiatePrefab(infectorPrefab) as GameObject;
            infector = infectorObject.GetComponent<SI_Infector>();
            Assert.NotNull(infectionController);
            Assert.NotNull(infector);
        }

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
                Object.DestroyImmediate(playerObject);
            if (infectorObject != null)
                Object.DestroyImmediate(infectorObject);
        }

        // =========================================================
        // INFECTION STAGE CALCULATION TESTS
        // =========================================================
        [TestCase(0f, InfectionStage.None)]
        [TestCase(0.5f, InfectionStage.None)]
        [TestCase(1f, InfectionStage.Incubation)]
        [TestCase(15f, InfectionStage.Incubation)]
        [TestCase(30.9f, InfectionStage.Incubation)]
        [TestCase(31f, InfectionStage.Symptomatic)]
        [TestCase(50f, InfectionStage.Symptomatic)]
        [TestCase(70.9f, InfectionStage.Symptomatic)]
        [TestCase(71f, InfectionStage.Critical)]
        [TestCase(85f, InfectionStage.Critical)]
        [TestCase(99.9f, InfectionStage.Critical)]
        [TestCase(100f, InfectionStage.Sepsis)]
        [TestCase(150f, InfectionStage.Sepsis)]
        public void InfectionStage_CalculatesThresholdsCorrectly(float amount, InfectionStage expectedStage)
        {
            Assert.AreEqual(expectedStage, PlayerInfectionController.CalculateStage(amount));
        }

        // =========================================================
        // INFECTION DEBUFF MODIFIERS TESTS
        // =========================================================
        [Test]
        public void InfectionController_NoneStage_HasDefaultModifiers()
        {
            infectionController.SetInfectionServer(0f);
            Assert.AreEqual(InfectionStage.None, infectionController.CurrentStage);
            Assert.AreEqual(1.0f, infectionController.MovementSpeedMultiplier);
            Assert.AreEqual(1.0f, infectionController.WeaponSwayMultiplier);
            Assert.AreEqual(1.0f, infectionController.ReloadSpeedMultiplier);
            Assert.IsTrue(infectionController.CanSprint);
        }

        [Test]
        public void InfectionController_SymptomaticStage_AppliesCorrectDebuffs()
        {
            infectionController.SetInfectionServer(50f);
            Assert.AreEqual(InfectionStage.Symptomatic, infectionController.CurrentStage);
            Assert.AreEqual(0.95f, infectionController.MovementSpeedMultiplier);
            Assert.AreEqual(1.10f, infectionController.WeaponSwayMultiplier);
            Assert.AreEqual(1.10f, infectionController.ReloadSpeedMultiplier);
            Assert.IsTrue(infectionController.CanSprint);
        }

        [Test]
        public void InfectionController_CriticalStage_AppliesCorrectDebuffs()
        {
            infectionController.SetInfectionServer(80f);
            Assert.AreEqual(InfectionStage.Critical, infectionController.CurrentStage);
            Assert.AreEqual(0.90f, infectionController.MovementSpeedMultiplier);
            Assert.AreEqual(1.20f, infectionController.WeaponSwayMultiplier);
            Assert.AreEqual(1.20f, infectionController.ReloadSpeedMultiplier);
            Assert.IsTrue(infectionController.CanSprint);
        }

        [Test]
        public void InfectionController_SepsisStage_AppliesSevereDebuffsAndDisablesSprint()
        {
            infectionController.SetInfectionServer(100f);
            Assert.AreEqual(InfectionStage.Sepsis, infectionController.CurrentStage);
            Assert.AreEqual(0.75f, infectionController.MovementSpeedMultiplier);
            Assert.AreEqual(1.50f, infectionController.WeaponSwayMultiplier);
            Assert.AreEqual(1.40f, infectionController.ReloadSpeedMultiplier);
            Assert.IsFalse(infectionController.CanSprint);
        }

        // =========================================================
        // MUTATION & TREATMENT API TESTS
        // =========================================================
        [Test]
        public void InfectionController_AddInfection_ClampsToMax()
        {
            infectionController.SetInfectionServer(0f);
            infectionController.AddInfectionServer(30f);
            Assert.AreEqual(30f, infectionController.CurrentInfection);

            infectionController.AddInfectionServer(90f);
            Assert.AreEqual(100f, infectionController.CurrentInfection);
            Assert.AreEqual(InfectionStage.Sepsis, infectionController.CurrentStage);
        }

        [Test]
        public void InfectionController_TreatInfection_ReducesAmountAndRestoresStage()
        {
            infectionController.SetInfectionServer(100f);
            Assert.AreEqual(InfectionStage.Sepsis, infectionController.CurrentStage);

            infectionController.TreatInfectionServer(60f);
            Assert.AreEqual(40f, infectionController.CurrentInfection);
            Assert.AreEqual(InfectionStage.Symptomatic, infectionController.CurrentStage);
            Assert.IsTrue(infectionController.CanSprint);
        }

        [Test]
        public void InfectionController_Cure_ResetsToZero()
        {
            infectionController.SetInfectionServer(85f);
            infectionController.CureServer();

            Assert.AreEqual(0f, infectionController.CurrentInfection);
            Assert.AreEqual(InfectionStage.None, infectionController.CurrentStage);
        }

        // =========================================================
        // SI_INFECTOR TESTS
        // =========================================================
        [Test]
        public void Infector_HasCorrectSpecialTypeAndSettings()
        {
            Assert.AreEqual(SpecialType.Infector, infector.Type);
            Assert.IsTrue(infector.AllowedInSoloMode);
            Assert.AreEqual(15f, infector.ImplantDamage);
            Assert.AreEqual(30f, infector.ImplantInfectionAmount);
            Assert.AreEqual(2.2f, infector.ImplantRange);
            Assert.AreEqual(0.5f, infector.ImplantWindup);
            Assert.AreEqual(12f, infector.ImplantCooldown);
            Assert.AreEqual(5f, infector.RetreatDuration);
            Assert.AreEqual(200f, infector.FixedMaxHealth);
        }

        [Test]
        public void Infector_TargetScoring_PrefersIsolatedOverGrouped()
        {
            PlayerProfile isolatedProfile = new PlayerProfile
            {
                playerTransform = playerObject.transform,
                isIsolated = true,
                currentHealth = 100f,
                distanceToNearestAlly = 25f
            };

            GameObject groupedPlayerObj = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            groupedPlayerObj.name = "GroupedPlayer";
            PlayerProfile groupedProfile = new PlayerProfile
            {
                playerTransform = groupedPlayerObj.transform,
                isIsolated = false,
                currentHealth = 100f,
                distanceToNearestAlly = 2f
            };

            float isolatedScore = infector.CalculateTargetScore(isolatedProfile);
            float groupedScore = infector.CalculateTargetScore(groupedProfile);

            Assert.Greater(isolatedScore, groupedScore, "Infector should prefer isolated targets over grouped teammates.");

            Object.DestroyImmediate(groupedPlayerObj);
        }

        [Test]
        public void Infector_TargetScoring_PenalizesAlreadyInfectedTargets()
        {
            infectionController.SetInfectionServer(0f);
            PlayerProfile uninfectedProfile = new PlayerProfile
            {
                playerTransform = playerObject.transform,
                isIsolated = false,
                currentHealth = 100f,
                distanceToNearestAlly = 10f
            };

            float uninfectedScore = infector.CalculateTargetScore(uninfectedProfile);

            // Infect the player to 80%
            infectionController.SetInfectionServer(80f);
            float infectedScore = infector.CalculateTargetScore(uninfectedProfile);

            Assert.Greater(uninfectedScore, infectedScore, "Infector should penalize already infected targets to spread disease.");
        }
    }
}
