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
        public void RequestFireServerRpc_RejectsNonOwnerSenderBeforeConsumingAmmo()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 2;
            data.totalAmmo = 2;
            data.fireRate = 0f;

            fireHandler.InitializeServerWeaponStateForTests(2, 0);

            Assert.IsFalse(fireHandler.ProcessFireServerForTests(
                Vector3.zero,
                Vector3.forward,
                senderClientId: 123,
                validateSender: true));
            Assert.AreEqual(2, fireHandler.ServerMagazineAmmo,
                "Rejected non-owner fire must not consume authoritative server ammo.");
        }

        [Test]
        public void RequestFireServerRpc_UsesWeaponHitMaskAndDamageType()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 2;
            data.totalAmmo = 2;
            data.fireRate = 0f;
            data.damage = 7f;
            data.damageType = DamageType.Bullet;
            data.hitMask = 1 << 0;

            fireHandler.InitializeServerWeaponStateForTests(2, 0);

            var target = CreateDamageTarget("LayerMaskedTarget", new Vector3(0f, 0f, 2f));
            target.gameObject.layer = 2;
            Physics.SyncTransforms();

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(0, target.HitCount,
                "Target on a layer outside WeaponData.hitMask must not receive damage.");

            target.gameObject.layer = 0;
            Physics.SyncTransforms();

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(1, target.HitCount);
            Assert.AreEqual(DamageType.Bullet, target.LastDamage.damageType);
        }

        [Test]
        public void RequestFireServerRpc_RespectsDamageFilter()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 2;
            data.totalAmmo = 2;
            data.fireRate = 0f;
            data.hitMask = Physics.DefaultRaycastLayers;

            fireHandler.InitializeServerWeaponStateForTests(2, 0);

            var target = CreateDamageTarget("ExplosionOnlyTarget", new Vector3(0f, 0f, 2f));
            var filter = target.gameObject.AddComponent<DamageFilter>();
            typeof(DamageFilter)
                .GetField("acceptedTypes", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(filter, DamageType.Explosion);
            typeof(DamageFilter)
                .GetField("acceptUnspecified", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(filter, false);
            Physics.SyncTransforms();

            data.damageType = DamageType.Bullet;
            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(0, target.HitCount,
                "Bullet damage must be blocked by an Explosion-only DamageFilter.");

            data.damageType = DamageType.Explosion;
            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(1, target.HitCount,
                "Explosion damage must pass an Explosion-only DamageFilter.");
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
        public void RequestFireServerRpc_RejectsBeforeMatchIsPlaying()
        {
            var matchGo = new GameObject("NetworkMatchStateManager");
            objectsToDestroy.Add(matchGo);
            matchGo.AddComponent<NetworkObject>();
            var matchManager = matchGo.AddComponent<NetworkMatchStateManager>();
            matchManager.SetStateForTests(NetworkMatchState.Warmup, Time.timeAsDouble);

            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 2;
            data.totalAmmo = 2;
            data.fireRate = 0f;

            fireHandler.InitializeServerWeaponStateForTests(2, 0);

            Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(2, fireHandler.ServerMagazineAmmo,
                "Warmup fire must be rejected before consuming authoritative ammo.");
        }

        [Test]
        public void RequestFireServerRpc_AppliesHitboxSegmentMultiplier()
        {
            var fireHandler = CreateServerFireHandler(out _, out WeaponData data);
            data.magazineSize = 1;
            data.totalAmmo = 1;
            data.fireRate = 0f;
            data.damage = 10f;
            data.damageType = DamageType.Bullet;
            data.hitMask = Physics.DefaultRaycastLayers;

            fireHandler.InitializeServerWeaponStateForTests(1, 0);

            var target = CreateDamageTarget("HeadSegmentTarget", new Vector3(0f, 0f, 2f));
            var segment = target.gameObject.AddComponent<HitboxSegment>();
            typeof(HitboxSegment)
                .GetField("zone", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(segment, HitboxZone.Head);
            Physics.SyncTransforms();

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.AreEqual(1, target.HitCount);
            Assert.AreEqual(20f, target.LastDamage.amount);
            Assert.AreEqual(HitboxZone.Head, target.LastDamage.hitZone);
            Assert.AreEqual(2f, target.LastDamage.damageMultiplier);
            Assert.True(target.LastDamage.isHeadshot);
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
        public void WeaponServerState_HandlesFireRateAndReloadRules()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 10;
            data.fireRate = 1f;
            data.reloadTime = 0f;

            var state = new WeaponServerState();
            state.EnsureInitialized(10, data);

            Assert.IsTrue(state.TryConsumeFire(data, 0.0));
            Assert.IsFalse(state.TryConsumeFire(data, 0.5));
            Assert.AreEqual(4, state.MagazineAmmo);

            state.InitializeForTests(10, 1, 4);
            Assert.IsTrue(state.TryBeginReload(data, 0.0));
            Assert.AreEqual(5, state.MagazineAmmo);
            Assert.AreEqual(0, state.ReserveAmmo);
        }

        [Test]
        public void WeaponServerState_DoesNotAcceptFireBeforeConfiguredCooldown()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 4;
            data.totalAmmo = 4;
            data.fireRate = 0.1f;

            var state = new WeaponServerState();
            state.EnsureInitialized(10, data);

            Assert.IsTrue(state.TryConsumeFire(data, 0.0));
            Assert.IsFalse(state.TryConsumeFire(data, 1.0 / 60.0));
            Assert.IsFalse(state.TryConsumeFire(data, 0.099f));
            Assert.IsTrue(state.TryConsumeFire(data, 0.1));
            Assert.AreEqual(2, state.MagazineAmmo);
        }

        [Test]
        public void Weapon_AuthoritativeAmmoSync_DoesNotClearLocalFireCooldown()
        {
            var weaponObject = new GameObject("WeaponCooldownSync");
            objectsToDestroy.Add(weaponObject);
            var weapon = weaponObject.AddComponent<Weapon>();
            FieldInfo canShootField = typeof(Weapon).GetField(
                "canShoot", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(canShootField);
            canShootField.SetValue(weapon, false);

            weapon.SetLocalAmmoState(29, 75, reloading: false);

            Assert.IsFalse((bool)canShootField.GetValue(weapon),
                "Authoritative ammo reconciliation must not bypass the local fire-rate cooldown.");
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

        private TestDamageReceiver CreateDamageTarget(string name, Vector3 position)
        {
            var targetGo = new GameObject(name);
            objectsToDestroy.Add(targetGo);
            targetGo.transform.position = position;
            targetGo.AddComponent<BoxCollider>();
            return targetGo.AddComponent<TestDamageReceiver>();
        }

        private sealed class TestDamageReceiver : MonoBehaviour, IAttributedDamageable
        {
            public int HitCount { get; private set; }
            public DamageInfo LastDamage { get; private set; }
            public bool IsDead => false;

            public void TakeDamage(float amount)
            {
                HitCount++;
                LastDamage = new DamageInfo(amount);
            }

            public void TakeDamage(DamageInfo damageInfo)
            {
                HitCount++;
                LastDamage = damageInfo;
            }
        }
    }
}
