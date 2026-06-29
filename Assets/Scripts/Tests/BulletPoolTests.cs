using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections;

namespace FPS.Tests
{
    /// <summary>
    /// Regression tests cho Task 11: Object Pooling cho dan trong Weapon.cs.
    /// Dam bao SpawnVisualBullet() su dung pool khi bulletPool duoc gan,
    /// va ReturnBulletToPool coroutine ton tai de tra dan ve pool sau delay.
    /// </summary>
    public class BulletPoolTests
    {
        // -------------------------------------------------------
        // Test 1: Weapon co SerializeField bulletPool
        // -------------------------------------------------------
        [Test]
        public void TestWeapon_BulletPool_FieldExists()
        {
            var field = typeof(Weapon).GetField(
                "bulletPool",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                "Weapon phai co private field 'bulletPool' kieu ObjectPooling " +
                "de tich hop Object Pooling cho dan bắn (Task 11)");

            Assert.AreEqual(typeof(ObjectPooling), field.FieldType,
                "bulletPool phai la kieu ObjectPooling");
        }

        // -------------------------------------------------------
        // Test 2: Weapon co ReturnBulletToPool IEnumerator method
        // -------------------------------------------------------
        [Test]
        public void TestWeapon_ReturnBulletToPool_MethodExists()
        {
            var method = typeof(Weapon).GetMethod(
                "ReturnBulletToPool",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "Weapon phai co private method 'ReturnBulletToPool' " +
                "de tra dan ve pool sau khoang thoi gian bulletLiveTime (Task 11)");

            Assert.AreEqual(typeof(IEnumerator), method.ReturnType,
                "ReturnBulletToPool phai la IEnumerator (Coroutine)");
        }

        // -------------------------------------------------------
        // Test 3: SpawnVisualBullet voi pool == null van hoat dong (fallback)
        //         Khong throw exception; dam bao backward compat
        // -------------------------------------------------------
        [Test]
        public void TestWeapon_SpawnVisualBullet_FallbackWhenPoolIsNull()
        {
            var go = new GameObject("Weapon");
            var weapon = go.AddComponent<Weapon>();

            // bulletPool = null (mac dinh), weaponData = null
            // SpawnVisualBullet phai kiem tra null an toan va return som
            Assert.DoesNotThrow(() =>
            {
                weapon.SpawnVisualBullet(Vector3.zero, Vector3.forward);
            }, "SpawnVisualBullet khong duoc throw exception khi bulletPrefab va bulletPool deu null");

            Object.DestroyImmediate(go);
        }
    }
}
