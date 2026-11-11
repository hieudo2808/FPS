#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.Tests
{
    /// <summary>Real GameScene, local host. The mandatory Factory Tank must always resolve: staged, or skipped, never stuck.</summary>
    public sealed class CampaignTankStagingTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Evidence = "Reports/TankSpawnFix-2026-11-11/";
        CampaignMissionController campaign;
        PlayerHealth owner;
        readonly List<string> log = new();

        [UnitySetUp]
        public IEnumerator StartGameplay()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Start Local Host"), Is.True);
            float deadline = Time.realtimeSinceStartup + 120;
            while (CampaignMissionController.Instance?.State.phase != CampaignPhase.Exploring && Time.realtimeSinceStartup < deadline)
                yield return null;
            campaign = CampaignMissionController.Instance;
            Assert.That(campaign?.State.phase, Is.EqualTo(CampaignPhase.Exploring));
            owner = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            campaign.enabled = false; // The test drives the encounter tick itself.
            typeof(PlayerHealth).GetField("campaignProtectionUntil", Private)?.SetValue(owner, campaign.Now + 600);
            Directory.CreateDirectory(Evidence);
            log.Clear();
        }

        [UnityTearDown]
        public IEnumerator StopGameplay()
        {
            File.WriteAllLines(Evidence + TestContext.CurrentContext.Test.Name + ".txt", log);
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            for (int i = 0; i < 5; i++) yield return null;
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }

        [UnityTest]
        public IEnumerator FactoryTankIsStagedAfterStrictPlacementTimesOut()
        {
            var spawns = DirectorSpawnService.Instance;
            Assert.That(spawns, Is.Not.Null);
            var controller = owner.GetComponent<CharacterController>();
            controller.enabled = false;
            // The Tank arms where the squad finishes a branch or goes for the case, so placement must work at each site.
            foreach (var id in new[] { CampaignObjectiveId.FactoryGenerator, CampaignObjectiveId.FactoryShipping, CampaignObjectiveId.FactoryCase })
            {
                var site = campaign.Objective(id);
                Assert.That(site, Is.Not.Null, id.ToString());
                Assert.That(NavMesh.SamplePosition(site.TargetPosition, out var floor, 3f, NavMesh.AllAreas), Is.True, id + " has no floor");
                owner.transform.position = floor.position;
                Physics.SyncTransforms();
                yield return null;
                bool strict = spawns.TryGetCampaignTankPosition(out var strictPosition);
                bool relaxed = spawns.TryGetCampaignTankPosition(out var position, false, true);
                log.Add($"{id}: strict={strict} strictDistance={(strict ? Vector3.Distance(strictPosition, floor.position) : -1):0.0} relaxed={relaxed} distance={(relaxed ? Vector3.Distance(position, floor.position) : -1):0.0}");
                Assert.That(relaxed, Is.True, "Relaxed placement must find a spot near " + id);
                Assert.That(spawns.IsRelaxedTankPositionValid(position), Is.True);
                Assert.That(Vector3.Distance(position, floor.position), Is.GreaterThanOrEqualTo(9.5f), "Never on top of the operator.");
            }

            ArmFactoryTank();
            Tick();
            Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Pending));
            AgeAttempt(campaign.settings.tankSpawnTimeoutSeconds + 1);
            Tick();
            log.Add("after one timeout: stage=" + campaign.State.tankStage + " reason=" + campaign.State.tankWaitReason);
            Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Active), "A full timeout stages the Tank instead of failing.");
            Assert.That(campaign.TryGetTankTarget(out var tank), Is.True);
            Assert.That(Vector3.Distance(tank, owner.transform.position), Is.GreaterThanOrEqualTo(9f));
            Assert.That(campaign.State.factoryTankDefeated, Is.False, "Staging is not a kill.");
            Assert.That(CampaignRules.CanUse(campaign.State, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Prerequisite));
        }

        [UnityTest]
        public IEnumerator FactoryTankThatCanNeverSpawnIsSkippedNotStuck()
        {
            var registry = SpecialInfectedRegistry.Instance;
            Assert.That(registry, Is.Not.Null);
            var types = (List<SpecialInfectedData>)typeof(SpecialInfectedRegistry).GetField("specialTypes", Private).GetValue(registry);
            var removed = types.Where(entry => entry != null && entry.type == SpecialType.Tank).ToList();
            types.RemoveAll(removed.Contains); // Fault injection: no Tank can ever be produced.
            try
            {
                ArmFactoryTank();
                Tick();
                AgeAttempt(campaign.settings.tankSpawnTimeoutSeconds + 1);
                Tick();
                log.Add("relaxed tier with no prefab: stage=" + campaign.State.tankStage + " reason=" + campaign.State.tankWaitReason);
                Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Pending), "Still trying during the relaxed tier.");
                AgeAttempt(campaign.settings.tankSpawnTimeoutSeconds * 3 + 1);
                Tick();
                log.Add("after three timeouts: stage=" + campaign.State.tankStage);
                Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Resolved));
                Assert.That(CampaignRules.CanUse(campaign.State, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Accepted),
                    "The case must open when the encounter cannot exist.");
                Assert.That(CampaignRules.ShouldArmTank(campaign.State), Is.False);
            }
            finally { types.AddRange(removed); }
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Tank could not be staged"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetiredFailureStageFromOldSaveResumesTrying()
        {
            ArmFactoryTank();
            campaign.State.tankStage = CampaignTankStage.SpawnFailed;
            typeof(CampaignMissionController).GetField("nextTankAttempt", Private).SetValue(campaign, campaign.Now + 60); // Normalisation only.
            Tick();
            Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Pending));
            yield return null;
        }

        void ArmFactoryTank()
        {
            var state = campaign.State;
            state.chapter = CampaignChapter.Factory; state.phase = CampaignPhase.Exploring;
            state.discoveredFiles |= CampaignState.FileBit(CampaignFileId.FactoryManifest);
            state.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
            state.factoryTankDefeated = false; state.tankStage = CampaignTankStage.Dormant;
            typeof(CampaignMissionController).GetField("readingUntil", Private).SetValue(campaign, 0d);
        }

        // Makes the next encounter tick observe this many seconds of unsuccessful attempts.
        void AgeAttempt(double seconds)
        {
            var attempt = (CampaignTankAttempt)typeof(CampaignMissionController).GetField("tankAttempt", Private).GetValue(campaign);
            // Fixed far-future stamps: server time may still be smaller than the span being simulated.
            attempt.Reset();
            attempt.Tick(1e6, false);
            attempt.Tick(1e6 + seconds, false);
            typeof(CampaignMissionController).GetField("nextTankAttempt", Private).SetValue(campaign, 0d);
        }

        void Tick() => typeof(CampaignMissionController).GetMethod("UpdateTankEncounter", Private).Invoke(campaign, null);
    }
}
#endif
