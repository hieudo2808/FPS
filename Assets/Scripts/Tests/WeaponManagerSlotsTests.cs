using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace FPS.Tests
{
    public class WeaponManagerSlotsTests
    {
        [Test]
        public void WeaponManager_HasMaxWeaponSlotsField()
        {
            var field = typeof(WeaponManager).GetField("maxWeaponSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "WeaponManager should have a maxWeaponSlots field.");
            Assert.AreEqual(typeof(int), field.FieldType, "maxWeaponSlots should be an integer.");
        }

        [Test]
        public void WeaponManager_AddWeapon_RespectsMaxSlots()
        {
            var go = new GameObject();
            var wm = go.AddComponent<WeaponManager>();

            var field = typeof(WeaponManager).GetField("maxWeaponSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(wm, 3); // Override to 3 for testing
            }

            var weaponsField = typeof(WeaponManager).GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic);
            var mockWeapons = new List<GameObject>();
            weaponsField.SetValue(wm, mockWeapons);

            var w1 = new GameObject("W1");
            var w2 = new GameObject("W2");
            var w3 = new GameObject("W3");
            var w4 = new GameObject("W4");

            wm.AddWeapon(w1);
            wm.AddWeapon(w2);
            wm.AddWeapon(w3);
            wm.AddWeapon(w4); // Should be ignored

            Assert.AreEqual(3, mockWeapons.Count, "WeaponManager should respect maxWeaponSlots.");

            Object.DestroyImmediate(w1);
            Object.DestroyImmediate(w2);
            Object.DestroyImmediate(w3);
            Object.DestroyImmediate(w4);
            Object.DestroyImmediate(go);
        }
    }
}
