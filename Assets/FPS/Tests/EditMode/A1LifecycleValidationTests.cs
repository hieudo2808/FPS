using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Tests
{
    public sealed class A1LifecycleValidationTests
    {
        [Test]
        public void PlayerPrefab_IsRegisteredInDefaultNetworkPrefabsList_AndHasDisabledListener()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab");
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                "Assets/DefaultNetworkPrefabs.asset");

            Assert.NotNull(playerPrefab);
            Assert.NotNull(list);
            Assert.True(list.Contains(playerPrefab), "PlayerPrefab must be in the configured NetworkPrefabsList.");

            AudioListener listener = playerPrefab.GetComponentInChildren<AudioListener>(true);
            Assert.NotNull(listener, "Player prefab must contain an AudioListener for local ownership.");
            Assert.False(listener.enabled, "AudioListener must be disabled before NetworkObject spawn.");
            Assert.NotNull(playerPrefab.GetComponent<NetworkObject>());
        }

        [Test]
        public void ConcreteUniBtNodes_AreSerializableForSerializeReference()
        {
            Type nodeType = typeof(UniBT.NodeBehavior);
            Type[] concreteTypes = TypeCache.GetTypesDerivedFrom(nodeType)
                .Where(type => type != null && !type.IsAbstract && !type.IsInterface)
                .ToArray();

            Assert.That(concreteTypes, Is.Not.Empty);
            foreach (Type concreteType in concreteTypes)
            {
                Assert.NotNull(Attribute.GetCustomAttribute(concreteType, typeof(SerializableAttribute)),
                    $"{concreteType.FullName} must be [Serializable] when stored by SerializeReference.");
            }
        }

        [Test]
        public void MainMenuNetworkManager_ReferencesRegisteredPlayerPrefabAndSceneManagement()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/MainMenu.unity", OpenSceneMode.Single);
            NetworkManager manager = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                manager = root.GetComponentInChildren<NetworkManager>(true);
                if (manager != null)
                    break;
            }

            Assert.NotNull(manager);
            Assert.NotNull(manager.NetworkConfig.PlayerPrefab);
            Assert.NotNull(manager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>());
            Assert.True(manager.NetworkConfig.EnableSceneManagement);
            Assert.Greater(manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Count, 0);
            Assert.True(manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Any(list =>
                list != null && list.Contains(manager.NetworkConfig.PlayerPrefab)));
        }

        [Test]
        public void NetworkMatchState_TestSeamDoesNotRequireSpawnedNetworkObject()
        {
            GameObject gameObject = new GameObject("A1NetworkMatchStateTest");
            try
            {
                gameObject.AddComponent<NetworkObject>();
                NetworkMatchStateManager manager = gameObject.AddComponent<NetworkMatchStateManager>();
                manager.SetStateForTests(NetworkMatchState.Warmup, 10.0);
                Assert.AreEqual(NetworkMatchState.Warmup, manager.State);
                Assert.AreEqual(10.0, manager.StateStartedServerTime, 0.001);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ZombieNetworkPoolHandler_ReturnsActiveObjectForNetworkSynchronization()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Enemies/ZombiegirlW/Prefabs/Zombiegirl W Kurniawan.prefab");
            Assert.NotNull(prefab);
            Assert.NotNull(prefab.GetComponent<NetworkObject>());

            GameObject poolObject = new GameObject("A1ZombiePool");
            ZombiePoolManager pool = poolObject.AddComponent<ZombiePoolManager>();
            try
            {
                pool.InitializePool(prefab, 1);
                var handler = new ZombieNetworkPoolHandler(prefab, pool);
                NetworkObject spawned = handler.Instantiate(NetworkManager.ServerClientId, Vector3.zero, Quaternion.identity);

                Assert.NotNull(spawned);
                Assert.True(spawned.gameObject.activeSelf,
                    "A pooled NetworkObject must be active before NGO synchronizes NetworkBehaviours.");

                handler.Destroy(spawned);
                Assert.False(spawned.gameObject.activeSelf,
                    "Destroy through the pool handler must return the object inactive.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(poolObject);
            }
        }

        [Test]
        public void ObjectPooling_ExpandsForTransientBurstWithoutDroppingObject()
        {
            GameObject prefab = new GameObject("A1PoolPrefab");
            GameObject poolObject = new GameObject("A1ObjectPool");
            poolObject.SetActive(false);

            try
            {
                ObjectPooling pool = poolObject.AddComponent<ObjectPooling>();
                SetPrivateField(pool, "objectPrefab", prefab);
                SetPrivateField(pool, "poolSize", 1);
                poolObject.SetActive(true);

                GameObject first = pool.GetObject();
                GameObject expanded = pool.GetObject();

                Assert.NotNull(first);
                Assert.NotNull(expanded,
                    "A transient visual-effects burst must expand the warm pool instead of dropping the object.");
                Assert.True(first.activeSelf);
                Assert.True(expanded.activeSelf);

                pool.ReturnObject(first);
                pool.ReturnObject(expanded);
                Assert.AreEqual(2, pool.AvailableCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(poolObject);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
