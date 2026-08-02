using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UniBT;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

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
            weaponData.fireRate = 10f;
            weaponData.reloadTime = 0f;

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
}
