using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace FPS.Tests
{
    public class MatchFlowTests
    {
        private readonly List<Object> objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in objectsToDestroy)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            objectsToDestroy.Clear();
            InputManager.MatchInputBlocked = false;
            InputManager.GameplayInputBlocked = false;
        }

        [Test]
        public void NetworkMatchStateManager_BlocksInputOutsidePlaying()
        {
            NetworkMatchStateManager manager = CreateMatchStateManager();

            manager.SetStateForTests(NetworkMatchState.Loading, Time.timeAsDouble);
            Assert.False(NetworkMatchStateManager.IsGameplayActive);
            Assert.True(InputManager.GameplayInputBlocked);

            manager.SetStateForTests(NetworkMatchState.Playing, Time.timeAsDouble);
            Assert.True(NetworkMatchStateManager.IsGameplayActive);
            Assert.False(InputManager.GameplayInputBlocked);
        }

        private NetworkMatchStateManager CreateMatchStateManager()
        {
            var go = new GameObject("NetworkMatchStateManager");
            objectsToDestroy.Add(go);
            go.AddComponent<NetworkObject>();
            return go.AddComponent<NetworkMatchStateManager>();
        }
    }
}
