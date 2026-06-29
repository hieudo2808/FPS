using NUnit.Framework;
using UnityEngine;
using FPS;

namespace FPS.Tests
{
    public class AIDifficultyTests
    {
        [Test]
        public void ZombieFactory_UsesDifficultyManagerStats()
        {
            // Test that ZombieFactory methods or ApplyStats uses DifficultyManager.
            // Just verifying that DifficultyManager is referenced in ZombieFactory.cs for now.
            string factoryCode = System.IO.File.ReadAllText("e:/Unity/Project/FPS/Assets/Scripts/Enemy/ZombieFactory.cs");
            Assert.IsTrue(factoryCode.Contains("DifficultyManager"), "ZombieFactory should read from DifficultyManager.");
        }
    }
}
