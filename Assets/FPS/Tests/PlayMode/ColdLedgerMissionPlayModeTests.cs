using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.PlayModeTests
{
    public sealed class ColdLedgerMissionPlayModeTests
    {
        private const string ScenePath = "Assets/FPS/Scenes/GameScene.unity";

        [UnityTest]
        public IEnumerator BothBranchOrders_OpenOnlyTheExpectedShortcutsAndStartInteractiveFinale()
        {
            yield return RunMissionOrder(utilitiesFirst: true);
            yield return RunMissionOrder(utilitiesFirst: false);
        }

        private static IEnumerator RunMissionOrder(bool utilitiesFirst)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.NotNull(load);
            while (!load.isDone)
                yield return null;
            yield return null;

            FactoryMissionController mission = Object.FindAnyObjectByType<FactoryMissionController>(FindObjectsInactive.Include);
            Assert.NotNull(mission);
            Assert.AreEqual(FactoryMissionState.Insertion, mission.State);

            FactoryMissionGate decon = FindGate("Gate_DeconExit");
            FactoryMissionGate utilitiesReturn = FindGate("Gate_UtilitiesServiceReturn");
            FactoryMissionGate logisticsReturn = FindGate("Gate_LogisticsCargoReturn");
            FactoryMissionGate coldDoor = FindGate("Gate_ColdStorageBlastDoor");
            FactoryMissionGate extractionCorridor = FindGate("Gate_ExtractionEmergencyCorridor");
            float deconClosedY = decon.transform.position.y;
            float utilitiesClosedY = utilitiesReturn.transform.position.y;
            float logisticsClosedY = logisticsReturn.transform.position.y;
            float coldClosedY = coldDoor.transform.position.y;
            float extractionClosedY = extractionCorridor.transform.position.y;

            mission.FinishInsertionForTests();
            Assert.AreEqual(FactoryMissionState.BranchesActive, mission.State);
            yield return WaitForGateOpen(decon, deconClosedY, "Decon exit");
            Assert.False(InputManager.CinematicInputBlocked,
                "Gameplay input must be restored after the insertion timeline.");

            if (utilitiesFirst)
            {
                CompleteUtilities(mission);
                yield return WaitForGateOpen(utilitiesReturn, utilitiesClosedY, "Utilities service return");
                AssertGateClosed(logisticsReturn, logisticsClosedY, "Logistics shortcut opened before its objectives.");
            }
            else
            {
                CompleteLogistics(mission);
                yield return WaitForGateOpen(logisticsReturn, logisticsClosedY, "Logistics cargo return");
                AssertGateClosed(utilitiesReturn, utilitiesClosedY, "Utilities shortcut opened before its objectives.");
            }

            Assert.AreEqual(FactoryMissionState.BranchesActive, mission.State);
            AssertGateClosed(coldDoor, coldClosedY, "Cold Storage opened after only one branch.");

            if (utilitiesFirst)
                CompleteLogistics(mission);
            else
                CompleteUtilities(mission);

            Assert.AreEqual(FactoryMissionState.SampleUnlocked, mission.State);
            yield return WaitForGateOpen(utilitiesReturn, utilitiesClosedY, "Utilities service return");
            yield return WaitForGateOpen(logisticsReturn, logisticsClosedY, "Logistics cargo return");
            yield return WaitForGateOpen(coldDoor, coldClosedY, "Cold Storage blast door");

            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.ColdStorageSample));
            Assert.AreEqual(FactoryMissionState.SampleSecured, mission.State);
            yield return WaitForGateOpen(extractionCorridor, extractionClosedY, "Extraction emergency corridor");

            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.ExtractionRadio));
            Assert.AreEqual(FactoryMissionState.ExtractionActive, mission.State);
            yield return null;
            yield return null;

            PlayableDirector approach = GameObject.Find("ExtractionApproach")?.GetComponent<PlayableDirector>();
            Assert.NotNull(approach);
            Assert.AreEqual(PlayState.Playing, approach.state,
                "The 85-second helicopter approach must run during the playable finale.");
            Assert.False(InputManager.CinematicInputBlocked,
                "Extraction defense must not lock player input.");
        }

        private static void CompleteUtilities(FactoryMissionController mission)
        {
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.UtilitiesBreakerWest));
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.UtilitiesBreakerEast));
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.UtilitiesGenerator));
        }

        private static void CompleteLogistics(FactoryMissionController mission)
        {
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.LogisticsManifest));
            Assert.AreEqual(FactoryInteractionResult.Accepted,
                mission.CompleteObjectiveForTests(FactoryObjectiveId.LogisticsSecurityOverride));
        }

        private static FactoryMissionGate FindGate(string name)
        {
            GameObject gate = GameObject.Find(name);
            Assert.NotNull(gate, $"Missing mission gate {name}.");
            FactoryMissionGate component = gate.GetComponent<FactoryMissionGate>();
            Assert.NotNull(component);
            return component;
        }

        private static IEnumerator WaitForGateOpen(FactoryMissionGate gate, float closedY, string label)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (gate.transform.position.y < closedY + 4.5f && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.GreaterOrEqual(gate.transform.position.y, closedY + 4.5f,
                $"{label} did not reach its open position.");
        }

        private static void AssertGateClosed(FactoryMissionGate gate, float closedY, string message)
        {
            Assert.AreEqual(closedY, gate.transform.position.y, 0.15f, message);
        }
    }
}
