using NUnit.Framework;
using UnityEngine;
using Unity.Netcode;
using System.Reflection;
using System.Collections.Generic;

namespace FPS.Tests
{
    public class PlayerHealthSecurityTests
    {
        [Test]
        public void TestPlayerHealth_ServerOnlyModifications()
        {
            // 1. Setup NetworkManager và giả lập các thành phần
            var nmGo = new GameObject("NetworkManager");
            var networkManager = nmGo.AddComponent<NetworkManager>();
            
            var singletonProp = typeof(NetworkManager).GetProperty("Singleton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            singletonProp.SetValue(null, networkManager);

            var connectionManagerField = typeof(NetworkManager).GetField("ConnectionManager", BindingFlags.Instance | BindingFlags.NonPublic);
            var connectionManager = connectionManagerField.GetValue(networkManager);

            var localClientField = connectionManager.GetType().GetField("LocalClient", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var localClient = localClientField.GetValue(connectionManager);

            var isServerProp = localClient.GetType().GetProperty("IsServer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            var playerGo = new GameObject("Player");
            var health = playerGo.AddComponent<PlayerHealth>();

            // Giả lập NetworkObject đã được Spawn
            var netObj = playerGo.AddComponent<NetworkObject>();
            typeof(NetworkObject).GetProperty("NetworkObjectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(netObj, 1111UL);
            var isSpawnedProp = typeof(NetworkObject).GetProperty("IsSpawned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            isSpawnedProp.SetValue(netObj, true);
            var ownerField = typeof(NetworkObject).GetField("NetworkManagerOwner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            ownerField.SetValue(netObj, networkManager);

            // Liên kết NetworkVariable với NetworkBehaviour để tránh cảnh báo/lỗi
            var networkHealthField = typeof(PlayerHealth).GetField("networkHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(networkHealthField, "Không tìm thấy trường networkHealth trong PlayerHealth");
            var networkHealth = (NetworkVariable<float>)networkHealthField.GetValue(health);
            networkHealth.Initialize(health);

            var networkIsDeadField = typeof(PlayerHealth).GetField("networkIsDead", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(networkIsDeadField, "Không tìm thấy trường networkIsDead trong PlayerHealth");
            var networkIsDead = (NetworkVariable<bool>)networkIsDeadField.GetValue(health);
            networkIsDead.Initialize(health);

            var nbIsServerProp = typeof(NetworkBehaviour).GetProperty("IsServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(nbIsServerProp, "Không tìm thấy IsServer trong NetworkBehaviour");

            try
            {
                // CASE 1: Chạy dưới quyền Server -> Cho phép thay đổi máu
                isServerProp.SetValue(localClient, true);
                nbIsServerProp.SetValue(health, true);
                
                // Khởi tạo máu
                networkHealth.Value = 50f;
                networkIsDead.Value = false;

                health.Heal(30f);
                Assert.AreEqual(80f, health.CurrentHealth, "Server phải hồi được máu");

                health.ResetHealth();
                Assert.AreEqual(100f, health.CurrentHealth, "Server phải reset được máu");

                // CASE 2: Chạy dưới quyền Client -> Gọi API không làm thay đổi máu
                isServerProp.SetValue(localClient, false);
                nbIsServerProp.SetValue(health, false);
                
                networkHealth.Value = 50f;
                health.Heal(30f);
                Assert.AreEqual(50f, health.CurrentHealth, "Client gọi Heal không được thay đổi máu");

                health.ResetHealth();
                Assert.AreEqual(50f, health.CurrentHealth, "Client gọi ResetHealth không được thay đổi máu");

                // CASE 3: Kiểm tra tính bảo mật: không được tồn tại các hàm RPC mạng cho phép client gọi Heal/Reset
                var healRpc = typeof(PlayerHealth).GetMethod("HealServerRpc", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var resetHealthRpc = typeof(PlayerHealth).GetMethod("ResetHealthServerRpc", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                Assert.Null(healRpc, "Hàm HealServerRpc phải bị xóa bỏ để chống hack");
                Assert.Null(resetHealthRpc, "Hàm ResetHealthServerRpc phải bị xóa bỏ để chống hack");
            }
            finally
            {
                singletonProp.SetValue(null, null);
                Object.DestroyImmediate(nmGo);
                Object.DestroyImmediate(playerGo);
            }
        }
    }
}
