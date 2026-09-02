using FPS.Editor;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FPS.Tests
{
    public class TankPrefabSetupTests
    {
        private GameObject tankPrefab;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabUtility.PrefabPath);
        }

        [Test]
        public void TankPrefab_CanBeCreatedAndLoaded()
        {
            Assert.IsNotNull(tankPrefab, "Tank.prefab should exist and load successfully.");
        }

        [Test]
        public void TankPrefab_HasAllRequiredComponents()
        {
            Assert.IsNotNull(tankPrefab.GetComponent<Animator>(), "Must have Animator.");
            Assert.IsNotNull(tankPrefab.GetComponent<NetworkObject>(), "Must have NetworkObject.");
            Assert.IsNotNull(tankPrefab.GetComponent<NetworkTransform>(), "Must have NetworkTransform.");
            Assert.IsNotNull(tankPrefab.GetComponent<SI_Tank>(), "Must have SI_Tank.");
            Assert.IsNotNull(tankPrefab.GetComponent<EnemyHealth>(), "Must have EnemyHealth.");
            Assert.IsNotNull(tankPrefab.GetComponent<NavMeshAgent>(), "Must have NavMeshAgent.");
            Assert.IsNotNull(tankPrefab.GetComponent<CapsuleCollider>(), "Must have CapsuleCollider.");
            Assert.IsNotNull(tankPrefab.GetComponent<HitboxSegment>(), "Must have HitboxSegment.");
            Assert.IsNotNull(tankPrefab.GetComponent<LagCompensatedTarget>(), "Must have LagCompensatedTarget.");
        }

        [Test]
        public void TankPrefab_NavMeshAgent_HasLargerRadiusAndConfig()
        {
            var agent = tankPrefab.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent);
            Assert.GreaterOrEqual(agent.radius, 0.8f, "Tank NavMeshAgent radius must be >= 0.8 to reflect brute size.");
            Assert.Less(agent.speed, 5f, "Tank speed must be slower than normal zombie speed (5f).");
        }

        [Test]
        public void TankPrefab_Health_ConfiguredTo2500()
        {
            var health = tankPrefab.GetComponent<EnemyHealth>();
            Assert.IsNotNull(health);
            Assert.AreEqual(2500f, health.MaxHealth, "Tank base health must be configured to 2500.");
        }

        [Test]
        public void TankPrefab_Collider_ConfiguredCorrectly()
        {
            var collider = tankPrefab.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(collider);
            Assert.GreaterOrEqual(collider.radius, 0.8f);
            Assert.GreaterOrEqual(collider.height, 2.5f);
        }

        [Test]
        public void TankPrefab_SI_Tank_HasAbilitiesConfigured()
        {
            var tank = tankPrefab.GetComponent<SI_Tank>();
            Assert.IsNotNull(tank);
            Assert.AreEqual(50f, tank.HeavySwingDamage);
            Assert.AreEqual(8f, tank.HeavySwingKnockbackForce);
            Assert.AreEqual(25f, tank.SlamDamage);
            Assert.AreEqual(4.5f, tank.SlamRadius);
            Assert.AreEqual(12f, tank.SlamKnockbackForce);
            Assert.AreEqual(2500f, tank.HealthPerPlayer);
            Assert.AreEqual(375f, tank.StaggerDamageThreshold);
            Assert.AreEqual(5f, tank.StaggerImmunityDuration);
        }

        [Test]
        public void TankPrefab_RegisteredInDefaultNetworkPrefabs()
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Contains(tankPrefab), "Tank prefab must be registered in DefaultNetworkPrefabs for multiplayer replication.");
        }
    }
}
