using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace FPS.Tests
{
    public class WeaponInputTests
    {
        [Test]
        public void InputManager_HasWeaponSwitchBindings()
        {
            var go = new GameObject();
            var im = go.AddComponent<InputManager>();

            Assert.AreEqual(KeyCode.Alpha1, im.GetKeyForAction("Weapon1"), "Missing Weapon1 binding");
            Assert.AreEqual(KeyCode.Alpha2, im.GetKeyForAction("Weapon2"), "Missing Weapon2 binding");
            Assert.AreEqual(KeyCode.F, im.GetKeyForAction("Interact"), "Missing Interact binding");
            Assert.AreEqual(KeyCode.G, im.GetKeyForAction("Grenade"), "Missing Grenade binding");
            Assert.AreEqual(KeyCode.Y, im.GetKeyForAction("Inspect"), "Missing Inspect binding");

            Object.DestroyImmediate(go);
        }
    }
}
