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
            data.ApplyBakedFireInterval(0.001f);

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
            data.ApplyBakedFireInterval(10f);

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
            data.ApplyBakedFireInterval(0.001f);

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
            data.ApplyBakedFireInterval(0.001f);
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
            fireHandler.InitializeServerWeaponStateForTests(1, 0);

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
            data.ApplyBakedFireInterval(0.001f);
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
            fireHandler.InitializeServerWeaponStateForTests(1, 0);
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
            data.ApplyBakedFireInterval(0.001f);

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
            data.ApplyBakedFireInterval(0.001f);
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
            data.ApplyBakedAnimationTimings(0f, 0f, 0f, 0f, 0f, 0f);

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
            data.ApplyBakedFireInterval(1f);
            data.ApplyBakedAnimationTimings(0f, 0f, 0f, 0f, 0f, 0f);

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
        public void WeaponServerState_PerShellReloadInsertsAndSchedulesExactlyOneRoundPerCycle()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 9;
            data.reloadMode = ReloadMode.PerShell;
            data.reloadLoopStartFrame = 1;
            data.reloadLoopEndFrame = 3;
            data.ApplyBakedAnimationTimings(0f, 4f, 0f, 0.5f, 1f, 0f);

            Assert.AreEqual(1, data.GetPerShellRoundsToLoad(4, 1));
            Assert.AreEqual(5, data.GetPerShellRoundsToLoad(0, 9));

            var state = new WeaponServerState();
            state.InitializeForTests(10, 1, 4);

            Assert.IsTrue(state.TryBeginReload(data, 0.0));
            state.CompleteReloadIfReady(data, 1.49);
            Assert.AreEqual(1, state.MagazineAmmo);

            state.CompleteReloadIfReady(data, 1.5);
            Assert.AreEqual(2, state.MagazineAmmo);
            Assert.AreEqual(3, state.ReserveAmmo);
            Assert.IsTrue(state.IsReloading(2.0));

            state.CompleteReloadIfReady(data, 2.5);
            state.CompleteReloadIfReady(data, 3.5);
            state.CompleteReloadIfReady(data, 4.5);
            Assert.AreEqual(5, state.MagazineAmmo);
            Assert.AreEqual(0, state.ReserveAmmo);
            Assert.IsFalse(state.IsReloading(4.5));
        }

        [Test]
        public void WeaponServerState_PerShellReloadCanBeTerminatedByFire()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 9;
            data.reloadMode = ReloadMode.PerShell;
            data.ApplyBakedFireInterval(0.8f);
            data.ApplyBakedAnimationTimings(0f, 4f, 0f, 0.5f, 1f, 0f);

            var state = new WeaponServerState();
            state.InitializeForTests(10, 1, 4);
            Assert.IsTrue(state.TryBeginReload(data, 0.0));

            // Inserts commit at 1.5, 2.5 and 3.5 seconds. Firing while the
            // fourth shell is being loaded keeps the three completed inserts,
            // cancels the rest of reload, then consumes exactly one shell.
            Assert.IsTrue(state.TryConsumeFire(data, 3.6));
            Assert.AreEqual(3, state.MagazineAmmo);
            Assert.AreEqual(1, state.ReserveAmmo);
            Assert.IsFalse(state.IsReloading(3.6));
            Assert.Less(state.ReloadAmmoCommitTime, 0.0);
            Assert.Less(state.ReloadCompleteTime, 0.0);
        }

        [Test]
        public void WeaponServerState_MagazineReloadCannotBeTerminatedByFire()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 9;
            data.reloadMode = ReloadMode.Magazine;
            data.ApplyBakedFireInterval(0.1f);
            data.ApplyBakedAnimationTimings(0f, 2f, 1f, 0f, 0f, 0f);

            var state = new WeaponServerState();
            state.InitializeForTests(10, 1, 4);
            Assert.IsTrue(state.TryBeginReload(data, 0.0));
            Assert.IsFalse(state.TryConsumeFire(data, 0.5));
            Assert.AreEqual(1, state.MagazineAmmo);
            Assert.IsTrue(state.IsReloading(0.5));
        }

        [Test]
        public void WeaponServerState_RejectedRapidFireDoesNotTerminatePerShellReload()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 9;
            data.reloadMode = ReloadMode.PerShell;
            data.ApplyBakedFireInterval(0.8f);
            data.ApplyBakedAnimationTimings(0f, 4f, 0f, 0.5f, 1f, 0f);

            var state = new WeaponServerState();
            state.InitializeForTests(10, 1, 4, nextFireTime: 10.0);
            Assert.IsTrue(state.TryBeginReload(data, 0.0));

            Assert.IsFalse(state.TryConsumeFire(data, 3.6));
            Assert.AreEqual(4, state.MagazineAmmo,
                "Three completed shell inserts remain committed.");
            Assert.IsTrue(state.IsReloading(3.6),
                "A cooldown-rejected shot must not terminate the reload.");
        }

        [Test]
        public void WeaponServerState_MagazineAmmoCommitsBeforeReloadUnlockAndSurvivesSnapshot()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 10;
            data.reloadMode = ReloadMode.Magazine;
            data.ApplyBakedAnimationTimings(0f, 2f, 1f, 0f, 0f, 0f);

            var state = new WeaponServerState();
            state.InitializeForTests(10, 1, 4);
            Assert.IsTrue(state.TryBeginReload(data, 0.0));

            state.AdvanceReloadIfReady(data, 0.99);
            Assert.AreEqual(1, state.MagazineAmmo);
            state.AdvanceReloadIfReady(data, 1.0);
            Assert.AreEqual(5, state.MagazineAmmo, "Ammo must commit at the authored magazine-seat frame.");
            Assert.IsTrue(state.IsReloading(1.5), "Fire remains locked through the closing part of Reload.");

            WeaponRuntimeSnapshot snapshot = state.Capture(0, data);
            Assert.Less(snapshot.reloadAmmoCommitTime, 0.0);
            Assert.AreEqual(2.0, snapshot.reloadCompleteTime, 0.0001);
            var restored = new WeaponServerState();
            restored.Restore(snapshot, 10);
            restored.AdvanceReloadIfReady(data, 2.0);
            Assert.IsFalse(restored.IsReloading(2.0));
            Assert.AreEqual(5, restored.MagazineAmmo);
        }

        [Test]
        public void WeaponServerState_DoesNotAcceptFireBeforeConfiguredCooldown()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 4;
            data.totalAmmo = 4;
            data.ApplyBakedFireInterval(0.1f);

            var state = new WeaponServerState();
            state.EnsureInitialized(10, data);

            Assert.IsTrue(state.TryConsumeFire(data, 0.0));
            Assert.IsFalse(state.TryConsumeFire(data, 1.0 / 60.0));
            Assert.IsFalse(state.TryConsumeFire(data, 0.099f));
            Assert.IsTrue(state.TryConsumeFire(data, 0.1));
            Assert.AreEqual(2, state.MagazineAmmo);
        }

        [Test]
        public void WeaponServerState_EquipDeadlineBlocksFireAndReloadAndSurvivesSnapshot()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.magazineSize = 5;
            data.totalAmmo = 10;
            data.ApplyBakedAnimationTimings(1f, 2.5f, 1.5f, 0f, 0f, 0f);

            var state = new WeaponServerState();
            state.InitializeForTests(10, 4, 6, equipReadyTime: 11.0);
            Assert.IsFalse(state.TryConsumeFire(data, 10.5));
            Assert.IsFalse(state.TryBeginReload(data, 10.5));
            Assert.AreEqual(4, state.MagazineAmmo);

            WeaponRuntimeSnapshot snapshot = state.Capture(0, data);
            var restored = new WeaponServerState();
            restored.Restore(snapshot, 10);
            Assert.AreEqual(11.0, restored.EquipCompleteTime);
            Assert.IsTrue(restored.TryConsumeFire(data, 11.0));
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
