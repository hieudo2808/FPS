using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    public class PrefabValidationTests
    {
        [Test]
        public void PlayerPrefab_HasMovementReferencesAndConfiguredWeaponSlots()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab");
            Assert.NotNull(prefab, "Player prefab should be loadable.");

            var movement = prefab.GetComponent<PlayerMovement>();
            Assert.NotNull(movement, "Player prefab should have PlayerMovement.");

            var movementSo = new SerializedObject(movement);
            Assert.NotNull(movementSo.FindProperty("visualRoot").objectReferenceValue,
                "PlayerMovement.visualRoot must be assigned for owner smoothing/reconciliation.");
            Assert.NotNull(movementSo.FindProperty("groundCheck").objectReferenceValue,
                "PlayerMovement.groundCheck must be assigned for robust grounded checks.");

            var weaponManager = prefab.GetComponent<WeaponManager>();
            Assert.NotNull(weaponManager, "Player prefab should have WeaponManager.");

            var weapons = new SerializedObject(weaponManager).FindProperty("weapons");
            Assert.Greater(weapons.arraySize, 0, "Player prefab should have at least one configured weapon slot.");

            for (int i = 0; i < weapons.arraySize; i++)
            {
                var weaponGo = weapons.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                Assert.NotNull(weaponGo, $"Weapon slot {i} must reference a weapon GameObject.");

                var weapon = weaponGo.GetComponent<Weapon>();
                Assert.NotNull(weapon, $"Weapon slot {i} must have a Weapon component.");

                var weaponSo = new SerializedObject(weapon);
                Assert.NotNull(weaponSo.FindProperty("weaponData").objectReferenceValue,
                    $"Weapon slot {i} must have WeaponData.");
                Assert.NotNull(weaponSo.FindProperty("bulletPool").objectReferenceValue,
                    $"Weapon slot {i} should use the shared visual bullet pool instead of Instantiate/Destroy fallback.");
                Assert.NotNull(weaponSo.FindProperty("fpsArmsAnimator").objectReferenceValue,
                    $"Weapon slot {i} should drive the FPS arms animator for reloads.");
            }
        }

        [Test]
        public void EnemyPrefabs_HaveRequiredNetworkAndNavigationComponents()
        {
            string[] prefabPaths =
            {
                "Assets/FPS/Features/Characters/Content/Enemies/ZombiegirlW/Prefabs/Zombiegirl W Kurniawan.prefab",
                "Assets/FPS/Features/Characters/Content/Enemies/Copzombie/Prefabs/copzombie_l_actisdato.prefab",
                "Assets/FPS/Features/Characters/Content/Enemies/Screamer/Prefabs/BookHeadMonster_withBlood.prefab"
            };

            foreach (string path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, $"{path} should be loadable.");
                Assert.NotNull(prefab.GetComponent<NetworkObject>(), $"{path} must have NetworkObject.");
                Assert.NotNull(prefab.GetComponent<NavMeshAgent>(), $"{path} must have NavMeshAgent.");
                Assert.NotNull(prefab.GetComponent<Animator>(), $"{path} must have Animator.");
                Assert.NotNull(prefab.GetComponent<Collider>(), $"{path} must have root Collider.");
                Assert.NotNull(prefab.GetComponent<EnemyHealth>(), $"{path} must have EnemyHealth.");
                Assert.NotNull(prefab.GetComponent<NetworkTransform>(), $"{path} must have NetworkTransform on the prefab, not added at runtime.");

                Bounds bounds = CalculateRendererBounds(prefab);
                Assert.Greater(bounds.size.y, 1.0f, $"{path} renderer bounds should be humanoid scale.");
            }
        }

        [Test]
        public void EnemyPrefabs_HaveHitboxSegmentsAndLagCompensationTarget()
        {
            string[] prefabPaths =
            {
                "Assets/FPS/Features/Characters/Content/Enemies/ZombiegirlW/Prefabs/Zombiegirl W Kurniawan.prefab",
                "Assets/FPS/Features/Characters/Content/Enemies/Copzombie/Prefabs/copzombie_l_actisdato.prefab",
                "Assets/FPS/Features/Characters/Content/Enemies/Screamer/Prefabs/BookHeadMonster_withBlood.prefab"
            };

            foreach (string path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, $"{path} should be loadable.");
                Assert.NotNull(prefab.GetComponent<LagCompensatedTarget>(),
                    $"{path} must have LagCompensatedTarget for server rewind.");

                var segments = prefab.GetComponentsInChildren<HitboxSegment>(true);
                Assert.GreaterOrEqual(segments.Length, 3,
                    $"{path} must have at least body/head/chest HitboxSegments.");

                bool hasHead = false, hasBody = false, hasChest = false;
                foreach (var segment in segments)
                {
                    Assert.NotNull(segment.GetComponent<Collider>(),
                        $"{path} segment {segment.name} must have a collider.");
                    var collider = segment.GetComponent<Collider>();
                    Assert.False(collider.isTrigger,
                        $"{path} segment {segment.name} must not be a trigger (weapon raycast ignores triggers).");

                    switch (segment.Zone)
                    {
                        case HitboxZone.Head:
                            hasHead = true;
                            Assert.AreEqual(2f, segment.DamageMultiplier,
                                $"{path} head segment should use x2 multiplier.");
                            break;
                        case HitboxZone.Body:
                            hasBody = true;
                            break;
                        case HitboxZone.Chest:
                            hasChest = true;
                            break;
                    }
                }

                Assert.True(hasHead, $"{path} must have a Head segment.");
                Assert.True(hasBody, $"{path} must have a Body segment.");
                Assert.True(hasChest, $"{path} must have a Chest segment.");
            }
        }

        [Test]
        public void GameScene_HasMatchStateAndLagCompensationManagers()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity", OpenSceneMode.Single);

            NetworkMatchStateManager matchManager = null;
            LagCompensationManager lagManager = null;
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene != scene)
                    continue;

                matchManager ??= go.GetComponent<NetworkMatchStateManager>();
                lagManager ??= go.GetComponent<LagCompensationManager>();
            }

            Assert.NotNull(matchManager, "GameScene must contain a NetworkMatchStateManager for match flow.");
            Assert.NotNull(matchManager.GetComponent<NetworkObject>(),
                "NetworkMatchStateManager must sit on an in-scene NetworkObject so NetworkVariables sync.");
            Assert.NotNull(lagManager, "GameScene must contain a LagCompensationManager for server rewind.");
            Assert.NotNull(lagManager.GetComponent<NetworkObject>(),
                "LagCompensationManager must sit on an in-scene NetworkObject.");
        }

        [Test]
        public void GameScene_HasNoDebugDefaultsOrInactiveWeaponManagerLeftovers()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity", OpenSceneMode.Single);

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene != scene)
                    continue;

                Assert.False(go.name == "WeaponManager" && !go.activeSelf,
                    "GameScene should not keep an inactive standalone WeaponManager with null refs.");

                AssertSerializedBoolFalse<ZombiePoolManager>(go, "showDebugLogs");
                AssertSerializedBoolFalse<SpecialInfectedRegistry>(go, "showDebugLogs");
                AssertSerializedBoolFalse<InfluenceMapManager>(go, "showDebugGizmos");
                AssertSerializedBoolFalse<AttackSlotManager>(go, "showDebugGizmos");
            }
        }

        private static Bounds CalculateRendererBounds(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, $"{prefab.name} should have at least one renderer.");

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static void AssertSerializedBoolFalse<TComponent>(GameObject go, string propertyName)
            where TComponent : Component
        {
            var component = go.GetComponent<TComponent>();
            if (component == null)
                return;

            var property = new SerializedObject(component).FindProperty(propertyName);
            Assert.NotNull(property, $"{typeof(TComponent).Name}.{propertyName} should remain serialized.");
            Assert.False(property.boolValue, $"{typeof(TComponent).Name}.{propertyName} should be disabled by default in GameScene.");
        }
    }
}
