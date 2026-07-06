using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class EnemyAnimationCombatPlayModeTests
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
        public IEnumerator EnemyAI_StartDoesNotAddRuntimeGroundingComponent()
        {
            GameObject enemyGo = CreateGameObject("EnemyWithoutRuntimeGrounding");
            enemyGo.AddComponent<EnemyAI>();

            yield return null;

            System.Type groundingType = System.Type.GetType("FPS.EnemyGrounding, FPS");
            if (groundingType != null)
            {
                Assert.IsNull(enemyGo.GetComponent(groundingType),
                    "EnemyAI should not hide floating-feet prefab issues by adding EnemyGrounding at runtime.");
            }
        }

        [UnityTest]
        public IEnumerator EnemyAttack_DoesNotDamageBeforeImpactDelay()
        {
            EnemyAI enemy = CreateEnemyAndDamageableTarget(out TestDamageableTarget target, out GameObject player);
            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = new Vector3(0f, 0f, 1.5f);

            yield return null;

            enemy.DebugConfigureCombatForTests(2.5f, 25f, 0.1f, 0.25f);
            enemy.DebugForceTargetForTests(player.transform);
            Assert.IsTrue(enemy.DebugIsTargetValidForTests(player.transform),
                "Test damageable target should be valid before starting melee attack.");
            Assert.IsTrue(enemy.DebugCanHitTargetForTests(player.transform),
                "Test damageable target should be inside melee range and forward hit arc before attack impact.");
            enemy.DebugBeginAttackForTests();

            yield return null;
            enemy.DebugProcessPendingAttackForTests();

            Assert.AreEqual(100f, target.CurrentHealth, 0.001f,
                "Enemy attack should not apply damage at trigger/hand-raise time before the impact delay.");

            yield return new WaitForSeconds(0.3f);
            enemy.DebugProcessPendingAttackForTests();

            Assert.AreEqual(75f, target.CurrentHealth, 0.001f,
                "Enemy attack should apply damage once at the impact moment while the target is still in range.");
        }

        [UnityTest]
        public IEnumerator EnemyAttack_CancelsPendingDamageWhenTargetRunsOutOfRange()
        {
            EnemyAI enemy = CreateEnemyAndDamageableTarget(out TestDamageableTarget target, out GameObject player);
            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = new Vector3(0f, 0f, 1.5f);

            yield return null;

            enemy.DebugConfigureCombatForTests(2.5f, 25f, 0.1f, 0.2f);
            enemy.DebugForceTargetForTests(player.transform);
            enemy.DebugBeginAttackForTests();
            Assert.IsTrue(enemy.CaptureTestSnapshot().hasPendingAttackDamage);

            player.transform.position = new Vector3(0f, 0f, 6f);
            yield return new WaitForSeconds(0.25f);
            enemy.ApplyAttackHit();

            Assert.AreEqual(100f, target.CurrentHealth, 0.001f,
                "Pending attack damage must not land after the player has escaped melee range.");
        }

        [UnityTest]
        public IEnumerator EnemyAttack_AnimationEventAppliesDamageOnceAtImpact()
        {
            EnemyAI enemy = CreateEnemyAndDamageableTarget(out TestDamageableTarget target, out GameObject player);
            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = new Vector3(0f, 0f, 1.5f);

            yield return null;

            enemy.DebugConfigureCombatForTests(2.5f, 25f, 0.1f, 0.4f);
            enemy.DebugForceTargetForTests(player.transform);
            enemy.DebugBeginAttackForTests();

            enemy.ApplyAttackHit();
            enemy.ApplyAttackHit();

            Assert.AreEqual(75f, target.CurrentHealth, 0.001f,
                "Animation event impact should apply exactly one melee hit while the target is still valid.");
        }

        [UnityTest]
        public IEnumerator EnemyAttack_DoesNotSwitchToChaseWhileImpactIsPending()
        {
            EnemyAI enemy = CreateEnemyAndDamageableTarget(out _, out GameObject player);
            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = new Vector3(0f, 0f, 1.5f);

            yield return null;

            enemy.DebugConfigureCombatForTests(2.5f, 25f, 0.1f, 0.35f);
            enemy.DebugForceTargetForTests(player.transform);
            enemy.DebugBeginAttackForTests();

            player.transform.position = new Vector3(0f, 0f, 6f);
            InvokeEnemyUpdate(enemy);

            EnemyAI.TestSnapshot snapshot = enemy.CaptureTestSnapshot();
            Assert.AreEqual("Attack", snapshot.currentState,
                "Enemy should stay movement-locked during attack windup instead of sliding after a fleeing target.");
            Assert.AreEqual(0f, snapshot.lastAnimatorSpeed, 0.001f,
                "Enemy movement animation should stay stopped while attack impact is pending.");
        }

        [UnityTest]
        public IEnumerator EnemyAttack_DoesNotResumeChaseImmediatelyAfterImpact()
        {
            EnemyAI enemy = CreateEnemyAndDamageableTarget(out _, out GameObject player);
            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = new Vector3(0f, 0f, 1.5f);

            yield return null;

            enemy.DebugConfigureCombatForTests(2.5f, 25f, 0.1f, 0.05f);
            enemy.DebugForceTargetForTests(player.transform);
            enemy.DebugBeginAttackForTests();

            yield return new WaitForSeconds(0.1f);
            player.transform.position = new Vector3(0f, 0f, 6f);
            enemy.ApplyAttackHit();
            InvokeEnemyUpdate(enemy);

            EnemyAI.TestSnapshot snapshot = enemy.CaptureTestSnapshot();
            Assert.AreEqual("Attack", snapshot.currentState,
                "Enemy should finish the attack action window instead of sliding into chase immediately after impact.");
            Assert.AreEqual(0f, snapshot.lastAnimatorSpeed, 0.001f,
                "Enemy locomotion animation should stay stopped until the attack action window ends.");
        }

        [UnityTest]
        public IEnumerator EnemyAttack_HoldsFacingDuringAttackActionWindow()
        {
            EnemyAI enemy = CreateEnemyAndDamageableTarget(out _, out GameObject player);
            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            player.transform.position = new Vector3(0f, 0f, 1.5f);
            typeof(EnemyAI)
                .GetField("rotationSpeed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, 1000f);

            yield return null;

            enemy.DebugConfigureCombatForTests(2.5f, 25f, 0.1f, 0.4f);
            enemy.DebugForceTargetForTests(player.transform);
            enemy.DebugBeginAttackForTests();

            player.transform.position = new Vector3(6f, 0f, 1.5f);
            InvokeEnemyUpdate(enemy);

            Assert.Greater(Vector3.Dot(enemy.transform.forward, Vector3.forward), 0.95f,
                "Enemy should not rotate sideways during a committed melee attack, which makes the attack clip slide.");
        }

        [UnityTest]
        public IEnumerator EnemyChase_FacesDestinationDirectionInsteadOfStrafingAtTarget()
        {
            GameObject enemyGo = CreateGameObject("ChaseFacingEnemy");
            EnemyAI enemy = enemyGo.AddComponent<EnemyAI>();

            yield return null;

            GameObject player = CreateGameObject("RuntimePlayer");
            player.tag = "Player";
            player.AddComponent<TestDamageableTarget>();
            player.transform.position = Vector3.forward * 10f;

            enemy.transform.position = Vector3.zero;
            enemy.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            typeof(EnemyAI)
                .GetField("rotationSpeed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, 1000f);
            enemy.DebugForceTargetForTests(player.transform);
            enemy.DebugSetStateForTests("Chase");
            enemy.DebugSetDesiredDestinationForTests(Vector3.right * 10f);
            enemy.DebugSmoothLookForTests();

            Assert.Greater(Vector3.Dot(enemy.transform.forward, Vector3.right), 0.75f,
                "Chasing enemies should face their movement destination so their forward run animation does not slide sideways.");
        }


        [UnityTest]
        public IEnumerator EnemyAnimationSpeed_UsesActualDisplacementAndStopsDuringAttack()
        {
            GameObject enemyGo = CreateGameObject("AnimatedEnemy");
            EnemyAI enemy = enemyGo.AddComponent<EnemyAI>();

            yield return null;

            enemy.DebugSetStateForTests("Chase");
            enemy.DebugUpdateAnimationForTests();
            enemyGo.transform.position += Vector3.forward;

            yield return null;

            enemy.DebugUpdateAnimationForTests();
            Assert.Greater(enemy.CaptureTestSnapshot().lastAnimatorSpeed, 0.1f,
                "Enemy animation speed should reflect actual transform displacement, not only NavMeshAgent.velocity.");

            enemy.DebugSetStateForTests("Attack");
            enemy.DebugUpdateAnimationForTests();
            Assert.AreEqual(0f, enemy.CaptureTestSnapshot().lastAnimatorSpeed, 0.001f,
                "Enemy animation speed should be zero while attacking so feet do not run during attack windup.");
        }

        private EnemyAI CreateEnemyAndDamageableTarget(out TestDamageableTarget target, out GameObject player)
        {
            player = CreateGameObject("RuntimePlayer");
            player.tag = "Player";
            target = player.AddComponent<TestDamageableTarget>();

            GameObject enemyGo = CreateGameObject("RuntimeEnemy");
            EnemyAI enemy = enemyGo.AddComponent<EnemyAI>();
            return enemy;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            objectsToDestroy.Add(go);
            return go;
        }

        private static void InvokeEnemyUpdate(EnemyAI enemy)
        {
            typeof(EnemyAI)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(enemy, null);
        }

    }

    public class TestDamageableTarget : MonoBehaviour, IDamageable
    {
        public float CurrentHealth { get; private set; } = 100f;
        public bool IsDead => CurrentHealth <= 0f;

        public void TakeDamage(float amount)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, amount));
        }
    }
}
