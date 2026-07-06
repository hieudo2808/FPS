using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using System.Reflection;
using System.Collections.Generic;

namespace FPS.Tests
{
    public class WeaponSecurityTests
    {
        private readonly List<Object> objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in objectsToDestroy)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            objectsToDestroy.Clear();
        }

        [Test]
        public void EnemyHealth_DoesNotAcceptClientSuppliedDamageRpc()
        {
            var legacyRpc = typeof(EnemyHealth).GetMethod(
                "ReportHitServerRpc",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ulong), typeof(Vector3), typeof(float) },
                null);

            Assert.IsNull(legacyRpc,
                "EnemyHealth must not expose a hit RPC that accepts client-supplied damage. " +
                "WeaponFireHandler.RequestFireServerRpc is the server-authoritative fire path.");
        }

        [Test]
        public void TestWeaponManager_RequestFire_RejectsDistantShootPosition()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 2;
            data.totalAmmo = 2;
            data.fireRate = 0f;

            fireHandler.InitializeServerWeaponStateForTests(2, 0);

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(new Vector3(0, 0, 2f), Vector3.forward));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rejecting fire request"));
            Assert.IsFalse(fireHandler.ProcessFireServerForTests(new Vector3(0, 0, 10f), Vector3.forward));
            Assert.AreEqual(1, fireHandler.ServerMagazineAmmo,
                "Rejected distant fire must not consume authoritative server ammo.");
        }

        [Test]
        public void RequestFireServerRpc_RejectsRapidFireBeforeFireRate()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 3;
            data.totalAmmo = 3;
            data.fireRate = 10f;

            fireHandler.InitializeServerWeaponStateForTests(3, 0);

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));

            Assert.AreEqual(2, fireHandler.ServerMagazineAmmo,
                "Server must consume ammo only for the first shot when the second shot violates fire-rate.");
        }

        [Test]
        public void RequestFireServerRpc_RejectsWhenServerMagazineEmpty()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 1;
            data.totalAmmo = 1;

            fireHandler.InitializeServerWeaponStateForTests(0, 0);

            Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(0, fireHandler.ServerMagazineAmmo,
                "Server must reject fire when its authoritative magazine is empty.");
        }

        [Test]
        public void RequestReloadServerRpc_ServerRestoresAmmoAfterReloadTime()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 5;
            data.totalAmmo = 20;
            data.reloadTime = 0f;

            fireHandler.InitializeServerWeaponStateForTests(1, 10);

            Assert.IsTrue(fireHandler.BeginServerReloadForTests());
            fireHandler.CompleteServerReloadIfReadyForTests();

            Assert.AreEqual(5, fireHandler.ServerMagazineAmmo);
            Assert.AreEqual(6, fireHandler.ServerReserveAmmo);
        }

        [Test]
        public void PickupAmmo_GrantsReserveAmmoOnServerState()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 5;
            data.totalAmmo = 20;

            fireHandler.InitializeServerWeaponStateForTests(5, 0);
            fireHandler.AddReserveAmmoServer(12);

            Assert.AreEqual(12, fireHandler.ServerReserveAmmo,
                "Pickup ammo must update server-side reserve ammo, not only client-local weapon ammo.");
        }

        [Test]
        public void Weapon_StartWithoutWeaponData_DoesNotThrow()
        {
            var weaponGo = new GameObject("UnconfiguredWeapon");
            objectsToDestroy.Add(weaponGo);
            var weapon = weaponGo.AddComponent<Weapon>();

            Assert.DoesNotThrow(() =>
                typeof(Weapon)
                    .GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(weapon, null),
                "Weapon.Start should tolerate prefab placeholders with no WeaponData instead of throwing NullReferenceException.");
            Assert.AreEqual(0, weapon.CurrentAmmo);
            Assert.AreEqual(0, weapon.ReservedAmmo);
            Assert.IsNull(weapon.WeaponIcon);
        }

        private WeaponFireHandler CreateServerFireHandler(out WeaponManager weaponManager, out WeaponData weaponData)
        {
            var playerGo = new GameObject("ServerPlayer");
            objectsToDestroy.Add(playerGo);
            playerGo.AddComponent<NetworkObject>();

            weaponManager = playerGo.AddComponent<WeaponManager>();
            var fireHandler = playerGo.AddComponent<WeaponFireHandler>();

            var weaponGo = new GameObject("ServerWeapon");
            objectsToDestroy.Add(weaponGo);
            var weapon = weaponGo.AddComponent<Weapon>();

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(weaponData);
            typeof(Weapon).GetField("weaponData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, weaponData);
            typeof(WeaponManager).GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(weaponManager, new List<GameObject> { weaponGo });

            var nbIsServerProp = typeof(NetworkBehaviour).GetProperty("IsServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            nbIsServerProp.SetValue(weaponManager, true);
            nbIsServerProp.SetValue(fireHandler, true);

            return fireHandler;
        }
    }
}
