using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace FPS.Tests
{
    public class InputManagerTests
    {
        private InputManager inputManager;
        private GameObject go;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("InputManagerGo");
            inputManager = go.AddComponent<InputManager>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void InputManager_HasDefaultBindings()
        {
            Assert.AreEqual(KeyCode.Mouse0, inputManager.GetKeyForAction("Fire"), "Default fire should be Mouse0");
            Assert.AreEqual(KeyCode.R, inputManager.GetKeyForAction("Reload"), "Default reload should be R");
            Assert.AreEqual(KeyCode.Mouse1, inputManager.GetKeyForAction("Aim"), "Default aim should be Mouse1");
        }

        [Test]
        public void InputManager_CanRebindKey()
        {
            inputManager.RebindKey("Fire", KeyCode.F);
            Assert.AreEqual(KeyCode.F, inputManager.GetKeyForAction("Fire"), "Fire key should be rebound to F");

            inputManager.RebindKey("Reload", KeyCode.T);
            Assert.AreEqual(KeyCode.T, inputManager.GetKeyForAction("Reload"), "Reload key should be rebound to T");
        }
    }
}
