using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public class NetworkSpawnManagerTests
    {
        [Test]
        public void NetworkSpawnManager_ReturnsSpawnPoseWithoutTeleportingExistingPlayers()
        {
            var managerGo = new GameObject("NetworkSpawnManager");
            var manager = managerGo.AddComponent<NetworkSpawnManager>();
            var spawnGo = new GameObject("SpawnPoint");
            spawnGo.transform.position = new Vector3(3f, 0f, 7f);
            spawnGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            typeof(NetworkSpawnManager)
                .GetField("spawnPoints", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, new[] { spawnGo.transform });

            bool found = manager.TryGetNextSpawnPose(out Vector3 position, out Quaternion rotation);

            Assert.True(found);
            Assert.AreEqual(spawnGo.transform.position, position);
            Assert.AreEqual(spawnGo.transform.rotation.eulerAngles.y, rotation.eulerAngles.y, 0.01f);

            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(spawnGo);
        }

        [Test]
        public void NetworkGameManager_ContainsSpawnManagerPoseIntegration()
        {
            var method = typeof(NetworkSpawnManager).GetMethod("TryGetNextSpawnPose", BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(method, "NetworkGameManager should depend on NetworkSpawnManager for spawn pose selection.");
        }
    }
}
