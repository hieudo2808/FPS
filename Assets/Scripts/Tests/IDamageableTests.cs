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

        [Test]
        public void DamageInfo_DefaultConstructor_KeepsLegacyUnspecifiedType()
        {
            var damageInfo = new DamageInfo(10f);

            Assert.AreEqual(10f, damageInfo.amount);
            Assert.AreEqual(DamageType.Unspecified, damageInfo.damageType);
            Assert.AreEqual(DamageType.Unspecified, damageInfo.DamageType);
            Assert.AreEqual(HitboxZone.Body, damageInfo.hitZone);
            Assert.AreEqual(1f, damageInfo.damageMultiplier);
        }

        [Test]
        public void DamageInfo_CarriesHitboxZoneAndMultiplier()
        {
            var damageInfo = new DamageInfo(
                20f,
                hitPoint: Vector3.one,
                damageType: DamageType.Bullet,
                hitZone: HitboxZone.Head,
                damageMultiplier: 2f);

            Assert.True(damageInfo.isHeadshot);
            Assert.AreEqual(HitboxZone.Head, damageInfo.HitZone);
            Assert.AreEqual(2f, damageInfo.DamageMultiplier);
        }

        [Test]
        public void DamageFilter_CanRequireExplosionDamage()
        {
            var go = new GameObject("ExplosionOnlyDamageFilter");
            try
            {
                var filter = go.AddComponent<DamageFilter>();
                typeof(DamageFilter)
                    .GetField("acceptedTypes", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(filter, DamageType.Explosion);
                typeof(DamageFilter)
                    .GetField("acceptUnspecified", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(filter, false);

                Assert.False(filter.Allows(new DamageInfo(10f, damageType: DamageType.Bullet)));
                Assert.False(filter.Allows(new DamageInfo(10f)));
                Assert.True(filter.Allows(new DamageInfo(10f, damageType: DamageType.Explosion)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
