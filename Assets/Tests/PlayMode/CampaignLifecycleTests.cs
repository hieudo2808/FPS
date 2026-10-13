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
using UnityEngine.AI;
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
        NavMeshData recoveryNavigation;
        NavMeshDataInstance recoveryNavigationInstance;

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
            if (recoveryNavigationInstance.valid) recoveryNavigationInstance.Remove();
            if (recoveryNavigation != null) Object.Destroy(recoveryNavigation);
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

        [UnityTest]
        public IEnumerator RecoveryAuthorityRejectsIsolatedLegacyPoseAndKeepsTransferGuards()
        {
            Vector3 connected = new(0, 0, -1000), isolated = connected + Vector3.right * 10;
            var chapter = new GameObject("Recovery chapter").AddComponent<CampaignChapterRoot>();
            chapter.chapter = CampaignChapter.Factory;
            var sources = new List<NavMeshBuildSource>();
            foreach (var position in new[] { connected, isolated })
            {
                var floor = new GameObject("Recovery floor", typeof(BoxCollider));
                floor.transform.SetParent(chapter.transform); floor.transform.position = position - Vector3.up * .1f;
                floor.GetComponent<BoxCollider>().size = new Vector3(4, .2f, 4);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = floor.transform.localToWorldMatrix,
                    size = new Vector3(4, .2f, 4), area = 0 });
            }
            recoveryNavigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(connected + Vector3.right * 5, new Vector3(30, 10, 20)), Vector3.zero, Quaternion.identity);
            Assert.That(recoveryNavigation, Is.Not.Null);
            recoveryNavigationInstance = NavMesh.AddNavMeshData(recoveryNavigation);
            var marker = new GameObject("Recovery arrival").AddComponent<MapRecoveryPoint>();
            marker.transform.SetParent(chapter.transform); marker.transform.position = connected + Vector3.up * .12f;
            chapter.recoveries = new[] { marker }; chapter.arrivals = new[] { marker.transform };
            campaign.chapters = new[] { chapter }; campaign.State.chapter = CampaignChapter.Factory;
            var service = new GameObject("Recovery authority", typeof(NetworkObject)).AddComponent<WorldRecoveryService>();
            service.Configure(chapter.recoveries); service.GetComponent<NetworkObject>().Spawn();
            var actor = new GameObject("Recovered player", typeof(NetworkObject), typeof(CharacterController), typeof(PlayerMovement), typeof(PlayerHealth));
            actor.transform.position = connected + Vector3.back * 10;
            actor.GetComponent<PlayerMovement>().enabled = false;
            actor.GetComponent<NetworkObject>().Spawn();
            var player = actor.GetComponent<PlayerHealth>();
            var controller = actor.GetComponent<CharacterController>();
            controller.center = Vector3.up;
            typeof(PlayerMovement).GetField("controller", Private).SetValue(actor.GetComponent<PlayerMovement>(), controller);
            Physics.SyncTransforms();
            var poses = (Dictionary<ulong, Pose>)typeof(WorldRecoveryService).GetField("campaignSafePoses", Private).GetValue(service);
            poses[player.NetworkObjectId] = new Pose(isolated + Vector3.up * .12f, Quaternion.identity);
            campaign.State.phase = CampaignPhase.Insertion;
            Assert.That(service.TryRecover(controller), Is.False, "Insertion must remain blocked.");
            campaign.State.phase = CampaignPhase.Exploring;
            Assert.That(service.TryRecover(controller), Is.True);
            Assert.That(Vector3.Distance(actor.transform.position, connected + Vector3.up * .12f), Is.LessThan(.1f));
            Assert.That(player.CampaignTransferPending, Is.True, "Server relocation still waits for the owner's acknowledgement.");
            Assert.That(player.IsInputReady, Is.False);
            Assert.That(service.TryRecover(controller), Is.False, "Repeated recovery is blocked during transfer/cooldown.");
            var cooldowns = (Dictionary<ulong, double>)typeof(WorldRecoveryService).GetField("nextAllowedRecoveryTime", Private).GetValue(service);
            Assert.That(cooldowns[player.NetworkObjectId], Is.GreaterThan(campaign.Now));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FactoryBakeLeavesBothInterlocksUnderDynamicGateControl()
        {
            var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(
                "Assets/FPS/Features/World/Content/Campaign/FactoryTraversal/FactoryNavigationFilled.asset");
            Assert.That(asset, Is.Not.Null);
            recoveryNavigation = Object.Instantiate(asset);
            recoveryNavigationInstance = NavMesh.AddNavMeshData(recoveryNavigation);
            var mask = (NetworkVariable<ulong>)typeof(CampaignMissionController).GetField("openDoorMask", Private).GetValue(campaign);
            // Authored interlocks share this footprint and slide offset; test the saved bake in isolation.
            foreach (float z in new[] { 188f, 198f })
            {
                Vector3 from = new(125, 0, z - 3), to = new(125, 0, z + 3);
                bool Complete() => CampaignPlacement.CompletePath(from, to, out _);
                Assert.That(Complete(), Is.True, "The moving leaf must not be a permanent baked wall.");
                var door = new GameObject("Interlock " + z).AddComponent<CampaignDoor>();
                door.transform.position = new Vector3(125, 0, z);
                door.leaf = new GameObject("Sliding leaf", typeof(BoxCollider)).transform;
                door.leaf.SetParent(door.transform, false); door.leaf.localPosition = new Vector3(0, 1.622836f, 0);
                door.leaf.GetComponent<BoxCollider>().size = new Vector3(5.6725f, 3.245672f, .5380554f);
                door.closedLocalPosition = door.leaf.localPosition; door.openOffset = Vector3.right * 6;
                var blocker = new GameObject("NavigationBlocker").AddComponent<NavMeshObstacle>();
                blocker.transform.SetParent(door.transform, false); blocker.transform.localPosition = Vector3.up * 1.5f;
                blocker.shape = NavMeshObstacleShape.Box; blocker.size = new Vector3(6, 3, .5f); blocker.carving = true;
                door.navigationBlocker = blocker; campaign.doors = new[] { door }; mask.Value = 0;
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (Complete() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Physics.SyncTransforms();
                bool Blocked() => Physics.CapsuleCast(from + Vector3.up * .5f, from + Vector3.up * 1.5f, .5f,
                    Vector3.forward, out _, 6, ~0, QueryTriggerInteraction.Ignore);
                Assert.That(door.IsClosed && Blocked() && !Complete(), Is.True, "Closed leaf and carve must both block crossing.");
                mask.Value = 1;
                deadline = Time.realtimeSinceStartupAsDouble + 4;
                while ((!door.IsOpen || !Complete()) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Physics.SyncTransforms();
                Assert.That(door.IsOpen && !Blocked() && Complete(), Is.True, "Opened interlock must restore its saved NavMesh corridor.");
            }
        }
    }
}
#endif
