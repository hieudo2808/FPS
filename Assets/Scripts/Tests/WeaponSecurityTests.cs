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
        [Test]
        public void TestWeaponManager_RequestFire_RejectsDistantShootPosition()
        {
            // 1. Setup NetworkManager
            var nmGo = new GameObject("NetworkManager");
            var networkManager = nmGo.AddComponent<NetworkManager>();
            
            var singletonProp = typeof(NetworkManager).GetProperty("Singleton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            singletonProp.SetValue(null, networkManager);

            var connectionManagerField = typeof(NetworkManager).GetField("ConnectionManager", BindingFlags.Instance | BindingFlags.NonPublic);
            var connectionManager = connectionManagerField.GetValue(networkManager);

            var localClientField = connectionManager.GetType().GetField("LocalClient", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var localClient = localClientField.GetValue(connectionManager);

            var isServerProp = localClient.GetType().GetProperty("IsServer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            isServerProp.SetValue(localClient, true);

            var isListeningProp = connectionManager.GetType().GetProperty("IsListening", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            isListeningProp.SetValue(connectionManager, true);

            // 2. Setup Player and WeaponManager
            var playerGo = new GameObject("Player");
            playerGo.transform.position = Vector3.zero; // Player is at (0, 0, 0)
            
            var netObj = playerGo.AddComponent<NetworkObject>();
            typeof(NetworkObject).GetProperty("NetworkObjectId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(netObj, 2222UL);
            var isSpawnedProp = typeof(NetworkObject).GetProperty("IsSpawned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            isSpawnedProp.SetValue(netObj, true);
            var ownerField = typeof(NetworkObject).GetField("NetworkManagerOwner", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            ownerField.SetValue(netObj, networkManager);

            var wm = playerGo.AddComponent<WeaponManager>();
            var nbIsServerProp = typeof(NetworkBehaviour).GetProperty("IsServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            nbIsServerProp.SetValue(wm, true);

            // Set __rpc_exec_stage to Execute (1) để chạy thân hàm RPC thay vì gửi qua mạng
            var execStageField = typeof(NetworkBehaviour).GetField("__rpc_exec_stage", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.NotNull(execStageField, "Không tìm thấy trường __rpc_exec_stage trong NetworkBehaviour");
            execStageField.SetValue(wm, 1); // 1 = Execute

            // Setup a mock weapon list to prevent null reference in RequestFireServerRpc
            var weaponsField = typeof(WeaponManager).GetField("weapons", BindingFlags.Instance | BindingFlags.NonPublic);
            var mockWeapons = new List<GameObject>();
            
            var weaponGo = new GameObject("MockWeapon");
            var weapon = weaponGo.AddComponent<Weapon>();
            
            // Create WeaponData and assign to weapon
            var weaponData = ScriptableObject.CreateInstance<WeaponData>();
            weaponData.damage = 10f;
            typeof(Weapon).GetField("weaponData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, weaponData);
            
            mockWeapons.Add(weaponGo);
            weaponsField.SetValue(wm, mockWeapons);

            // 3. Test Fire within limits (e.g. 2 meters away)
            // It should not log any "Rejecting fire request" warning
            wm.RequestFireServerRpc(new Vector3(0, 0, 2f), Vector3.forward);

            // 4. Test Fire outside limits (e.g. 10 meters away)
            // It should log a "Rejecting fire request" warning
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Rejecting fire request"));
            execStageField.SetValue(wm, 1); // 1 = Execute (Cần thiết lập lại vì Netcode tự động reset về Send sau mỗi cuộc gọi RPC)
            wm.RequestFireServerRpc(new Vector3(0, 0, 10f), Vector3.forward);

            // Clean up
            singletonProp.SetValue(null, null);
            Object.DestroyImmediate(nmGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(weaponGo);
            Object.DestroyImmediate(weaponData);
        }
    }
}
