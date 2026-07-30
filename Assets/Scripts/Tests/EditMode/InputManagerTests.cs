using NUnit.Framework;
using UnityEngine;
using FPS;

namespace FPS.Tests
{
    public class InputManagerTests
    {
        private GameObject inputManagerGo;
        private InputManager inputManager;

        [SetUp]
        public void Setup()
        {
            PlayerPrefs.DeleteAll();
            inputManagerGo = new GameObject("InputManager");
            inputManager = inputManagerGo.AddComponent<InputManager>();
        }

        [TearDown]
        public void Teardown()
        {
            PlayerPrefs.DeleteAll();
            if (inputManagerGo != null)
            {
                Object.DestroyImmediate(inputManagerGo);
            }
        }

        [Test]
        public void InputManager_RebindKey_SavesToPlayerPrefs()
        {
            // Act
            inputManager.RebindKey("Fire", KeyCode.JoystickButton0);

            // Assert
            string savedKeyStr = PlayerPrefs.GetString("Input_Fire", "");
            Assert.AreEqual(KeyCode.JoystickButton0.ToString(), savedKeyStr, "RebindKey should save to PlayerPrefs.");
        }

        [Test]
        public void InputManager_LoadsCustomBindings_FromPlayerPrefs()
        {
            // Arrange
            PlayerPrefs.SetString("Input_Fire", KeyCode.K.ToString());
            
            // Need to force re-initialization to simulate loading
            Object.DestroyImmediate(inputManagerGo);
            inputManagerGo = new GameObject("InputManager");
            inputManager = inputManagerGo.AddComponent<InputManager>();

            // Act
            KeyCode fireKey = inputManager.GetKeyForAction("Fire");

            // Assert
            Assert.AreEqual(KeyCode.K, fireKey, "InputManager should load previously saved binding.");
        }
    }
}
