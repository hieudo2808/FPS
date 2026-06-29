using NUnit.Framework;
using UnityEngine;
using FPS;

namespace FPS.Tests
{
    public class SettingsManagerTests
    {
        [SetUp]
        public void Setup()
        {
            PlayerPrefs.DeleteAll();
        }

        [TearDown]
        public void Teardown()
        {
            PlayerPrefs.DeleteAll();
        }

        [Test]
        public void SettingsManager_HasDefaultValues_WhenNoPlayerPrefs()
        {
            float sensitivity = SettingsManager.Instance.MouseSensitivity;
            int quality = SettingsManager.Instance.GraphicsQuality;

            Assert.AreEqual(2.0f, sensitivity, "Default sensitivity should be 2.0");
            Assert.IsTrue(quality >= 0, "Quality should be a non-negative integer");
        }

        [Test]
        public void SettingsManager_SavesAndLoads_MouseSensitivity()
        {
            SettingsManager.Instance.SetMouseSensitivity(3.5f);
            
            // Reload from prefs directly to verify
            Assert.AreEqual(3.5f, PlayerPrefs.GetFloat("MouseSensitivity"), "PlayerPrefs should store sensitivity");
            Assert.AreEqual(3.5f, SettingsManager.Instance.MouseSensitivity, "Property should return updated sensitivity");
        }

        [Test]
        public void SettingsManager_SavesAndLoads_GraphicsQuality()
        {
            SettingsManager.Instance.SetGraphicsQuality(1);
            
            Assert.AreEqual(1, PlayerPrefs.GetInt("GraphicsQuality"), "PlayerPrefs should store graphics quality");
            Assert.AreEqual(1, SettingsManager.Instance.GraphicsQuality, "Property should return updated graphics quality");
        }
    }
}
