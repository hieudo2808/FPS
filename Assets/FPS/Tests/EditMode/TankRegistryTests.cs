using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class TankRegistryTests
    {
        private GameObject registryGo;
        private SpecialInfectedRegistry registry;
        private GameObject tankPrefab;
        private GameObject emptyPrefab;

        [SetUp]
        public void SetUp()
        {
            var instanceProperty = typeof(SpecialInfectedRegistry).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var setMethod = instanceProperty?.GetSetMethod(true);
            setMethod?.Invoke(null, new object[] { null });

            registryGo = new GameObject("SpecialInfectedRegistry");
            registry = registryGo.AddComponent<SpecialInfectedRegistry>();
            var awakeMethod = typeof(SpecialInfectedRegistry).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            awakeMethod?.Invoke(registry, null);

            tankPrefab = new GameObject("TankPrefab");
            tankPrefab.AddComponent<SI_Tank>();

            emptyPrefab = new GameObject("EmptyPrefab");
        }

        [TearDown]
        public void TearDown()
        {
            if (registryGo != null) Object.DestroyImmediate(registryGo);
            if (tankPrefab != null) Object.DestroyImmediate(tankPrefab);
            if (emptyPrefab != null) Object.DestroyImmediate(emptyPrefab);
        }

        [Test]
        public void Registry_CanPromoteToPlayable_Tank_WithSI_Tank()
        {
            bool result = registry.RegisterPlayableSpecialPrefab(SpecialType.Tank, tankPrefab);
            Assert.IsTrue(result, "RegisterPlayableSpecialPrefab should return true for prefab with SI_Tank.");
        }

        [Test]
        public void Registry_CanPromoteToPlayable_Tank_WithoutSI_Tank_Fails()
        {
            bool result = registry.RegisterPlayableSpecialPrefab(SpecialType.Tank, emptyPrefab);
            Assert.IsFalse(result, "RegisterPlayableSpecialPrefab should fail for prefab without SI_Tank.");
        }

        [Test]
        public void Registry_IsPlayableSpecial_AcceptsTank()
        {
            registry.RegisterPlayableSpecialPrefab(SpecialType.Tank, tankPrefab);

            var isPlayableMethod = typeof(SpecialInfectedRegistry).GetMethod(
                "IsPlayableSpecial",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(isPlayableMethod);

            var specialTypesField = typeof(SpecialInfectedRegistry).GetField(
                "specialTypes",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<SpecialInfectedData>)specialTypesField.GetValue(registry);
            var tankData = list.Find(s => s.type == SpecialType.Tank);

            Assert.NotNull(tankData);
            bool isPlayable = (bool)isPlayableMethod.Invoke(null, new object[] { tankData });
            Assert.IsTrue(isPlayable, "IsPlayableSpecial should accept Tank once registered as Playable.");
        }

        [Test]
        public void Registry_Tank_DefaultCooldown_Is120s()
        {
            var specialTypesField = typeof(SpecialInfectedRegistry).GetField(
                "specialTypes",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<SpecialInfectedData>)specialTypesField.GetValue(registry);
            var tankData = list.Find(s => s.type == SpecialType.Tank);

            Assert.NotNull(tankData);
            Assert.AreEqual(120f, tankData.cooldown);
        }

        [Test]
        public void Registry_Tank_SpawnWeight_LowerThanScreamer()
        {
            var specialTypesField = typeof(SpecialInfectedRegistry).GetField(
                "specialTypes",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<SpecialInfectedData>)specialTypesField.GetValue(registry);
            var tankData = list.Find(s => s.type == SpecialType.Tank);
            var screamerData = list.Find(s => s.type == SpecialType.Screamer);

            Assert.NotNull(tankData);
            Assert.NotNull(screamerData);
            Assert.Less(tankData.spawnWeight, screamerData.spawnWeight);
        }
    }
}
