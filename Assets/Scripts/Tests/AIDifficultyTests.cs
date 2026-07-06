using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class AIDifficultyTests
    {
        [Test]
        public void ZombieFactory_AppliesDifficultyManagerDamageStats()
        {
            var difficultyGo = new GameObject("DifficultyManager");
            var rubberBandGo = new GameObject("RubberBandingSystem");
            var registryGo = new GameObject("ZombieRegistry");
            var factoryGo = new GameObject("ZombieFactory");
            var prefab = new GameObject("ZombiePrefab");
            GameObject spawned = null;

            try
            {
                var difficultyManager = difficultyGo.AddComponent<DifficultyManager>();
                typeof(DifficultyManager)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(difficultyManager, null);
                typeof(Unity.Netcode.NetworkVariable<DifficultyLevel>)
                    .GetField("m_InternalValue", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(difficultyManager.CurrentDifficulty, DifficultyLevel.Pandemonium);

                Assert.AreEqual(2f, difficultyManager.GetCurrentStats().damageMultiplier, 0.001f,
                    "Test setup should force Pandemonium stats without network server ownership.");

                var rubberBandingSystem = rubberBandGo.AddComponent<RubberBandingSystem>();
                typeof(RubberBandingSystem)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(rubberBandingSystem, null);

                prefab.AddComponent<EnemyAI>();

                var registry = registryGo.AddComponent<ZombieRegistry>();
                typeof(ZombieRegistry)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(registry, null);
                registry.AddZombieType(new ZombieData
                {
                    displayName = "Pandemonium Test Zombie",
                    prefab = prefab,
                    baseHP = 100f,
                    baseSpeed = 3f,
                    baseDamage = 10f,
                    attackRate = 1.5f,
                    spawnWeight = 1
                });

                var factory = factoryGo.AddComponent<ZombieFactory>();
                typeof(ZombieFactory)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(factory, null);

                spawned = factory.SpawnZombie(Vector3.zero, Quaternion.identity);
                Assert.NotNull(spawned, "Test setup should spawn through ZombieFactory's public gameplay path.");

                var ai = spawned.GetComponent<EnemyAI>();
                Assert.AreEqual(20f, ai.AttackDamage, 0.001f,
                    "ZombieFactory should apply Pandemonium damage multiplier from DifficultyManager stats.");
            }
            finally
            {
                typeof(DifficultyManager)
                    .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .SetValue(null, null);
                typeof(SceneSingleton<RubberBandingSystem>)
                    .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, null);
                typeof(SceneSingleton<ZombieFactory>)
                    .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, null);

                typeof(ZombieRegistry)
                    .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .SetValue(null, null);

                Object.DestroyImmediate(spawned);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(factoryGo);
                Object.DestroyImmediate(registryGo);
                Object.DestroyImmediate(rubberBandGo);
                Object.DestroyImmediate(difficultyGo);
            }
        }
    }
}
