using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public class InfectorGameScenePlayModeTests
    {
        private const string GameScenePath = "Assets/FPS/Scenes/GameScene.unity";

        [UnityTest]
        public IEnumerator GameScene_AuthoredInfector_IsBoundAndNavigable()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(GameScenePath, LoadSceneMode.Single);
            Assert.NotNull(load);
            while (!load.isDone)
                yield return null;
            yield return null;

            Assert.AreEqual("GameScene", SceneManager.GetActiveScene().name);
            SpecialInfectedRegistry registry = Object.FindAnyObjectByType<SpecialInfectedRegistry>();
            InfectionThreatService threat = Object.FindAnyObjectByType<InfectionThreatService>();
            HUDManager hud = Object.FindAnyObjectByType<HUDManager>();
            Assert.NotNull(registry, "GameScene must author SpecialInfectedRegistry.");
            Assert.NotNull(threat, "GameScene must author InfectionThreatService.");
            Assert.NotNull(hud, "GameScene must author HUDManager.");

            List<SpecialInfectedData> entries = GetPrivateField<List<SpecialInfectedData>>(registry, "specialTypes");
            SpecialInfectedData infectorEntry = entries.SingleOrDefault(entry => entry.type == SpecialType.Infector);
            Assert.NotNull(infectorEntry);
            Assert.AreEqual(SpecialImplementationState.Playable, infectorEntry.implementationState);
            Assert.NotNull(infectorEntry.prefab);

            Assert.NotNull(GetPrivateField<object>(hud, "infectionFill"));
            Assert.NotNull(GetPrivateField<object>(hud, "infectionIcon"));
            Assert.NotNull(GetPrivateField<object>(hud, "infectionStageText"));
            Assert.NotNull(GetPrivateField<object>(hud, "treatmentProgressFill"));
            GameObject sepsisWarning = GetPrivateField<GameObject>(hud, "sepsisWarning");
            Assert.NotNull(sepsisWarning);
            Assert.IsFalse(sepsisWarning.activeSelf);

            Transform spawnRoot = GameObject.Find("SpawnPosition")?.transform;
            Assert.NotNull(spawnRoot);
            Assert.GreaterOrEqual(spawnRoot.childCount, 2);
            Assert.IsTrue(NavMesh.SamplePosition(spawnRoot.GetChild(0).position, out NavMeshHit startHit, 8f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(spawnRoot.GetChild(1).position, out NavMeshHit destinationHit, 8f, NavMesh.AllAreas));

            GameObject instance = Object.Instantiate(infectorEntry.prefab, startHit.position, Quaternion.identity);
            try
            {
                yield return null;
                SI_Infector infector = instance.GetComponent<SI_Infector>();
                EnemyHealth health = instance.GetComponent<EnemyHealth>();
                NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
                Assert.NotNull(infector);
                Assert.NotNull(health);
                Assert.NotNull(agent);
                Assert.AreEqual(200f, health.MaxHealth);
                Assert.IsTrue(agent.Warp(startHit.position));
                Assert.IsTrue(agent.isOnNavMesh);

                var path = new NavMeshPath();
                Assert.IsTrue(agent.CalculatePath(destinationHit.position, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);

                infector.ResetAI();
                Assert.AreEqual(InfectorState.Search, infector.CurrentState);
                infector.OnDeath();
                Assert.AreEqual(InfectorState.Dead, infector.CurrentState);
                Assert.IsFalse(infector.IsPerformingImplant);
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [Test]
        public void TreatmentProtocol_HasNoClientCompleteRpc()
        {
            MethodInfo forbidden = typeof(PlayerInfectionController).GetMethod(
                "CompleteTreatmentServerRpc",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNull(forbidden, "Clients must never be able to request treatment completion.");
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {target.GetType().Name}.{fieldName}.");
            return (T)field.GetValue(target);
        }
    }
}
