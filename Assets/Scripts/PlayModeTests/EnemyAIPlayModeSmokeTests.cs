using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class EnemyAIPlayModeSmokeTests
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
        public IEnumerator EnemyAI_RuntimeBrainTicks_AcquiresPlayer_AndRequestsFairSlotDestination()
        {
            GameObject profilerGo = CreateGameObject("PlayerProfiler");
            profilerGo.AddComponent<PlayerProfiler>();

            GameObject slotManagerGo = CreateGameObject("AttackSlotManager");
            slotManagerGo.AddComponent<AttackSlotManager>();

            GameObject playerGo = CreateGameObject("RuntimePlayer");
            playerGo.tag = "Player";
            playerGo.transform.position = Vector3.zero;
            playerGo.AddComponent<NetworkObject>();
            playerGo.AddComponent<PlayerHealth>();

            GameObject enemyGo = CreateGameObject("RuntimeEnemy");
            enemyGo.transform.position = new Vector3(0f, 0f, 10f);
            EnemyAI enemy = enemyGo.AddComponent<EnemyAI>();

            for (int i = 0; i < 8; i++)
                yield return null;

            EnemyAI.TestSnapshot snapshot = enemy.CaptureTestSnapshot();
            Assert.Greater(snapshot.brainTickCount, 0, "EnemyAI should execute its runtime brain loop in PlayMode.");
            Assert.AreSame(playerGo.transform, snapshot.currentTarget, "EnemyAI should acquire the tagged runtime player through PlayerProfiler.");
            Assert.AreEqual(0, snapshot.currentTargetIndex, "EnemyAI should resolve the runtime player profile index.");
            Assert.AreEqual("Chase", snapshot.currentState, "EnemyAI should leave Idle and enter Chase when the player is inside detection range.");
            Assert.Greater(snapshot.lastDestinationRequestTime, 0f, "EnemyAI should request a chase destination during runtime.");

            float destinationDistanceFromPlayer = Vector3.Distance(snapshot.lastDesiredDestination, playerGo.transform.position);
            Assert.Greater(destinationDistanceFromPlayer, 0.9f,
                "With AttackSlotManager present, runtime chase intent should go to a pressure/slot destination, not dogpile the player center.");
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }
    }
}
