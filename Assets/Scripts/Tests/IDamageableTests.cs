using NUnit.Framework;
using UnityEngine;
using System;
using System.Reflection;
using FPS;

namespace FPS.Tests
{
    public class IDamageableTests
    {
        [Test]
        public void IDamageable_Interface_Exists()
        {
            Type interfaceType = typeof(IDamageable);
            Assert.IsNotNull(interfaceType, "IDamageable interface should exist in FPS namespace.");
            Assert.IsTrue(interfaceType.IsInterface, "IDamageable should be an interface.");

            MethodInfo takeDamageMethod = interfaceType.GetMethod("TakeDamage", new Type[] { typeof(float) });
            Assert.IsNotNull(takeDamageMethod, "IDamageable should have TakeDamage(float amount) method.");

            PropertyInfo isDeadProperty = interfaceType.GetProperty("IsDead");
            Assert.IsNotNull(isDeadProperty, "IDamageable should have IsDead property.");
            Assert.AreEqual(typeof(bool), isDeadProperty.PropertyType, "IsDead property should be of type bool.");
        }

        [Test]
        public void EnemyHealth_Implements_IDamageable()
        {
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(EnemyHealth)), "EnemyHealth should implement IDamageable.");
        }

        [Test]
        public void PlayerHealth_Implements_IDamageable()
        {
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(PlayerHealth)), "PlayerHealth should implement IDamageable.");
        }
    }
}
