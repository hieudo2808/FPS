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
    }
}
