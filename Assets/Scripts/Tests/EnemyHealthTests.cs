using NUnit.Framework;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FPS.Tests
{
    public class EnemyTests
    {
        [Test]
        public void TestEnemyHealth_OnDeath_ReturnsZombieToPool()
        {
            // 1. Tạo NetworkManager giả lập qua Reflection để tránh khởi chạy socket/mạng thật
            var nmGo = new GameObject("NetworkManager");
            var networkManager = nmGo.AddComponent<NetworkManager>();
            
            // Thiết lập Singleton
            var singletonProp = typeof(NetworkManager).GetProperty("Singleton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(singletonProp, "Không tìm thấy property Singleton trong NetworkManager");
            singletonProp.SetValue(null, networkManager);

            // Thiết lập IsServer và IsListening qua ConnectionManager và LocalClient
            var connectionManagerField = typeof(NetworkManager).GetField("ConnectionManager", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(connectionManagerField, "Không tìm thấy ConnectionManager trong NetworkManager");
            var connectionManager = connectionManagerField.GetValue(networkManager);
            Assert.NotNull(connectionManager, "ConnectionManager đang bị null");

            var localClientField = connectionManager.GetType().GetField("LocalClient", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(localClientField, "Không tìm thấy LocalClient trong ConnectionManager");
            var localClient = localClientField.GetValue(connectionManager);
            Assert.NotNull(localClient, "LocalClient đang bị null");

            var isServerProp = localClient.GetType().GetProperty("IsServer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(isServerProp, "Không tìm thấy property IsServer trong NetworkClient");
            isServerProp.SetValue(localClient, true);

            var isListeningProp = connectionManager.GetType().GetProperty("IsListening", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(isListeningProp, "Không tìm thấy property IsListening trong NetworkConnectionManager");
            isListeningProp.SetValue(connectionManager, true);

            // Khởi tạo NetworkConfig để tránh NullReference khi kiểm tra RecycleNetworkIds
            networkManager.NetworkConfig = new NetworkConfig();

            // Khởi tạo và gán RealTimeProvider giả lập cho NetworkManager
            var realTimeProviderType = typeof(NetworkManager).Assembly.GetType("Unity.Netcode.RealTimeProvider");
            Assert.NotNull(realTimeProviderType, "Không tìm thấy type RealTimeProvider trong Netcode assembly");
            var realTimeProvider = System.Activator.CreateInstance(realTimeProviderType);
            var realTimeProviderProp = typeof(NetworkManager).GetProperty("RealTimeProvider", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(realTimeProviderProp, "Không tìm thấy property RealTimeProvider trong NetworkManager");
            realTimeProviderProp.SetValue(networkManager, realTimeProvider);

            // Khởi tạo và gán SpawnManager giả lập để xử lý Despawn
            var spawnManager = (Unity.Netcode.NetworkSpawnManager)System.Activator.CreateInstance(
                typeof(Unity.Netcode.NetworkSpawnManager),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { networkManager },
                null
            );
            var spawnManagerProp = typeof(NetworkManager).GetProperty("SpawnManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            spawnManagerProp.SetValue(networkManager, spawnManager);

            // 2. Tạo prefab giả lập
            var prefab = new GameObject("ZombiePrefab");
            var netPrefabObj = prefab.AddComponent<NetworkObject>();
            typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(netPrefabObj, 12345U);
            networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });

            // 3. Khởi tạo ZombiePoolManager giả lập
            var poolGo = new GameObject("ZombiePoolManager");
            var poolManager = poolGo.AddComponent<ZombiePoolManager>();
            
            // Gọi Awake() trên poolManager để thiết lập Singleton instance
            var poolAwake = typeof(ZombiePoolManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(poolAwake, "Không tìm thấy Awake trong ZombiePoolManager");
            poolAwake.Invoke(poolManager, null);

            // Khởi tạo pool với 5 đối tượng chờ
            poolManager.InitializePool(prefab, 5);

            // 4. Tạo thực thể zombie giả lập từ pool
            var zombieGo = Object.Instantiate(prefab);
            zombieGo.name = "ZombiePrefab"; // Đặt tên để trùng khớp với key trong pool
            var zombieNetObj = zombieGo.GetComponent<NetworkObject>();

            // Gán NetworkObjectId và IsSpawned = true
            typeof(NetworkObject).GetProperty("NetworkObjectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(zombieNetObj, 9999UL);
            var isSpawnedProp = typeof(NetworkObject).GetProperty("IsSpawned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(isSpawnedProp, "Không tìm thấy property IsSpawned trong NetworkObject");
            isSpawnedProp.SetValue(zombieNetObj, true);

            // Gán GlobalObjectIdHash phi-0 để Netcode nhận diện prefab handler
            typeof(NetworkObject).GetField("GlobalObjectIdHash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(zombieNetObj, 12345U);

            // Gán NetworkManagerOwner để tránh NullReferenceException khi gọi Despawn
            var ownerField = typeof(NetworkObject).GetField("NetworkManagerOwner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(ownerField, "Không tìm thấy field NetworkManagerOwner trong NetworkObject");
            ownerField.SetValue(zombieNetObj, networkManager);

            // Đăng ký zombie vào SpawnedObjects của SpawnManager qua Reflection để tránh lỗi biên dịch do khác biệt phiên bản
            var spawnedObjectsField = typeof(Unity.Netcode.NetworkSpawnManager).GetField("SpawnedObjects", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(spawnedObjectsField, "Không tìm thấy field SpawnedObjects trong NetworkSpawnManager");
            var spawnedObjects = (Dictionary<ulong, NetworkObject>)spawnedObjectsField.GetValue(spawnManager);
            spawnedObjects.Add(9999UL, zombieNetObj);

            var health = zombieGo.AddComponent<EnemyHealth>();
            
            // Cấu hình sử dụng pooling và tắt delay để chạy nhanh
            typeof(EnemyHealth).GetField("usePooling", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(health, true);
            typeof(EnemyHealth).GetField("destroyDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(health, 0f);

            try
            {
                // 5. Kích hoạt cái chết của zombie
                var dieMethod = typeof(EnemyHealth).GetMethod("Die", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(dieMethod, "Không tìm thấy hàm Die trong EnemyHealth");
                dieMethod.Invoke(health, null);

                // Chạy từng bước coroutine DespawnRoutine theo cách thủ công
                var despawnRoutine = typeof(EnemyHealth).GetMethod("DespawnRoutine", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(despawnRoutine, "Không tìm thấy hàm DespawnRoutine trong EnemyHealth");
                var enumerator = (IEnumerator)despawnRoutine.Invoke(health, new object[] { zombieNetObj, 0f });
                while (enumerator.MoveNext()) { }

                // 6. Kiểm chứng kết quả
                // KỲ VỌNG: Khi dùng pooling, zombie được trả về ZombiePoolManager (active = false và được xếp vào hàng đợi)
                // và GameObject KHÔNG được bị Destroy.
                Assert.NotNull(zombieGo, "GameObject của zombie không được bị Destroy khi dùng pooling");
                Assert.False(zombieGo.activeSelf, "Zombie GameObject phải được vô hiệu hóa (SetActive(false)) khi trả về pool");
                Assert.AreEqual(6, poolManager.GetPoolCount("ZombiePrefab"), "Zombie phải được đưa về hàng đợi pool (pool size tăng lên 6)");
            }
            finally
            {
                // Reset singleton
                singletonProp.SetValue(null, null);

                // Giải phóng các singleton khác để tránh rò rỉ bộ nhớ
                var poolInstanceField = typeof(SceneSingleton<ZombiePoolManager>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
                if (poolInstanceField != null)
                    poolInstanceField.SetValue(null, null);

                // 7. Cleanup
                Object.DestroyImmediate(nmGo);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(poolGo);
                if (zombieGo != null)
                {
                    Object.DestroyImmediate(zombieGo);
                }
            }
        }
    }
}
