using System.Collections;
using System.IO;
using System.Reflection;
using FPS.Editor;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.Tests
{
    public class TankVerificationSuite
    {
        private GameObject tankPrefab;
        private GameObject spawnedTank;
        private GameObject playerGo1;
        private GameObject playerGo2;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabUtility.PrefabPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (spawnedTank != null) Object.DestroyImmediate(spawnedTank);
            if (playerGo1 != null) Object.DestroyImmediate(playerGo1);
            if (playerGo2 != null) Object.DestroyImmediate(playerGo2);
        }

        // ==========================================
        // 1. PREFAB & ASSET INTEGRITY
        // ==========================================

        [Test]
        public void Verification_1_Prefab_ExistsAndHasAllComponents()
        {
            Assert.IsNotNull(tankPrefab, "Tank.prefab must exist in project.");

            Assert.IsNotNull(tankPrefab.GetComponent<Animator>(), "Must have Animator.");
            Assert.IsNotNull(tankPrefab.GetComponent<NetworkObject>(), "Must have NetworkObject.");
            Assert.IsNotNull(tankPrefab.GetComponent<NetworkTransform>(), "Must have NetworkTransform.");
            Assert.IsNotNull(tankPrefab.GetComponent<SI_Tank>(), "Must have SI_Tank.");
            Assert.IsNotNull(tankPrefab.GetComponent<EnemyHealth>(), "Must have EnemyHealth.");
            Assert.IsNotNull(tankPrefab.GetComponent<NavMeshAgent>(), "Must have NavMeshAgent.");
            Assert.IsNotNull(tankPrefab.GetComponent<CapsuleCollider>(), "Must have CapsuleCollider.");
            Assert.IsNotNull(tankPrefab.GetComponent<HitboxSegment>(), "Must have HitboxSegment.");
            Assert.IsNotNull(tankPrefab.GetComponent<LagCompensatedTarget>(), "Must have LagCompensatedTarget.");

            var smr = tankPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr, "Tank prefab must have SkinnedMeshRenderer.");
            Assert.IsNotNull(smr.sharedMaterial, "Tank SkinnedMeshRenderer must have a material assigned.");
        }

        [Test]
        public void Verification_2_Model_DimensionsAndSilhouette()
        {
            spawnedTank = Object.Instantiate(tankPrefab);
            var smr = spawnedTank.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr);

            Bounds bounds = smr.bounds;
            Assert.GreaterOrEqual(bounds.size.y, 2.0f, "Tank height should be >= 2.0m (brute size).");
            Assert.GreaterOrEqual(bounds.size.x, 1.8f, "Tank shoulder width should be >= 1.8m (broad shoulders).");
        }

        // ==========================================
        // 2. NAVMESH & AGENT CONFIGURATION
        // ==========================================

        [Test]
        public void Verification_3_NavMeshAgent_ConfiguredForBrute()
        {
            var agent = tankPrefab.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent);
            Assert.AreEqual(0.8f, agent.radius, 0.05f, "Tank NavMeshAgent radius must be 0.8m (larger than common zombie 0.5m).");
            Assert.AreEqual(3.5f, agent.speed, 0.5f, "Tank NavMeshAgent speed must be 3.5m/s (heavy approach).");
            Assert.AreEqual(2.0f, agent.stoppingDistance, 0.5f, "Stopping distance must accommodate heavy melee range.");
        }

        // ==========================================
        // 3. COMBAT ABILITIES & MECHANICS
        // ==========================================

        [Test]
        public void Verification_4_HeavySwing_Mechanic()
        {
            spawnedTank = Object.Instantiate(tankPrefab);
            var tank = spawnedTank.GetComponent<SI_Tank>();
            Assert.IsNotNull(tank);

            Assert.AreEqual(50f, tank.HeavySwingDamage);
            Assert.AreEqual(8f, tank.HeavySwingKnockbackForce);
            Assert.AreEqual(0.8f, tank.HeavySwingWindup, 0.05f);
            Assert.AreEqual(3.5f, tank.HeavySwingRange, 0.1f);
        }

        [Test]
        public void Verification_5_SlamAoE_Mechanic()
        {
            spawnedTank = Object.Instantiate(tankPrefab);
            var tank = spawnedTank.GetComponent<SI_Tank>();
            Assert.IsNotNull(tank);

            Assert.AreEqual(25f, tank.SlamDamage);
            Assert.AreEqual(4.5f, tank.SlamRadius);
            Assert.AreEqual(12f, tank.SlamKnockbackForce);
            Assert.AreEqual(15f, tank.SlamCooldown);
            Assert.AreEqual(1.2f, tank.SlamWindup, 0.05f);
        }

        [Test]
        public void Verification_6_Stagger_DamageThresholdAndWindow()
        {
            spawnedTank = Object.Instantiate(tankPrefab);
            var tank = spawnedTank.GetComponent<SI_Tank>();

            Assert.AreEqual(375f, tank.StaggerDamageThreshold);
            Assert.AreEqual(3.0f, tank.StaggerWindow);
            Assert.AreEqual(1.25f, tank.StaggerDuration);

            // Simulate damage below threshold
            tank.RecordDamage(200f, 0f);
            Assert.IsFalse(tank.CheckAndTriggerStagger(0f));
            Assert.IsFalse(tank.IsStaggered);

            // Simulate burst damage reaching the 15% solo threshold within window.
            tank.RecordDamage(175f, 1.5f);
            Assert.IsTrue(tank.CheckAndTriggerStagger(1.5f));
            Assert.IsTrue(tank.IsStaggered);
        }

        // ==========================================
        // 4. KNOCKBACK INTEGRATION
        // ==========================================

        [Test]
        public void Verification_7_PlayerKnockback_AppliesAndDecays()
        {
            playerGo1 = new GameObject("TestPlayerKnockback");
            playerGo1.AddComponent<CharacterController>();
            var pMove = playerGo1.AddComponent<PlayerMovement>();

            Vector3 force = new Vector3(8f, 2f, 0f);
            pMove.ApplyKnockback(force);

            Assert.AreEqual(force, pMove.ExternalVelocity);

            // Simulate tick to verify decay
            var method = typeof(PlayerMovement).GetMethod("SimulateTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(pMove, new object[] { new PlayerInputPayload(), 0.1f });

            Assert.Less(pMove.ExternalVelocity.magnitude, force.magnitude);
        }

        // ==========================================
        // 5. REGISTRY & DIRECTOR SPAWN RULES
        // ==========================================

        [Test]
        public void Verification_8_Registry_PromotionAndEligibility()
        {
            GameObject regGo = new GameObject("TestRegistry");
            var registry = regGo.AddComponent<SpecialInfectedRegistry>();

            try
            {
                bool promoted = registry.RegisterPlayableSpecialPrefab(SpecialType.Tank, tankPrefab);
                Assert.IsTrue(promoted, "Tank prefab must be eligible for promotion to Playable in SpecialInfectedRegistry.");
            }
            finally
            {
                Object.DestroyImmediate(regGo);
            }
        }

        [Test]
        public void Verification_9_DefaultNetworkPrefabs_ContainsTank()
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Contains(tankPrefab), "Tank.prefab must be registered in DefaultNetworkPrefabs for multiplayer.");
        }

        [Test]
        public void Verification_10_SceneVerification_OpenAndVerifyInScene()
        {
            TankSceneVerification.VerifyTankInScene();
            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "TankSceneVerificationReport.txt");
            Assert.IsTrue(File.Exists(reportPath), "Scene verification report should be generated.");
            string content = File.ReadAllText(reportPath);
            Assert.IsTrue(content.Contains("ALL CHECKS PASSED"), "Scene verification should pass all checks.");
        }
    }
}
