using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class TankPlayModeSmokeTests
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
        public IEnumerator Tank_RuntimeComponents_PresentAndConfigured()
        {
            GameObject tankGo = CreateGameObject("RuntimeTank");
            tankGo.AddComponent<CapsuleCollider>();
            tankGo.AddComponent<EnemyHealth>();
            SI_Tank tank = tankGo.AddComponent<SI_Tank>();

            yield return null;

            Assert.AreEqual(SpecialType.Tank, tank.Type);
            Assert.IsTrue(tank.AllowedInSoloMode);
            Assert.Greater(tank.HeavySwingDamage, 0f);
            Assert.Greater(tank.SlamDamage, 0f);
            Assert.Greater(tank.StaggerDamageThreshold, 0f);
        }

        [UnityTest]
        public IEnumerator Tank_Stagger_RuntimeMechanic_TriggersOnBurstDamage()
        {
            GameObject tankGo = CreateGameObject("RuntimeTankStagger");
            tankGo.AddComponent<CapsuleCollider>();
            tankGo.AddComponent<EnemyHealth>();
            SI_Tank tank = tankGo.AddComponent<SI_Tank>();

            yield return null;

            tank.RecordDamage(200f, Time.time);
            Assert.IsFalse(tank.IsStaggered);

            tank.RecordDamage(175f, Time.time);
            bool triggered = tank.CheckAndTriggerStagger(Time.time);

            Assert.IsTrue(triggered, "Burst damage reaching the 15% solo threshold should trigger stagger.");
            Assert.IsTrue(tank.IsStaggered);
        }

        [UnityTest]
        public IEnumerator Tank_HeavySwing_AppliesDamageAndKnockback()
        {
            GameObject playerGo = CreateGameObject("TestPlayer");
            playerGo.transform.position = new Vector3(0f, 0f, 2f);
            var controller = playerGo.AddComponent<CharacterController>();
            var pMove = playerGo.AddComponent<PlayerMovement>();
            var pHealth = playerGo.AddComponent<PlayerHealth>();

            GameObject behindPlayer = CreateGameObject("BehindPlayer");
            behindPlayer.transform.position = new Vector3(0f, 0f, -2f);
            behindPlayer.AddComponent<CharacterController>();
            PlayerMovement behindMovement = behindPlayer.AddComponent<PlayerMovement>();
            behindPlayer.AddComponent<PlayerHealth>();

            GameObject tankGo = CreateGameObject("TestTank");
            tankGo.transform.position = Vector3.zero;
            tankGo.transform.forward = Vector3.forward;
            var tankCol = tankGo.AddComponent<CapsuleCollider>();
            tankGo.AddComponent<EnemyHealth>();
            SI_Tank tank = tankGo.AddComponent<SI_Tank>();

            yield return null;

            Physics.SyncTransforms();
            InvokeTankImpact(tank, "ExecuteHeavySwingHit");

            Assert.Greater(pMove.ExternalVelocity.magnitude, 0f, "Heavy swing knockback should apply external velocity to player.");
            Assert.AreEqual(Vector3.zero, behindMovement.ExternalVelocity,
                "Heavy swing must not hit a player behind the Tank.");
        }

        [UnityTest]
        public IEnumerator Tank_Slam_AoE_KnocksBackMultiplePlayers()
        {
            GameObject player1 = CreateGameObject("Player1");
            player1.transform.position = new Vector3(2f, 0f, 0f);
            var col1 = player1.AddComponent<CharacterController>();
            var move1 = player1.AddComponent<PlayerMovement>();

            GameObject player2 = CreateGameObject("Player2");
            player2.transform.position = new Vector3(-2f, 0f, 0f);
            var col2 = player2.AddComponent<CharacterController>();
            var move2 = player2.AddComponent<PlayerMovement>();

            GameObject tankGo = CreateGameObject("TestTankSlam");
            tankGo.transform.position = Vector3.zero;
            SI_Tank tank = tankGo.AddComponent<SI_Tank>();

            yield return null;

            player1.AddComponent<PlayerHealth>();
            player2.AddComponent<PlayerHealth>();
            var duplicateCollider = CreateGameObject("Player1ExtraCollider");
            duplicateCollider.transform.SetParent(player1.transform, false);
            duplicateCollider.AddComponent<SphereCollider>();

            Physics.SyncTransforms();
            InvokeTankImpact(tank, "ExecuteSlamAoE");

            Assert.Greater(move1.ExternalVelocity.magnitude, 0f);
            Assert.Greater(move2.ExternalVelocity.magnitude, 0f);
            Assert.AreEqual(tank.SlamKnockbackForce, move1.ExternalVelocity.magnitude, 0.01f,
                "Multiple colliders on one player must not duplicate Slam knockback.");
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }

        private static void InvokeTankImpact(SI_Tank tank, string methodName)
        {
            MethodInfo method = typeof(SI_Tank).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(tank, null);
        }
    }
}
