using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UniBT;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FPS.PlayModeTests
{
    public class CoreGameplayPlayModeIntegrationTests
    {
        private readonly List<Object> objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (objectsToDestroy[i] != null)
                    Object.Destroy(objectsToDestroy[i]);
            }

            objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator PlayerMovement_RuntimeInputSanitizesAndMovesCharacter()
        {
            GameObject player = CreateGameObject("PlayerMovementRuntime");
            player.AddComponent<NetworkObject>();
            CharacterController controller = player.AddComponent<CharacterController>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            SetPrivateField(movement, "controller", controller);

            yield return null;

            Vector3 start = player.transform.position;
            var input = new PlayerInputPayload
            {
                tick = 0,
                move = new Vector2(20f, 0f),
                jumpPressed = false,
                sprint = true,
                yaw = 450f
            };

            Assert.IsTrue(movement.SimulateInputForTests(input, 0.2f));
            Assert.Greater(Vector3.Distance(start, player.transform.position), 0.1f,
                "Runtime player movement should apply sanitized movement through CharacterController.");
            Assert.AreEqual(90f, player.transform.eulerAngles.y, 0.1f,
                "Runtime player movement should normalize hostile yaw values before applying them.");

            input.move = new Vector2(float.NaN, 0f);
            Assert.IsFalse(movement.SimulateInputForTests(input, 0.2f),
                "Runtime player movement should reject NaN input instead of applying it.");
        }

        [UnityTest]
        public IEnumerator WeaponRuntime_ServerStateControlsFireReloadAndPickupAmmo()
        {
            WeaponFireHandler fireHandler = CreateRuntimeWeaponRig(out _, out WeaponData weaponData);
            weaponData.magazineSize = 3;
            weaponData.totalAmmo = 9;
            weaponData.ApplyBakedFireInterval(10f);
            weaponData.ApplyBakedAnimationTimings(0f, 0f, 0f, 0f, 0f, 0f);

            fireHandler.InitializeServerWeaponStateForTests(3, 0);

            Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
            Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward),
                "Runtime weapon fire should reject shots before server fire-rate cooldown.");
            Assert.AreEqual(2, fireHandler.ServerMagazineAmmo);

            fireHandler.InitializeServerWeaponStateForTests(0, 6);
            Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward),
                "Runtime weapon fire should reject shots when authoritative server magazine is empty.");

            Assert.IsTrue(fireHandler.BeginServerReloadForTests());
            fireHandler.CompleteServerReloadIfReadyForTests();
            Assert.AreEqual(3, fireHandler.ServerMagazineAmmo);
            Assert.AreEqual(3, fireHandler.ServerReserveAmmo);

            fireHandler.AddReserveAmmoServer(5);
            Assert.AreEqual(8, fireHandler.ServerReserveAmmo,
                "Runtime ammo pickup path should update server-side reserve ammo.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator WeaponPickup_ReplacesPrimaryButKeepsClassicAndRejectsDuplicate()
        {
            GameObject player = CreateGameObject("WeaponPickupPlayer");
            NetworkObject playerNetworkObject = player.AddComponent<NetworkObject>();
            WeaponManager manager = player.AddComponent<WeaponManager>();

            var candidates = new List<PrimaryWeaponCandidate>();
            GameObject vandal = CreateTestWeapon("Vandal");
            GameObject classic = CreateTestWeapon("Classic");
            GameObject odin = null;
            foreach (PrimaryWeaponId id in System.Enum.GetValues(typeof(PrimaryWeaponId)))
            {
                GameObject weapon = id == PrimaryWeaponId.Vandal
                    ? vandal
                    : CreateTestWeapon(id.ToString());
                if (id == PrimaryWeaponId.Odin)
                    odin = weapon;

                var candidate = new PrimaryWeaponCandidate();
                SetPrivateField(candidate, "id", id);
                SetPrivateField(candidate, "weaponObject", weapon);
                candidates.Add(candidate);
            }

            SetPrivateField(manager, "weapons", new List<GameObject> { vandal, classic });
            SetPrivateField(manager, "primaryWeaponCandidates", candidates);

            PickupItem firstPickup = CreateWeaponPickup(PrimaryWeaponId.Odin);
            Assert.AreEqual(PickupResultCode.Accepted, firstPickup.TryClaimServer(playerNetworkObject));
            Assert.AreEqual(PrimaryWeaponId.Odin, manager.ActivePrimaryWeaponId);
            Assert.AreSame(odin, manager.GetWeapon(0).gameObject);
            Assert.AreSame(classic, manager.GetWeapon(1).gameObject);
            Assert.False(firstPickup.CanInteract);

            PickupItem duplicatePickup = CreateWeaponPickup(PrimaryWeaponId.Odin);
            Assert.AreEqual(PickupResultCode.InventoryFull, duplicatePickup.TryClaimServer(playerNetworkObject));
            Assert.True(duplicatePickup.CanInteract,
                "A duplicate weapon pickup must remain available for another player.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bucky_ServerShotConsumesOneShellAndAppliesEightPellets()
        {
            WeaponFireHandler fireHandler = CreateRuntimeWeaponRig(out _, out WeaponData data);
            data.magazineSize = 5;
            data.totalAmmo = 45;
            data.damage = 18.75f;
            data.projectileCount = 8;
            data.hipSpreadAngle = 4f;
            data.maximumRange = 25f;
            data.falloffStartDistance = 8f;
            data.falloffEndDistance = 18f;
            data.minimumDamageMultiplier = 0.15f;
            data.hitMask = 1 << 0;
            data.ApplyBakedFireInterval(0.001f);
            fireHandler.InitializeServerWeaponStateForTests(5, 40);

            GameObject targetObject = CreateGameObject("BuckyCloseRangeTarget");
            targetObject.transform.position = new Vector3(0f, 0f, 2f);
            BoxCollider collider = targetObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 2f, 0.2f);
            AccumulatingDamageTarget target = targetObject.AddComponent<AccumulatingDamageTarget>();
            Physics.SyncTransforms();

            Assert.True(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward, fireSequence: 7));
            Assert.AreEqual(4, fireHandler.ServerMagazineAmmo,
                "A shotgun shot consumes one shell, not one shell per pellet.");
            Assert.AreEqual(8, target.HitCount);
            Assert.AreEqual(150f, target.TotalDamage, 0.01f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator VisualBulletProjectile_MovesAndAlignsAuthoredTipWithShotDirection()
        {
            GameObject bullet = CreateGameObject("RuntimeVisualBullet");
            VisualBulletProjectile projectile = bullet.AddComponent<VisualBulletProjectile>();
            Vector3 start = new Vector3(1f, 2f, 3f);
            Vector3 direction = new Vector3(0.2f, 0.1f, 1f).normalized;

            projectile.Launch(start, direction, 20f, 1f, null);

            Assert.That(
                Vector3.Angle(bullet.transform.TransformDirection(projectile.LocalForwardAxis), direction),
                Is.LessThan(0.01f),
                "The mesh-authored bullet tip axis must point along the resolved shot direction.");

            yield return null;

            Vector3 displacement = bullet.transform.position - start;
            Assert.Greater(Vector3.Dot(displacement, direction), 0f,
                "A visual bullet must advance after it is launched even though the prefab has no Rigidbody.");
            Assert.That(Vector3.Cross(displacement.normalized, direction).magnitude, Is.LessThan(0.001f),
                "The visual bullet must travel along the same direction used to orient it.");
        }

        [UnityTest]
        public IEnumerator OperatorScope_TransitionsOwnerCameraAndRestoresPresentation()
        {
            GameObject player = CreateGameObject("OperatorScopePlayer");
            player.AddComponent<NetworkObject>();
            MouseMovement mouseMovement = player.AddComponent<MouseMovement>();

            Camera bodyCamera = CreateGameObject("BodyCamera").AddComponent<Camera>();
            bodyCamera.transform.SetParent(player.transform);
            bodyCamera.fieldOfView = 60f;
            Camera weaponCamera = CreateGameObject("WeaponCamera").AddComponent<Camera>();
            weaponCamera.transform.SetParent(player.transform);
            weaponCamera.enabled = true;
            SetPrivateField(mouseMovement, "bodyCam", bodyCamera);
            SetPrivateField(mouseMovement, "weaponCam", weaponCamera);

            GameObject hand = CreateGameObject("Hand");
            hand.transform.SetParent(player.transform);
            hand.transform.localPosition = new Vector3(0.25f, -0.5f, 0.8f);
            hand.transform.localRotation = Quaternion.Euler(0f, 90f, -2f);
            Vector3 hipPosition = hand.transform.localPosition;
            Quaternion hipRotation = hand.transform.localRotation;
            GameObject armsModel = CreateGameObject("FP_Core_NewFemale_Skelmesh.ao");
            armsModel.transform.SetParent(hand.transform, false);
            Animator armsAnimator = armsModel.AddComponent<Animator>();

            GameObject weaponObject = CreateGameObject("OperatorRuntime");
            weaponObject.transform.SetParent(hand.transform, false);
            GameObject sightObject = CreateGameObject("ScopeTargetSocket");
            sightObject.transform.SetParent(weaponObject.transform, false);
            sightObject.transform.localPosition = new Vector3(0.1f, 0.05f, 0.6f);
            GameObject sightEndObject = CreateGameObject("ScopeTargetSocket_end");
            sightEndObject.transform.SetParent(sightObject.transform, false);
            sightEndObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            Weapon weapon = weaponObject.AddComponent<Weapon>();
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(data);
            data.supportsAim = true;
            data.aimedWorldFov = 25f;
            data.aimTransitionDuration = 0.12f;
            data.aimedSensitivityMultiplier = 0.65f;
            data.showScopeOverlay = true;
            data.hideViewmodelWhenAimed = false;
            SetPrivateField(weapon, "weaponData", data);
            SetPrivateField(weapon, "aimSight", sightObject.transform);
            SetPrivateField(weapon, "aimSightEnd", sightEndObject.transform);
            weapon.BindFirstPersonPresentation(bodyCamera, armsAnimator);
            weapon.SetOwner(true);

            MethodInfo applyAim = typeof(Weapon).GetMethod(
                "ApplyAimPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(applyAim);

            MethodInfo resolveHeldAim = typeof(Weapon).GetMethod(
                "ResolveHeldAimRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(resolveHeldAim);
            Assert.True((bool)resolveHeldAim.Invoke(weapon, new object[] { true }),
                "Holding RMB must request ADS.");

            applyAim.Invoke(weapon, new object[] { true, 0.12f });
            Assert.AreEqual(25f, bodyCamera.fieldOfView, 0.01f);
            Assert.AreEqual(0.65f, mouseMovement.LookSensitivityMultiplier, 0.001f);
            Assert.False(weaponCamera.enabled,
                "The physical raise animation finishes first, then the scope HUD must hide the viewmodel camera.");
            Assert.True(weapon.IsAiming);
            Assert.AreNotEqual(hipPosition, hand.transform.localPosition,
                "Physical ADS must move the whole Hand viewmodel root, not only change FOV.");
            Assert.That(Quaternion.Angle(hipRotation, hand.transform.localRotation), Is.LessThan(0.001f),
                "Authored sight axes validate ADS, but must never rotate the user-calibrated Hand pose.");

            MethodInfo beginExitAfterShot = typeof(Weapon).GetMethod(
                "BeginExitAimAfterShot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(beginExitAfterShot);
            beginExitAfterShot.Invoke(weapon, null);
            Assert.False(weapon.IsAimRequested,
                "An Operator shot must lower ADS even while RMB is still held.");
            Assert.False((bool)resolveHeldAim.Invoke(weapon, new object[] { true }),
                "Operator must not immediately re-scope until RMB is released.");
            Assert.False((bool)resolveHeldAim.Invoke(weapon, new object[] { false }));
            Assert.True((bool)resolveHeldAim.Invoke(weapon, new object[] { true }),
                "RMB may request ADS again after a physical release.");
            Assert.True(weapon.IsAiming,
                "The scope HUD must remain during the first lowering frames to avoid a camera pop.");
            Assert.False(weaponCamera.enabled);

            applyAim.Invoke(weapon, new object[] { false, 0.03f });
            Assert.True(weapon.IsAiming);
            Assert.False(weaponCamera.enabled,
                "Do not reveal the opaque 3D optic while it is still centered.");

            applyAim.Invoke(weapon, new object[] { false, 0.03f });
            Assert.False(weapon.IsAiming);
            Assert.True(weaponCamera.enabled,
                "Reveal the viewmodel only after it has moved clear of the target.");

            applyAim.Invoke(weapon, new object[] { false, 0.06f });
            Assert.AreEqual(60f, bodyCamera.fieldOfView, 0.01f);
            Assert.AreEqual(1f, mouseMovement.LookSensitivityMultiplier, 0.001f);
            Assert.True(weaponCamera.enabled);
            Assert.False(weapon.IsAiming);
            Assert.AreEqual(hipPosition, hand.transform.localPosition,
                "Lowering ADS must restore the exact authored hip pose.");
            Assert.That(Quaternion.Angle(hipRotation, hand.transform.localRotation), Is.LessThan(0.001f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator AimHud_DistinguishesVandalIronSightFromOperatorScope()
        {
            GameObject canvasObject = CreateGameObject("AimHudCanvas");
            canvasObject.AddComponent<Canvas>();
            GameObject crosshair = new GameObject("Crosshair", typeof(RectTransform));
            objectsToDestroy.Add(crosshair);
            crosshair.transform.SetParent(canvasObject.transform, false);

            GameObject hudObject = CreateGameObject("AimHudManager");
            HUDManager hud = hudObject.AddComponent<HUDManager>();
            SetPrivateField(hud, "crosshairRoot", crosshair);
            yield return null;

            hud.SetAimHudVisible(true, false);
            Assert.False(crosshair.activeSelf,
                "Vandal physical ADS hides the hip crosshair.");
            Transform scope = canvasObject.transform.Find("OperatorScopeOverlay");
            Assert.True(scope == null || !scope.gameObject.activeSelf,
                "Vandal must not show the Operator sniper mask.");

            hud.SetAimHudVisible(true, true);
            scope = canvasObject.transform.Find("OperatorScopeOverlay");
            Assert.NotNull(scope,
                "Operator must create its owner-only scope HUD when ADS completes.");
            Assert.True(scope.gameObject.activeSelf);
            Assert.NotNull(scope.Find("ScopeArtwork"));
            Assert.NotNull(scope.Find("ScopeArtwork").GetComponent<Image>());
            Assert.Null(scope.Find("MaskTop"));
            Assert.Null(scope.Find("MaskBottom"));
            Assert.Null(scope.Find("MaskLeft"));
            Assert.Null(scope.Find("MaskRight"));

            hud.SetAimHudVisible(false, false);
            Assert.True(crosshair.activeSelf);
            Assert.False(scope.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator SpecialInfectedRuntime_RegistryOnlySpawnsPlayableScreamer()
        {
            GameObject registryGo = CreateGameObject("SpecialRegistryRuntime");
            SpecialInfectedRegistry registry = registryGo.AddComponent<SpecialInfectedRegistry>();

            GameObject screamerPrefab = CreateGameObject("ScreamerPlayablePrefab");
            screamerPrefab.AddComponent<NetworkObject>();
            screamerPrefab.AddComponent<SI_Screamer>();

            GameObject frameworkOnlyPrefab = CreateGameObject("FrameworkOnlySpecialPrefab");
            frameworkOnlyPrefab.AddComponent<NetworkObject>();
            frameworkOnlyPrefab.AddComponent<SI_Stalker>();

            yield return null;

            Assert.IsFalse(registry.RegisterPlayableSpecialPrefab(SpecialType.Stalker, frameworkOnlyPrefab),
                "Framework-only special infected should not be promoted to live playable spawn.");
            Assert.IsTrue(registry.RegisterPlayableSpecialPrefab(SpecialType.Screamer, screamerPrefab));

            GameObject spawned = registry.SpawnSpecial(new Vector3(3f, 0f, 0f));
            objectsToDestroy.Add(spawned);

            Assert.NotNull(spawned, "Runtime special registry should spawn when a playable Screamer prefab is registered.");
            Assert.NotNull(spawned.GetComponent<SI_Screamer>());
            Assert.AreEqual(1, registry.AliveSpecialCount);
        }

        [UnityTest]
        public IEnumerator ScreamerRuntime_DisablesLegacyBehaviorTreeAndTelegraphsScream()
        {
            GameObject screamerGo = CreateGameObject("RuntimeScreamer");
            screamerGo.SetActive(false);
            screamerGo.AddComponent<NetworkObject>();
            BehaviorTree behaviorTree = screamerGo.AddComponent<BehaviorTree>();
#if UNITY_EDITOR
            // Give the legacy tree a valid root before Awake. Otherwise UniBT logs
            // "has no root child" before SI_Screamer.Start disables the legacy brain,
            // polluting an otherwise valid lifecycle test with a fixture warning.
            behaviorTree.Root.Child = new Selector();
#endif
            SI_Screamer screamer = screamerGo.AddComponent<SI_Screamer>();
            screamerGo.SetActive(true);

            yield return null;

            Assert.IsFalse(behaviorTree.enabled,
                "Runtime Screamer should disable the legacy UniBT brain and use its single server-authoritative brain.");

            screamer.UseAbility();
            yield return null;

            Assert.IsTrue(screamer.IsScreaming,
                "Runtime Screamer should enter its scream telegraph state when its ability is used.");
        }

        private WeaponFireHandler CreateRuntimeWeaponRig(out WeaponManager weaponManager, out WeaponData weaponData)
        {
            GameObject player = CreateGameObject("RuntimeWeaponPlayer");
            player.AddComponent<NetworkObject>();
            weaponManager = player.AddComponent<WeaponManager>();
            WeaponFireHandler fireHandler = player.AddComponent<WeaponFireHandler>();
            SetNetworkServer(weaponManager, true);
            SetNetworkServer(fireHandler, true);

            GameObject weaponObject = CreateGameObject("RuntimeWeapon");
            weaponObject.transform.SetParent(player.transform);
            Weapon weapon = weaponObject.AddComponent<Weapon>();
            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            objectsToDestroy.Add(weaponData);

            SetPrivateField(weapon, "weaponData", weaponData);
            SetPrivateField(weaponManager, "weapons", new List<GameObject> { weaponObject });

            return fireHandler;
        }

        private GameObject CreateTestWeapon(string weaponName)
        {
            GameObject weaponObject = CreateGameObject(weaponName);
            Weapon weapon = weaponObject.AddComponent<Weapon>();
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            data.weaponName = weaponName;
            data.magazineSize = 10;
            data.totalAmmo = 30;
            objectsToDestroy.Add(data);
            SetPrivateField(weapon, "weaponData", data);
            return weaponObject;
        }

        private PickupItem CreateWeaponPickup(PrimaryWeaponId primaryWeaponId)
        {
            GameObject pickupObject = CreateGameObject(primaryWeaponId + "Pickup");
            pickupObject.AddComponent<NetworkObject>();
            PickupItem pickup = pickupObject.AddComponent<PickupItem>();
            SetNetworkServer(pickup, true);
            SetPrivateField(pickup, "pickupType", PickupType.Weapon);
            SetPrivateField(pickup, "primaryWeaponId", primaryWeaponId);
            return pickup;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }

        private static void SetNetworkServer(NetworkBehaviour behaviour, bool isServer)
        {
            PropertyInfo property = typeof(NetworkBehaviour).GetProperty(
                "IsServer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            property.SetValue(behaviour, isServer);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }

    public sealed class AccumulatingDamageTarget : MonoBehaviour, IDamageable
    {
        public int HitCount { get; private set; }
        public float TotalDamage { get; private set; }
        public bool IsDead => false;

        public void TakeDamage(float amount)
        {
            HitCount++;
            TotalDamage += amount;
        }
    }
}
