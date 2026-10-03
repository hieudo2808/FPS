#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>Isolated authority/lifecycle checks. These do not replace multi-peer campaign acceptance.</summary>
    public sealed class CampaignLifecycleTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        NetworkManager server;
        CampaignMissionController campaign;
        CampaignSettings settings;
        Scene scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(CampaignMissionController.Instance, Is.Null, "Run in the Test Runner's isolated scene.");
            scene = SceneManager.CreateScene("CampaignLifecycleProbe");
            SceneManager.SetActiveScene(scene);
            var managerRoot = new GameObject("Campaign test server");
            server = managerRoot.AddComponent<NetworkManager>();
            var transport = managerRoot.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 17842, "127.0.0.1");
            server.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = false };
            server.SetSingleton();
            Assert.That(server.StartServer(), Is.True);
            var root = new GameObject("Campaign authority", typeof(NetworkObject));
            campaign = root.AddComponent<CampaignMissionController>();
            campaign.enabled = false; // Exercise explicit transitions, not an empty world's gameplay Update.
            settings = ScriptableObject.CreateInstance<CampaignSettings>();
            settings.contentCatalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(
                "Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset");
            campaign.settings = settings;
            root.GetComponent<NetworkObject>().Spawn();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (server != null) server.Shutdown();
            yield return null;
            if (server != null) Object.Destroy(server.gameObject);
            if (settings != null) Object.Destroy(settings);
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TankDespawnDoesNotCountAsKill_DeathUnlocksFactory()
        {
            campaign.State.phase = CampaignPhase.Exploring;
            campaign.State.completed = CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
            var enemyRoot = new GameObject("Tracked Tank health", typeof(NetworkObject));
            var health = enemyRoot.AddComponent<EnemyHealth>();
            typeof(CampaignMissionController).GetField("campaignTank", Private).SetValue(campaign, health);
            campaign.State.tankStage = CampaignTankStage.Active;
            enemyRoot.SetActive(false);
            Invoke(campaign, "UpdateTankEncounter");
            Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Pending));
            Assert.That(campaign.State.factoryTankDefeated, Is.False);
            Assert.That(CampaignRules.CanUse(campaign.State, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Prerequisite));

            enemyRoot.SetActive(true);
            typeof(CampaignMissionController).GetField("campaignTank", Private).SetValue(campaign, health);
            campaign.State.tankStage = CampaignTankStage.Active;
            health.OnDeathServer += (Action)Delegate.CreateDelegate(typeof(Action), campaign,
                typeof(CampaignMissionController).GetMethod("OnCampaignTankKilled", Private));
            health.TakeDamage(health.MaxHealth + 1);
            Assert.That(campaign.State.tankStage, Is.EqualTo(CampaignTankStage.Resolved));
            Assert.That(campaign.State.factoryTankDefeated, Is.True);
            Assert.That(CampaignRules.CanUse(campaign.State, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Accepted));
            yield return null;
            // Unity Test Runner still fails errors/exceptions; ordinary enemy/editor diagnostics are allowed.
        }

        [UnityTest]
        public IEnumerator StateRestoreDoesNotReplayDialogue_QueueResetClearsPresentation()
        {
            var hud = new GameObject("Campaign presentation").AddComponent<CampaignHUD>();
            hud.enabled = false;
            var queue = (List<CampaignDialogueDefinition>)typeof(CampaignHUD).GetField("pendingDialogue", Private).GetValue(hud);
            campaign.State.completed = ulong.MaxValue;
            campaign.State.evidenceTransmitted = true;
            Invoke(hud, "UpdateSubtitles");
            Assert.That(queue, Is.Empty, "Historical state cannot enqueue old radio lines.");
            hud.EnqueueDialogue(CampaignDialogueId.ClientSuppressionOrder);
            hud.EnqueueDialogue(CampaignDialogueId.MiraRejectsOrder);
            Assert.That(queue.Count, Is.EqualTo(2));
            hud.ResetDialogue();
            Assert.That(queue, Is.Empty);
            var subtitle = typeof(CampaignHUD).GetField("subtitle", Private).GetValue(hud);
            Assert.That((string)subtitle.GetType().GetProperty("text").GetValue(subtitle), Is.Empty);
            yield return null;
        }

        static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    }
}
#endif
