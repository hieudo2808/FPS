using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace FPS.Tests
{
    public class DifficultyManagerTests
    {
        [Test]
        public void DifficultyManager_HasFourTiers()
        {
            var enumType = typeof(DifficultyManager).Assembly.GetType("FPS.DifficultyLevel");
            Assert.IsNotNull(enumType, "DifficultyLevel enum should exist.");
            
            var names = System.Enum.GetNames(enumType);
            Assert.Contains("Easy", names);
            Assert.Contains("Medium", names);
            Assert.Contains("Hard", names);
            Assert.Contains("Pandemonium", names);
            Assert.AreEqual(4, names.Length, "Should have exactly 4 tiers.");
        }

        [Test]
        public void DifficultyManager_StaticProfiles_PreserveCurrentGoldenValues()
        {
            var gameObject = new GameObject("DifficultyManager_Golden");
            try
            {
                var manager = gameObject.AddComponent<DifficultyManager>();

                AssertStats(manager.GetStats(DifficultyLevel.Easy), 0.5f, 0.5f, 0.8f, 2, 1.25f, 0.7f, 0.05f, false);
                AssertStats(manager.GetStats(DifficultyLevel.Medium), 1f, 1f, 1f, 3, 1f, 1f, 0.15f, false);
                AssertStats(manager.GetStats(DifficultyLevel.Hard), 1.5f, 1.5f, 1.2f, 4, 0.75f, 1.25f, 0.2f, true);
                AssertStats(manager.GetStats(DifficultyLevel.Pandemonium), 3f, 2f, 1.5f, 6, 0.5f, 1.75f, 0.35f, true);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void AssertStats(
            DifficultyStats actual,
            float hp,
            float damage,
            float speed,
            int attackers,
            float interval,
            float maxAlive,
            float special,
            bool rubberBanding)
        {
            Assert.That(actual.hpMultiplier, Is.EqualTo(hp));
            Assert.That(actual.damageMultiplier, Is.EqualTo(damage));
            Assert.That(actual.speedMultiplier, Is.EqualTo(speed));
            Assert.That(actual.maxConcurrentAttackers, Is.EqualTo(attackers));
            Assert.That(actual.spawnIntervalMultiplier, Is.EqualTo(interval));
            Assert.That(actual.maxAliveMultiplier, Is.EqualTo(maxAlive));
            Assert.That(actual.specialSpawnChance, Is.EqualTo(special));
            Assert.That(actual.enableRubberBanding, Is.EqualTo(rubberBanding));
        }
    }
}
